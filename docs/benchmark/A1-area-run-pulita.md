# A1 — Area di run pulita per ogni esecuzione

## Registro

- **Data:** 8 novembre 2026
- **Fase:** architettura single-agent, iterazione A1
- **Branch di lavoro:** `feat/single-agent` (worktree separato da `main`, dove
  prosegue il lavoro sul server MCP)
- **Stato:** implementata e verificata con test automatici; in attesa di revisione

## Contesto e decisioni confermate

La serie `A*` documenta lo sviluppo delle architetture agentiche
(prima single-agent, poi gerarchia adattiva). Le decisioni confermate dall'utente
l'8 novembre 2026 per il single-agent sono:

1. **Esecuzione:** una sola sessione `claude -p`, monitorata in tempo reale
   dall'orchestratore tramite `stream-json`; tempo e token vengono sommati durante
   la sessione e il processo viene terminato al superamento dei limiti. La
   presenza dell'uso per singolo messaggio nello stream deve essere verificata
   prima di farvi affidamento (iterazione successiva).
2. **Prompt:** il prompt comune del task è il contenuto esatto di
   `docs/benchmark/Tn-specifica-pubblica.md`; l'istruzione architetturale
   single-agent è un file separato e breve, senza suggerimenti sulla soluzione.
   Entrambi sono versionati e identificati tramite hash.
3. **Primo passo:** A1, la preparazione dell'area di run, prerequisito comune
   alle tre architetture.

## Obiettivo dell'iterazione

Implementare nell'orchestratore la preparazione di un'area di run nuova e pulita
(passi 2–5 del flusso B3), a partire da un commit approvato della baseline:

- copia esclusiva dei file **versionati** di `BenchmarkTarget` in quel commit;
- nessuna directory `.git`, file non versionati, `node_modules`, `dist`,
  credenziali o test riservati;
- manifest con provenienza e hash, conservato fuori dalla workspace dell'agente.

## Componenti coinvolti

- `AgenticExperimentRunner/RunPreparation/` (nuova cartella):
  - politica dei percorsi autorizzati (logica di dominio pura);
  - accesso al processo `git` (risoluzione commit, `git archive`);
  - costruzione e serializzazione del manifest;
  - preparatore dell'area di run che coordina i passi.
- `AgenticExperimentRunner.Tests/RunPreparation/` (nuovi file di test).

Non vengono modificati il server MCP, il progetto di test (`.csproj`) né
`Program.cs`. Il collegamento a una riga di comando dell'orchestratore è rinviato
all'iterazione che avvierà la sessione dell'agente.

## Comportamento atteso

Input espliciti: percorso del repository, riferimento della baseline (risolto in
SHA completo del commit), sottodirectory sorgente (`BenchmarkTarget`), root delle
run (percorso assoluto) e `runId`.

Struttura prodotta:

```text
<runsRoot>/<runId>/
├── workspace/               ← unica root che verrà assegnata all'agente
└── baseline-manifest.json   ← fuori dalla workspace, non modificabile via MCP
```

- La sorgente è letta da Git con
  `git -c core.autocrlf=false -c core.eol=lf archive <sha>:<sorgente>`: il
  contenuto coincide byte per byte con i blob del commit, indipendentemente dallo
  stato dell'albero di lavoro e dalla configurazione dei fine riga della macchina
  (vedi «Problema osservato»).
- Ogni voce dell'archivio deve essere un file regolare o una directory con
  percorso relativo valido. Collegamenti simbolici o fisici sono rifiutati.
- Sono rifiutati (la preparazione fallisce, non li salta in silenzio): segmenti
  `.git` e `node_modules`, file `.gitconfig`, `.npmrc`, `.env`, `.env.*` e
  qualsiasi percorso sotto `tests/reserved`. Il rifiuto riprende la regola già
  applicata in P2.2.
- `runsRoot` non può trovarsi all'interno del repository sorgente, per evitare che
  processi avviati nella workspace risalgano al repository e alla sua cronologia.
- Una cartella `<runId>` già esistente non viene mai riutilizzata.
- In caso di errore l'area appena creata viene rimossa e l'errore è riportato con
  un messaggio diagnostico; nessuna area parziale resta disponibile.
- Il manifest contiene: versione dello schema, `runId`, orario UTC, SHA completo
  del commit, sottodirectory sorgente, numero di file, elenco ordinato
  (ordinamento ordinale dei percorsi con separatore `/`) con dimensione e
  SHA-256 di ogni file, e un hash aggregato. L'hash aggregato è lo SHA-256 del
  testo UTF-8 formato da una riga `<sha256>  <percorso>\n` per file, nello stesso
  ordine; dipende quindi solo dal contenuto e non da `runId` o orario.
- Gli hash sono calcolati sui file effettivamente scritti nella workspace.

## Criteri oggettivi di accettazione

1. L'insieme dei file della workspace coincide con
   `git ls-tree -r --name-only <sha> -- BenchmarkTarget` e ogni file ha lo stesso
   SHA-256 del blob corrispondente (test di integrazione sul repository reale).
2. Un file non versionato presente nell'albero di lavoro sorgente non compare
   nella workspace.
3. Due preparazioni dello stesso commit con `runId` diversi producono lo stesso
   hash aggregato e manifest con `runId` distinti.
4. Un commit che contiene `tests/reserved/...`, `.npmrc` o `.env` fa fallire la
   preparazione e non lascia la cartella della run.
