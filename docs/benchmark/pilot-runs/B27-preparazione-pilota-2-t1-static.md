# B27 — Secondo pilota T1 statico

## Stato e obiettivo

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 2 del protocollo B25.
- **Stato:** pilota eseguito e valutato dopo autorizzazione esplicita.

Obiettivo: predisporre una nuova area T1 per la gerarchia statica, separata dal
primo pilota. Componenti: preparatore dell'harness congelato, commit sorgente,
area agente e risultati. Criteri: commit e task corretti, file corrispondenti al
manifest, sessione e test agente vuoti, nessun avvio della CLI.

## Preparazione e verifiche

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' prepare-run `
  --repository 'C:\repos\AgenticOrchestrationBenchmark' `
  --commit aee8d1162bb496895990a00095cc14c2651ff752 `
  --task T1 --runs-root 'C:\AgenticAIRuns' --results-root 'C:\AgenticAIResults'
```

Exit code 0; `runId` `20261118T174844Z-c22ba9c4`, task T1 e commit richiesto.
Area: `C:\AgenticAIRuns\20261118T174844Z-c22ba9c4`; risultati:
`C:\AgenticAIResults\20261118T174844Z-c22ba9c4`. Le directory scrivibili
dichiarate sono `workspace` e `agent-tests`.

Confronto tramite dimensione e SHA-256 con `manifest.json`: 15 file previsti e
presenti, zero differenze o file aggiuntivi; sessione e `agent-tests` vuoti.
Hash SHA-256:

- `manifest.json`: `3e44829ec72e17c82461cea30b16132c88d11f6275c578b38ca6085dd788adc6`;
- `prepared-run.json`: `d21880b686165e209e3e2d8071e188738a32e8a8b7b92e099ee4409e8a23e308`.

Questa verifica riguarda l'area preparata e non dimostra l'isolamento completo.

## Comando predisposto ed eseguito

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-static-hierarchy `
  --prepared-run 'C:\AgenticAIResults\20261118T174844Z-c22ba9c4\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 2700 --token-budget 3000000 `
  --run-kind pilot --repetition 1 --order-seed d9dc66a --order-position 2
```

Il comando è stato eseguito dopo l'autorizzazione esplicita dell'utente e i
controlli pre-run. Nessun'altra run viene autorizzata da questo documento.

## Controlli pre-run ed esito

Prima dell'avvio: area 15/15, harness 16/16, toolchain 1148/1148 e baseline
19/19 conformi ai manifest; CLI, evaluator, manifest pubblici e riservati e
schema conformi alle identità congelate. La verifica degli identificatori dei
test nei manifest pubblico e riservato è terminata con exit code 0.

Il comando del pilota è terminato con exit code 0 e stato `completed`.

- `agentTaskDurationMs`: 81767; `totalTokens`: 198857, con misurazione completa.
- Input non in cache: 40; cache read: 143734; cache creation: 43062; output:
  12021. Per tutti e quattro gli agenti l'uso dello stream coincide con quello
  autorevole; 20 messaggi LLM registrati complessivamente.
- Deleghe richieste, avviate e completate: 3/3/3; fallite e rifiutate: 0/0;
  massimo worker contemporanei: 1.
- Tutti gli agenti hanno exit code 0, zero violazioni di configurazione, errori
  strumento o righe malformate; zero differenze d'integrità protetta.
- Cinque file runtime confrontati con gli hash nel record: 5/5 conformi.
  Stderr non vuoti: 0; sessione vuota; nessun processo ancora attivo con
  percorso dell'harness o della CLI congelati. Audit: due typecheck, due build,
  due test pubblici e un test agente, tutti completati con exit code 0.

L'evaluator al commit congelato ha restituito `passed`, exit code 0: build
superata, test pubblici 31/31 e riservati 3/3; nessun test fallito, saltato,
mancante o inatteso. Input originale, copia archiviata e manifest delle suite
integri; diagnostica vuota. Durata della valutazione: 3165 ms.

- `run-record.json`, SHA-256:
  `841ef153cb4b749a4b337a3af0b5931c05c2d826a222b56516da44316421426b`;
- `evaluation-summary.json`, SHA-256:
  `b316c1be48ff33e742cde9d1b6c6d97efe0275a85cfd42cb02c1c7892be4e392`.

Il pilota è valido per la calibrazione B25 sulla base delle verifiche riportate.
Restano quattro slot; i limiti ufficiali non sono ancora derivabili. Le
evidenze riservate della valutazione restano nell'area fidata dell'evaluator.
