# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Le regole operative vincolanti (metodo a iterazioni, autorizzazioni, qualità del
codice, commenti, validità sperimentale) sono in `AGENTS.md` e valgono anche per
Claude Code:

@AGENTS.md

La documentazione del repository è in italiano: mantenere l'italiano per
documenti, messaggi diagnostici e commenti.

## Comandi

### .NET (net10.0, xUnit v3 su Microsoft.Testing.Platform — vedi `global.json`)

Non esiste un file `.sln`: l'unico progetto di test referenzia sia
`AgenticExperimentRunner` sia `McpReadOnlyPrototype`, quindi è il punto di
ingresso per build e test.

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build  AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
# singola classe o singolo metodo (filtri xUnit v3 / MTP, accettano wildcard)
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --filter-class "*RootedFileAccessTests"
dotnet test AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --filter-method "*NomeMetodo*"
```

`AgenticExperimentRunner` espone gli `internal` ai test tramite `AssemblyInfo.cs`.

### BenchmarkTarget (TypeScript strict, ESM `nodenext`, Vitest)

Eseguire da `BenchmarkTarget/`; installazione riproducibile con `npm ci`.

```powershell
npm run build        # tsc -> dist/
npm run typecheck    # tsc --noEmit
npm test             # suite pubblica comune (vitest.public.config.ts)
npm run test:public:t1   # ... fino a t6: suite pubblica del singolo task
```

Ogni task Tn ha un proprio `vitest.public.tN.config.ts` che include solo
`tests/public/tN/`. Tutte le config escludono `tests/reserved/**`.

### Prove di accettazione dell'harness (PowerShell)

```powershell
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Direct   # prova MCP diretta, senza LLM
.\scripts\Invoke-McpReadOnlyPrototype.ps1 -Mode Claude   # sessione Claude CLI reale: consuma chiamate LLM
```

Gli script creano una root temporanea `%TEMP%\mcp-readonly-prototype-<guid>\`
con sottocartelle `trusted`, `agent-workspace`, `external` (sentinelle per i
tentativi di uscita) ed `evidence`. Le evidenze restano fuori dal repository;
nei documenti si riporta il percorso e si versiona solo un riepilogo in
`docs/benchmark/evidence/`. Non avviare la modalità `Claude`/`All` senza
autorizzazione: è una chiamata LLM.

## Architettura

- **`BenchmarkTarget/`** — piccolo gestionale ordini/prodotti/magazzino (tutto in
  `src/index.ts`), bersaglio comune dei task T1–T6. Le specifiche pubbliche dei
  task sono in `docs/benchmark/Tn-specifica-pubblica.md`; i test pubblici in
  `tests/public/`. Lo stato del codice è la **baseline**: non implementare le
  soluzioni dei task.
- **Test riservati** — non esistono in questo repository e non devono mai
  entrarvi, nemmeno temporaneamente (resterebbero nella cronologia Git). Sono
  conservati in un'area fidata separata (vedi `docs/benchmark/B3-*.md`).
- **`McpReadOnlyPrototype/`** — server MCP stdio scritto a mano (JSON-RPC
  riga per riga, senza SDK), identificato come `workspace-prototype`. Argomenti:
  `<root-assoluta-consentita> [runner-assoluto]`. Strumenti: `list_files`,
  `read_file`, `write_file`; `run_public_suite` compare solo se viene passato il
  runner opzionale (storico, Windows Sandbox). `RootedFileAccess` è il confine di
  sicurezza: rifiuta percorsi assoluti, `..` e reparse point/junction che
  escono dalla root. Il nome "ReadOnly" è storico.
- **`AgenticExperimentRunner/`** — orchestratore, ancora minimale: invoca
  `claude.exe` (percorso e modello hard-coded in `Program.cs`) tramite
  `ClaudeCliClient`, interpreta l'output JSON in `ClaudeCliResult` (token
  input/output/cache, durata, modello effettivo) e accoda una riga JSON nel file
  di risultati. Non è ancora collegato al server MCP.
- **`P21IsolationProbe/`, `NodeBaselineSandboxProbe/`, `McpSandboxBridgeProbe/`,
  script `*Sandbox*`/`P21*` e `.wsb`** — evidenze storiche del percorso Windows
  Sandbox, non più parte dell'architettura operativa (vedi `AGENTS.md`).

## Documentazione sperimentale

`docs/benchmark/` è il registro dell'esperimento: `B1`–`B5` progettazione
(struttura, task, strategia test, equità, metriche), `P1`–`P5` iterazioni
dell'harness, ognuna con data, comandi, output osservati e limiti. Prima di
proseguire leggere il documento P più recente: la sezione finale indica cosa è
ancora non verificato e la prossima ipotesi da testare. Ogni nuova iterazione
produce un nuovo documento `Pn` con la stessa struttura.
