# B29 — Preparazione del quarto pilota T6 statico

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 4 del protocollo B25.
- **Stato:** area preparata; pilota non avviato; nessuna chiamata LLM.

Preparare un'area T6 nuova per la gerarchia statica, separata dai piloti già
eseguiti. Componenti: preparatore congelato, commit sorgente, area agente e
risultati. Criteri: commit e task corretti, file conformi al manifest, sessione
e test agente vuoti, nessun avvio della CLI.

## Preparazione e verifiche

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' prepare-run `
  --repository 'C:\repos\AgenticOrchestrationBenchmark' `
  --commit aee8d1162bb496895990a00095cc14c2651ff752 `
  --task T6 --runs-root 'C:\AgenticAIRuns' --results-root 'C:\AgenticAIResults'
```

Exit code 0; `runId` `20261118T185448Z-52ec0b68`, task T6 e commit richiesto.
Area: `C:\AgenticAIRuns\20261118T185448Z-52ec0b68`; risultati:
`C:\AgenticAIResults\20261118T185448Z-52ec0b68`. Directory scrivibili
dichiarate: `workspace` e `agent-tests`.

Confronto dimensioni e SHA-256 con il manifest: 15 file previsti e presenti,
zero differenze o file aggiuntivi; sessione e `agent-tests` vuoti. SHA-256:

- `manifest.json`: `bcb2c9729b157f8161e1af1918b311849f780d20bfa63042a99172ebcf4054f4`;
- `prepared-run.json`: `94418a5b06cf76116973ce51709a228f7b265d39d503531cbd05aeb1144da568`.

Questa verifica riguarda l'area preparata e non dimostra l'isolamento completo.

## Comando previsto, non eseguito

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-static-hierarchy `
  --prepared-run 'C:\AgenticAIResults\20261118T185448Z-52ec0b68\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 2700 --token-budget 3000000 `
  --run-kind pilot --repetition 1 --order-seed d9dc66a --order-position 4
```

Prima dell'avvio verificare gli input congelati e l'area secondo B25 e ottenere
l'autorizzazione esplicita alle chiamate LLM. Dopo la run verificare record,
metriche, integrità e residui ed eseguire la valutazione indipendente. Nessuna
run viene autorizzata da questo documento.
