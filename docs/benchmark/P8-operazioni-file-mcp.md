# P8 — Operazioni file MCP minime

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** implementazione, test, prova MCP diretta e prova Claude CLI
  completati con esito positivo.
- **Obiettivo:** completare il set minimo di operazioni file dell'agente con
  cancellazione e spostamento confinati alle aree scrivibili definite in P7.

L'iterazione non introduce una shell, l'esecuzione di processi, build o test.
Non modifica la preparazione delle run, che rimane responsabilità del runner.

## Comportamento implementato

L'inventario ordinario del server contiene esattamente:

```text
list_files, read_file, write_file, delete_file, move_file
```

`delete_file` elimina soltanto un file esistente contenuto in una delle aree
configurate da `MCP_WRITABLE_DIRECTORIES`. Non elimina directory.

`move_file` sposta un singolo file tra aree scrivibili, crea le directory padre
interne necessarie e rifiuta di sostituire una destinazione già esistente. Sia
la sorgente sia la destinazione vengono validate prima dell'operazione.

Entrambe le operazioni rifiutano file protetti, percorsi assoluti, traversal e
percorsi che attraversano una junction verso l'esterno. La risoluzione del
percorso continua a essere eseguita dal componente comune `RootedFileAccess`.

## Criteri di accettazione

- inventario MCP esatto dei cinque strumenti;
- cancellazione riuscita soltanto in `workspace/` o `agent-tests/`;
- spostamento riuscito soltanto tra aree scrivibili;
- contenuti protetti invariati dopo tentativi di spostamento o cancellazione;
- nessuna lettura o scrittura esterna tramite percorso assoluto, traversal o
  junction;
- stessi risultati osservati tramite protocollo diretto e tramite Claude CLI.

## Verifiche eseguite

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore --filter-class "*RootedFileAccessTests"
```

Risultato: formattazione superata; build con zero errori e zero avvisi; 16 test
mirati eseguiti, 16 superati, 0 falliti, 0 ignorati.

```powershell
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Direct
```

Risultato: exit code `0`; tutti i 20 controlli diretti veri. Lo spostamento e la
cancellazione nelle aree scrivibili sono riusciti; le operazioni equivalenti su
`public-reference/` sono state rifiutate. Evidenze non versionate in
`%TEMP%\mcp-readonly-prototype-174223402cd54bc19e37ff18935a22cb\evidence`.

```powershell
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Claude
```

Risultato: exit code `0`; un solo server `benchmark_workspace`, inventario
esatto dei cinque strumenti, 16 chiamate previste osservate e tutti i 31
controlli veri. Nessun agente integrato, slash command, skill o hook è stato
osservato. I file protetti sono rimasti invariati e nessun contenuto esterno è
stato divulgato. Evidenze non versionate in
`%TEMP%\mcp-readonly-prototype-652ab4339ada498ea44e6e373b797c75\evidence`.

## Limiti e prossimo passo

P8 certifica soltanto le cinque operazioni file esposte dal server nella fixture
verificata. Non dimostra che l'intero harness sia isolato e non autorizza ancora
una run sperimentale ufficiale.

Il prossimo passo è definire e verificare un worker fidato per typecheck, build e
test che non esponga una shell generale e che non permetta al codice modificato
dall'agente di leggere test riservati, configurazioni fidate o altri percorsi
dell'host. La composizione temporanea di `workspace/`, `public-reference/` e
`agent-tests/` deve essere definita prima di implementare tale worker.
