# B30 — Preparazione del quinto pilota T6 single-agent

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 5 del protocollo B25.
- **Stato:** area preparata; pilota non avviato; nessuna chiamata LLM.

Preparare un'area T6 nuova per single-agent, separata dai piloti già eseguiti.
Componenti: preparatore congelato, commit sorgente, area agente e risultati.
Criteri: commit e task corretti, file conformi al manifest, sessione e test
agente vuoti, nessun avvio della CLI.

## Preparazione e verifiche

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' prepare-run `
  --repository 'C:\repos\AgenticOrchestrationBenchmark' `
  --commit aee8d1162bb496895990a00095cc14c2651ff752 `
  --task T6 --runs-root 'C:\AgenticAIRuns' --results-root 'C:\AgenticAIResults'
```

Exit code 0; `runId` `20261118T190522Z-ebd7cd95`, task T6 e commit richiesto.
Area: `C:\AgenticAIRuns\20261118T190522Z-ebd7cd95`; risultati:
`C:\AgenticAIResults\20261118T190522Z-ebd7cd95`. Directory scrivibili
dichiarate: `workspace` e `agent-tests`.

Confronto dimensioni e SHA-256 con il manifest: 15 file previsti e presenti,
zero differenze o file aggiuntivi; sessione e `agent-tests` vuoti. SHA-256:

- `manifest.json`: `3d5b6f1d232ecefef83a5dcb31d5e52f9af10fa30795e76dc37f2158e1023efc`;
- `prepared-run.json`: `e1a07ad8a0fd53a0f02aaf9c77229b0902b0568c56aed898626e2d67aef090cb`.

Questa verifica riguarda l'area preparata e non dimostra l'isolamento completo.

## Comando previsto, non eseguito

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-single-agent `
  --prepared-run 'C:\AgenticAIResults\20261118T190522Z-ebd7cd95\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 2700 --token-budget 3000000 `
  --run-kind pilot --repetition 1 --order-seed d9dc66a --order-position 5
```

Prima dell'avvio verificare gli input congelati e l'area secondo B25 e ottenere
l'autorizzazione esplicita alle chiamate LLM. Dopo la run verificare record,
metriche, integrità e residui ed eseguire la valutazione indipendente. Nessuna
run viene autorizzata da questo documento.
