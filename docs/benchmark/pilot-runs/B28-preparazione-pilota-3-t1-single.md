# B28 — Terzo pilota T1 single-agent

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 3 del protocollo B25.
- **Stato:** pilota eseguito e valutato dopo autorizzazione esplicita.

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

## Comando predisposto ed eseguito

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

Il comando è stato eseguito dopo l'autorizzazione esplicita dell'utente e i
controlli pre-run. Nessun'altra run viene autorizzata da questo documento.

## Controlli pre-run ed esito

Prima dell'avvio: area 15/15, harness 16/16, toolchain 1148/1148 e baseline
19/19 conformi ai manifest; CLI, evaluator, manifest pubblici e riservati e
schema conformi alle identità congelate. Le verifiche degli identificatori dei
test nei manifest pubblico e riservato sono terminate con exit code 0.

Il pilota è terminato con exit code 0 e stato `completed`.

- `agentTaskDurationMs`: 24963; `totalTokens`: 36298, misurazione completa.
- Input non in cache: 8; cache read: 21003; cache creation: 10826; output:
  4461. Uso dello stream coerente con quello autorevole; quattro messaggi LLM.
- Modello e server MCP dichiarati conformi; violazioni di configurazione,
  differenze d'integrità protetta, errori strumento e righe malformate: zero.
- Tre file runtime confrontati con gli hash nel record: 3/3 conformi. Stderr e
  sessione vuoti; nessun processo ancora attivo con percorso dell'harness o
  della CLI congelati. Audit: un typecheck e un test pubblico, entrambi
  completati con exit code 0.

L'evaluator al commit congelato ha restituito `passed`, exit code 0: build
superata, test pubblici 31/31 e riservati 3/3; nessun test fallito, saltato,
mancante o inatteso. Input originale, copia archiviata e manifest delle suite
integri; diagnostica vuota. Durata della valutazione: 3285 ms.

- `session-record.json`, SHA-256:
  `1fdde34575b8c7fc546a33a517b9724145e0f8d5895ac4ce447232a2a7f82a9f`;
- `evaluation-summary.json`, SHA-256:
  `31a108b1e55bb4b6fd15b10ee6200753bed53edc5ee3a06955c8c4311986eecf`.

Il pilota è valido per la calibrazione B25 sulla base delle verifiche riportate.
Restano tre slot T6; i limiti ufficiali non sono ancora derivabili. Le evidenze
riservate della valutazione restano nell'area fidata dell'evaluator.
