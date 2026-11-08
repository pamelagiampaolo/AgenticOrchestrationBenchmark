using AgenticExperimentRunner.Coordination;
using AgenticExperimentRunner.RunPreparation;
using AgenticExperimentRunner.SessionExecution;

if (args.Length > 0 && args[0] == CoordinationServerCommand.Name)
{
    // Relay MCP: stdout è riservato al protocollo, la diagnostica va su stderr.
    Environment.ExitCode = await CoordinationServerCommand.ExecuteAsync(
        args[1..], Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.Error);
    return;
}

if (args.Length > 0 && args[0] == RunAdaptiveHierarchyCommand.Name)
{
    // La CLI del coordinatore avvia questo stesso eseguibile come relay di coordinamento.
    string coordinationServerPath = Environment.ProcessPath
        ?? throw new InvalidOperationException("Percorso dell'eseguibile corrente non disponibile.");
    var runAdaptiveHierarchy = new RunAdaptiveHierarchyCommand(
        new HierarchicalRunRunner(TimeProvider.System, Path.Combine(AppContext.BaseDirectory, "Instructions")),
        coordinationServerPath);
    Environment.ExitCode = await runAdaptiveHierarchy.ExecuteAsync(args[1..], Console.Out, Console.Error);
    return;
}

if (args.Length > 0 && args[0] == PrepareRunCommand.Name)
{
    var prepareRun = new PrepareRunCommand(
        new RunAreaPreparer(
            new GitCommandRunner(),
            TimeProvider.System,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        TimeProvider.System);
    Environment.ExitCode = await prepareRun.ExecuteAsync(args[1..], Console.Out, Console.Error);
    return;
}

if (args.Length > 0 && args[0] == RunSingleAgentCommand.Name)
{
    var runSingleAgent = new RunSingleAgentCommand(
        new SingleAgentSessionRunner(TimeProvider.System, Path.Combine(AppContext.BaseDirectory, "Instructions")));
    Environment.ExitCode = await runSingleAgent.ExecuteAsync(args[1..], Console.Out, Console.Error);
    return;
}

const string claudeCliPath = @"C:\Users\Utente\.local\bin\claude.exe";
const string requestedModel = "claude-sonnet-5-5";

if (!TryPrepareResultsFile(args, out string percorsoRisultati, out string? erroreConfigurazione))
{
    Console.Error.WriteLine($"Errore di configurazione: {erroreConfigurazione}");
    Environment.ExitCode = 2;
    return;
}

Console.Write("Inserisci una richiesta per Claude: ");
string? prompt = Console.ReadLine();

if (string.IsNullOrWhiteSpace(prompt))
{
    Console.WriteLine("Nessuna richiesta inserita.");
    return;
}

var client = new ClaudeCliClient(
    claudeCliPath,
    requestedModel,
    Directory.GetCurrentDirectory());

ClaudeCliResult result = await client.InvokeAsync(prompt);

Console.WriteLine($"Codice di uscita: {result.ExitCode?.ToString() ?? "non disponibile"}");
Console.WriteLine($"Risposta di Claude: {result.Response ?? "non disponibile"}");

if (result.DurationMs is long durationMs)
{
    Console.WriteLine($"Durata esecuzione riportata dalla CLI: {durationMs / 1000.0:F3} secondi");
}
else
{
    Console.WriteLine("Durata esecuzione riportata dalla CLI: non disponibile");
}

Console.WriteLine($"Token generati: {FormatMetric(result.OutputTokens)}");
Console.WriteLine($"Token input (esclusa cache): {FormatMetric(result.InputTokens)}");
Console.WriteLine($"Token letti dalla cache: {FormatMetric(result.CacheReadInputTokens)}");
Console.WriteLine($"Token creazione cache: {FormatMetric(result.CacheCreationInputTokens)}");
Console.WriteLine($"Token input totali (inclusa cache): {FormatMetric(result.TotalInputTokens)}");
Console.WriteLine($"Token complessivi: {FormatMetric(result.TotalTokens)}");

var metriche = new
{
    dataOraUtc = DateTimeOffset.UtcNow,
    richiesta = prompt,
    risposta = result.Response,
    isError = result.IsError,
    codiceUscita = result.ExitCode,
    durataMs = result.DurationMs,
    tokenInput = result.InputTokens,
    tokenCacheRead = result.CacheReadInputTokens,
    tokenCacheCreation = result.CacheCreationInputTokens,
    tokenInputTotali = result.TotalInputTokens,
    tokenOutput = result.OutputTokens,
    tokenTotali = result.TotalTokens,
    modelloRichiesto = requestedModel,
    modelloEffettivo = result.EffectiveModel,
    sessionId = result.SessionId,
    standardError = result.StandardError,
    outputGrezzo = result.RawOutput,
    erroreAvvio = result.StartError,
    erroreParsing = result.ParsingError
};

string jsonMetriche = System.Text.Json.JsonSerializer.Serialize(metriche);
File.AppendAllText(percorsoRisultati, jsonMetriche + Environment.NewLine);


Console.WriteLine($"Metriche salvate in: {percorsoRisultati}");

if (!string.IsNullOrWhiteSpace(result.StandardError))
{
    Console.WriteLine($"Errore CLI: {result.StandardError.Trim()}");
}

if (!string.IsNullOrWhiteSpace(result.StartError))
{
    Console.WriteLine($"Errore di avvio: {result.StartError}");
}

if (!string.IsNullOrWhiteSpace(result.ParsingError))
{
    Console.WriteLine($"Errore di interpretazione dell'output: {result.ParsingError}");
}

static string FormatMetric(long? value) => value?.ToString() ?? "non disponibile";

static bool TryPrepareResultsFile(
    string[] arguments,
    out string resultsFilePath,
    out string? errorMessage)
{
    const string optionName = "--results-file";
    resultsFilePath = string.Empty;
    errorMessage = null;
    string? configuredPath = null;

    for (int index = 0; index < arguments.Length; index++)
    {
        string argument = arguments[index];
        if (!string.Equals(argument, optionName, StringComparison.Ordinal))
        {
            errorMessage = $"Argomento non riconosciuto: '{argument}'. Uso: {optionName} <percorso-assoluto.jsonl>.";
            return false;
        }

        if (configuredPath is not null)
        {
            errorMessage = $"L'argomento {optionName} non può essere specificato più di una volta.";
            return false;
        }

        if (++index >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index]))
        {
            errorMessage = $"L'argomento obbligatorio {optionName} richiede un percorso assoluto a un file JSONL.";
            return false;
        }

        configuredPath = arguments[index];
    }

    if (configuredPath is null)
    {
        errorMessage = $"Argomento obbligatorio mancante. Uso: {optionName} <percorso-assoluto.jsonl>.";
        return false;
    }

    if (!Path.IsPathFullyQualified(configuredPath))
    {
        errorMessage = $"Il percorso fornito a {optionName} deve essere assoluto: '{configuredPath}'.";
        return false;
    }

    try
    {
        string normalizedPath = Path.GetFullPath(configuredPath);
        if (!string.Equals(Path.GetExtension(normalizedPath), ".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"Il percorso fornito a {optionName} deve identificare un file con estensione .jsonl.";
            return false;
        }

        if (Directory.Exists(normalizedPath))
        {
            errorMessage = $"Il percorso fornito a {optionName} identifica una directory, non un file JSONL.";
            return false;
        }

        string? parentDirectory = Path.GetDirectoryName(normalizedPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            errorMessage = $"Impossibile determinare la directory padre del percorso '{normalizedPath}'.";
            return false;
        }

        Directory.CreateDirectory(parentDirectory);
        VerifyDirectoryIsWritable(parentDirectory);
        resultsFilePath = normalizedPath;
        return true;
    }
    catch (Exception exception) when (exception is ArgumentException
        or IOException
        or NotSupportedException
        or UnauthorizedAccessException)
    {
        errorMessage = $"Il percorso dei risultati non è utilizzabile: {exception.Message}";
        return false;
    }
}

static void VerifyDirectoryIsWritable(string directoryPath)
{
    string probePath = Path.Combine(directoryPath, $".write-test-{Guid.NewGuid():N}.tmp");
    using var probe = new FileStream(
        probePath,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 1,
        FileOptions.DeleteOnClose);

    probe.WriteByte(0);
    probe.Flush(flushToDisk: true);
}
