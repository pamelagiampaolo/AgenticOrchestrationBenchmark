# P28 — Verifica build, test e baseline pre-freeze

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** readiness precedente al freeze finale e ai nuovi piloti.
- **Commit verificato:** `c773558527d1f1718a07c5fe0e9fb7b578f7196a`.
- **Stato:** infrastruttura e baseline comune verificate; funzionalità dei task
  sperimentali intenzionalmente assenti.
- **Chiamate LLM:** nessuna.

## Obiettivo e criteri

Verificare lo stato effettivo dell'orchestratore, del server MCP e del benchmark
TypeScript prima del freeze. I criteri applicati sono:

- formattazione e build .NET senza errori;
- suite .NET completa senza fallimenti, distinguendo gli skip espliciti;
- typecheck, build e suite pubblica comune del benchmark completati;
- configurazioni pubbliche T1–T6 eseguibili e fallite soltanto perché la
  baseline non contiene ancora le soluzioni sperimentali.

Non è criterio di readiness rendere verdi le suite T1–T6 sulla baseline: farlo
richiederebbe implementare in anticipo le soluzioni assegnate agli agenti.

## Toolchain osservata

- .NET SDK: `10.0.403`;
- Node.js: `v24.17.0`;
- npm: `11.13.0`;
- Vitest osservato nell'output: `5.1.0`.

Queste versioni descrivono l'ambiente della prova e non costituiscono ancora il
manifest della toolchain definitiva.

## Comandi e risultati

### Infrastruttura .NET

```powershell
dotnet format .\AgenticOrchestrationBenchmark.slnx --verify-no-changes --no-restore
dotnet build .\AgenticOrchestrationBenchmark.slnx --no-restore
dotnet test .\AgenticOrchestrationBenchmark.slnx --no-build
```

Risultati osservati:

- `dotnet format`: exit code `0`; ha segnalato genericamente avvisi durante il
  caricamento del workspace senza dettagli al livello di verbosità usato;
- build: exit code `0`, `0` errori, `0` avvisi;
- test: `345` totali, `344` superati, `0` falliti, `1` ignorato;
- test ignorato: `RealCliHierarchyProbes.LongDelegation_IsAwaitedByRealCoordinator`,
  escluso esplicitamente e quindi non certificato da questa prova.

### Baseline TypeScript comune

```powershell
npm run typecheck
npm run build
npm test
```

I tre comandi sono terminati con exit code `0`. La suite pubblica comune ha
eseguito `2` file e `27` test: `27` superati, `0` falliti.

La build ha prodotto `benchmark/BenchmarkTarget/dist/`, correttamente ignorata
da Git.

### Suite pubbliche specifiche dei task

Sono stati eseguiti separatamente gli script `test:public:t1` fino a
`test:public:t6`. Ogni script è stato caricato ed eseguito da Vitest e ha
restituito exit code `1` per requisiti sperimentali ancora assenti:

| Task | Superati | Falliti | Saltati | Causa osservata principale |
| --- | ---: | ---: | ---: | --- |
| T1 | 0 | 4 | 0 | `findProductsBelowStock` assente |
| T2 | 0 | 4 | 0 | `receiveStock` assente |
| T3 | 2 | 1 | 0 | aggregazione atomica delle righe duplicate non implementata |
| T4 | 0 | 4 | 0 | API di sospensione e stato prodotto assenti |
| T5 | 29 | 1 | 0 | factory con repository forniti assente |
| T6 | 21 | 5 | 6 | modello di evasione parziale e relativo stato ordine assenti |

Nessuna soluzione è stata aggiunta alla baseline.

## Limiti e lavoro rimanente

- La prova real-CLI ignorata dalla suite .NET resta da eseguire separatamente.
- Gli avvisi generici di caricamento di `dotnet format` non sono stati espansi
  con verbosità diagnostica; il comando è comunque terminato con exit code `0`.
- I sei test saltati nella configurazione T6 sono stati osservati e registrati;
  questa prova non ne modifica né presume la motivazione metodologica.
- Non sono stati eseguiti evaluator o test riservati.
- La verifica non congela ancora commit, toolchain, evaluator o manifest.
