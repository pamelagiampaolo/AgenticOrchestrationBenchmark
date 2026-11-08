using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Comando <c>coordination-server</c>: relay MCP stdio avviato dalla CLI del coordinatore,
/// collegato all'orchestratore tramite la pipe indicata.
/// </summary>
public static partial class CoordinationServerCommand
{
    /// <summary>Nome del sottocomando sulla riga di comando.</summary>
    public const string Name = "coordination-server";

    /// <summary>Input chiuso dalla CLI: fine normale.</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Pipe dell'orchestratore non raggiungibile.</summary>
    public const int ChannelFailedExitCode = 1;

    /// <summary>Argomenti non validi.</summary>
    public const int UsageErrorExitCode = 2;

    /// <summary>Attesa predefinita della connessione alla pipe.</summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

    private const string Usage = "Uso: coordination-server --pipe <nome> [--connect-timeout-ms <n>]";

    // Stdin e stdout della CLI sono UTF-8: la codifica predefinita della console di Windows
    // altererebbe i caratteri non ASCII dei compiti.
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Esegue il relay.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="standardInput">Stream da cui la CLI invia le richieste MCP.</param>
    /// <param name="standardOutput">Stream su cui scrivere le risposte MCP.</param>
    /// <param name="standardError">Destinazione della diagnostica (mai stdout, riservato a MCP).</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        Stream standardInput,
        Stream standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        if (!TryParse(arguments, out string? pipeName, out TimeSpan connectTimeout, out string? error))
        {
            await standardError.WriteLineAsync($"Errore negli argomenti: {error}");
            return UsageErrorExitCode;
        }

        DelegationPipeClient client;
        try
        {
            client = await DelegationPipeClient.ConnectAsync(pipeName!, connectTimeout, cancellationToken);
        }
        catch (CoordinationChannelException exception)
        {
            await standardError.WriteLineAsync($"Canale di coordinamento non disponibile: {exception.Message}");
            return ChannelFailedExitCode;
        }

        await using (client)
        {
            using var input = new StreamReader(standardInput, Utf8WithoutBom, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var output = new StreamWriter(standardOutput, Utf8WithoutBom, leaveOpen: true) { NewLine = "\n" };
            await CoordinationRelay.RunAsync(input, output, new CoordinationMcpServer(client.DelegateAsync), cancellationToken);
        }

        return SuccessExitCode;
    }

    /// <summary>
    /// Interpreta gli argomenti.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="pipeName">Nome della pipe.</param>
    /// <param name="connectTimeout">Attesa massima della connessione.</param>
    /// <param name="error">Messaggio diagnostico, se gli argomenti non sono validi.</param>
    /// <returns><see langword="true"/> se gli argomenti sono validi.</returns>
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out string? pipeName,
        out TimeSpan connectTimeout,
        out string? error)
    {
        pipeName = null;
        connectTimeout = DefaultConnectTimeout;
        if (!CommandLineOptions.TryParse(arguments, ["--pipe"], ["--connect-timeout-ms"], Usage, out IReadOnlyDictionary<string, string> values, out error))
        {
            return false;
        }

        if (!PipeNamePattern().IsMatch(values["--pipe"]))
        {
            error = $"Nome di pipe non valido: '{values["--pipe"]}'. {Usage}";
            return false;
        }

        if (values.TryGetValue("--connect-timeout-ms", out string? timeoutText))
        {
            if (!int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds) || milliseconds <= 0)
            {
                error = $"--connect-timeout-ms deve essere un intero positivo: '{timeoutText}'.";
                return false;
            }

            connectTimeout = TimeSpan.FromMilliseconds(milliseconds);
        }

        pipeName = values["--pipe"];
        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}\z")]
    private static partial Regex PipeNamePattern();
}
