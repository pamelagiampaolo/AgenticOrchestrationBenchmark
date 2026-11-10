# B14 — Nuovo pilota 2: T1 con gerarchia statica

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6 dopo il fix B12.
- **Ordine:** secondo dei sei piloti del nuovo freeze.
- **Run ID:** `20261110T162200Z-914660f7`.
- **Task:** T1.
- **Architettura:** `hierarchical-static`.
- **Commit sorgente e harness:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

## Parametri ed esito

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- stato: `completed`;
- `agentTaskDurationMs`: 92.817;
- `totalTokens`: 190.293;
- massimo lavoratori concorrenti: 1;
- deleghe richieste/avviate/completate: 3/3/3;
- deleghe fallite o rifiutate: 0.

Uso per ruolo: coordinator 38.008 token, analyst 53.126, implementer 46.468 e
verifier 52.691. Totali: 38 input non in cache, 140.711 cache read, 36.025 cache
creation e 13.519 output. L'uso autorevole è completo per tutti gli agenti.

## Verifiche dell'harness

- `MCP_TASK_ID=T1` presente nella configurazione registrata;
- quattro agenti terminati con exit code 0;
- violazioni, errori strumento e righe malformate: 0;
- typecheck, build e test pubblici eseguiti due volte con exit code 0;
- test aggiuntivi creati dal verifier e superati;
- differenze nei materiali protetti: 0;
- stderr vuoti, UTF-8 valido e sessione vuota;
- processi residui dell'orchestratore o del server MCP: 0.

## Valutazione indipendente

Il valutatore fidato ha prodotto outcome `passed` ed exit code 0:

- build superata;
- test pubblici: 31/31;
- test riservati: 3/3;
- test mancanti, inattesi o saltati: 0;
- input originale, archivio e manifest delle suite integri;
- diagnostica vuota;
- durata della valutazione: 3.015 ms.

## Evidenze e nuova allowlist

- harness: `C:\AgenticAIResults\20261110T162200Z-914660f7`;
- workspace: `C:\AgenticAIRuns\20261110T162200Z-914660f7`;
- evaluator:
  `C:\repos\TesiAgenticAI-TrustedEvaluator\runs\20261110T162200Z-914660f7`.

| File | SHA-256 |
| --- | --- |
| `run-record.json` | `09cdc4c4a09357b0fe8b707132bd481a4d20c6573b0c6373e7815ffbaa283933` |
| `evaluation-summary.json` | `398eb6a9f6076814477b312fd34eaa4166ba2b8ea80810da012ada5da561662c` |

La nuova allowlist contiene esclusivamente:

1. `20261110T161912Z-359958f0` — T1 adattiva;
2. `20261110T162200Z-914660f7` — T1 statica.

Restano quattro slot da eseguire prima della calibrazione.
