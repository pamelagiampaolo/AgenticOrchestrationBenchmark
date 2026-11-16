# A8 — Toolchain congelata e protocollo dei piloti B4

- **Data:** 9 novembre 2026
- **Fase:** verifica di readiness prima dei piloti B4
- **Branch:** `feat/single-agent` (worktree `C:\repos\AgenticOrchestrationBenchmark-agent`)
- **Stato:** toolchain congelata creata e verificata; protocollo dei piloti
  definito, **non avviabile** finché manca la gerarchia statica

## Decisioni dell'utente (9 novembre 2026)

1. Descrizione di `delegate_task` approvata così com'è.
2. Toolchain delle run ufficiali in una cartella dedicata fuori dai worktree,
   `C:\AgenticAIToolchain`, installata una volta con `npm ci` e poi non più
   modificata.
3. Protocollo dei piloti scelto da Claude (sezione seguente), nel rispetto delle
   regole di B4.

## Parte 1 — Toolchain congelata

### Collocazione e contenuto

```text
C:\AgenticAIToolchain\node-v24.17.0-lock-1b283562\
├── node\node.exe                    copia di C:\nvm4w\nodejs\node.exe
├── packages\package.json            dal commit 01e8f27 (BenchmarkTarget)
├── packages\package-lock.json       dal commit 01e8f27 (SHA-256 1b283562…)
├── packages\node_modules\...        npm ci dal lockfile
└── toolchain-manifest.json          hash di ogni file e hash aggregato
```

Il nome della cartella contiene versione di Node e prefisso dell'hash del lockfile:
una toolchain diversa avrà una cartella diversa e non sostituisce questa.

Percorsi da passare ai comandi `run-single-agent` / `run-adaptive-hierarchy`:

- `--node C:\AgenticAIToolchain\node-v24.17.0-lock-1b283562\node\node.exe`
- `--vitest C:\AgenticAIToolchain\node-v24.17.0-lock-1b283562\packages\node_modules\vitest\vitest.mjs`
- `--typescript C:\AgenticAIToolchain\node-v24.17.0-lock-1b283562\packages\node_modules\typescript\lib\tsc.js`

### Comandi eseguiti

```bash
git show HEAD:BenchmarkTarget/package.json      > packages/package.json
git show HEAD:BenchmarkTarget/package-lock.json > packages/package-lock.json
cp /c/nvm4w/nodejs/node.exe node/node.exe
PATH="<toolchain>/node:$PATH" npm ci --no-audit --no-fund
attrib +R C:\AgenticAIToolchain\node-v24.17.0-lock-1b283562\* /S /D
```

### Verifiche osservate

- `npm ci`: `added 37 packages`.
- `node.exe` copiato: SHA-256 `c6335d08…3664`, identico a quello registrato in A6;
  `node --version` → `v24.17.0`.
- `tsc.js --version` → `Version 7.0.2`; `vitest.mjs --version` →
  `vitest/5.1.0 win32-x64 node-v24.17.0`.
- `toolchain-manifest.json`: 1148 file, hash aggregato
  `3c8c3ae026c898a52e50b40e952cea4d4ee53baa078230a51c54e06938265752`.
- Sola lettura: un tentativo di scrittura su `node.exe` viene rifiutato
  (`Permission denied`) e l'hash resta invariato.
- **Comandi MCP con la toolchain congelata.** Copia locale di
  `scripts/Invoke-McpPublicTestsProbe.ps1` (script di Codex, non modificato nel
  repository) con i soli tre percorsi della toolchain e la root del worktree
  sostituiti; nessuna chiamata LLM. Esito: inventario esatto di nove strumenti,
  test pubblici 27/27, typecheck e build exit 0, test agente 1/1, quattro record di
  audit, nessun residuo nel worker, stderr del server vuoto.
- Dopo la prova il manifest della toolchain coincide ancora (1148 file, stesso
  hash aggregato) e il worktree non contiene file nuovi.

### Limite

La sola lettura (`attrib +R`) protegge da modifiche accidentali, non da un
processo con i permessi dell'utente che rimuova l'attributo. L'integrità si
verifica confrontando la cartella con `toolchain-manifest.json` prima delle run;
il record di ogni run contiene comunque versione e hash di Node e degli entry point
(A6).

## Parte 2 — Protocollo dei piloti

### Prerequisito non soddisfatto

