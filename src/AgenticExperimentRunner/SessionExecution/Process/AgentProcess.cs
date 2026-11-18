using System.Diagnostics;
using System.Text;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Parametri di avvio di una sessione CLI di un agente.
/// </summary>
/// <param name="AgentId">Identificatore dell'agente nella run (per esempio <c>coordinator</c>, <c>worker-01</c>).</param>
/// <param name="Role">Ruolo dell'agente (per esempio <c>single-agent</c>, <c>coordinator</c>, <c>worker</c>).</param>
/// <param name="ClaudePath">Eseguibile della CLI.</param>
/// <param name="Arguments">Argomenti della CLI; il prompt non compare.</param>
/// <param name="Prompt">Byte del prompt inviati su stdin.</param>
/// <param name="WorkingDirectory">Directory di lavoro del processo.</param>
/// <param name="StreamPath">File, da creare, in cui copiare lo stdout grezzo.</param>
/// <param name="StandardErrorPath">File, da creare, in cui scrivere lo stderr.</param>
/// <param name="FindConfigurationViolations">Confronta l'evento <c>system/init</c> con la configurazione attesa.</param>
public sealed record AgentLaunch(
    string AgentId,
    string Role,
    string ClaudePath,
    IReadOnlyList<string> Arguments,
    byte[] Prompt,
    string WorkingDirectory,
    string StreamPath,
    string StandardErrorPath,
    Func<SessionInitialization, IReadOnlyList<string>> FindConfigurationViolations);

/// <summary>
/// Esito del processo di un agente.
/// </summary>
/// <param name="AgentId">Identificatore dell'agente.</param>
/// <param name="Role">Ruolo dell'agente.</param>
/// <param name="Tracker">Stream interpretato.</param>
/// <param name="ConfigurationViolations">Differenze dalla configurazione attesa.</param>
/// <param name="TerminationReason">Motivo della terminazione da parte del controllore, se avvenuta.</param>
/// <param name="ExitCode">Codice di uscita del processo.</param>
/// <param name="StartedAt">Istante di avvio del processo.</param>
/// <param name="EndedAt">Istante di uscita del processo.</param>
/// <param name="Duration">Durata misurata con cronometro monotono.</param>
/// <param name="RemovedEnvironmentVariables">Nomi delle variabili ereditate rimosse.</param>
public sealed record AgentProcessOutcome(
    string AgentId,
    string Role,
    StreamUsageTracker Tracker,
    IReadOnlyList<string> ConfigurationViolations,
    string? TerminationReason,
    int ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan Duration,
    IReadOnlyList<string> RemovedEnvironmentVariables)
{
    /// <summary>Il processo è stato terminato dall'orchestratore e non è uscito da solo.</summary>
    public bool TerminatedByController => TerminationReason is not null;

    /// <summary>La sessione si è conclusa da sola con un <c>result</c> di successo.</summary>
    public bool Succeeded =>
        !TerminatedByController && ExitCode == 0 && Tracker.Result is { Subtype: "success", IsError: false };
}

