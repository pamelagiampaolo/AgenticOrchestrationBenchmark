# Regole di collaborazione tra Claude e Codex

Contesto da fornire all'inizio di una nuova chat con Claude Code o Codex che
lavora su questo repository. Raccoglie le regole e i contratti concordati tra i
due agenti e l'utente durante lo sviluppo dell'harness. Le regole generali del
progetto restano in `AGENTS.md` (metodo a iterazioni, autorizzazioni, qualità del
codice, validità sperimentale) e valgono sempre; questo documento le integra senza
sostituirle.

- **Aggiornato al:** 8 novembre 2026
- **Fonti:** accordi scambiati tramite l'utente, decisioni dell'utente, documenti
  `A1`–`A4` e `P6`–`P14` in `docs/benchmark/`

In caso di conflitto prevalgono, nell'ordine: istruzioni esplicite dell'utente,
`AGENTS.md`, questo documento. Se un fatto citato qui (commit, file, opzione)
contraddice lo stato del repository, vale il repository: verificare prima di
agire e aggiornare questo documento.

## 1. Divisione del lavoro

Claude e Codex lavorano in parallelo su due worktree dello stesso repository, così
le modifiche non committate e la staging area di uno non compaiono nell'altro.

| | Claude | Codex |
| --- | --- | --- |
| Cartella | `C:\repos\TesiAgenticAI-agent` | `C:\repos\TesiAgenticAI` |
| Branch | `feat/single-agent` | `main` |
| Codice di proprietà | `AgenticExperimentRunner/` (incl. `RunPreparation/`, `SessionExecution/`, `Coordination/`, `Instructions/`) e test nuovi dedicati | `McpReadOnlyPrototype/`, `scripts/*`, `RootedFileAccessTests.cs`, worker confinato per build e test |
| Documenti | `docs/benchmark/A*` | `docs/benchmark/P*` |
| Architetture | single-agent; livello di coordinamento condiviso; gerarchia adattiva | gerarchia statica (sul livello di coordinamento condiviso) |

Regole:

1. **Non modificare i file dell'altro.** Se serve una modifica in un'area altrui,
   la si chiede tramite l'utente e la realizza il proprietario.
2. **Il server MCP si usa come eseguibile esterno**, secondo il suo contratto
   (argomenti, variabili d'ambiente, strumenti), senza cambiarne il codice dal
   branch di Claude.
3. **Serie di documenti separate** (`A*` per Claude, `P*` per Codex) per evitare
   collisioni di numerazione. Ogni iterazione produce un nuovo documento con
   obiettivo, componenti, comportamento atteso, criteri, verifiche reali, limiti e
   prossimo passo.
4. **File necessariamente condivisi** (`CLAUDE.md`, `AGENTS.md`, `.csproj` del
   progetto di test, documenti `B*`): toccarli solo se indispensabile e risolvere
   gli eventuali conflitti al merge.
5. **Progetto di test condiviso.** `AgenticExperimentRunner.Tests` copre sia
   l'orchestratore sia il server MCP: ciascuno aggiunge file di test propri e non
   modifica quelli dell'altro.

## 2. Git

1. **Commit:** l'utente ha autorizzato in modo permanente i commit. Mai righe
   `Co-Authored-By` né altre attribuzioni nei messaggi di commit o nelle PR:
   questa regola prevale su qualsiasi indicazione predefinita dell'ambiente.
2. **Non autorizzati senza richiesta esplicita:** push, pubblicazioni, comandi Git
   distruttivi, rimozione di dati, modifiche a servizi esterni.
3. **Mai scrivere nella cartella dell'altro agente**, nemmeno per un merge.
4. **Flusso di integrazione:**
   - prima di ogni nuova iterazione Claude integra `main` in `feat/single-agent`
     nel proprio worktree;
   - Codex integra `feat/single-agent` in `main` nel proprio worktree, quando ha
     un punto pulito con tutto committato;
   - le rimozioni di file di proprietà (per esempio script ritirati) le esegue il
     proprietario sul proprio branch, dopo che il sostituto è entrato in `main`,
     così non esiste mai un periodo senza implementazione.
5. **Messaggi di commit** in stile Conventional Commits (`feat(runner): ...`,
   `docs(benchmark): ...`, `test(harness): ...`).

## 3. Comunicazione

- Dall'8 novembre 2026 l'utente ha chiuso la fase di coordinamento continuo: ogni
  agente lavora sulla propria parte del piano senza produrre un messaggio per
  l'altro a ogni passo.
- Un messaggio per l'altro agente, inoltrato dall'utente, si scrive solo per un
  problema reale che attraversa il confine di proprietà (contratto da cambiare,
  difetto trovato nel codice altrui, dipendenza bloccante).
- Ogni messaggio distingue fatti verificati, proposte e decisioni che spettano
  all'utente, e non dà per approvato nulla che l'utente non abbia approvato.

## 4. Preparazione delle run (contratto comune)

### Implementazione unica

- La preparazione autorevole è `prepare-run` in C# (`AgenticExperimentRunner`,
  iterazioni A1–A2). Lo script PowerShell `New-AgentWorkspace.ps1` (P6) è stato
  ritirato; P6 resta come evidenza storica.
