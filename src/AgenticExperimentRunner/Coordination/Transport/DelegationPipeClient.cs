using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Lato relay della pipe di coordinamento: invia le deleghe all'orchestratore e
/// associa le risposte alle richieste tramite <see cref="DelegationRequest.RequestId"/>.
/// </summary>
public sealed class DelegationPipeClient : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream pipe;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<DelegationResponse>> pending = new();
    private readonly Task readLoop;
    private long lastRequestId;
    private volatile CoordinationChannelException? failure;

    private DelegationPipeClient(Stream pipe)
    {
        this.pipe = pipe;
        reader = new StreamReader(pipe, Utf8WithoutBom, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        writer = new StreamWriter(pipe, Utf8WithoutBom, leaveOpen: true) { NewLine = "\n" };
        readLoop = ReadResponsesAsync();
    }

    /// <summary>
    /// Si connette alla pipe dell'orchestratore, riservata all'utente corrente.
    /// </summary>
    /// <param name="pipeName">Nome della pipe creata da <see cref="CoordinationHost"/>.</param>
    /// <param name="timeout">Attesa massima della connessione.</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>Il client connesso.</returns>
    /// <exception cref="CoordinationChannelException">La pipe non è raggiungibile entro il tempo indicato.</exception>
    public static async Task<DelegationPipeClient> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stream = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await stream.ConnectAsync(timeout, cancellationToken);
            return new DelegationPipeClient(stream);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            await stream.DisposeAsync();
            throw new CoordinationChannelException(
                $"Connessione alla pipe '{pipeName}' non riuscita entro {timeout.TotalMilliseconds:F0} ms: {exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// Inoltra un compito e attende l'esito.
    /// </summary>
    /// <param name="task">Compito già validato dal protocollo MCP.</param>
    /// <param name="cancellationToken">Annullamento dell'attesa (la delega lato orchestratore non viene revocata).</param>
    /// <returns>Esito della delega.</returns>
    /// <exception cref="CoordinationChannelException">Il canale è chiuso o guasto.</exception>
    public async Task<DelegationOutcome> DelegateAsync(string task, CancellationToken cancellationToken)
    {
        long requestId = Interlocked.Increment(ref lastRequestId);
        var completion = new TaskCompletionSource<DelegationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        // La registrazione precede il controllo del guasto: se il ciclo di lettura fallisce
        // subito dopo, trova questa richiesta tra quelle in attesa e la conclude con l'errore.
        pending[requestId] = completion;
        try
        {
            ThrowIfFailed();
            await SendAsync(new DelegationRequest(requestId, task), cancellationToken);
            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
            {
                DelegationResponse response = await completion.Task;
                return new DelegationOutcome(response.Status, response.Text);
            }
        }
        finally
        {
            pending.TryRemove(requestId, out _);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Fail(new CoordinationChannelException("Il relay ha chiuso il canale di coordinamento."));
        await pipe.DisposeAsync();
        try
        {
            await readLoop;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // Il ciclo di lettura termina per la chiusura della pipe: è l'esito atteso.
        }

        writeLock.Dispose();
    }

    private async Task SendAsync(DelegationRequest request, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await writer.WriteLineAsync(DelegationWireFormat.Serialize(request).AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            throw new CoordinationChannelException($"Invio della delega non riuscito: {exception.Message}", exception);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task ReadResponsesAsync()
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                DelegationResponse response = DelegationWireFormat.Deserialize<DelegationResponse>(line);
                if (!pending.TryGetValue(response.RequestId, out TaskCompletionSource<DelegationResponse>? completion))
                {
                    throw new CoordinationChannelException(
                        $"Risposta per una richiesta sconosciuta: {response.RequestId}.");
                }

                completion.TrySetResult(response);
            }

            Fail(new CoordinationChannelException("L'orchestratore ha chiuso il canale di coordinamento."));
        }
        catch (CoordinationChannelException exception)
        {
            Fail(exception);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            Fail(new CoordinationChannelException($"Lettura dalla pipe interrotta: {exception.Message}", exception));
        }
    }

    private void Fail(CoordinationChannelException exception)
    {
        failure ??= exception;
        foreach (TaskCompletionSource<DelegationResponse> completion in pending.Values)
        {
            completion.TrySetException(failure);
        }
    }

    private void ThrowIfFailed()
    {
        if (failure is { } exception)
        {
            throw new CoordinationChannelException(exception.Message, exception);
        }
    }
}
