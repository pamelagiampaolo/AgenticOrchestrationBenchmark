# B26 — Preparazione del primo pilota T1 adattivo

## Stato e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione del primo pilota B25.
- **Stato:** area preparata e controllata; run non avviata.
- **Chiamate LLM:** nessuna; l'avvio richiede autorizzazione esplicita.

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

## Avvio previsto, non eseguito

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

Prima dell'avvio restano da ripetere i controlli pre-run previsti da B25,
inclusa l'integrità dell'area preparata, degli input congelati e dei materiali
riservati. Dopo la run restano valutazione indipendente, controlli di integrità,
metriche e residui. Questo documento non autorizza chiamate LLM o altri piloti.