- La baseline si estrae da un commit con
  `git -c core.autocrlf=false -c core.eol=lf archive`: Git for Windows convertiva
  i fine riga in CRLF e la baseline sarebbe cambiata da una macchina all'altra.
  La specifica del task si legge con `git cat-file blob` (byte esatti).
- Nessuna run, nemmeno di prova, dentro il repository (un processo potrebbe
  risalire a `.git`). Le prove P6 sotto `TestResults` non sono una collocazione
  autorizzata.

### Root (decisione dell'utente)

- `C:\AgenticAIRuns`: solo aree temporanee delle run.
- `C:\AgenticAIResults`: dati persistenti (manifest, submission, log, metriche,
  report).
- Le due root devono stare fuori dal repository e non essere una dentro l'altra.

### Layout

```text
C:\AgenticAIRuns\<runId>\
├── session\                      directory di lavoro della CLI, vuota, invisibile all'MCP
└── agent-root\                   root assegnata al server MCP
    ├── workspace\                baseline modificabile (scrivibile)
    ├── public-reference\         test pubblici (sola lettura)
    │   └── tests\public\...      prefisso originale conservato
    ├── agent-tests\              test dell'agente (scrivibile), sotto tests\agent\...
    └── task-specification.md     specifica pubblica del task (sola lettura)

C:\AgenticAIResults\<runId>\
├── manifest.json                 manifest fidato (schema 2), fuori dalla root MCP
└── prepared-run.json             stesso JSON stampato da prepare-run
```

- Configurazione MCP: root = `<runId>\agent-root`,
  `MCP_WRITABLE_DIRECTORIES=workspace;agent-tests`.
