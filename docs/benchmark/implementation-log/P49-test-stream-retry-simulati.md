# P49 — Test circoscritti sul consumo nei retry simulati

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** verifica delle metriche B5 prima delle run ufficiali.
- **Stato:** due casi simulati verificati; semantica di retry reali e consumi
  non emessi dalla CLI ancora non dimostrata.
- **Chiamate LLM sperimentali:** nessuna.

Obiettivo: controllare la deduplicazione quando due tentativi simulati sono
presenti nello stream con ID diversi e il risultato senza uso autorevole.
Componenti: `StreamUsageTracker` e test .NET; nessuna modifica al runner,
all'harness congelato o alla toolchain. Criteri: due ID distinti contati una
volta ciascuno anche con evento ripetuto; nessun totale autorevole inventato
se il campo `usage` del risultato manca.

## Modifica e verifiche

È stato aggiunto `RetryUsageTests.cs` con due casi:

1. due messaggi sintetici etichettati come tentativo fallito e retry, con ID
   distinti, un evento duplicato e un risultato autorevole uguale alla somma;
2. un messaggio osservato seguito da un risultato d'errore privo di `usage`:
   l'uso osservato resta disponibile e l'uso autorevole resta `null`.

Comandi eseguiti con accesso autorizzato alle directory di build e temporanee:

```powershell
dotnet format .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --filter-class '*RetryUsageTests'
dotnet test .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build `
  --filter-class '*StreamUsageTrackerTests' --filter-class '*RunLimitControllerTests' `
  --filter-class '*SingleAgentSessionRunnerTests' --filter-class '*HierarchicalRunRunnerTests' `
  --filter-class '*RetryUsageTests'
```

Risultati osservati: format exit code 0; build exit code 0, zero avvisi e zero
errori; test mirati 2/2 passati; gruppo pertinente 43/43 passati, zero falliti
e ignorati. I test sono simulazioni locali e non avviano chiamate LLM.

## Limiti e lavoro rimanente

L'etichetta `failed_attempt` è un ID sintetico: il test non dimostra che la
CLI reale emetta un record o un ID per ogni richiesta fallita o retry. Un
consumo non presente nello stream o nel totale autorevole non può essere
ricostruito da questi dati. I test dimostrano solo il trattamento degli eventi
forniti; la semantica e completezza della telemetria del fornitore restano da
verificare o dichiarare come limite della misurazione.

Non sono stati modificati gli input sperimentali congelati e non sono stati
ripetuti i piloti. L'aggregazione e la propagazione dei dati mancanti restano
separate da questa prova. Questo passo non autorizza run o chiamate LLM.
