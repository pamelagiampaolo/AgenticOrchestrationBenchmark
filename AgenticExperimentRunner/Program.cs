
const string claudeCliPath = @"C:\Users\Utente\.local\bin\claude.exe";
const string requestedModel = "claude-sonnet-5-5";

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

string percorsoRisultati = Path.Combine("results", "esecuzioni.jsonl");
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
