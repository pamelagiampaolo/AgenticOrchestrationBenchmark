# P5 — Workspace MCP con scrittura confinata

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** implementazione, prova diretta e prova tramite Claude CLI completate
  con esito positivo dopo la correzione del nome MCP riservato.
- **Obiettivo:** evolvere il prototipo MCP di sola lettura affinché un agente possa
  creare e aggiornare file soltanto nella propria root di lavoro, senza dipendere
  da Windows Sandbox.

L'iterazione non collega ancora l'esecuzione della suite pubblica al processo
host e non modifica l'orchestratore. Il vecchio runner Windows Sandbox rimane
disponibile soltanto quando viene passato esplicitamente come secondo argomento;
non fa parte del percorso MCP verificato in P5.

## Componenti modificati

- `McpReadOnlyPrototype/RootedFileAccess.cs`: aggiunta della scrittura UTF-8
  confinata e creazione delle directory interne necessarie;
- `McpReadOnlyPrototype/Program.cs`: aggiunta dello strumento MCP `write_file` e
  identificazione del server come `workspace-prototype` versione `0.2.0`;
- `AgenticExperimentRunner.Tests/RootedFileAccessTests.cs`: casi positivi e
  negativi per la scrittura;
- `scripts/Invoke-McpReadOnlyPrototype.ps1`: inventario `workspace`, prove dirette
  di scrittura e configurazione predisposta per una futura prova Claude CLI.

I nomi del progetto e dello script conservano per ora il termine storico
`ReadOnlyPrototype`; la loro rinomina non è necessaria per verificare il confine
funzionale ed è rinviata a un'iterazione separata.

## Comportamento verificato

`write_file` riceve esclusivamente `path` e `content`. Il percorso deve essere
relativo alla root assegnata. Le directory interne mancanti vengono create; un
file esistente viene sostituito integralmente. Percorsi assoluti, attraversamenti
`..` e directory collegate a destinazioni esterne vengono rifiutati.

La prova MCP diretta ha osservato:

- server `workspace-prototype` versione `0.2.0`;
- inventario esatto `list_files`, `read_file`, `write_file` quando il server viene
  avviato senza runner opzionale;
- scrittura e rilettura di 19 caratteri nella root assegnata;
- nessuna dipendenza da Windows Sandbox nel percorso esercitato;
- superamento delle verifiche negative dello script per lettura e scrittura
  assoluta, traversal e junction esterna.

