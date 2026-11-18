# B29 — Quarto pilota T6 statico

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dello slot 4 del protocollo B25.
- **Stato:** pilota eseguito e valutato dopo autorizzazione esplicita.

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

## Comando predisposto ed eseguito

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

Il comando è stato eseguito dopo l'autorizzazione esplicita dell'utente e i
controlli pre-run. Nessun'altra run viene autorizzata da questo documento.

## Controlli pre-run ed esito

Prima dell'avvio: area 15/15, harness 16/16, toolchain 1148/1148 e baseline
19/19 conformi ai manifest; CLI, evaluator, manifest pubblici e riservati e
schema conformi alle identità congelate. Le verifiche degli identificatori dei
test nei manifest pubblico e riservato sono terminate con exit code 0.

Il pilota è terminato con exit code 0 e stato `completed`.

- `agentTaskDurationMs`: 118531; `totalTokens`: 352308, misurazione completa.
- Input non in cache: 48; cache read: 270568; cache creation: 63173; output:
  18519. Per tutti e quattro gli agenti l'uso dello stream coincide con quello
  autorevole; 24 messaggi LLM registrati complessivamente.
- Deleghe richieste, avviate e completate: 3/3/3; fallite e rifiutate: 0/0;
  massimo worker contemporanei: 1.
- Tutti gli agenti hanno exit code 0, zero violazioni di configurazione e
  righe malformate; zero differenze d'integrità protetta.
- Un'invocazione `agent-tests` dell'implementer è fallita con exit code 1 e
  una suite fallita; due invocazioni successive sono state completate con
  exit code 0. Il record conta un errore strumento; la causa precisa della suite
  fallita non è stabilita. L'audit registra inoltre due typecheck, due build e
  due test pubblici completati con exit code 0.
- Cinque file runtime confrontati con gli hash nel record: 5/5 conformi.
  Stderr non vuoti: 0; sessione vuota; nessun processo ancora attivo con
  percorso dell'harness o della CLI congelati.

L'evaluator al commit congelato ha restituito `passed`, exit code 0: build
superata, test pubblici 26/26 e riservati 4/4; nessun test fallito, saltato,
mancante o inatteso. Input originale, copia archiviata e manifest delle suite
integri; diagnostica vuota. Durata della valutazione: 2765 ms.

- `run-record.json`, SHA-256:
  `1963f1f3a4b69ca696811a23c7294d428f83c72789df3d76f54e7b25fcbd7171`;
- `evaluation-summary.json`, SHA-256:
  `7dc062d26e91b635ad8940c38bcd761a44f33f5f34876b5f5423db0eaaf35dea`.

Sulla base delle verifiche riportate il pilota è valido per la calibrazione B25;
il fallimento di un test agente è mantenuto come osservazione della run e non
costituisce, da solo, evidenza di un guasto infrastrutturale. Restano due slot;
i limiti ufficiali non sono ancora derivabili. Le evidenze riservate della
valutazione restano nell'area fidata dell'evaluator.
