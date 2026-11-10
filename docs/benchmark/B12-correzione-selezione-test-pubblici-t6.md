# B12 — Correzione della selezione dei test pubblici T6

## Iterazione

- **Data:** 10 novembre 2026.
- **Obiettivo:** impedire che `run_public_tests` esegua come falliti i sei casi
  comuni dichiarati non applicabili a T6.
- **Componenti:** profilo MCP dell'orchestratore, server MCP, runner pubblico e
  test dell'harness.
- **Stato:** implementazione e verifiche completate; piloti T6 non ancora
  ripetuti.

## Causa

`EphemeralPublicTestRunner` generava sempre una configurazione Vitest generica
con tutti i file pubblici e non riceveva il task selezionato. Di conseguenza non
applicava il filtro T6 e riportava 26 casi passati e 6 falliti anche per una
submission corretta.

## Modifica

L'orchestratore inserisce ora il task canonico nell'ambiente del server MCP come
`MCP_TASK_ID`. Il server lo valida e il runner pubblico costruisce internamente,
per T6, il filtro dei 21 casi comuni compatibili e dei 5 casi specifici.

Il filtro viene passato a Vitest come argomento controllato
`--testNamePattern`; non proviene dalla workspace e non è modificabile
dall'agente. Per T1–T5 il comportamento rimane invariato. L'audit registra
anche l'argomento effettivo.

## Verifiche eseguite

### Test mirati

```powershell
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~EphemeralPublicTestRunnerTests|FullyQualifiedName~ControlledPublicTestRunnerTests|FullyQualifiedName~ClaudeLaunchProfileTests|FullyQualifiedName~CoordinatorLaunchProfileTests"
```

Esito finale: 18 test superati, 0 falliti. Il nuovo test reale sulla baseline T6
osserva 21 casi compatibili passati, 5 casi T6 falliti perché non implementati e
6 casi comuni non applicabili pending.

Durante lo sviluppo il test ha inizialmente osservato 50 casi perché la fixture
copiava anche T1–T5; corretto il layout, ha osservato 32 casi ma nessun filtro.
L'uso del filtro come argomento controllato ha prodotto l'esito atteso. Questi
fallimenti intermedi non sono dichiarati come test superati.

### Formattazione e suite completa

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --configuration Debug --no-restore
```

Formattazione: exit code 0. Suite: 300 test totali, 299 superati, 0 falliti e 1
probe reale esplicitamente esclusa; durata 23,555 secondi.

### Accettazione sulla submission T6 corretta

Il nuovo server MCP ha rieseguito `run_public_tests` sulla submission
`20261110T154825Z-1276f338`, già certificata dall'evaluator:

- stato `completed`;
- exit code 0;
- 32 test nel report;
- 26 passati;
- 0 falliti;
- 6 pending perché non applicabili;
- durata 510 ms.

Prima della correzione la stessa submission produceva 26 passati e 6 falliti.
La prova certifica quindi direttamente la proprietà corretta.

## Limiti e lavoro rimanente

La correzione non rende validi retroattivamente i tentativi T6 già eseguiti,
perché il feedback errato ha influenzato tempo e comportamento degli agenti.
Inoltre cambia hash del server MCP e configurazione registrata aggiungendo
`MCP_TASK_ID`. Per applicare B6 con lo stesso harness effettivo, non è corretto
combinare i tre vecchi piloti T1 con nuovi piloti T6.

La baseline sperimentale e l'harness devono quindi essere ricongelati sul commit
che conterrà B12. Tutti i sei slot devono ripartire da nuove aree, nell'ordine
già approvato:

1. T1 con gerarchia adattiva;
2. T1 con gerarchia statica;
3. T1 single-agent;
4. T6 con gerarchia statica;
5. T6 single-agent;
6. T6 con gerarchia adattiva.

I tre precedenti T1 e i due tentativi T6 restano evidenze storiche, ma nessuno
contribuirà alla calibrazione finale dopo il nuovo freeze.
