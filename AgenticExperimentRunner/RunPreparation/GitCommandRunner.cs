using System.Diagnostics;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Esegue comandi Git non interattivi passando gli argomenti senza interpretazione
/// della shell.
/// </summary>
public sealed class GitCommandRunner
{
    private readonly string gitExecutable;

    /// <summary>
    /// Crea un esecutore che usa l'eseguibile Git indicato.
    /// </summary>
    /// <param name="gitExecutable">Nome o percorso dell'eseguibile Git; per impostazione predefinita <c>git</c> dal PATH.</param>
    /// <exception cref="ArgumentException">Il valore è vuoto o composto soltanto da spazi.</exception>
    public GitCommandRunner(string gitExecutable = "git")
    {
        if (string.IsNullOrWhiteSpace(gitExecutable))
        {
            throw new ArgumentException("L'eseguibile Git deve essere specificato.", nameof(gitExecutable));
        }

        this.gitExecutable = gitExecutable;
    }

    /// <summary>
    /// Esegue un comando e restituisce lo standard output testuale senza spazi finali.
    /// </summary>
    /// <param name="workingDirectory">Directory in cui eseguire Git.</param>
    /// <param name="arguments">Argomenti passati singolarmente al processo.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <returns>Lo standard output del comando.</returns>
    /// <exception cref="GitCommandException">Git non è avviabile o termina con exit code diverso da zero.</exception>
    public async Task<string> RunForTextAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        string output = string.Empty;
        await RunWithOutputStreamAsync(
            workingDirectory,
            arguments,
            async (stream, token) =>
            {
                // leaveOpen: lo stream viene poi svuotato e chiuso da RunWithOutputStreamAsync.
                using var reader = new StreamReader(
                    stream,
                    System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: -1,
                    leaveOpen: true);
                output = await reader.ReadToEndAsync(token);
            },
            cancellationToken);

        return output.TrimEnd();
    }

    /// <summary>
    /// Esegue un comando consegnando lo standard output binario a un consumatore,
    /// senza caricarlo interamente in memoria.
    /// </summary>
    /// <param name="workingDirectory">Directory in cui eseguire Git.</param>
    /// <param name="arguments">Argomenti passati singolarmente al processo.</param>
    /// <param name="consumeOutput">Consumatore dello standard output; può non leggerlo fino alla fine.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <exception cref="GitCommandException">Git non è avviabile o termina con exit code diverso da zero.</exception>
    /// <remarks>
    /// Se il consumatore fallisce, il processo Git viene terminato e l'errore del
    /// consumatore viene propagato invariato.
    /// </remarks>
    public async Task RunWithOutputStreamAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        Func<Stream, CancellationToken, Task> consumeOutput,
        CancellationToken cancellationToken = default)
    {
        using Process process = StartProcess(workingDirectory, arguments);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Stream standardOutput = process.StandardOutput.BaseStream;

        try
        {
            await consumeOutput(standardOutput, cancellationToken);

            // Il consumatore può fermarsi prima della fine (per esempio dopo il
            // terminatore di un archivio tar): il resto va letto, altrimenti Git può
            // restare bloccato in scrittura e non terminare.
            await standardOutput.CopyToAsync(Stream.Null, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            TerminateQuietly(process);
            throw;
        }

        string standardError = await standardErrorTask;
        if (process.ExitCode != 0)
        {
            throw new GitCommandException(
                $"Il comando 'git {string.Join(' ', arguments)}' è terminato con exit code {process.ExitCode}: {standardError.Trim()}");
        }
    }

    private Process StartProcess(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Impedisce qualsiasi richiesta interattiva di credenziali.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            return Process.Start(startInfo)
                ?? throw new GitCommandException($"Impossibile avviare '{gitExecutable}'.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new GitCommandException($"Impossibile avviare '{gitExecutable}': {exception.Message}", exception);
        }
    }

    private static void TerminateQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Il processo è terminato tra il controllo e la richiesta di chiusura.
        }
    }
}

/// <summary>
/// Segnala il fallimento di un comando Git, includendo exit code e standard error.
/// </summary>
public sealed class GitCommandException : Exception
{
    /// <summary>
    /// Crea l'eccezione con un messaggio diagnostico.
    /// </summary>
    /// <param name="message">Descrizione del fallimento.</param>
    public GitCommandException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Crea l'eccezione con un messaggio diagnostico e la causa originale.
    /// </summary>
    /// <param name="message">Descrizione del fallimento.</param>
    /// <param name="innerException">Errore originale.</param>
    public GitCommandException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
