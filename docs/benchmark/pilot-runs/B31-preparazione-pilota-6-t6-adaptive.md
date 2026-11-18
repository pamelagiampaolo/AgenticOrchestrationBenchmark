# B31 — Sesto pilota T6 adattivo

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 6 del protocollo B25.
- **Stato:** pilota eseguito e valutato dopo autorizzazione esplicita.

Preparare un'area T6 nuova per la gerarchia adattiva, separata dai piloti già
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

Exit code 0; `runId` `20261118T191206Z-104ed5e9`, task T6 e commit richiesto.
Area: `C:\AgenticAIRuns\20261118T191206Z-104ed5e9`; risultati:
`C:\AgenticAIResults\20261118T191206Z-104ed5e9`. Directory scrivibili
dichiarate: `workspace` e `agent-tests`.

Confronto dimensioni e SHA-256 con il manifest: 15 file previsti e presenti,
zero differenze o file aggiuntivi; sessione e `agent-tests` vuoti. SHA-256:

- `manifest.json`: `740304dceaed92729c755737b1488ce7f5e57aa2a8035f0d68e9771783f155f4`;
- `prepared-run.json`: `ebe1a1f3f03cbeb227f01699c68cb669f28fdd38158b9fe8c673bfe4101eb291`.

Questa verifica riguarda l'area preparata e non dimostra l'isolamento completo.

## Comando predisposto ed eseguito

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-adaptive-hierarchy `
  --prepared-run 'C:\AgenticAIResults\20261118T191206Z-104ed5e9\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 2700 --token-budget 3000000 `
  --max-delegations 10 --run-kind pilot --repetition 1 --order-seed d9dc66a --order-position 6
```

Il comando è stato eseguito dopo l'autorizzazione esplicita dell'utente e i
controlli pre-run. Nessun'altra run viene autorizzata da questo documento.

## Controlli pre-run ed esito

Prima dell'avvio: area 15/15, harness 16/16, toolchain 1148/1148 e baseline
19/19 conformi ai manifest; CLI, evaluator, manifest pubblici e riservati e
schema conformi alle identità congelate. Le verifiche degli identificatori dei
test nei manifest pubblico e riservato sono terminate con exit code 0.

Il pilota è terminato con exit code 0 e stato `completed`.

- `agentTaskDurationMs`: 30175; `totalTokens`: 46049, misurazione completa.
- Input non in cache: 8; cache read: 26880; cache creation: 13701; output:
  5460. Uso dello stream coerente con quello autorevole; quattro messaggi LLM.
- Deleghe richieste, avviate e completate: 0/0/0. La scelta di non delegare è
  ammessa dalla condizione adattiva.
- Violazioni di configurazione, differenze d'integrità protetta, errori
  strumento e righe malformate: zero.
- Cinque file runtime confrontati con gli hash nel record: 5/5 conformi. Stderr
  e sessione vuoti; nessun processo ancora attivo con percorso dell'harness o
  della CLI congelati. Audit: un typecheck e un test pubblico, entrambi
  completati con exit code 0.

L'evaluator al commit congelato ha restituito `passed`, exit code 0: build
superata, test pubblici 26/26 e riservati 4/4; nessun test fallito, saltato,
mancante o inatteso. Input originale, copia archiviata e manifest delle suite
integri; diagnostica vuota. Durata della valutazione: 2792 ms.

- `run-record.json`, SHA-256:
  `6d7e2a38c285186e508907c7938fe009382a89fd31d0b4014276dca2b384b74e`;
- `evaluation-summary.json`, SHA-256:
  `e15e76f9d97d1f3558afc20b5e2e0d9941b9958bb4d1f4c05ee94664087c2c29`.

Il pilota è valido per la calibrazione B25 sulla base delle verifiche riportate.
Sono disponibili sei piloti validi; calcolo e approvazione dei limiti ufficiali
restano un passo separato. Le evidenze riservate restano nell'area fidata
dell'evaluator.