B4 richiede che **tutte e tre** le architetture eseguano gli stessi workload di
calibrazione. La gerarchia statica (lavoro di Codex sul livello di coordinamento
condiviso) non esiste ancora: i piloti non possono iniziare prima che sia
disponibile e verificata.

### Workload

| Task | Categoria (B2) | Motivo dell'inclusione |
| --- | --- | --- |
| T1 | modifica semplice e circoscritta | caso circoscritto richiesto da B4 |
| T4 | modifica trasversale a più moduli | caso trasversale richiesto da B4 |
| T6 | funzionalità complessa e scomponibile | task più costoso: i limiti devono permettere realisticamente anche questo |

I piloti usano le specifiche e i test pubblici ufficiali; i test riservati non sono
esposti (B4). Le run pilota non entrano nelle 54 run ufficiali (B4, B5).

### Numero di run e ordine

- 3 task × 3 architetture × 1 ripetizione = **9 run pilota**.
- Task nell'ordine T1, T4, T6; architetture in ordine casuale per task, generato
  prima dei piloti con `random.Random(20261109).shuffle` (Python 3.13.7) sulla lista
  `[single-agent, hierarchical-static, hierarchical-adaptive]`:

| Task | 1ª | 2ª | 3ª |
| --- | --- | --- | --- |
| T1 | hierarchical-static | hierarchical-adaptive | single-agent |
| T4 | single-agent | hierarchical-adaptive | hierarchical-static |
| T6 | hierarchical-static | single-agent | hierarchical-adaptive |

- Run sequenziali, ognuna da un'area nuova preparata con `prepare-run` dallo stesso
  commit congelato; sessioni nuove, mai riprese.
- Identificatori: `pilot-<task>-<architettura>-<n>`; B5 richiede anche un campo
  esplicito "pilota/ufficiale" nei record, da aggiungere prima dei piloti.

### Condizioni comuni

- Modello `claude-sonnet-5-5` per tutti gli agenti; stessa CLI, stessa macchina,
  stessa toolchain congelata, stesse istruzioni (hash di A7, più le istruzioni
  della gerarchia statica quando esisteranno).
- **Tetto di calibrazione** uguale per tutte le run pilota: 45 minuti e
  3 000 000 di token; `--max-delegations 10` per la gerarchia adattiva.

### Regola di derivazione dei limiti (fissata prima di osservare i piloti)

Insieme di riferimento: le run pilota concluse dall'architettura stessa (stato
`completed` o `cli-failed` non infrastrutturale) di **tutti** i task e di **tutte**
le architetture insieme.

```text
limite di tempo   = arrotondamento al minuto superiore di 2 × max(agentTaskDurationMs)
budget di token   = arrotondamento ai 100 000 superiori di 2 × max(totalTokens)
```

La stessa regola vale per le tre architetture (B4: nessuna calibrazione separata).
Il margine 2× compensa la presenza di una sola ripetizione per coppia.

### Casi particolari e criteri di arresto

- **Tetto raggiunto.** Se una run pilota supera il tetto di calibrazione, le tre run
  di quel task vengono ripetute **una sola volta** con il tetto raddoppiato
  (90 minuti, 6 000 000 di token). Se anche così una run lo supera, il task è
  segnalato all'utente prima di fissare i limiti, senza ulteriori ripetizioni.
- **Errore infrastrutturale** (CLI non avviabile, servizio non disponibile, limite
  d'uso di Claude Pro): la run viene ripetuta una volta e l'evento resta
  registrato; non conta nell'insieme di riferimento.
- **Numero massimo assoluto:** 9 run + 3 ripetizioni per tetto + 2 ripetizioni
  infrastrutturali = 14 run pilota. Oltre questo numero ci si ferma e si decide con
  l'utente.
- I valori derivati, con le run che li hanno determinati, vengono documentati e
  sottoposti all'utente; solo dopo l'approvazione vengono congelati per le 54 run
  ufficiali.

### Ancora da fare prima dei piloti

1. Gerarchia statica realizzata e verificata (Codex), con istruzioni simmetriche a
   quelle approvate in A7.
2. Campo esplicito pilota/ufficiale e numero di ripetizione nei record (B5).
3. Controllo dell'integrità della toolchain congelata rispetto al manifest
   all'avvio di ogni run.
4. Autorizzazione dell'utente ad avviare le 9 run pilota (chiamate LLM).
