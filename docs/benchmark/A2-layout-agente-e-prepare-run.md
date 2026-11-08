# A2 — Layout dell'area agente e comando `prepare-run`

## Registro

- **Data:** 8 novembre 2026
- **Fase:** architettura single-agent, iterazione A2 (estende A1)
- **Branch di lavoro:** `feat/single-agent`, allineato a `main` in `102c123`
  (ritiro del prototipo P6)
- **Stato:** implementata e verificata con test automatici e prova manuale; in
  attesa di revisione. Le sezioni fino a «Fuori perimetro» sono state scritte
  prima delle modifiche al codice.

## Contesto e decisioni confermate

A1 prepara una workspace pulita da un commit. A2 la trasforma nell'area che il
server MCP (P7) e la sessione dell'agente useranno davvero. Il contratto è stato
concordato con Codex e confermato dall'utente l'8 novembre 2026:

1. **Root:** `C:\AgenticAIRuns` per le aree di run (temporanee) e
   `C:\AgenticAIResults` per i dati persistenti. Entrambe sono parametri espliciti
   del comando, non valori codificati.
2. **Layout:**

   ```text
   <runsRoot>/<runId>/
   ├── session/                 cwd della CLI dell'agente, vuota
   └── agent-root/              root MCP
       ├── workspace/           sorgenti della baseline (scrivibile)
       ├── public-reference/    test pubblici selezionati (sola lettura via MCP)
       ├── agent-tests/         test aggiuntivi dell'agente (scrivibile), vuota
       └── task-specification.md

   <resultsRoot>/<runId>/manifest.json   manifest fidato, fuori dall'area agente
   ```

3. **Test pubblici esposti:** soltanto le suite comuni
   (`tests/public/*.test.ts`) e la suite del task selezionato
   (`tests/public/tN/`). Le suite degli altri task non vengono copiate da nessuna
   parte.
4. **`public-reference/` conserva il prefisso `tests/public/...`**, così gli import
   relativi dei test (`../../src/index.js`, `../../../src/index.js`) restano validi
   quando il worker di esecuzione sovrappone `public-reference/` alla radice di una
   copia di `workspace/`. La regola di composizione per l'esecuzione appartiene al
   worker confinato (Codex): A2 la documenta e non la impone.
5. **`prepare-run` è l'unica fonte dei percorsi:** stdout contiene soltanto JSON,
   la diagnostica va su stderr, exit code `0` (successo), `2` (argomenti non
   validi), `1` (preparazione fallita).
6. **A1 resta l'unica implementazione della preparazione;** P6 è stato ritirato su
   `main` (`102c123`).

## Obiettivo dell'iterazione

Estendere la preparazione A1 in modo che produca, da un unico commit e per un task
`Tn`, l'area agente completa con il layout concordato, il manifest persistente
nella root dei risultati e una verifica d'integrità riutilizzabile, esponendo il
tutto tramite il comando `AgenticExperimentRunner prepare-run`.

## Componenti coinvolti

Tutti in `AgenticExperimentRunner/` (perimetro di Claude); nessun file del server
MCP o degli script di Codex viene toccato.

- `RunPreparation/WorkspacePathPolicy.cs`: nuovi nomi vietati.
- `RunPreparation/TaskMaterialSelection.cs` (nuovo, logica pura): validazione di
  `Tn` e smistamento di ogni file della baseline verso `workspace/`,
  `public-reference/` o esclusione.
- `RunPreparation/SessionAncestorPolicy.cs` (nuovo): controllo delle directory
  superiori dell'area di run.
- `RunPreparation/RunManifest.cs` (sostituisce `BaselineManifest.cs`): manifest
  schema 2.
- `RunPreparation/RunIntegrity.cs` (nuovo): confronto tra manifest e stato su
  disco.
- `RunPreparation/RunAreaPreparer.cs`: nuovo layout, task, root dei risultati.
- `RunPreparation/PrepareRunCommand.cs` (nuovo): parsing degli argomenti e output
  JSON.
- `Program.cs`: instradamento del sottocomando `prepare-run`; il comportamento
  interattivo esistente resta invariato.
- Test in `AgenticExperimentRunner.Tests/RunPreparation/`.

## Comportamento atteso

### Comando

```text
AgenticExperimentRunner prepare-run
    --repository <assoluto> --commit <riferimento> --task <T1..T6>
    --runs-root <assoluto> --results-root <assoluto> [--run-id <id>]
```

- `--run-id` predefinito: `yyyyMMddTHHmmssZ-<8 esadecimali casuali>` (UTC).
- La sorgente è sempre `BenchmarkTarget`; la specifica è
  `docs/benchmark/<Tn>-specifica-pubblica.md` **dello stesso commit**.
