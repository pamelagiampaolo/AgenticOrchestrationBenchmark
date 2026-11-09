using System.IO.Pipes;
using System.Text;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Lato orchestratore del canale di coordinamento: crea la pipe, accetta la sola
/// connessione del relay ed esegue le deleghe una alla volta nell'ordine di arrivo.
/// </summary>
public sealed class CoordinationHost : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly NamedPipeServerStream pipe;
    private readonly CancellationTokenSource stopping = new();
    private int disposed;

    private CoordinationHost(string pipeName, NamedPipeServerStream pipe, IDelegationHandler handler)
    {
        PipeName = pipeName;
        this.pipe = pipe;
        Completion = ServeAsync(handler, stopping.Token);
    }

    /// <summary>Nome della pipe da passare al relay.</summary>
    public string PipeName { get; }

    /// <summary>
    /// Termina normalmente quando il relay chiude o interrompe la connessione; fallisce con
    /// <see cref="CoordinationChannelException"/> per un messaggio non valido o con
    /// l'eccezione del gestore. In entrambi i casi la connessione viene chiusa.
    /// </summary>
    public Task Completion { get; }

    /// <summary>
    /// Nome di pipe casuale, diverso per ogni run.
    /// </summary>
    /// <returns>Nome senza prefisso <c>\\.\pipe\</c>.</returns>
    public static string CreatePipeName() => $"agentic-coordination-{Guid.NewGuid():N}";

    /// <summary>
    /// Crea la pipe e inizia ad attendere il relay.
    /// </summary>
    /// <param name="pipeName">Nome della pipe; la creazione fallisce se esiste già.</param>
    /// <param name="handler">Esecutore delle deleghe.</param>
    /// <returns>L'host avviato.</returns>
    /// <exception cref="CoordinationChannelException">La pipe non può essere creata.</exception>
    public static CoordinationHost Start(string pipeName, IDelegationHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(handler);
        NamedPipeServerStream pipe;
        try
        {
            // Una sola istanza: finché il relay è connesso nessun altro processo può connettersi,
            // e una seconda pipe con lo stesso nome non può essere creata da altri.
            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CoordinationChannelException($"Impossibile creare la pipe '{pipeName}': {exception.Message}", exception);
        }

        return new CoordinationHost(pipeName, pipe, handler);
    }

    /// <summary>
    /// Interrompe l'attesa o la delega in corso e chiude la pipe. Non rilancia i guasti,
    /// che restano osservabili in <see cref="Completion"/>.
    /// </summary>
    /// <returns>Completato quando il servizio è terminato.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        await stopping.CancelAsync();
        try
        {
            await Completion;
        }
        catch (Exception)
        {
            // Il guasto è esposto da Completion; la chiusura non deve sollevarlo di nuovo.
        }

        stopping.Dispose();
    }

    private async Task ServeAsync(IDelegationHandler handler, CancellationToken cancellationToken)
    {
        // Cede subito il controllo: Start non deve eseguire parti del servizio in modo sincrono.
        await Task.Yield();
        try
        {
            await pipe.WaitForConnectionAsync(cancellationToken);
            using var reader = new StreamReader(pipe, Utf8WithoutBom, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var writer = new StreamWriter(pipe, Utf8WithoutBom, leaveOpen: true) { NewLine = "\n" };
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                DelegationRequest request = DelegationWireFormat.Deserialize<DelegationRequest>(line);
                ValidateRequest(request);

                // L'attesa del gestore prima di leggere la riga successiva rende le deleghe sequenziali.
                DelegationOutcome outcome = await handler.DelegateAsync(request, cancellationToken);
                var response = new DelegationResponse(request.RequestId, outcome.Status, outcome.Text);
                await writer.WriteLineAsync(DelegationWireFormat.Serialize(response).AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }
        }
        catch (IOException)
        {
            // Pipe interrotta: il relay è terminato insieme alla sessione del coordinatore.
            // È una fine normale del canale; la terminazione della sessione è registrata dal controllore.
        }
        finally
        {
            await pipe.DisposeAsync();
        }
    }

    /// <summary>
    /// Ripete i controlli del relay: la pipe è un confine tra processi e l'host non
    /// presume che il messaggio provenga da un relay corretto.
    /// </summary>
    private static void ValidateRequest(DelegationRequest request)
    {
        if (request.RequestId <= 0
            || string.IsNullOrWhiteSpace(request.Task)
            || request.Task.Length > CoordinationMcpServer.MaxTaskLength)
        {
            throw new CoordinationChannelException($"Delega non valida ricevuta dal relay (richiesta {request.RequestId}).");
        }
    }
}