## Comandi e risultati osservati

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
```

Risultato finale: formattazione superata; build completata con zero avvisi e zero
errori; 5 test eseguiti, 5 superati, 0 falliti, 0 ignorati.

```powershell
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Direct
```

Risultato finale: exit code `0`; restore offline e publish riusciti; verifiche
dirette completate. Le evidenze non versionate sono state prodotte sotto
`%TEMP%\mcp-readonly-prototype-834c6ec7ab594fd39b9aee5c86097363\evidence`.

Il primo tentativo dello stesso script nell'ambiente ristretto della sessione
Codex non ha potuto leggere la configurazione NuGet dell'utente. È stato ripetuto
con accesso al normale ambiente host ed è riuscito. Questo errore non riguarda il
confine implementato dal workspace MCP.

## Limiti e lavoro rimanente

Il confinamento degli strumenti MCP non confina automaticamente il codice
modificato dall'agente quando viene eseguito come processo host. Per questo P5
non sostituisce il runner Sandbox con un semplice `npm test` sul computer host:
una submission malevola potrebbe tentare accessi oltre la root durante import,
build o test.

## Prova Claude CLI dell'8 novembre 2026

Sono stati eseguiti due tentativi controllati con Claude Code `2.1.312`. Il primo
ha riusato la configurazione P1.1 con `--restricted`, `--strict-mcp-config` e
`--tools ''`. La CLI è terminata con exit code `0`, ma l'evento di
inizializzazione riportava zero server MCP e zero strumenti; Claude non ha
effettuato tool call e ha dichiarato di non poter eseguire la sequenza.

Nel secondo tentativo il launcher è stato allineato alla configurazione stdio
documentata dalla versione corrente: rimosso il campo MCP `type`, rimosso
`--strict-mcp-config` dalla sessione agente e disabilitati i connettori Claude.ai
tramite `ENABLE_CLAUDEAI_MCP_SERVERS=false`. L'esito è rimasto invariato: zero
server, zero strumenti e zero tool call. Le evidenze non versionate sono in:

- `%TEMP%\mcp-readonly-prototype-66a2066fd57748c19a49aa50d7cd78e2\evidence`;
- `%TEMP%\mcp-readonly-prototype-630f3f8e74634faf85e4a8c6e22c9bd5\evidence`.

In entrambi i tentativi sono invece risultati veri i controlli su exit code,
assenza di slash command, skill, deleghe e hook, mancata scrittura esterna e
mancata divulgazione del contenuto esterno. Queste proprietà non compensano il
mancato caricamento dell'MCP e la prova complessiva resta non superata.

La diagnostica locale ha confermato che il server risponde direttamente, mentre
la CLI non lo include nell'inizializzazione. La prossima ipotesi da verificare è
l'interazione della CLI `2.1.312` tra `--tools ''` e gli strumenti MCP: il flag
potrebbe ora eliminare l'intero inventario invece dei soli strumenti built-in.
Non sono stati eseguiti ulteriori tentativi LLM in questa iterazione.

### Verifiche successive sulla CLI 2.1.312

Una prova successiva ha rimosso soltanto `--tools ''`. Il server MCP è rimasto
assente, mentre l'inventario ha esposto 13 strumenti built-in. Claude ha invocato
due volte `ToolSearch`, senza trovare strumenti MCP. L'ipotesi che
`--tools ''` fosse la causa del mancato caricamento è quindi confutata; il flag
resta necessario per eliminare gli strumenti built-in dalla superficie agente.

È stato poi osservato che PowerShell aveva serializzato `mcp.json` e i file di
settings con BOM UTF-8 (`EF BB BF`). Il launcher è stato corretto per scrivere i
file di configurazione con `UTF8Encoding(false)` ed è stata ripetuta la prova con
`--tools ''`. Anche questa volta l'inizializzazione ha riportato zero server,
zero strumenti e zero tool call. La presenza del BOM non era quindi la causa.

Le evidenze non versionate dei due tentativi aggiuntivi sono in:

- `%TEMP%\mcp-readonly-prototype-12b7ed6e8d5d45c28c05f0f3652408f8\evidence`;
- `%TEMP%\mcp-readonly-prototype-5013e0e3c16441368b1bcdba4d984267\evidence`.

Il tentativo di sostituire il quoting manuale con
`ProcessStartInfo.ArgumentList` non ha avviato Claude perché il runtime di
Windows PowerShell usato dallo script espone la proprietà come `null`. La
modifica è stata annullata e non costituisce una prova a favore o contro il
caricamento MCP.

## Causa radice e soluzione verificata

La documentazione della CLI `2.1.312` indica che un server `--mcp-config`
scartato compare in `system/init.mcp_server_errors`. L'harness non registrava
questo campo. Una prova minima con log MCP ha mostrato l'errore autorevole:

```text
"workspace" is a reserved MCP server name and was not loaded
```

La causa radice era quindi il nome riservato `workspace` usato come chiave del
server, non il protocollo del server, Windows Sandbox, `--restricted`,
`--tools ''`, il BOM o il quoting degli argomenti.

La chiave è stata rinominata `benchmark_workspace` e gli strumenti autorizzati
sono diventati:

- `mcp__benchmark_workspace__list_files`;
- `mcp__benchmark_workspace__read_file`;
- `mcp__benchmark_workspace__write_file`.

L'harness ora acquisisce anche `mcp_server_errors` e fallisce quando contiene
un errore non nullo. La prima prova dopo la rinomina ha già superato tutte le
proprietà operative, ma un controllo PowerShell trattava il campo assente `null`
come una collezione con un elemento. Il controllo è stato corretto filtrando i
valori null.

La prova finale ha terminato con exit code `0` e ha osservato:

- un solo server `benchmark_workspace`, stato `connected`, nessun errore MCP;
- inventario esatto dei tre strumenti autorizzati;
- otto tool call previste e otto risultati;
- elenco, lettura e scrittura interne riuscite;
- lettura assoluta, traversal di lettura, junction di lettura, traversal di
  scrittura e junction di scrittura rifiutati;
- nessuna scrittura esterna, divulgazione, delega o esecuzione di hook.

Le evidenze conclusive non versionate sono in
`%TEMP%\mcp-readonly-prototype-acc13ee5e112491a8066775c529413b6\evidence`.

Non sono ancora verificati:

- il collegamento del workspace MCP all'orchestratore;
- la preparazione di una copia pulita e priva di `.git` per ogni run;
- il confine del processo che eseguirà build e test;
- la raccolta completa di eventi, token e tempi della run.

La prova P5 certifica soltanto il confine osservato delle operazioni file MCP e
non l'isolamento completo dell'esperimento.
