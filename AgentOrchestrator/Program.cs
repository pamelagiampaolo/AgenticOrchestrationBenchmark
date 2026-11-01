
using System.Diagnostics;

var startInfo = new ProcessStartInfo
{
    FileName = @"C:\Users\Utente\.local\bin\claude.exe",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

startInfo.ArgumentList.Add("--output-format");
startInfo.ArgumentList.Add("json");
Console.Write("Inserisci una richiesta per Claude: ");
string? prompt = Console.ReadLine();

if (string.IsNullOrWhiteSpace(prompt))
{
    Console.WriteLine("Nessuna richiesta inserita.");
    return;
}

startInfo.ArgumentList.Add(prompt);

using var process = new Process { StartInfo = startInfo };
process.Start();

var outputTask = process.StandardOutput.ReadToEndAsync();
var errorTask = process.StandardError.ReadToEndAsync();

await process.WaitForExitAsync();

var output = await outputTask;
var error = await errorTask;

Console.WriteLine($"Codice di uscita: {process.ExitCode}");
using var json = System.Text.Json.JsonDocument.Parse(output);
string? risposta = json.RootElement
    .GetProperty("result")
    .GetString();


Console.WriteLine($"Risposta di Claude: {risposta}");
long durataMs = json.RootElement
    .GetProperty("duration_ms")
    .GetInt64();

double durataSecondi = durataMs / 1000.0;


Console.WriteLine($"Durata esecuzione: {durataSecondi:F3} secondi");
int tokenOutput = json.RootElement
    .GetProperty("usage")
    .GetProperty("output_tokens")
    .GetInt32();


Console.WriteLine($"Token generati: {tokenOutput}");
int tokenInput = json.RootElement
    .GetProperty("usage")
    .GetProperty("input_tokens")
    .GetInt32();


Console.WriteLine($"Token input (esclusa cache): {tokenInput}");
int tokenCacheRead = json.RootElement
    .GetProperty("usage")
    .GetProperty("cache_read_input_tokens")
    .GetInt32();


Console.WriteLine($"Token letti dalla cache: {tokenCacheRead}");
int tokenCacheCreation = json.RootElement
    .GetProperty("usage")
    .GetProperty("cache_creation_input_tokens")
    .GetInt32();


Console.WriteLine($"Token creazione cache: {tokenCacheCreation}");
long tokenInputTotali = (long)tokenInput
    + tokenCacheRead
    + tokenCacheCreation;



Console.WriteLine($"Token input totali (inclusa cache): {tokenInputTotali}");
long tokenTotali = tokenInputTotali + tokenOutput;


Console.WriteLine($"Token complessivi: {tokenTotali}");
string percorsoRisultati = Path.Combine("results", "esecuzioni.jsonl");
var metriche = new
{
    dataOraUtc = DateTimeOffset.UtcNow,
    richiesta = prompt,
    risposta,
    codiceUscita = process.ExitCode,
    durataMs,
    tokenInput,
    tokenCacheRead,
    tokenCacheCreation,
    tokenInputTotali,
    tokenOutput,
    tokenTotali
};

string jsonMetriche = System.Text.Json.JsonSerializer.Serialize(metriche);
File.AppendAllText(percorsoRisultati, jsonMetriche + Environment.NewLine);


Console.WriteLine($"Metriche salvate in: {percorsoRisultati}");




if (!string.IsNullOrWhiteSpace(error))
{
    Console.WriteLine($"Errore: {error.Trim()}");
}

