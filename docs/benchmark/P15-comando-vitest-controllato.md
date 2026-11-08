# P15 - Comando Vitest controllato senza isolamento di processo

## Data, obiettivo e decisione

- Data: 2026-11-08.
- Obiettivo: esporre il primo comando di sviluppo Node senza shell o argomenti
  scelti dall'agente.
- Decisione: per rispettare i tempi del progetto, il comando viene eseguito sul
  sistema host senza AppContainer. Prompt, allowlist e audit sono controlli
  operativi e non vengono dichiarati equivalenti a un isolamento di sicurezza.

## Componenti

- `McpReadOnlyPrototype/ControlledPublicTestRunner.cs`;
- tool MCP `run_public_tests`;
- `AgenticExperimentRunner.Tests/ControlledPublicTestRunnerTests.cs`.

## Comportamento

Il tool non accetta proprietà. Percorsi di Node, entry point Vitest,
configurazione, progetto composto e audit vengono forniti dal launcher tramite
variabili d'ambiente. Il processo riceve soltanto gli argomenti fissi:

```text
<vitest-entry-point> run --config <trusted-config> --reporter=json
```

Entry point e configurazione devono essere esterni alla directory modificabile
eseguita. Il log JSONL deve essere esterno alla stessa directory. Il processo ha
un timeout; alla scadenza viene terminato insieme ai figli. Il risultato
distingue `completed`, `failed`, `timeout` e `infrastructure-error` e limita la
dimensione della diagnostica restituita.

Il tool compare nell'inventario soltanto quando il launcher definisce
`MCP_PUBLIC_TEST_NODE` e tutte le altre impostazioni richieste. Il precedente
`run_public_suite`, legato alle evidenze storiche Windows Sandbox, non viene
usato dal nuovo percorso.

## Configurazione del launcher

- `MCP_PUBLIC_TEST_NODE`;
- `MCP_PUBLIC_TEST_VITEST`;
- `MCP_PUBLIC_TEST_CONFIG`;
- `MCP_PUBLIC_TEST_WORKING_DIRECTORY`;
- `MCP_COMMAND_AUDIT_PATH`;
- `MCP_PUBLIC_TEST_TIMEOUT_SECONDS` opzionale, valore predefinito 300.

La directory di lavoro dovrà essere la copia effimera composta secondo P9. Il
launcher definitivo e la verifica d'integrità prima/dopo il comando non fanno
parte di questa iterazione.

## Criteri di accettazione ed evidenze

1. Nessun parametro del chiamante può cambiare comando o percorsi.
2. L'esecuzione registra comando, argomenti fissi, directory, durata ed esito.
3. Un audit collocato dentro il progetto viene rifiutato.
4. Formattazione, build, test mirati e suite .NET completa terminano senza
   errori.

Risultati osservati:

- `dotnet format` sui due progetti con `--no-restore`: exit code 0;
- `dotnet build AgenticExperimentRunner.Tests/... --no-restore`: 0 avvisi e 0
  errori;
- test mirati: 2/2;
- suite completa: 68/68.

Un primo tentativo di formattazione ha indicato una soluzione `.sln` inesistente;
il comando è stato corretto usando i due file `.csproj`. Senza `--no-restore`,
`dotnet format` non ha completato il restore; la verifica finale ha riutilizzato
il restore già presente.

## Limiti e lavoro rimanente

Il codice TypeScript importato da Vitest viene eseguito con i permessi del
processo host. L'audit osserva il comando avviato, ma non prova l'assenza di
letture o comunicazioni effettuate dal codice. Questo rischio è accettato
esplicitamente per la prima esecuzione sperimentale.

La prossima iterazione deve collegare il launcher alla composizione effimera P9
e aggiungere verifica d'integrità prima e dopo l'esecuzione. Soltanto allora il
tool sarà pronto per una run con agente.
