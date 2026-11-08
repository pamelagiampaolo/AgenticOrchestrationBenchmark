# P5 — Workspace MCP con scrittura confinata

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** implementazione e prova diretta completate; prova tramite Claude CLI
  eseguita ma non superata per mancato caricamento del server MCP.
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

Non sono ancora verificati:

- l'uso di `write_file` attraverso una sessione Claude CLI reale;
- il collegamento del workspace MCP all'orchestratore;
- la preparazione di una copia pulita e priva di `.git` per ogni run;
- il confine del processo che eseguirà build e test;
- la raccolta completa di eventi, token e tempi della run.

La prova P5 certifica soltanto il confine osservato delle operazioni file MCP e
non l'isolamento completo dell'esperimento.
