# P7 — Policy MCP per aree scrivibili

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** implementazione, test, prova MCP diretta e prova Claude CLI
  completati con esito positivo.
- **Obiettivo:** rendere leggibile l'intera area agente P6 consentendo la
  scrittura soltanto in `workspace/` e `agent-tests/`.

L'iterazione non introduce cancellazione, rinomina, patch incrementali o
esecuzione di build e test.

## Configurazione

Il server legge dall'ambiente fidato `MCP_WRITABLE_DIRECTORIES`, separato con il
separatore di percorso del sistema operativo. Il launcher verificato usa:

```text
MCP_WRITABLE_DIRECTORIES=workspace;agent-tests
```

Se la variabile è assente o vuota, `write_file` non dispone di alcuna area
scrivibile. Ogni directory configurata deve essere relativa, esistente,
contenuta nella root e non risolvere attraverso un collegamento esterno.

La policy viene controllata prima di creare directory e nuovamente dopo la
risoluzione dei componenti esistenti. In questo modo una richiesta negata non
lascia directory parziali e una junction collocata dentro `workspace/` non può
spostare la destinazione effettiva fuori root.

## Comportamento verificato

Sono consentiti:

- elenco dell'intera root;
- lettura di `task-specification.md`;
- lettura di `public-reference/`;
- creazione e sostituzione sotto `workspace/`;
- creazione e sostituzione sotto `agent-tests/`.

Sono rifiutati:

- scrittura di file esistenti o nuovi sotto `public-reference/`;
- scrittura di `task-specification.md`;
- scrittura nella root o in altre sottodirectory;
- percorsi assoluti, traversal e junction che risolvono fuori root.

## Verifiche eseguite

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
```

Risultato: formattazione superata; build con zero errori e zero avvisi; 10 test
eseguiti, 10 superati, 0 falliti, 0 ignorati.

```powershell
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Direct
```

Risultato: exit code `0`; tutti i 16 controlli diretti veri. Evidenze non
versionate in
`%TEMP%\mcp-readonly-prototype-cd4f877b90554c6da67696677712a316\evidence`.

```powershell
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Claude
```

Risultato: exit code `0`; un solo server `benchmark_workspace`, inventario esatto
dei tre strumenti MCP, 12 chiamate previste osservate e tutti i 27 controlli
veri. Le scritture in `workspace/` e `agent-tests/` sono riuscite; quelle in
`public-reference/`, nella specifica, via traversal e via junction sono state
rifiutate. I contenuti protetti sono rimasti invariati e nessun contenuto esterno
è stato divulgato. Evidenze non versionate in
`%TEMP%\mcp-readonly-prototype-e2fe0b065ba34b6883fda1f4d7f46ef3\evidence`.

## Limiti e prossimo passo

P7 dimostra il confine delle operazioni file attualmente esposte. Non dimostra
che l'agente disponga ancora di un ciclo di sviluppo completo: mancano operazioni
controllate per cancellare o rinominare file e manca un meccanismo sicuro per
eseguire typecheck, build e test senza concedere una shell generale o permettere
al codice modificato di accedere all'host.

Il prossimo passo deve definire il set minimo di operazioni file ancora
necessarie e separarlo dal problema più rischioso dell'esecuzione del codice.
