using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Esito di una delega restituito al coordinatore.
/// </summary>
public enum DelegationStatus
{
    /// <summary>Il lavoratore ha concluso la sessione con successo.</summary>
    Completed,

    /// <summary>Il lavoratore è stato avviato ma la sessione non è riuscita.</summary>
    Failed,

    /// <summary>L'orchestratore ha rifiutato la delega senza avviare un lavoratore (per esempio per un limite).</summary>
    Rejected,
}

/// <summary>
/// Delega inoltrata dal relay all'orchestratore.
/// </summary>
/// <param name="RequestId">Identificatore assegnato dal relay, univoco nella connessione.</param>
/// <param name="Task">Compito del lavoratore scritto dal coordinatore.</param>
public sealed record DelegationRequest(long RequestId, string Task);

/// <summary>
/// Risposta dell'orchestratore a una delega.
/// </summary>
/// <param name="RequestId">Identificatore della richiesta a cui risponde.</param>
/// <param name="Status">Esito.</param>
/// <param name="Text">Testo restituito al coordinatore (riepilogo del lavoratore o motivo del rifiuto).</param>
public sealed record DelegationResponse(long RequestId, DelegationStatus Status, string Text);

/// <summary>
/// Esito prodotto dal gestore delle deleghe.
/// </summary>
/// <param name="Status">Esito.</param>
/// <param name="Text">Testo restituito al coordinatore.</param>
public sealed record DelegationOutcome(DelegationStatus Status, string Text);

/// <summary>
/// Esegue le deleghe dal lato dell'orchestratore. L'host garantisce che non riceva
/// mai due deleghe contemporaneamente.
/// </summary>
public interface IDelegationHandler
{
    /// <summary>
    /// Esegue una delega e ne restituisce l'esito.
    /// </summary>
    /// <param name="request">Delega ricevuta.</param>
    /// <param name="cancellationToken">Annullato quando l'host viene chiuso.</param>
    /// <returns>Esito da restituire al coordinatore.</returns>
    /// <remarks>
    /// Un'eccezione indica un guasto dell'orchestratore, non un esito del lavoratore:
    /// l'host chiude il canale e il guasto resta osservabile in <see cref="CoordinationHost.Completion"/>.
    /// </remarks>
    Task<DelegationOutcome> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Formato delle righe JSON scambiate sulla pipe tra relay e orchestratore.
/// </summary>
public static class DelegationWireFormat
{
    /// <summary>Opzioni di serializzazione: camelCase, stati in kebab-case, una riga per messaggio.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false) },
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Serializza un messaggio su una sola riga.
    /// </summary>
    /// <typeparam name="TMessage">Tipo del messaggio.</typeparam>
    /// <param name="message">Messaggio.</param>
    /// <returns>JSON senza fine riga.</returns>
    public static string Serialize<TMessage>(TMessage message) => JsonSerializer.Serialize(message, SerializerOptions);

    /// <summary>
    /// Interpreta una riga ricevuta dalla pipe.
    /// </summary>
    /// <typeparam name="TMessage">Tipo atteso.</typeparam>
    /// <param name="line">Riga JSON.</param>
    /// <returns>Il messaggio.</returns>
    /// <exception cref="CoordinationChannelException">La riga non è un messaggio valido del tipo atteso.</exception>
    public static TMessage Deserialize<TMessage>(string line)
        where TMessage : class
    {
        try
        {
            return JsonSerializer.Deserialize<TMessage>(line, SerializerOptions)
                ?? throw new CoordinationChannelException($"Messaggio nullo sulla pipe di coordinamento.");
        }
        catch (JsonException exception)
        {
            throw new CoordinationChannelException(
                $"Messaggio non valido sulla pipe di coordinamento: {exception.Message}", exception);
        }
    }
}

/// <summary>
/// Guasto del canale tra relay e orchestratore.
/// </summary>
public sealed class CoordinationChannelException : Exception
{
    /// <summary>Crea l'eccezione con un messaggio diagnostico.</summary>
    /// <param name="message">Messaggio diagnostico.</param>
    public CoordinationChannelException(string message)
        : base(message)
    {
    }

    /// <summary>Crea l'eccezione con un messaggio diagnostico e la causa.</summary>
    /// <param name="message">Messaggio diagnostico.</param>
    /// <param name="innerException">Causa.</param>
    public CoordinationChannelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
