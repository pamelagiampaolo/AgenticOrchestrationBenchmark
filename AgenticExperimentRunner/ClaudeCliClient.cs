using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Esegue una singola invocazione non interattiva di Claude CLI e ne interpreta l'output JSONL.
/// </summary>
public sealed class ClaudeCliClient
{
    private readonly string cliPath;
    private readonly string model;
    private readonly string workingDirectory;

    /// <summary>
    /// Inizializza un client con configurazione esplicita dell'eseguibile, del modello e della directory di lavoro.
    /// </summary>
    /// <param name="cliPath">Percorso dell'eseguibile Claude CLI.</param>
    /// <param name="model">Identificatore completo del modello richiesto.</param>
    /// <param name="workingDirectory">Directory di lavoro assegnata al processo.</param>
    /// <exception cref="ArgumentException">Uno dei valori è vuoto o composto soltanto da spazi.</exception>
    public ClaudeCliClient(string cliPath, string model, string workingDirectory)
    {
        this.cliPath = RequireValue(cliPath, nameof(cliPath));
        this.model = RequireValue(model, nameof(model));
        this.workingDirectory = RequireValue(workingDirectory, nameof(workingDirectory));
    }

    /// <summary>
    /// Avvia Claude CLI in modalità non interattiva e restituisce tutti i dati osservabili della chiamata.
    /// </summary>
    /// <param name="prompt">Richiesta da inviare a Claude.</param>
    /// <param name="cancellationToken">Segnale facoltativo per interrompere l'attesa del processo.</param>
    /// <returns>
    /// Un risultato che conserva anche output grezzo e diagnostica. Errori di avvio, parsing o uscita
    /// non zero restano rappresentati nel risultato e non vengono scartati.
    /// </returns>
    /// <exception cref="ArgumentException">Il prompt è vuoto o composto soltanto da spazi.</exception>
    /// <exception cref="OperationCanceledException">L'attesa viene annullata tramite il token fornito.</exception>
    public async Task<ClaudeCliResult> InvokeAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        RequireValue(prompt, nameof(prompt));

        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(model);
        startInfo.ArgumentList.Add("--output-format");
        startInfo.ArgumentList.Add("stream-json");
        startInfo.ArgumentList.Add("--verbose");
        startInfo.ArgumentList.Add(prompt);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return ParseOutput(string.Empty, string.Empty, null, exception.Message);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        string output = await outputTask;
        string error = await errorTask;

        return ParseOutput(output, error, process.ExitCode, null);
    }

    internal static ClaudeCliResult ParseOutput(
        string output,
        string standardError,
        int? exitCode,
        string? startError = null)
    {
        JsonElement? finalResult = null;
        string? sessionId = null;
        string? effectiveModel = null;
        var parsingErrors = new List<string>();
        int lineNumber = 0;

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    parsingErrors.Add(
                        $"Riga {lineNumber}: atteso un oggetto JSON, trovato {root.ValueKind}.");
                    continue;
                }

                sessionId ??= GetString(root, "session_id");
                effectiveModel ??= GetString(root, "model") ?? GetNestedString(root, "message", "model");

                if (GetString(root, "type") == "result")
                {
                    finalResult = root.Clone();
                    sessionId = GetString(root, "session_id") ?? sessionId;
                    effectiveModel = GetString(root, "model") ?? effectiveModel;
                }
            }
            catch (JsonException exception)
            {
                parsingErrors.Add($"Riga {lineNumber}: {exception.Message}");
            }
        }

        if (finalResult is null)
        {
            parsingErrors.Add("Evento finale di tipo 'result' assente.");
        }

        JsonElement? usage = finalResult is JsonElement resultElement
            && resultElement.TryGetProperty("usage", out JsonElement usageElement)
            && usageElement.ValueKind == JsonValueKind.Object
                ? usageElement
                : null;

        return new ClaudeCliResult(
            exitCode,
            finalResult is JsonElement result ? GetString(result, "result") : null,
            finalResult is JsonElement errorResult ? GetBoolean(errorResult, "is_error") : null,
            finalResult is JsonElement durationResult ? GetInt64(durationResult, "duration_ms") : null,
            usage is JsonElement inputUsage ? GetInt64(inputUsage, "input_tokens") : null,
            usage is JsonElement cacheReadUsage ? GetInt64(cacheReadUsage, "cache_read_input_tokens") : null,
            usage is JsonElement cacheCreationUsage ? GetInt64(cacheCreationUsage, "cache_creation_input_tokens") : null,
            usage is JsonElement outputUsage ? GetInt64(outputUsage, "output_tokens") : null,
            sessionId,
            effectiveModel,
            output,
            standardError,
            startError,
            parsingErrors.Count == 0 ? null : string.Join(" ", parsingErrors));
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Il valore non può essere vuoto.", parameterName);
        }

        return value;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? GetNestedString(
        JsonElement element,
        string objectPropertyName,
        string valuePropertyName) =>
        element.TryGetProperty(objectPropertyName, out JsonElement nested)
        && nested.ValueKind == JsonValueKind.Object
            ? GetString(nested, valuePropertyName)
            : null;

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt64(out long value)
            ? value
            : null;

    private static bool? GetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property)
            ? property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            }
            : null;
}
