# A6 — Identità della toolchain e prova completa non ufficiale

- **Data:** 9 novembre 2026
- **Fase:** verifica di readiness prima dei piloti B4
- **Branch:** `feat/single-agent` (worktree `C:\repos\TesiAgenticAI-agent`), dopo
  l'integrazione di `main` fino a `4fe5235` (P15–P23 di Codex: comandi MCP di
  sviluppo e loro collegamento ai launcher)
- **Stato:** completata; prova completa non ufficiale eseguita (risultati sotto)

## Contesto

Con P23 l'agente dispone di `run_typecheck`, `run_build`, `run_public_tests` e
`run_agent_tests`, eseguiti dal server MCP con una toolchain passata dal launcher
(`--node`, `--vitest`, `--typescript`). I record registrano i percorsi della
toolchain (dentro la configurazione MCP, di cui c'è l'hash) ma non la sua
identità: con Node gestito da nvm lo stesso percorso può corrispondere a versioni
diverse in run diverse, e B4 richiede lo stesso ambiente per tutte le condizioni.
P23 indica inoltre come passo successivo una prova completa non ufficiale prima di
autorizzare i piloti.

Decisione dell'utente (9 novembre 2026): registrare l'identità della toolchain ed
eseguire la prova completa su T1 per single-agent e gerarchia adattiva.

## Obiettivo

1. Registrare nei record di entrambe le architetture l'identità della toolchain
   effettivamente usata.
2. Eseguire una prova completa non ufficiale su T1 per le due architetture, con i
   quattro comandi MCP, e ispezionare record, audit dei comandi, integrità e
   workspace finale.

## Componenti coinvolti

- `SessionExecution/ToolchainIdentity.cs` (nuovo): versione dichiarata da
  `node --version`, SHA-256 di `node.exe`, degli entry point di Vitest e TypeScript
  e versioni lette dai rispettivi `package.json`.
- `SessionExecution/SessionRecord.cs`, `Coordination/HierarchicalRunRecord.cs`:
  campo `toolchain`.
- `SingleAgentSessionRunner`, `HierarchicalRunRunner`: rilevazione prima
  dell'avvio della CLI.
- Test unitari e aggiornamento dei test dei runner.

## Comportamento atteso

1. Prima della sessione il runner esegue `node --version` con l'ambiente ripulito e
   calcola gli hash dei tre file; un errore di `node --version` impedisce l'avvio
   (`SessionExecutionException`), perché senza toolchain identificata la run non è
   comparabile.
2. Le versioni di Vitest e TypeScript si leggono dal `package.json` del pacchetto
   che contiene l'entry point (`name` uguale a `vitest` / `typescript`), risalendo
   le directory; se non viene trovato, la versione è `null`: il dato manca, non
   viene stimato.
3. Il record contiene percorsi, versioni e hash; i valori sono identici per due
   run consecutive con la stessa toolchain.

## Criteri di accettazione

- Test unitari della rilevazione (versione di Node, ricerca del `package.json`,
  pacchetto assente, errore di `node --version`).
