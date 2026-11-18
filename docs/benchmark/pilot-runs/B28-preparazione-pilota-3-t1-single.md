# B28 — Preparazione del terzo pilota T1 single-agent

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 3 del protocollo B25.
- **Stato:** area preparata; pilota non avviato; nessuna chiamata LLM.

Preparare un'area T1 nuova per single-agent, separata dai primi due piloti.
Componenti: preparatore congelato, commit sorgente, area agente e risultati.
Criteri: commit e task corretti, file conformi al manifest, sessione e test
agente vuoti, nessun avvio della CLI.

## Preparazione e verifiche

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' prepare-run `
  --repository 'C:\repos\AgenticOrchestrationBenchmark' `
  --commit aee8d1162bb496895990a00095cc14c2651ff752 `
  --task T1 --runs-root 'C:\AgenticAIRuns' --results-root 'C:\AgenticAIResults'
```

Exit code 0; `runId` `20261118T181009Z-260b2323`, task T1 e commit richiesto.
Area: `C:\AgenticAIRuns\20261118T181009Z-260b2323`; risultati:
`C:\AgenticAIResults\20261118T181009Z-260b2323`. Directory scrivibili
dichiarate: `workspace` e `agent-tests`.

Confronto dimensioni e SHA-256 con il manifest: 15 file previsti e presenti,
zero differenze o file aggiuntivi; sessione e `agent-tests` vuoti. SHA-256:

- `manifest.json`: `52c29457ad7acee345cac68dd77d32a98fe88284cfafea4584148835a2c60900`;
- `prepared-run.json`: `15ba08acd81f35769228b7d1a5bdedf9a905626433ac8b23420f889f7ed38c6e`.

Questa verifica riguarda l'area preparata e non dimostra l'isolamento completo.

## Comando previsto, non eseguito

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-single-agent `
  --prepared-run 'C:\AgenticAIResults\20261118T181009Z-260b2323\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 2700 --token-budget 3000000 `
  --run-kind pilot --repetition 1 --order-seed d9dc66a --order-position 3
```

Prima dell'avvio verificare gli input congelati e l'area secondo B25 e ottenere
l'autorizzazione esplicita alle chiamate LLM. Dopo la run verificare record,
metriche, integrità e residui ed eseguire la valutazione indipendente. Nessuna
run viene autorizzata da questo documento.