5. Sono rifiutati: `runsRoot` interno al repository, `runId` già esistente,
   `runId` non valido, riferimento inesistente.
6. Test unitari della politica dei percorsi per i casi ammessi e vietati.
7. `dotnet format --verify-no-changes`, build con zero avvisi e zero errori, tutti
   i test superati; nessun artefatto generato tra i file versionabili.

## Note sull'ambiente di test

La configurazione Git globale dell'utente ha `commit.gpgsign=true`. I repository
temporanei usati come fixture dai test vengono creati sotto `%TEMP%` con
configurazione locale esplicita (identità fittizia, firma disattivata,
`core.autocrlf=false`). La disattivazione della firma riguarda solo questi
repository di prova usa e getta e non i commit del progetto.

## Fuori perimetro (rinviato)

- permessi di sola lettura sui test pubblici e directory separata per i test
  aggiuntivi dell'agente (B3 passo 4): il nome della directory non è ancora
  definito;
- installazione delle dipendenze (`node_modules`) per l'esecuzione dei test, che
  dipende dal runner del server MCP;
- registrazione di versioni di ambiente, prompt e configurazione CLI nel manifest
  della run (B5), che appartiene alla sessione dell'agente.

## File creati

- `AgenticExperimentRunner/RunPreparation/WorkspacePathPolicy.cs`
- `AgenticExperimentRunner/RunPreparation/GitCommandRunner.cs`
- `AgenticExperimentRunner/RunPreparation/BaselineManifest.cs`
- `AgenticExperimentRunner/RunPreparation/RunAreaPreparer.cs`
- `AgenticExperimentRunner/RunPreparation/RunPreparationException.cs`
- `AgenticExperimentRunner.Tests/RunPreparation/` (4 file di test e supporto)

## Problema osservato e soluzione

Il primo run del test di accettazione sul repository reale è **fallito**: i
percorsi coincidevano, ma tutti gli SHA-256 differivano dai blob. Causa
verificata: la configurazione di sistema di Git for Windows
(`C:/Program Files/Git/etc/gitconfig`) imposta `core.autocrlf=true` e
`git archive` converte LF in CRLF. Prova diretta su `package.json`:

- senza override l'output contiene `\r\n`;
- con `-c core.autocrlf=false` lo SHA-256 è
  `a301939014881cd53ea1d635f1261c8fdc7155f91d9fc1b5352144676b6dfa39`, identico a
  `git cat-file blob HEAD:BenchmarkTarget/package.json`.

Il comando è stato reso indipendente dalla configurazione host con
`-c core.autocrlf=false -c core.eol=lf` ed è stato aggiunto il test di regressione
`PrepareAsync_WithHostAutoCrlf_KeepsExactCommittedBytes`. Senza la correzione, la
stessa baseline avrebbe prodotto contenuti diversi su macchine configurate
diversamente, violando B4. La workspace contiene quindi i fine riga dei blob (LF
per i file attuali di `BenchmarkTarget`).

## Verifiche eseguite e risultati

Eseguite nel worktree `C:\repos\TesiAgenticAI-agent`, l'8 novembre 2026.

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build  AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore --filter-class "*WorkspacePathPolicyTests" --filter-class "*RunAreaPreparerTests" --filter-class "*BenchmarkBaselinePreparationTests"
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
```

| Verifica | Risultato osservato |
|---|---|
| Formattazione | exit code 0, nessuna modifica richiesta |
| Build | 0 avvisi, 0 errori (dopo la correzione di 15 avvisi xUnit1051 sul token di annullamento) |
| Test mirati A1 | primo tentativo: 47/48, fallito il test sul repository reale (vedi sopra); dopo la correzione: 48/48 |
| Test di regressione autocrlf | 1/1 superato |
| Suite completa | 54 eseguiti, 54 superati, 0 falliti, 0 ignorati |
| Artefatti | `git status` mostra solo i file sorgente e la documentazione; nessuna cartella `a1-*` residua in `%TEMP%` |

Corrispondenza con i criteri di accettazione:

1. Superato: `BenchmarkBaselinePreparationTests` confronta insieme dei file e
   SHA-256 con `git ls-tree`/`git cat-file` sul commit HEAD reale (19 file).
2. Superato: file non versionati e modifiche non committate esclusi.
3. Superato: stesso hash aggregato e `runId` distinti.
4. Superato per `tests/reserved/...`, `.npmrc`, `.env`, `node_modules/...` e per i
   collegamenti simbolici; l'area della run non resta su disco.
5. Superato per root interna al repository, area esistente (lasciata intatta),
   `runId` non valido, riferimento o sorgente inesistenti e riferimento che imita
   un'opzione.
6. Superato: 29 casi della politica dei percorsi.
7. Superato.

## Limiti e punti non verificati

- La verifica di `runsRoot` esterna al repository confronta percorsi normalizzati
  e non risolve eventuali junction che portino nel repository.
- Un file di attributi Git globale (`core.attributesFile`) potrebbe ancora
  modificare l'output di `git archive`; sulla macchina attuale non è configurato.
  Il test di accettazione confronta comunque i byte con i blob e rileverebbe la
  differenza.
- L'area non è ancora collegata al server MCP né a una sessione dell'agente.
- Lo script `scripts/New-AgentWorkspace.ps1`, in sviluppo da parte di Codex su
  `main`, prepara anch'esso una workspace: le due implementazioni andranno
  riconciliate prima delle run, mantenendone una sola come riferimento.
