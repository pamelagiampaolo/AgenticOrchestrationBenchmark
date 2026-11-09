# A6 — Identità della toolchain e prova completa non ufficiale

- **Data:** 9 novembre 2026
- **Fase:** verifica di readiness prima dei piloti B4
- **Branch:** `feat/single-agent` (worktree `C:\repos\TesiAgenticAI-agent`), dopo
  l'integrazione di `main` fino a `4fe5235` (P15–P23 di Codex: comandi MCP di
  sviluppo e loro collegamento ai launcher)
- **Stato:** pianificato; le sezioni dei risultati sono completate a fine iterazione

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
