# TesiAgenticAI

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
| [`AgenticExperimentRunner/`](AgenticExperimentRunner/) | Orchestratore .NET: preparazione delle run, esecuzione delle tre condizioni, coordinamento e raccolta delle metriche. |
| [`AgenticExperimentRunner.Tests/`](AgenticExperimentRunner.Tests/) | Test automatici dell'orchestratore e del server MCP. |
| [`BenchmarkTarget/`](BenchmarkTarget/) | Applicazione TypeScript usata come bersaglio comune dei task sperimentali T1-T6. |
| [`McpReadOnlyPrototype/`](McpReadOnlyPrototype/) | Server MCP del workspace. Il nome è storico: il componente offre oggi accesso ai file e comandi di sviluppo controllati, non soltanto lettura. |
| [`docs/benchmark/`](docs/benchmark/) | Progettazione, implementazione, specifiche pubbliche dei task, piloti ed evidenze sperimentali. |
| [`scripts/`](scripts/) | Script PowerShell di supporto e probe, molti dei quali appartengono alle precedenti iterazioni sull'isolamento. |
| [`AppContainerIsolationProbe/`](AppContainerIsolationProbe/) | Prototipo storico per le prove di isolamento con Windows AppContainer. |
| [`McpSandboxBridgeProbe/`](McpSandboxBridgeProbe/) | Fixture storiche per il collegamento tra MCP e Windows Sandbox. |
| [`NodeBaselineSandboxProbe/`](NodeBaselineSandboxProbe/) | Probe storico della baseline Node.js in Windows Sandbox. |
| [`P21IsolationProbe/`](P21IsolationProbe/) | Materiale della prova storica P21 sull'isolamento in Windows Sandbox. |

Le directory `bin/`, `obj/`, `dist/`, `node_modules/`, `results/` e
`TestResults/` contengono output locali o generati e sono escluse dal controllo
di versione.

### Orchestratore

`AgenticExperimentRunner/` è suddiviso per responsabilità:

- `RunPreparation/`: prepara da un commit una copia pulita della baseline;
- `SessionExecution/`: esegue e misura la condizione single-agent;
- `Coordination/`: implementa coordinamento, deleghe e gerarchie statica e
  adattiva;
- `Instructions/`: contiene le istruzioni versionate assegnate agli agenti.

I sottocomandi pubblici dell'orchestratore sono:

- `prepare-run`;
- `run-single-agent`;
- `run-static-hierarchy`;
- `run-adaptive-hierarchy`.

Le run richiedono percorsi assoluti, toolchain e limiti espliciti. Non usare
valori di esempio per una run ufficiale: fare riferimento alla configurazione e
ai limiti sperimentali approvati in
[`docs/benchmark/`](docs/benchmark/).

### Benchmark

`BenchmarkTarget/` contiene:

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
dotnet build .\AgenticExperimentRunner\AgenticExperimentRunner.csproj
dotnet build .\McpReadOnlyPrototype\McpReadOnlyPrototype.csproj
dotnet test .\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj

Push-Location .\BenchmarkTarget
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
