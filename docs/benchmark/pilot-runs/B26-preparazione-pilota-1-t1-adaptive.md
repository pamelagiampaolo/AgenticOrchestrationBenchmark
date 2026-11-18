# B26 — Primo pilota T1 adattivo

## Stato e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione del primo pilota B25.
- **Stato:** pilota eseguito e valutato dopo autorizzazione esplicita.

Obiettivo: predisporre un'area nuova per il primo slot, senza eseguire l'agente.
Componenti: preparatore dell'harness congelato, repository sorgente, area agente
e directory dei risultati. Criteri: task e commit esatti, file corrispondenti al
manifest, sessione e test dell'agente inizialmente vuoti, nessun avvio della CLI.

## Preparazione osservata

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' prepare-run `
  --repository 'C:\repos\AgenticOrchestrationBenchmark' `
  --commit aee8d1162bb496895990a00095cc14c2651ff752 `
  --task T1 --runs-root 'C:\AgenticAIRuns' --results-root 'C:\AgenticAIResults'
```

Exit code `0`. Il preparatore ha restituito `runId`
`20261118T172146Z-77c1a569`, task `T1` e commit richiesto. L'area è
`C:\AgenticAIRuns\20261118T172146Z-77c1a569`; risultati e `prepared-run.json`
sono in `C:\AgenticAIResults\20261118T172146Z-77c1a569`.

Il confronto di dimensioni e `Get-FileHash` contro `manifest.json` ha rilevato
15 file nel manifest, 15 file nell'area agente e zero differenze. Sessione e
`agent-tests` erano vuoti. Directory scrivibili dichiarate: `workspace` e
`agent-tests`. Hash SHA-256:

- `manifest.json`: `fd30448bf86189993d8c6eb518ac7863c5ac82e7ab22bd6f3080b11f3eaaedbd`;
- `prepared-run.json`: `4732a5b784874e0c07b39bce642f1b5e7376405c24a40b27ec50608375bf361d`.

Il controllo certifica soltanto la corrispondenza osservata dell'area al proprio
manifest e le directory vuote, non l'isolamento completo del sistema.

## Comando di avvio predisposto

Il comando seguente era stato preparato prima dell'autorizzazione; è stato
successivamente eseguito per il primo pilota, con esito registrato in fondo.

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-adaptive-hierarchy `
  --prepared-run 'C:\AgenticAIResults\20261118T172146Z-77c1a569\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 2700 --token-budget 3000000 `
  --max-delegations 10 --run-kind pilot --repetition 1 --order-seed d9dc66a --order-position 1
```

Al momento della preparazione restavano da ripetere i controlli pre-run previsti da B25,
inclusa l'integrità dell'area preparata, degli input congelati e dei materiali
riservati. Dopo la run erano previsti valutazione indipendente, controlli di integrità,
metriche e residui. Questo documento non autorizza chiamate LLM o altri piloti.

## Ricontrollo pre-avvio del 18 novembre 2026

Senza avviare la CLI dell'agente sono stati ripetuti i seguenti controlli:

- area preparata: hash dei due JSON invariati, 15 file su 15 corrispondenti al
  manifest, nessun file aggiuntivo, sessione e `agent-tests` vuoti;
- harness: manifest con hash atteso, 16 file attesi e presenti, zero differenze
  di dimensione o SHA-256; toolchain: manifest con hash atteso, 1148 file attesi
  e presenti, zero differenze;
- CLI: dimensione e hash dell'eseguibile e hash del manifest corrispondenti a
  P34; `--version` ha restituito `2.1.312 (Claude Code)`;
- manifest sorgente: SHA-256 corrispondente a P30; confronto della baseline
  fidata con i 19 file benchmark del manifest: zero differenze;
- evaluator: repository pulito al commit registrato in P35; hash dei manifest
  pubblico e riservato e dello schema corrispondenti a P35;
- `Test-PublicManifest.ps1` con baseline e toolchain congelate: exit code 0,
  `Manifest coerente (test verificati: True)`; `Test-ReservedManifest.ps1` con
  toolchain congelata: exit code 0, identificatori verificati.

Il controllo della baseline protetta è stato eseguito con accesso autorizzato.
Le prove precedenti P27 e P29 restano le evidenze di accettazione MCP: questo
ricontrollo non ripete una prova completa di isolamento né dimostra l'assenza
di tutti i percorsi alternativi. I controlli sono osservazioni del momento e,
se l'avvio non è immediato, devono essere ripetuti prima della run. La run e le
verifiche dei record e dei residui erano ancora da eseguire dopo l'autorizzazione.

## Esecuzione e valutazione osservate

Dopo l'autorizzazione dell'utente sono stati ripetuti i controlli pre-avvio:
area 15/15, harness 16/16, toolchain 1148/1148 e baseline 19/19 conformi; CLI,
evaluator e manifest pubblici e riservati conformi. Il comando riportato sopra
è stato eseguito una volta, con exit code 0 e stato `completed`.

- `agentTaskDurationMs`: 24820; `totalTokens`: 38503.
- Input non in cache: 8; cache read: 20562; cache creation: 13491; output: 4442.
  Misurazione completa; uso dallo stream coerente con quello autorevole.
- Quattro messaggi LLM registrati; 0 deleghe richieste, avviate o completate.
  La scelta di non delegare è ammessa dalla condizione adattiva.
- Modello e server MCP dichiarati conformi, violazioni di configurazione e
  differenze d'integrità protetta: zero; errori strumento e righe malformate:
  zero. Typecheck e test pubblici nell'audit hanno exit code 0.
- Confronto dei cinque file runtime effettivi con gli hash nel record: 5/5
  conformi. Stderr del coordinatore vuoto; sessione vuota; nessun processo
  ancora attivo con percorso dell'harness o della CLI congelati.

L'evaluator al commit congelato ha restituito `passed`, exit code 0: build
superata, test pubblici 31/31 e riservati 3/3; nessun test fallito, saltato,
mancante o inatteso. Integrità dell'input originale, copia archiviata e manifest
delle suite confermata; diagnostica vuota. Durata valutazione: 3150 ms.

- `run-record.json`, SHA-256:
  `09316dd20af0b977c2093fb1adb3c3b6bb5156ca656e90a2dea4ac0894f20a91`;
- `evaluation-summary.json`, SHA-256:
  `b261475cd12fc13306a4ee950cfc417065adbdbd9014b63b7d330eefdbf4bba7`.

L'evidenza della valutazione è conservata nell'area fidata dell'evaluator,
separata dal workspace agente. Il pilota è valido per la calibrazione B25 sulla
base delle verifiche riportate; restano cinque slot e non si derivano ancora i
limiti ufficiali. Non sono state avviate altre run.