/// <summary>
/// Esegue una sessione CLI: avvio con ambiente ripulito, prompt su stdin, copia dello
/// stream, controllo di <c>system/init</c>, segnalazione dell'uso al controllore della
/// run e terminazione dell'intero albero di processi quando la run si ferma.
/// </summary>
public static class AgentProcess
{
    /// <summary>Motivo registrato per una configurazione non conforme.</summary>
    public const string ConfigurationTerminationReason = "configurazione effettiva diversa da quella prevista";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Esegue la sessione fino all'uscita del processo o alla sua terminazione.
    /// </summary>
    /// <param name="launch">Parametri di avvio.</param>
    /// <param name="limits">Controllore della run, già avviato.</param>
    /// <param name="timeProvider">Orologio per istanti e durata.</param>
    /// <param name="terminate">Se annullato, il processo viene terminato e l'esito restituito con <paramref name="terminateReason"/>.</param>
    /// <param name="terminateReason">Motivo registrato quando interviene <paramref name="terminate"/>.</param>
    /// <param name="cancellationToken">Annullamento esterno: termina il processo e propaga l'annullamento.</param>
    /// <returns>L'esito del processo.</returns>
    /// <exception cref="SessionExecutionException">La CLI non può essere avviata.</exception>
    public static async Task<AgentProcessOutcome> RunAsync(
        AgentLaunch launch,
        RunLimitController limits,
        TimeProvider timeProvider,
        CancellationToken terminate,
        string terminateReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(limits);
        ProcessStartInfo startInfo = CreateStartInfo(launch);
        IReadOnlyList<string> removedVariables = ChildProcessEnvironment.RemoveInheritedAgentVariables(startInfo.Environment);
        var tracker = new StreamUsageTracker();
        var violations = new List<string>();
        string? terminationReason = null;

        await using var streamFile = new StreamWriter(
            new FileStream(launch.StreamPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Utf8WithoutBom);

        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        long startTimestamp = timeProvider.GetTimestamp();
        using Process process = StartProcess(startInfo);
        Task<string> standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        // Il prompt viaggia su stdin: niente limiti di lunghezza o quoting della riga di comando.
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(launch.Prompt, cancellationToken);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // La CLI è già uscita: l'esito emerge da exit code e stream.
        }

        Task stopTask = Task.Delay(Timeout.Infinite, limits.Stopping);
        Task terminateTask = Task.Delay(Timeout.Infinite, terminate);
        Task cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
        Task<string?> pendingRead = process.StandardOutput.ReadLineAsync(CancellationToken.None).AsTask();
        while (true)
        {
            Task completed = await Task.WhenAny(pendingRead, stopTask, terminateTask, cancellationTask);
            if (completed == cancellationTask)
            {
                Terminate(process);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (completed == stopTask || completed == terminateTask)
            {
                terminationReason = completed == stopTask ? limits.StopReason : terminateReason;
                Terminate(process);
                break;
            }

            string? line = await pendingRead;
            if (line is null)
            {
                break;
            }

            await streamFile.WriteAsync(line + "\n");
            if (!string.IsNullOrWhiteSpace(line)
                && tracker.Observe(line) == StreamLineKind.Initialization
                && violations.Count == 0)
            {
                violations.AddRange(launch.FindConfigurationViolations(tracker.Initialization!));
                if (violations.Count > 0)
                {
                    // Una configurazione non conforme invalida l'intera run: si fermano tutti gli agenti.
                    terminationReason = ConfigurationTerminationReason;
                    limits.Stop($"configurazione non conforme dell'agente {launch.AgentId}");
                    Terminate(process);
                    break;
                }
            }

            limits.ReportUsage(launch.AgentId, tracker.ObservedUsage);
            if (limits.Stopping.IsCancellationRequested)
            {
                terminationReason = limits.StopReason;
                Terminate(process);
                break;
            }

            pendingRead = process.StandardOutput.ReadLineAsync(CancellationToken.None).AsTask();
        }

        // Dopo la fine dello stdout il processo deve uscire prima che la run si fermi.
        if (terminationReason is null)
        {
            Task exit = process.WaitForExitAsync(CancellationToken.None);
            Task completed = await Task.WhenAny(exit, stopTask, terminateTask);
            if (completed != exit)
            {
                terminationReason = completed == stopTask ? limits.StopReason : terminateReason;
                Terminate(process);
            }
        }

        await process.WaitForExitAsync(CancellationToken.None);
        TimeSpan duration = timeProvider.GetElapsedTime(startTimestamp);
        DateTimeOffset endedAt = timeProvider.GetUtcNow();
        await File.WriteAllTextAsync(launch.StandardErrorPath, await standardError, Utf8WithoutBom, CancellationToken.None);

        return new AgentProcessOutcome(
            launch.AgentId,
            launch.Role,
            tracker,
            violations,
            terminationReason,
            process.ExitCode,
            startedAt,
            endedAt,
            duration,
            removedVariables);
    }

    /// <summary>
    /// Legge l'output di <c>claude --version</c> con lo stesso ambiente ripulito delle sessioni.
    /// </summary>
    /// <param name="claudePath">Eseguibile della CLI.</param>
    /// <param name="workingDirectory">Directory di lavoro.</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>La versione dichiarata.</returns>
    /// <exception cref="SessionExecutionException">La CLI non risponde o fallisce.</exception>
    public static async Task<string> ReadCliVersionAsync(string claudePath, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(claudePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--version");
        ChildProcessEnvironment.RemoveInheritedAgentVariables(startInfo.Environment);

        using Process process = StartProcess(startInfo);
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(VersionProbeTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Terminate(process);
            throw new SessionExecutionException($"'{claudePath} --version' non ha risposto entro {VersionProbeTimeout.TotalSeconds} secondi.");
        }

        if (process.ExitCode != 0)
        {
            throw new SessionExecutionException($"'{claudePath} --version' è terminato con codice {process.ExitCode}: {await error}");
        }

        return (await output).Trim();
    }

    /// <summary>
    /// Avvia un processo e ne restituisce l'oggetto.
    /// </summary>
    /// <param name="startInfo">Parametri del processo.</param>
    /// <returns>Il processo avviato.</returns>
    /// <exception cref="SessionExecutionException">Avvio non riuscito.</exception>
    public static Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new SessionExecutionException($"Avvio di '{startInfo.FileName}' non riuscito.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new SessionExecutionException($"Avvio di '{startInfo.FileName}' non riuscito: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Termina il processo con l'intero albero, che comprende i server MCP avviati dalla CLI.
    /// </summary>
    /// <param name="process">Processo da terminare.</param>
    public static void Terminate(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Il processo è già terminato.
        }
    }

    private static ProcessStartInfo CreateStartInfo(AgentLaunch launch)
    {
        var startInfo = new ProcessStartInfo(launch.ClaudePath)
        {
            WorkingDirectory = launch.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8WithoutBom,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
        };
        foreach (string argument in launch.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
