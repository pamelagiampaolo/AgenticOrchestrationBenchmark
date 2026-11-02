/// <summary>
/// Contiene l'esito osservabile di una singola invocazione di Claude CLI.
/// </summary>
/// <param name="ExitCode">Codice di uscita del processo, oppure <see langword="null"/> se il processo non è stato avviato.</param>
/// <param name="Response">Risposta contenuta nell'evento finale <c>result</c>, se disponibile.</param>
/// <param name="IsError">Indicatore di errore dichiarato dall'evento finale, oppure <see langword="null"/> se assente.</param>
/// <param name="DurationMs">Durata in millisecondi dichiarata dalla CLI nell'evento finale.</param>
/// <param name="InputTokens">Token di input non serviti dalla cache dichiarati nell'evento finale.</param>
/// <param name="CacheReadInputTokens">Token di input letti dalla cache dichiarati nell'evento finale.</param>
/// <param name="CacheCreationInputTokens">Token di input usati per creare la cache dichiarati nell'evento finale.</param>
/// <param name="OutputTokens">Token di output dichiarati nell'evento finale.</param>
/// <param name="SessionId">Identificatore della sessione, se osservabile nel flusso.</param>
/// <param name="EffectiveModel">Modello effettivo, se osservabile nel flusso.</param>
/// <param name="RawOutput">Output standard completo prodotto dalla CLI.</param>
/// <param name="StandardError">Output diagnostico completo prodotto sullo stream di errore.</param>
/// <param name="StartError">Errore verificatosi prima o durante l'avvio del processo.</param>
/// <param name="ParsingError">Errore rilevato durante l'interpretazione del flusso JSONL.</param>
public sealed record ClaudeCliResult(
    int? ExitCode,
    string? Response,
    bool? IsError,
    long? DurationMs,
    long? InputTokens,
    long? CacheReadInputTokens,
    long? CacheCreationInputTokens,
    long? OutputTokens,
    string? SessionId,
    string? EffectiveModel,
    string RawOutput,
    string StandardError,
    string? StartError,
    string? ParsingError)
{
    /// <summary>
    /// Restituisce il totale dei token di input soltanto quando tutte le relative categorie sono disponibili.
    /// </summary>
    public long? TotalInputTokens =>
        InputTokens is long inputTokens
        && CacheReadInputTokens is long cacheReadInputTokens
        && CacheCreationInputTokens is long cacheCreationInputTokens
            ? inputTokens + cacheReadInputTokens + cacheCreationInputTokens
            : null;

    /// <summary>
    /// Restituisce il totale complessivo soltanto quando input totale e output sono entrambi disponibili.
    /// </summary>
    public long? TotalTokens =>
        TotalInputTokens is long totalInputTokens && OutputTokens is long outputTokens
            ? totalInputTokens + outputTokens
            : null;
}