- Il comando va invocato come eseguibile compilato, non con `dotnet run`, per non
  mescolare output di build allo stdout JSON.

### Output JSON (stdout, schema 1)

```json
{
  "schemaVersion": 1,
  "runId": "...",
  "taskId": "T1",
  "sourceCommit": "<sha completo>",
  "runAreaPath": "C:\\AgenticAIRuns\\<runId>",
  "agentRoot": "C:\\AgenticAIRuns\\<runId>\\agent-root",
  "sessionDirectory": "C:\\AgenticAIRuns\\<runId>\\session",
  "resultsDirectory": "C:\\AgenticAIResults\\<runId>",
  "manifestPath": "C:\\AgenticAIResults\\<runId>\\manifest.json",
  "workspaceDirectory": "workspace",
  "agentTestsDirectory": "agent-tests",
  "publicReferenceDirectory": "public-reference",
  "taskSpecificationPath": "task-specification.md",
  "writableDirectories": ["workspace", "agent-tests"]
}
```

I percorsi interni all'area agente sono relativi ad `agentRoot`, con separatore
`/`, nello stesso formato atteso da `MCP_WRITABLE_DIRECTORIES`.

### Smistamento dei file della baseline (`BenchmarkTarget` del commit)

| Percorso relativo alla sorgente | Destinazione |
|---|---|
| `tests/public/<file>` (direttamente nella cartella) | `public-reference/tests/public/<file>` |
| `tests/public/<tn>/...` del task selezionato | `public-reference/tests/public/<tn>/...` |
| `tests/public/t<k>/...` di un altro task | escluso, registrato nel manifest |
| `tests/public/<altra cartella>/...` | **errore**: struttura non prevista |
| qualsiasi altro file | `workspace/<percorso>` |

Restano in vigore tutte le regole A1 (solo file regolari, niente collegamenti,
byte identici ai blob, rifiuto di `.git`, `node_modules`, `.npmrc`, `.env*`,
`tests/reserved`). A2 aggiunge il rifiuto, in qualsiasi posizione, di `.claude`,
`CLAUDE.md`, `CLAUDE.local.md`, `AGENTS.md` e `.mcp.json`: istruzioni o
configurazioni dell'agente nella baseline altererebbero le condizioni iniziali in
modo diverso tra le architetture (B4).

La preparazione fallisce se manca la specifica del task, se manca la cartella
`tests/public/<tn>` o se `workspace/` risulta vuota.

### Controlli sulle root