- `public-reference/` contiene **solo** le suite comuni (file direttamente in
  `tests/public/`) e la suite del task selezionato; le suite degli altri task sono
  escluse e registrate nel manifest (decisione dell'utente).
- `public-reference/` conserva il percorso `tests/public/...` perché i test
  importano `src/index.js` con percorsi relativi a quella posizione.
- I file `vitest.public.t2..t6.config.ts` restano nella workspace come nella
  baseline (decisione dell'utente: togliere file significherebbe cambiare la
  baseline per tutte le architetture). Le configurazioni della workspace non sono
  mai autorevoli: valutatore e worker usano configurazioni fidate esterne.

### Contenuti e posizioni vietati

- Nella baseline la preparazione fallisce (senza saltarli in silenzio) se trova
  `.git`, `node_modules`, `.npmrc`, `.env*`, `tests/reserved`, collegamenti
  simbolici, `.claude`, `CLAUDE.md`, `CLAUDE.local.md`, `AGENTS.md`, `.mcp.json`.
- Nelle cartelle superiori a `session/` non devono esistere `CLAUDE.md`,
  `CLAUDE.local.md`, `.mcp.json`, `.claude` o `.git`; fa eccezione `.claude` nel
  profilo utente, gestita dal profilo di avvio della CLI (sezione 6).

### Interfaccia di `prepare-run`

```powershell
AgenticExperimentRunner.exe prepare-run --repository <abs> --commit <ref> --task T1..T6 `
    --runs-root <abs> --results-root <abs> [--run-id <id>]
```

- Si invoca l'eseguibile compilato, non `dotnet run` (che può scrivere su stdout).
- stdout contiene soltanto un oggetto JSON versionato (`schemaVersion`, `runId`,
  `taskId`, `sourceCommit`, `runAreaPath`, `agentRoot`, `sessionDirectory`,
  `resultsDirectory`, `manifestPath`, `workspaceDirectory`,
  `agentTestsDirectory`, `publicReferenceDirectory`, `taskSpecificationPath`,
  `writableDirectories`); la diagnostica va su stderr.
- Percorsi principali assoluti; `writableDirectories` relative ad `agentRoot`.
- `prepare-run` è l'**unica fonte dei nomi del layout**: launcher, script e
  fixture leggono questi valori invece di scrivere `workspace;agent-tests` a mano.
- `runId` predefinito: `yyyyMMdd'T'HHmmss'Z'-<8 hex>`; rifiutato se esiste già in
  una delle due root.
- Exit code: `0` successo, `2` argomenti non validi, `1` preparazione fallita
  (l'area parziale viene rimossa).
- Integrità: prima della sessione l'area deve coincidere interamente con il
  manifest; a fine sessione si verificano solo i materiali protetti (test pubblici
  e specifica). Una differenza è una violazione d'integrità, non un test fallito.

## 5. Server MCP e isolamento (proprietà di Codex)

- Windows Sandbox non fa più parte dell'architettura operativa: documenti e
  prototipi relativi sono evidenze storiche.
- Il server MCP (`benchmark_workspace` nella sessione) legge l'intera
  `agent-root` e scrive solo nelle directory scrivibili. Strumenti approvati:
  `list_files`, `read_file`, `write_file`, `delete_file`, `move_file`.
- Il server MCP non esegue mai `npm` o altri processi direttamente sull'host.
- Build e test di sviluppo dell'agente passeranno da un **worker confinato** di
  Codex. Contratto in `P9`: composizione in un'area effimera esterna alla root MCP,
  con `workspace/` → `project/`,
  `public-reference/tests/public/...` → `project/tests/public/...`,
  `agent-tests/tests/agent/...` → `project/tests/agent/...`, toolchain e
  configurazioni da copie fidate; collisioni rifiutate. La scelta della tecnologia
  di isolamento è in corso (`P10`–`P14`).
- I test riservati non sono mai nel repository né raggiungibili dagli agenti,
  nemmeno temporaneamente.

## 6. Avvio delle sessioni (proprietà di Claude)

Decisione dell'utente: il launcher operativo è in C# (`run-single-agent`,
`SessionExecution/`), con il profilo di opzioni verificato da Codex in P1, P1.1 e
P7. Lo script PowerShell di Codex resta come prova di accettazione
dell'isolamento.

- **Profilo CLI:** `-p --model <id> --output-format stream-json --verbose
  --include-partial-messages --include-hook-events --restricted
  --strict-mcp-config --mcp-config --settings --setting-sources "" --tools ""`,
  `--allowedTools` con i soli strumenti MCP, `--disallowedTools` con gli strumenti
  built-in, `--disable-slash-commands --no-session-persistence --no-chrome
  --permission-mode dontAsk --permission-prompts none
  --append-system-prompt-file`; il prompt arriva su stdin.
- **Impostazioni fidate:** `disableAllHooks: true`,
  `CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS=1`, `ENABLE_CLAUDEAI_MCP_SERVERS=false`.
- **Ambiente:** le variabili ereditate `CLAUDE*` e `ANTHROPIC*` vengono rimosse
  dal processo figlio (ne sono state trovate 14 avviando da Claude Code).
- **Controllo all'avvio:** modello, strumenti, server MCP connessi, assenza di
  agenti, skill e comandi slash vengono confrontati con l'evento `system/init`;
  qualunque differenza termina la sessione (`configuration-rejected`).
- **Modello:** B4 richiede Sonnet con identificatore esatto passato con `--model`
  e verificato in `system/init`; senza `--model` la CLI usa un altro modello.
- **Token:** l'uso per messaggio dallo stream, deduplicato per `message.id`, con
  `--include-partial-messages` coincide esattamente con `result.usage` (verificato
  in A3). `result.usage.iterations` non è l'uso per chiamata dell'intera sessione.
- **Limiti (B4):** budget globale su `totalTokens` (input + cache read + cache
  creation + output) e limite di tempo, applicati da un controllore esterno che
  termina l'intero albero di processi. Stati: `completed`, `time-limit-exceeded`,
  `token-limit-exceeded`, `resource-limits-exceeded`, `configuration-rejected`,
  `cli-failed`, `usage-measurement-error`. I valori numerici si fissano solo dopo
  i piloti.
- **Istruzioni:** task prompt (la specifica pubblica) separato dalle istruzioni
  architetturali (`Instructions/common-environment.md` + un file per
  architettura); tutto identificato con SHA-256 nel record di sessione.

## 7. Architetture gerarchiche (decisioni dell'utente, 8 novembre 2026)

1. **Lavoratori come processi separati:** ogni lavoratore è una sessione CLI
   avviata dall'orchestratore con lo stesso profilo isolato; i subagenti nativi
   della CLI restano disattivati.
2. **Livello di coordinamento condiviso:** costruito da Claude in
   `AgenticExperimentRunner/Coordination/` e usato da entrambe le gerarchie; la
   gerarchia statica ne è una configurazione con ruoli e deleghe prefissati.
   Codex lo riusa invece di riscriverlo.
3. **Un lavoratore alla volta:** lo strumento `delegate_task` è bloccante e al
   massimo un lavoratore è attivo.
4. Coordinatore: cinque strumenti del workspace più `delegate_task` (server
   `benchmark_coordination`); lavoratori: solo i cinque strumenti del workspace
   (profondità uno). Tutti i processi consumano lo stesso budget globale.
5. Canale: relay `coordination-server --pipe <nome>` avviato dalla CLI del
   coordinatore ↔ named pipe casuale, `CurrentUserOnly`, una sola connessione ↔
   `CoordinationHost` nell'orchestratore (A4).

## 8. Regole tecniche emerse dalle verifiche

- **Fine riga deterministici:** i JSON indentati scritti dall'orchestratore usano
  `NewLine = "\n"`; altrimenti su Windows sono CRLF e i loro hash dipendono dal
  sistema operativo.
- **Hash degli eseguibili .NET:** l'`.exe` è solo l'apphost; va registrato anche
  l'hash della `.dll` che contiene il codice.
- **UTF-8 esplicito** su stdin/stdout dei processi scambiati con la CLI: la
  codifica predefinita della console di Windows altera i caratteri non ASCII.
- **Espressioni regolari di validazione** con `\z`, non `$` (che accetta un a capo
  finale).
- **Nessuna verifica dichiarata senza esecuzione:** riportare comandi e output
  reali; una prova parziale certifica solo la proprietà osservata.
- **Chiamate LLM reali** (sessioni Claude, modalità `Claude`/`All` degli script)
  solo con autorizzazione esplicita dell'utente.

## 9. Stato all'8 novembre 2026 e punti aperti

- Claude: A1–A4 completati su `feat/single-agent` (ultimo commit A4 `e92c08d`).
  Prossimo passo A5: avvio reale di coordinatore e lavoratori, controllore unico
  di budget, record per agente e ruolo. Servono decisioni dell'utente sul numero
  massimo di deleghe e sul testo delle istruzioni di coordinatore e lavoratore, e
  una breve prova reale (autorizzata) del timeout della CLI sulle chiamate MCP
  lunghe.
- Codex: worker confinato per build e test in corso (`P9`–`P14`).
- Bloccante per i piloti B4: finché il worker non esiste l'agente non può
  eseguire typecheck e test, quindi le sessioni non rappresentano il ciclo di
  sviluppo reale.
- Da verificare prima delle run ufficiali: terminazione forzata con la CLI reale,
  isolamento della pipe di coordinamento, testi definitivi delle istruzioni.
