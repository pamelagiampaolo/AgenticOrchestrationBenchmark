# Agentic Orchestration Benchmark

Repository sperimentale per lo studio dell'orchestrazione adattiva di sistemi
multi-agente basati su Large Language Models.

L'esperimento confronta tre condizioni sullo stesso benchmark software:

- agente singolo;
- gerarchia multi-agente statica;
- gerarchia multi-agente adattiva.

Il repository non assume che una condizione sia migliore delle altre. L'harness
serve a eseguire confronti riproducibili usando la stessa baseline, gli stessi
task, gli stessi criteri di valutazione e limiti comparabili.

## Architettura corrente

Il percorso operativo corrente usa un server MCP per confinare gli agenti al
workspace assegnato ed esporre soltanto gli strumenti autorizzati. Windows
Sandbox non fa parte dell'architettura corrente: i relativi prototipi, script e
documenti sono conservati come evidenze storiche delle iterazioni precedenti.

Prima delle run ufficiali, il confinamento MCP, l'inaccessibilità dei test
riservati e l'assenza di percorsi alternativi devono essere verificati con prove
di accettazione sull'harness effettivamente usato. La sola presenza dei
meccanismi di isolamento non dimostra queste proprietà.

## Struttura del repository

| Percorso | Ruolo |
| --- | --- |
| [`src/AgenticExperimentRunner/`](src/AgenticExperimentRunner/) | Orchestratore .NET: preparazione delle run, esecuzione delle tre condizioni, coordinamento e raccolta delle metriche. |
| [`tests/AgenticExperimentRunner.Tests/`](tests/AgenticExperimentRunner.Tests/) | Test automatici dell'orchestratore e del server MCP. |
| [`benchmark/BenchmarkTarget/`](benchmark/BenchmarkTarget/) | Applicazione TypeScript usata come bersaglio comune dei task sperimentali T1-T6. |
| [`src/AgentWorkspaceMcpServer/`](src/AgentWorkspaceMcpServer/) | Server MCP che confina l'accesso al workspace ed espone file e comandi di sviluppo controllati. |
| [`docs/benchmark/`](docs/benchmark/) | Progettazione, implementazione, specifiche pubbliche dei task, piloti ed evidenze sperimentali. |
| [`scripts/`](scripts/) | Script PowerShell separati tra verifiche correnti e prove storiche di isolamento. |
| [`experiments/legacy-isolation-probes/`](experiments/legacy-isolation-probes/) | Prototipi ed evidenze storiche delle precedenti prove con Windows Sandbox e AppContainer; non appartengono all'architettura operativa corrente. |
| [`AgenticOrchestrationBenchmark.slnx`](AgenticOrchestrationBenchmark.slnx) | Solution .NET dell'infrastruttura operativa e dei relativi test; esclude il benchmark TypeScript e i probe storici. |

Le directory `bin/`, `obj/`, `dist/`, `node_modules/`, `results/` e
`TestResults/` contengono output locali o generati e sono escluse dal controllo
di versione.

### Orchestratore

`src/AgenticExperimentRunner/` è suddiviso per responsabilità:

- `RunPreparation/`: prepara da un commit una copia pulita della baseline;
- `SessionExecution/`: esegue e misura la condizione single-agent;
- `Coordination/`: implementa coordinamento, deleghe e gerarchie statica e
  adattiva;
- `Instructions/`: contiene le istruzioni versionate assegnate agli agenti.

I sottocomandi pubblici dell'orchestratore sono:

- `prepare-run`;
- `generate-experiment-plan`;
- `run-single-agent`;
- `run-static-hierarchy`;
- `run-adaptive-hierarchy`.

Le run richiedono percorsi assoluti, toolchain, limiti e metadati sperimentali
espliciti: `--run-kind`, `--repetition`, `--order-seed` e `--order-position`.
Il modello congelato per piloti e run ufficiali è esclusivamente
`claude-sonnet-5-5`; i comandi rifiutano identificatori differenti.
Non usare valori di esempio per una run ufficiale: fare riferimento alla
configurazione e ai limiti sperimentali approvati in
[`docs/benchmark/`](docs/benchmark/).

### Benchmark

`benchmark/BenchmarkTarget/` contiene:

- `src/`: codice TypeScript del sistema sottoposto ai task;
- `tests/public/`: test accessibili agli agenti;
- `tests/public/t1/` ... `tests/public/t6/`: test pubblici specifici dei task;
- `vitest.public*.config.ts`: configurazioni delle suite pubbliche.

Le specifiche pubbliche T1-T6 sono in
[`docs/benchmark/`](docs/benchmark/). I test riservati usati per la valutazione
indipendente non devono essere collocati nel workspace accessibile agli agenti.

## Build e test di sviluppo

Prerequisiti correnti ricavabili dai progetti:

- .NET SDK compatibile con `net10.0`;
- Node.js e npm per il benchmark TypeScript.

Da PowerShell, nella radice del repository:

```powershell
dotnet build .\AgenticOrchestrationBenchmark.slnx
dotnet test .\AgenticOrchestrationBenchmark.slnx --no-build

Push-Location .\benchmark\BenchmarkTarget
npm ci
npm run typecheck
npm test
Pop-Location
```

Sono inoltre disponibili gli script npm `test:public:t1` fino a
`test:public:t6` per eseguire la suite pubblica di un singolo task.

Questi sono controlli di sviluppo. Il loro superamento non certifica da solo
l'isolamento completo o la readiness di una run ufficiale.

## Documentazione e regole operative

- [`AGENTS.md`](AGENTS.md) definisce metodo di lavoro, autorizzazioni, qualità
  del codice e requisiti di validità sperimentale.
- [`CLAUDE.md`](CLAUDE.md) contiene le istruzioni dedicate a Claude Code.
- [`docs/regole-collaborazione-claude-codex.md`](docs/regole-collaborazione-claude-codex.md)
  documenta il contratto di collaborazione tra gli agenti.
- [`docs/benchmark/`](docs/benchmark/) conserva il diario tecnico delle
  iterazioni e le specifiche pubbliche dei task.

Non implementare anticipatamente le soluzioni dei task sperimentali. Non creare
commit, pubblicare dati o modificare servizi esterni senza autorizzazione
esplicita.