- Test dei runner con il campo `toolchain` presente.
- Suite completa, `dotnet format --verify-no-changes`, build senza avvisi.
- Prova completa: due run reali (dichiarate sotto prima dell'esecuzione) che
  terminano con record scritto, uso dei comandi MCP visibile negli stream e
  negli audit, materiali protetti invariati e toolchain identica tra le due run.

## Parametri della prova completa (dichiarati prima dell'esecuzione)

Prova non ufficiale: non è un pilota B4 e i limiti sono valori di prova.

- Task T1; area preparata con `prepare-run --commit HEAD` dal commit che contiene
  questa sezione, una area distinta per ciascuna architettura.
- Modello `claude-sonnet-5-5`, CLI `C:\Users\Utente\.local\bin\claude.exe`.
- Toolchain: `C:\nvm4w\nodejs\node.exe`,
  `BenchmarkTarget\node_modules\vitest\vitest.mjs` e
  `BenchmarkTarget\node_modules\typescript\lib\tsc.js` del worktree, installati con
  `npm ci` dal `package-lock.json` versionato.
- Server MCP e relay dalla build Debug del worktree.
- Run 1 `run-single-agent`: 900 s, 1 000 000 token.
- Run 2 `run-adaptive-hierarchy`: 900 s, 1 000 000 token, `--max-delegations 10`.
- Istruzioni: testi correnti del branch, ancora bozze non approvate.

Ispezione prevista: stato e token dai record, comandi MCP chiamati dagli stream,
file di audit in `mcp-audit/`, assenza di residui in `mcp-worker/`, integrità dei
materiali protetti, diff della workspace rispetto alla baseline, identità della
toolchain uguale nelle due run. Nessuna valutazione con i test riservati (fuori
ambito: valutatore esterno).

## Realizzazione e verifiche

- Integrazione di `main` fino a `4fe5235`: fast-forward senza conflitti. Al primo
  `dotnet test` 3 test di Codex fallivano (`EphemeralBuildRunnerTests`,
  `EphemeralTypecheckRunnerTests`, `EphemeralPublicTestRunnerTests`) con
  `FileNotFoundException`: nel worktree mancava `BenchmarkTarget/node_modules`, che
  non è versionato. Dopo `npm ci` (lockfile versionato, 0 vulnerabilità segnalate)
  la suite è verde. Nessuna modifica al codice di Codex.
- `ToolchainIdentity` e campo `toolchain` nei due record (commit `ab899f3`).
- `dotnet format --verify-no-changes`: codice di uscita 0; build: 0 avvisi,
  0 errori; suite completa: 285 superati, 1 ignorato (prova reale esplicita),
  0 falliti; i 4 nuovi test unitari e le asserzioni aggiunte ai test dei runner
  sono inclusi.

## Risultati della prova completa

Commit della baseline e del codice: `ab899f3`. CLI `2.1.312`, modello
`claude-sonnet-5-5` in `system/init`, nove strumenti MCP dichiarati (cinque file,
quattro comandi), nessuna violazione di configurazione.

| | Run 1 single-agent | Run 2 gerarchia adattiva |
| --- | --- | --- |
| Run | `a6-trial-single-20261109T140058Z` | `a6-trial-adaptive-20261109T140058Z` |
| Stato | `completed` | `completed` |
| Durata (`agentTaskDurationMs`) | 29045 | 30427 |
| Token totali | 36020 | 38286 |
| Stream = `result` | sì | sì |
| Strumenti | `list_files`, `read_file`, `write_file`, `run_typecheck`, `run_public_tests` (1 ciascuno) | stessi, 1 ciascuno |
| Deleghe | non applicabile | 0 richieste |
| Audit MCP | typecheck exit 0 (102 ms), public-tests exit 0 (479 ms) | typecheck exit 0 (101 ms), public-tests exit 0 (452 ms) |
| Residui in `mcp-worker/` | nessuno | nessuno |
| `session/` a fine run | vuota | vuota |
| File cambiati rispetto al manifest | `workspace/src/index.ts` | `workspace/src/index.ts` |
| Materiali protetti | invariati | invariati |

Toolchain registrata, identica nelle due run: Node `v24.17.0`
(`C:\nvm4w\nodejs\node.exe`, SHA-256 `c6335d08…3664`), Vitest `5.1.0`,
TypeScript `7.0.2`.

Entrambi gli agenti dichiarano nel riepilogo typecheck superato e 31 test pubblici
superati su 31; l'audit conferma exit code 0 per entrambi i comandi. Nessuno dei
due ha scritto test propri né eseguito la build.

## Interpretazione (non misura)

- La catena completa funziona per le due architetture: preparazione, avvio
  isolato, comandi di sviluppo nel worker effimero, audit, record, integrità.
- Su T1 il coordinatore adattivo non ha delegato in nessuna delle due run reali
  (A5 R2 e questa): con un task semplice le due condizioni si comportano quasi
  allo stesso modo. È coerente con B2/B4 (su task semplici la differenza attesa è
  nel costo, non nella correttezza) ma due run non dicono nulla sulla frequenza.

## Limiti e punti non verificati

- Correttezza della soluzione non valutata: servono i test riservati e il
  valutatore esterno.
- Durata del typecheck di circa 100 ms: plausibile con TypeScript 7 (compilatore
  nativo), ma non verificato qui che il comando rilevi davvero un errore di tipo
  in una run reale; i test di Codex (P20) coprono il comportamento del runner.
- Nessuna delega reale con i comandi di sviluppo: il percorso lavoratore + comandi
  MCP è verificato solo per costruzione (stesso profilo e stessa configurazione
  del server) e con la CLI finta.
- Istruzioni ancora bozze; prova di controllo sulle contromisure MCP (A5) non
  eseguita.
- `node_modules` del worktree è la toolchain della prova: per le run ufficiali va
  deciso dove sta la toolchain congelata (fuori dai worktree di sviluppo).

## Prossimo passo proposto

1. Approvazione delle istruzioni e della descrizione di `delegate_task`.
2. Decisione sulla collocazione della toolchain congelata per le run ufficiali.
3. Protocollo dei piloti B4 (task, ripetizioni, calibrazione di tempo e token).