- `runsRoot` e `resultsRoot` devono essere assolute, esterne al repository e
  disgiunte tra loro (nessuna contenuta nell'altra).
- `<runsRoot>/<runId>` e `<resultsRoot>/<runId>` non devono esistere; in caso di
  errore A2 rimuove soltanto ciò che ha creato.
- **Directory superiori:** la CLI dell'agente cerca istruzioni e configurazioni
  risalendo da `session/`. Se `runsRoot` o una directory superiore contiene
  `CLAUDE.md`, `CLAUDE.local.md`, `.mcp.json`, `.claude` o `.git`, la preparazione
  fallisce. Fa eccezione `.claude` nella cartella del profilo utente, che è la
  configurazione utente della CLI e va controllata dal launcher (responsabilità
  concordata con Codex); un `CLAUDE.md` nel profilo resta invece un errore.

### Manifest (schema 2)

Campi: `schemaVersion`, `runId`, `taskId`, `preparedAtUtc`, `sourceCommit`,
`sourceDirectory`, `taskSpecificationSource`, `writableDirectories`, `fileCount`,
`aggregateSha256`, `files` (per ciascuno: `path` relativo ad `agent-root`,
`sourcePath` relativo alla radice del repository, `sizeBytes`, `sha256`),
`excludedSourcePaths` (file della baseline non consegnati, ordinati). L'hash
aggregato usa la formula A1 sui percorsi relativi ad `agent-root`.

### Verifica d'integrità

`RunIntegrity.FindDifferences(manifest, agentRoot, ambito)` restituisce l'elenco
delle differenze (file mancante, contenuto diverso, file in più):

- ambito **completo**: tutto `agent-root` deve coincidere con il manifest (da
  usare subito prima di avviare l'agente);
- ambito **protetto**: soltanto ciò che sta fuori dalle directory scrivibili
  (`public-reference/`, `task-specification.md`, eventuali file in radice), da
  usare a fine run per dimostrare che l'agente non ha alterato i materiali di
  riferimento.

## Criteri oggettivi di accettazione

1. **Repository reale, T1 da HEAD:** `workspace/` coincide con i file versionati
   di `BenchmarkTarget` esclusa `tests/public/`; `public-reference/` contiene
   esattamente i 2 test comuni e quello di `t1`; i file di `t2`–`t6` sono in
   `excludedSourcePaths` e assenti da `agent-root`; `task-specification.md` ha lo
   SHA-256 del blob `docs/benchmark/T1-specifica-pubblica.md`; ogni file ha lo
   SHA-256 del proprio blob; `agent-tests/` e `session/` esistono e sono vuote; il
   manifest è in `<resultsRoot>/<runId>/manifest.json` e nulla è scritto
   nell'area di run fuori da `agent-root/` e `session/`.
2. Ciascuno dei nuovi nomi vietati, una cartella `tests/public/helpers/` e la
   mancanza della specifica o della suite del task fanno fallire la preparazione
   senza lasciare né area di run né cartella dei risultati.
3. Un `CLAUDE.md` o una `.git` in una directory superiore di `runsRoot` fanno
   fallire la preparazione prima di creare qualsiasi cartella; la `.claude` nel
   profilo utente è ammessa.
4. Sono rifiutate: root dei risultati interna al repository, root contenute
   l'una nell'altra, cartella dei risultati già esistente (lasciata intatta),
   task non valido.
5. Verifica d'integrità: subito dopo la preparazione nessuna differenza in ambito
   completo; una modifica in `workspace/` è segnalata solo in ambito completo; una
   modifica, cancellazione o aggiunta in `public-reference/` o nella specifica è
   segnalata in entrambi gli ambiti.
6. `prepare-run`: stdout è un singolo documento JSON con i campi indicati e
   percorsi coerenti con il disco; argomenti mancanti, sconosciuti, duplicati o
   relativi danno exit `2` con stdout vuoto; un errore di preparazione dà exit `1`
   con stdout vuoto; il `runId` predefinito rispetta il formato.
7. `dotnet format --verify-no-changes`, build con 0 avvisi e 0 errori, suite
   completa superata; nessun artefatto tra i file versionabili.
8. Prova manuale del comando compilato sulle root reali `C:\AgenticAIRuns` e
   `C:\AgenticAIResults` per T1, con verifica dell'output e rimozione delle sole
   cartelle create dalla prova.

## Fuori perimetro

- Avvio della CLI dell'agente, prompt, limiti di budget e telemetria (A3).
- Installazione delle dipendenze ed esecuzione di typecheck e test (worker
  confinato, Codex).
- Permessi del file system su `public-reference/`: la protezione è la policy MCP
  P7, la prova a posteriori è la verifica d'integrità in ambito protetto.
- Verifica delle sorgenti di configurazione utente della CLI (launcher, Codex).

## File creati o modificati

- `AgenticExperimentRunner/RunPreparation/`: `TaskMaterialSelection.cs`,
  `SessionAncestorPolicy.cs`, `RunIntegrity.cs`, `PrepareRunCommand.cs` (nuovi);
  `RunManifest.cs` (rinominato da `BaselineManifest.cs`, schema 2);
  `RunAreaPreparer.cs`, `WorkspacePathPolicy.cs` (modificati).
- `AgenticExperimentRunner/Program.cs`: instradamento di `prepare-run`.
- `AgenticExperimentRunner.Tests/RunPreparation/`: `TaskMaterialSelectionTests.cs`,
  `SessionAncestorPolicyTests.cs`, `RunIntegrityTests.cs`,
  `PrepareRunCommandTests.cs` (nuovi); `RunAreaPreparerTests.cs`,
  `BenchmarkBaselinePreparationTests.cs`, `WorkspacePathPolicyTests.cs`
  (adeguati ad A2).

## Problemi osservati e soluzioni

1. **Ancoraggio delle espressioni regolari.** In .NET `$` accetta anche un `\n`
   finale: `"T1\n"` e `"run-1\n"` (quest'ultimo già in A1) superavano la
   validazione. Le espressioni usano ora `\z`; aggiunti i due casi di regressione.
2. **Test del profilo utente.** Il primo run della suite completa è fallito
   (141/142): il test che simulava il profilo utente con una cartella sotto
   `%TEMP%` riceveva, correttamente, il conflitto del vero
   `C:\Users\Utente\.claude`, perché `%TEMP%` si trova dentro il profilo reale.
   L'errore era nel test, non nel codice: i casi sul profilo sono stati spostati in
   test unitari di `SessionAncestorPolicy` che valutano soltanto le voci sotto la
   cartella temporanea.

## Verifiche eseguite e risultati

Eseguite nel worktree `C:\repos\TesiAgenticAI-agent`, l'8 novembre 2026, con
`HEAD` = `102c1236447754dd3a3f81a60a27a9dfe62ec97b`.

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build  AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore --filter-class "*SessionAncestorPolicyTests" --filter-class "*TaskMaterialSelectionTests" --filter-class "*RunIntegrityTests" --filter-class "*RunAreaPreparerTests" --filter-class "*PrepareRunCommandTests" --filter-class "*WorkspacePathPolicyTests" --filter-class "*BenchmarkBaselinePreparationTests"
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
```

| Verifica | Risultato osservato |
|---|---|
| Formattazione | exit code 0 |
| Build | 0 avvisi, 0 errori |
| Test mirati A1/A2 | 135/135 superati |
| Suite completa | primo tentativo 141/142 (vedi sopra); dopo la correzione 145 eseguiti, 145 superati, 0 falliti, 0 ignorati |
| Residui | nessuna cartella `a1-*`/`a2-*` in `%TEMP%` |

**Prova manuale (criterio 8)** con l'eseguibile compilato
`AgenticExperimentRunner\bin\Debug\net10.0\AgenticExperimentRunner.exe`:

```powershell
AgenticExperimentRunner.exe prepare-run --repository C:\repos\TesiAgenticAI-agent --commit HEAD --task T1 --runs-root C:\AgenticAIRuns --results-root C:\AgenticAIResults
```

- exit `0`, stderr vuoto, stdout un solo documento JSON con `runId`
  `20261108T170141Z-3c353edb` e `sourceCommit` uguale a `HEAD`;
- `agent-root`: 15 file, cioè `task-specification.md`, 3 test in
  `public-reference/tests/public/` (2 comuni e `t1/find-products-below-stock.test.ts`)
  e 11 file in `workspace/`; `agent-tests/` e `session/` vuote; nell'area di run
  soltanto `agent-root` e `session`; nella cartella dei risultati soltanto
  `manifest.json`;
- manifest: schema 2, `fileCount` 15, hash aggregato
  `be2627bc07c8c416006cfceea72260c83e84c7764f485e2b7c9c39430a6652b3`,
  `excludedSourcePaths` con le 5 suite `t2`–`t6`;
- stesso `runId` ripetuto: exit `1` («esiste già»); `--runs-root relativo`:
  exit `2`; root delle run dentro il repository: exit `1`, nessuna cartella creata;
- pulizia: rimosse l'area di run, la cartella dei risultati e le due root, che non
  esistevano prima della prova ed erano rimaste vuote.

Corrispondenza con i criteri di accettazione:

1. Superato (`BenchmarkBaselinePreparationTests`, destinazione attesa ricavata
   in modo indipendente da `git ls-tree`, hash confrontati con `git cat-file`).
2. Superato per tutti i nomi vietati, `tests/public/helpers/`, specifica e suite
   mancanti.
3. Superato per `CLAUDE.md`, `.mcp.json`, `.git` e `.claude` nelle directory
   superiori; `.claude` tollerata e `CLAUDE.md` segnalato nel profilo utente
   (test unitari della politica).
4. Superato.
5. Superato (`RunIntegrityTests`, incluso un prefisso ingannevole
   `workspace-copy/`); verifica completa vuota anche sull'area reale.
6. Superato (`PrepareRunCommandTests`).
7. Superato.
8. Superato.

## Limiti e punti non verificati

- **Nomi dei test degli altri task.** `workspace/` contiene, come la baseline,
  `package.json` e i file `vitest.public.t2..t6.config.ts`, che nominano i file
  di test degli altri task (per esempio `receive-stock.test.ts`). Il contenuto di
  quei test e le specifiche degli altri task non sono consegnati. Escludere le
  configurazioni modificherebbe la baseline rispetto al commit approvato; la
  scelta va eventualmente presa a livello di baseline.
- Errori diversi da `RunPreparationException` (per esempio un bug) non sono
  convertiti in exit `1`: il processo termina con l'eccezione non gestita, stdout
  resta vuoto e il codice di uscita è comunque diverso da zero.
- `git archive` applica gli attributi `export-ignore` ed `export-subst` del
  commit; oggi il repository non ne usa e il test di accettazione confronta
  comunque i byte con i blob.
- Restano i limiti A1 sulle junction verso le root (percorsi confrontati senza
  risolvere i reparse point) e su `core.attributesFile`.
- La verifica d'integrità è disponibile come libreria ma non è ancora invocata da
  un flusso di run: lo farà l'orchestratore della sessione (A3).
- Il collegamento con il server MCP reale (`agentRoot` come root,
  `writableDirectories` come `MCP_WRITABLE_DIRECTORIES`) non è stato provato in
  questa iterazione.

## Prossimo passo proposto

A3: avvio della sessione single-agent sull'area preparata (`claude -p` con cwd
`session/`, server MCP P7 su `agent-root`, prompt = specifica + istruzione
architetturale con hash), monitoraggio `stream-json` di token e tempo, verifica
d'integrità prima e dopo la sessione. Prima di implementarlo va verificato se lo
stream riporta l'uso per singolo messaggio.
