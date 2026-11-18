# P32 — Contratto delle configurazioni runtime congelato

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Commit sorgente:** `aee8d1162bb496895990a00095cc14c2651ff752`.
- **Stato:** generatori identificati, contratto verificato e corrispondenza tra
  file runtime e hash dei record coperta da test.
- **Chiamate LLM:** nessuna.

## Obiettivo

Congelare le configurazioni runtime senza trattarle erroneamente come file
statici. I JSON MCP contengono necessariamente percorsi della singola run, root
dei worker, directory di audit, nome casuale della pipe e timeout applicato.
Pertanto la comparabilità richiede lo stesso generatore e la stessa derivazione
dei campi, non lo stesso SHA-256 grezzo tra run differenti.

Ogni record conserva comunque gli hash dei file effettivamente usati, così una
configurazione inattesa resta rilevabile e verificabile a posteriori.

## Componenti congelati

I seguenti file coincidono con gli hash registrati nel manifest P30:

| Componente | SHA-256 |
| --- | --- |
| `ClaudeLaunchProfile.cs` | `959395cff45ba33b5cc13b0f148799dc531d520a12e1c4d4e345e76002928ab8` |
| `SingleAgentSessionRunner.cs` | `1bd4e72b91afcd74e15b08e0d32a59d6c9fc1cf0494fe1fac8d41dfaec73d0c5` |
| `HierarchicalRunRunner.cs` | `aa3a76a6ac221e96086666fa05a34b1d6eb56e449e77739c0677de5565ea8d90` |
| `RunInputs.cs` | `1d3aad221f5c309a9cdf4cc1a570d3dae9af4a82b5e2098ae92fbcf0d52fde0a` |
| `SessionRecord.cs` | `c95eff0ba6ebc8583218bf75e54fe028ebbc5943a481d4e3f54437ffa8e907d5` |
| `HierarchicalRunRecord.cs` | `e117a7a565e69eb13c33febeee3dd051dc5335b4ac223965c23efab2e2a172d0` |

## Invarianti verificati

- modello passato esplicitamente alla CLI;
- `--restricted`, `--strict-mcp-config`, nessuna source di settings implicita,
  nessuno strumento built-in e nessuna persistenza di sessione;
- hook, agenti built-in e server Claude.ai disabilitati;
- nove strumenti MCP del workspace per single-agent e worker;
- coordinatore con gli stessi nove strumenti più `delegate_task`;
- worker senza `delegate_task` e con il solo server `benchmark_workspace`;
- coordinatore con `benchmark_workspace` e `benchmark_coordination`;
- aree scrivibili limitate a `workspace` e `agent-tests`;
- toolchain, task, worker root e audit directory passati come variabili esplicite;
- timeout del relay derivato dal limite temporale della run;
- JSON e system prompt con fine riga LF;
- configurazione dichiarata da `system/init` rifiutata se differisce per modello,
  strumenti, server, agenti, skill o slash command;
- hash dei file effettivamente scritti registrati nei record schema 2.

## Rafforzamento della verifica post-freeze

Senza modificare il runner congelato sono state aggiunte asserzioni ai test che
confrontano gli SHA-256 dei byte realmente scritti con i campi del record:

- single-agent: system prompt, settings e configurazione MCP;
- gerarchie statica/adattiva: prompt coordinatore e worker, settings,
  configurazione MCP coordinatore e configurazione MCP worker.

Queste modifiche riguardano soltanto i test post-freeze e non cambiano i binari
P31 né il commit sperimentale designato.

## Comandi e risultati

```powershell
dotnet format .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build `
  --filter-class "*ClaudeLaunchProfileTests" `
  --filter-class "*CoordinatorLaunchProfileTests" `
  --filter-class "*SingleAgentSessionRunnerTests" `
  --filter-class "*HierarchicalRunRunnerTests"
dotnet test .\AgenticOrchestrationBenchmark.slnx --no-build
```

Risultati osservati con accesso alle normali cache e directory temporanee:

- format: exit code `0`;
- build: exit code `0`, zero errori e zero avvisi;
- test mirati: `36/36` superati;
- suite completa: `345` totali, `344` superati, zero falliti e uno escluso
  esplicitamente (`RealCliHierarchyProbes.LongDelegation_IsAwaitedByRealCoordinator`).

Il primo tentativo di build in sandbox è stato bloccato dalla scrittura delle
cache sotto `obj`. Il primo tentativo della suite completa in sandbox ha
registrato 17 errori di creazione sotto `bin/.../TestResults`, con 327 test
superati. Ripetendo gli stessi comandi con accesso alle directory di lavoro non
sono comparsi fallimenti funzionali.

## Limiti e lavoro rimanente

Il contratto non rende uguali i percorsi o i nomi pipe delle diverse run: ne
rende deterministica la derivazione. Prima di ciascun pilota e run ufficiale
devono essere verificati il commit sorgente, il manifest P31 e gli hash scritti
nel record finale.

Restano da congelare toolchain, Claude CLI, evaluator e test riservati. Questo
passo non autorizza chiamate LLM o run.
