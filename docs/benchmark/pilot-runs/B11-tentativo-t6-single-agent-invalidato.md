# B11 — Tentativo T6 single-agent invalidato e difetto della suite pubblica

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** tentativo invalidato per configurazione errata della suite pubblica
  dell'harness; escluso dalla calibrazione B6.
- **Slot previsto:** quinto dei sei piloti congelati.
- **Run ID:** `20261110T154825Z-1276f338`.
- **Task:** T6.
- **Architettura:** `single-agent`.
- **Commit sorgente:** `748e09f6b3d315027e554dd2af7035ad0fc78a96`.

## Parametri ed esito osservato

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- stato runner: `completed`;
- `agentTaskDurationMs`: 35.244;
- `totalTokens`: 75.092;
- exit code del processo: 0.

Uso osservato: 12 token input non in cache, 55.254 cache read, 13.934 cache
creation e 5.892 output. L'uso autorevole è completo. Configurazione, integrità,
UTF-8, stderr, sessione e terminazione dei processi non presentano anomalie.

Il typecheck è passato. L'unica esecuzione della suite pubblica ha restituito
exit code 1; l'agente non ha creato test aggiuntivi.

## Valutazione indipendente

La submission congelata ha prodotto outcome `passed` ed exit code 0 nel
valutatore fidato:

- build superata;
- test pubblici applicabili a T6: 26/26;
- test riservati: 4/4;
- test mancanti, inattesi o saltati: 0;
- integrità degli input, dell'archivio e delle suite verificata;
- diagnostica vuota;
- durata della valutazione: 2.763 ms.

Non essendoci state scritture dopo il test pubblico fallito, la differenza tra
harness ed evaluator non poteva dipendere da una correzione successiva.

## Difetto infrastrutturale dimostrato

Lo stream pubblico registra 32 test totali, 26 superati e 6 falliti. I sei
fallimenti coincidono con i sei casi comuni dichiarati non applicabili a T6.

`BenchmarkTarget/vitest.public.t6.config.ts` contiene il `testNamePattern` che
seleziona i 21 casi comuni compatibili e i 5 casi T6. Il server MCP non usa però
quella configurazione: `McpReadOnlyPrototype/EphemeralPublicTestRunner.cs` genera
una configurazione generica con:

```text
include: tests/public/**/*.test.ts
```

e nessun `testNamePattern`. Di conseguenza espone ed esegue anche i sei casi non
applicabili. I test automatici correnti verificano la suite comune da 27 casi,
ma non coprono la selezione pubblica specifica di T6.

Lo stesso difetto era presente nel precedente tentativo statico
`20261110T153951Z-badf5ea7`, che viene pertanto invalidato retroattivamente.

## Evidenze e allowlist

Evidenze dell'harness:
`C:\AgenticAIResults\20261110T154825Z-1276f338`.

Workspace congelata:
`C:\AgenticAIRuns\20261110T154825Z-1276f338`.

Evidenze private dell'evaluator:
`<trusted-evaluator-root>\runs\20261110T154825Z-1276f338`.

| File | SHA-256 |
| --- | --- |
| `session-record.json` | `11289422a403d6adac3f96ff50d53764882e1873b1fe73ef51fd12c8eec18c51` |
| `evaluation-summary.json` | `27f1771a9d7750c3b2bfc2a7330b9933201d71da10e3b59a54e01e7deec67ddf` |

L'allowlist dei piloti validi resta limitata ai tre slot T1:

1. `20261109T195256Z-9fc50de5` — T1 adattiva;
2. `20261109T203833Z-bdd6ac20` — T1 statica;
3. `20261110T152614Z-c438ecc5` — T1 single-agent.

I due tentativi T6 vengono conservati ma non contribuiscono alla calibrazione.
Prima di altre run T6 occorre correggere il runner pubblico, aggiungere una prova
mirata della selezione 26/26 e rieseguire build e suite completa dell'harness.
