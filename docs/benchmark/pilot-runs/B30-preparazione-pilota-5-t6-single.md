# B30 — Quinto pilota T6 single-agent

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 5 del protocollo B25.
- **Stato:** pilota eseguito e valutato dopo autorizzazione esplicita.

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

## Comando predisposto ed eseguito

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

Il comando è stato eseguito dopo l'autorizzazione esplicita dell'utente e i
controlli pre-run. Nessun'altra run viene autorizzata da questo documento.

## Controlli pre-run ed esito

Prima dell'avvio: area 15/15, harness 16/16, toolchain 1148/1148 e baseline
19/19 conformi ai manifest; CLI, evaluator, manifest pubblici e riservati e
schema conformi alle identità congelate. Le verifiche degli identificatori dei
test nei manifest pubblico e riservato sono terminate con exit code 0.

Il pilota è terminato con exit code 0 e stato `completed`.

- `agentTaskDurationMs`: 33311; `totalTokens`: 58905, misurazione completa.
- Input non in cache: 10; cache read: 39288; cache creation: 13845; output:
  5762. Uso dello stream coerente con quello autorevole; cinque messaggi LLM.
- Violazioni di configurazione, differenze d'integrità protetta, errori
  strumento e righe malformate: zero.
- Tre file runtime confrontati con gli hash nel record: 3/3 conformi. Stderr e
  sessione vuoti; nessun processo ancora attivo con percorso dell'harness o
  della CLI congelati. Audit: un typecheck e un test pubblico, entrambi
  completati con exit code 0.

L'evaluator al commit congelato ha restituito `passed`, exit code 0: build
superata, test pubblici 26/26 e riservati 4/4; nessun test fallito, saltato,
mancante o inatteso. Input originale, copia archiviata e manifest delle suite
integri; diagnostica vuota. Durata della valutazione: 2812 ms.

- `session-record.json`, SHA-256:
  `a03a39dcb3d0c75990b7bda7ae9425b0acc3551c0a3eaf9bd2d456aed35b093c`;
- `evaluation-summary.json`, SHA-256:
  `9c7d89dac2db0fab00a07a9d26d6eea811780347e4c217a02eba028e4cba9c7f`.

Il pilota è valido per la calibrazione B25 sulla base delle verifiche riportate.
Resta uno slot T6; i limiti ufficiali non sono ancora derivabili. Le evidenze
riservate della valutazione restano nell'area fidata dell'evaluator.
