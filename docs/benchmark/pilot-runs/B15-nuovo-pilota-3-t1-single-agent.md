# B15 — Nuovo pilota 3: T1 single-agent

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6 dopo il fix B12.
- **Ordine:** terzo dei sei piloti del nuovo freeze.
- **Run ID:** `20261110T162624Z-2e6886a2`.
- **Task:** T1.
- **Architettura:** `single-agent`.
- **Commit sorgente e harness:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

## Parametri ed esito

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- stato: `completed`;
- `agentTaskDurationMs`: 25.614;
- `totalTokens`: 36.916;
- exit code: 0.

L'uso autorevole è completo. L'agente non ha creato test aggiuntivi.

## Verifiche dell'harness

- `MCP_TASK_ID=T1` presente nella configurazione registrata;
- violazioni, errori strumento e righe malformate: 0;
- typecheck e test pubblici completati con exit code 0;
- differenze nei materiali protetti: 0;
- stderr vuoto, UTF-8 valido e sessione vuota;
- processi residui dell'orchestratore o del server MCP: 0.

## Valutazione indipendente

Il valutatore fidato ha prodotto outcome `passed` ed exit code 0:

- build superata;
- test pubblici: 31/31;
- test riservati: 3/3;
- test mancanti, inattesi o saltati: 0;
- input originale, archivio e manifest delle suite integri;
- diagnostica vuota;
- durata della valutazione: 3.076 ms.

## Evidenze e nuova allowlist

- harness: `C:\AgenticAIResults\20261110T162624Z-2e6886a2`;
- workspace: `C:\AgenticAIRuns\20261110T162624Z-2e6886a2`;
- evaluator:
  `<trusted-evaluator-root>\runs\20261110T162624Z-2e6886a2`.

| File | SHA-256 |
| --- | --- |
| `session-record.json` | `fb72bc804fdd6856a5bda48a5f617415eba484baafd787424bcc6e674922ca2c` |
| `evaluation-summary.json` | `b8597ba0af7818fe10e1b4ea3f2b83ec2217f0e7c811bcd58350554988abdfb0` |

La nuova allowlist contiene esclusivamente:

1. `20261110T161912Z-359958f0` — T1 adattiva;
2. `20261110T162200Z-914660f7` — T1 statica;
3. `20261110T162624Z-2e6886a2` — T1 single-agent.

I tre slot T1 sono completi. Restano i tre slot T6 prima della calibrazione.
