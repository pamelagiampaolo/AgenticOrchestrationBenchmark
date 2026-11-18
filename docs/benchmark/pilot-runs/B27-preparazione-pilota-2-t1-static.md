# B27 — Preparazione del secondo pilota T1 statico

## Stato e obiettivo

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 2 del protocollo B25.
- **Stato:** area preparata; pilota non avviato; nessuna chiamata LLM.

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

## Comando previsto, non eseguito

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

Prima dell'avvio verificare gli input congelati e l'area secondo B25, e ottenere
l'autorizzazione esplicita alle chiamate LLM. Dopo la run verificare record,
metriche, integrità e residui ed eseguire la valutazione indipendente. Nessuna
run viene autorizzata da questo documento.
