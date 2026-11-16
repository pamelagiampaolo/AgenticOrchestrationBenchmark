# P3 — Collegamento sperimentale MCP–Windows Sandbox

## Stato e perimetro

Iterazione eseguita il 6 novembre 2026. Estende il prototipo MCP P1 con una sola
operazione sperimentale, `run_public_suite`, collegata al runner pubblico P2.2.
Claude, agenti, valutatore, test riservati e congelamento della baseline sono
esclusi.

## Obiettivo, ambito e comportamento atteso

L'obiettivo è verificare che un client MCP possa richiedere un'operazione
predefinita, osservare l'avvio di una nuova Windows Sandbox e ricevere l'esito
strutturato della suite pubblica comune. Il chiamante non deve poter fornire
comandi, argomenti o percorsi.

La chiamata valida deve restituire build e typecheck riusciti, 27 test superati e
zero istanze residue. Parametri estranei devono essere rifiutati prima
dell'avvio. Timeout e report mancante devono produrre errori, mai un falso
successo.

## Implementazione circoscritta

Con il solo argomento root il server conserva l'inventario P1 di `list_files` e
`read_file`. Quando il controllore fornisce anche il percorso fidato del runner
P2.2, aggiunge soltanto:

```text
run_public_suite
inputSchema: { "type": "object", "properties": {}, "additionalProperties": false }
```

[`PublicSuiteRunner.cs`](../../../src/AgentWorkspaceMcpServer/PublicSuiteRunner.cs) riceve
dal controllore, all'avvio del server, il percorso assoluto del runner P2.2. La
chiamata MCP non può modificarlo. Il processo avviato è sempre
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File <runner-fidato>` senza
argomenti derivati dal client.

Il risultato viene letto esclusivamente da `host-verification.json` prodotto da
P2.2. Il server convalida exit code, conteggi 27/27 e arresto della Sandbox prima
di restituire `isError: false` e `structuredContent`. Un exit code non zero,
timeout, report mancante, JSON invalido, campi incompleti o conteggi inattesi
generano un errore JSON-RPC `-32002`.

La variabile `MCP_PUBLIC_SUITE_TIMEOUT_SECONDS` è configurazione fidata del
processo server e non fa parte dello schema MCP. Serve a delimitare il processo e
alla fixture di timeout; non è controllabile dalla chiamata allo strumento.

## Client e prove

[`Invoke-McpSandboxBridgeProbe.ps1`](../../../scripts/legacy-isolation/Invoke-McpSandboxBridgeProbe.ps1)
avvia il server su stdio, esegue handshake, `tools/list` e quattro chiamate:

1. `run_public_suite` con un parametro `command` vietato;
2. `run_public_suite` con oggetto argomenti vuoto e runner P2.2 reale;
3. chiamata con una fixture che supera il timeout del server;
4. chiamata con una fixture che termina senza report.

Durante la chiamata valida il client interroga `wsb list --raw` mentre attende la
risposta, registrando l'istanza osservata. Prima e dopo il rifiuto dei parametri e
dopo ogni scenario registra il numero di istanze attive.

Le fixture
[`TimeoutRunnerFixture.ps1`](../../../experiments/legacy-isolation-probes/McpSandboxBridgeProbe/TimeoutRunnerFixture.ps1)
e
[`MissingReportRunnerFixture.ps1`](../../../experiments/legacy-isolation-probes/McpSandboxBridgeProbe/MissingReportRunnerFixture.ps1)
non contengono materiali del benchmark e non avviano Sandbox: isolano soltanto la
gestione MCP degli errori.

## Risultati osservati

- handshake MCP riuscito, protocollo `2025-06-18`;
- inventario esatto: `list_files`, `read_file`, `run_public_suite`;
- schema dell'operazione: zero proprietà e `additionalProperties: false`;
- parametro estraneo: errore `-32602`, zero Sandbox prima e dopo;
- chiamata valida: una nuova Sandbox osservata durante l'attesa;
- build, typecheck e suite: exit code zero;
- suite: 27 totali, 27 superati, 0 falliti, 0 pending;
- commit riportato: `3231ca6ca4012c52b4f6cfe12c74d19ed70f8172`;
- Node `v24.17.0`, npm `11.13.0`;
- dopo la chiamata valida: zero Sandbox attive;
- timeout controllato: errore `-32002`, nessun successo e zero istanze;
- report mancante controllato: errore `-32002`, nessun successo e zero istanze;
- stato finale: zero Sandbox attive.

## Esito per criterio

| # | Stato | Evidenza sintetica |
| --- | --- | --- |
| 1 | **Superato** | Una sola nuova operazione; schema chiuso e vuoto. |
| 2 | **Superato** | Runner fissato all'avvio; nessun valore della chiamata entra nel comando. |
| 3 | **Superato** | Handshake, lista e chiamate completate dal client stdio. |
| 4 | **Superato** | Risultato strutturato con build/typecheck zero e 27/27. |
| 5 | **Superato** | Nuova istanza osservata; zero istanze dopo la chiamata. |
| 6 | **Superato** | Parametro vietato rifiutato con `-32602` prima di qualsiasi Sandbox. |
| 7 | **Superato con limite** | Timeout restituito come `-32002`; fixture senza Sandbox reale. |
| 8 | **Superato con limite** | Report mancante restituito come `-32002`; fixture senza Sandbox reale. |
| 9 | **Superato** | Il runner reale usa esclusivamente il pacchetto pubblico P2.2. |
| 10 | **Superato** | Parsing, format, build, accettazione e stato finale verificati. |

## Problemi incontrati e correzioni

La prima esecuzione si è fermata prima della Sandbox perché Windows PowerShell
5.1 non espone `ProcessStartInfo.ArgumentList`; il client è stato reso compatibile
usando `Arguments` quotati. Due esecuzioni successive hanno completato tutti i
controlli sostanziali, ma l'asserzione client sullo schema vuoto falliva per il
comportamento di `.Count` su un singolo `PSCustomObject` in PowerShell 5.1. Il
conteggio è stato reso esplicito con `@(...)`. Tutte queste esecuzioni hanno
lasciato zero Sandbox attive. L'accettazione completa finale è terminata con exit
code zero.

## Limiti

Le fixture dimostrano che il livello MCP non trasforma timeout o report mancante
in successo; non inducono tali guasti dopo l'avvio di una Sandbox reale e non
ricertificano l'isolamento P2.1. Il runner P2.2 conserva il proprio timeout
interno e il blocco `finally` per arrestare l'istanza.

Questa prova non congela l'API MCP completa, non designa la baseline definitiva e
non verifica Claude, autorizzazioni per agente, concorrenza, cancellazione MCP o
valutazione riservata. La sintesi stabile e priva di segreti è in
[`evidence/P3-mcp-sandbox-bridge-summary.json`](../evidence/P3-mcp-sandbox-bridge-summary.json).

Il percorso successivo attraverso Claude CLI è stato provato separatamente in
[P4 — Percorso Claude CLI–MCP–Windows
Sandbox](P4-claude-mcp-windows-sandbox.md), senza modificare il contratto P3.
