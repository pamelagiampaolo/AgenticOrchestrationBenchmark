namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Ciclo del relay: legge righe MCP dal client, le gestisce in modo concorrente e scrive
/// le risposte. La concorrenza serve a rispondere a <c>ping</c> mentre una delega è in
/// corso; la sequenzialità delle deleghe è garantita dall'host, non dal relay.
/// </summary>
public static class CoordinationRelay
{
    /// <summary>
    /// Esegue il relay fino alla chiusura dell'input.
    /// </summary>
    /// <param name="input">Righe MCP in arrivo dalla CLI.</param>
    /// <param name="output">Destinazione delle risposte.</param>
    /// <param name="server">Protocollo MCP dello strumento di delega.</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>Completato quando l'input è chiuso e le richieste in corso sono state abbandonate.</returns>
    /// <remarks>
    /// Alla chiusura dell'input la CLI ha terminato la sessione: le attese ancora aperte
    /// vengono annullate senza scrivere risposte, perché nessuno le leggerebbe.
    /// </remarks>
    public static async Task RunAsync(
        TextReader input,
        TextWriter output,
        CoordinationMcpServer server,
        CancellationToken cancellationToken)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var writeLock = new SemaphoreSlim(1, 1);
        var inFlight = new List<Task>();
        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            inFlight.RemoveAll(task => task.IsCompleted);
            inFlight.Add(HandleAsync(line, output, server, writeLock, stopping.Token));
        }

        await stopping.CancelAsync();
        await Task.WhenAll(inFlight);
    }

    private static async Task HandleAsync(
        string line,
        TextWriter output,
        CoordinationMcpServer server,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        try
        {
            string? response = await server.HandleLineAsync(line, cancellationToken);
            if (response is null)
            {
                return;
            }

            await writeLock.WaitAsync(cancellationToken);
            try
            {
                await output.WriteLineAsync(response.AsMemory(), cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Sessione terminata: la risposta non ha più destinatario.
        }
    }
}
