# B13 — Nuovo pilota 1: T1 con gerarchia adattiva

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6 dopo il fix B12.
- **Ordine:** primo dei sei piloti del nuovo freeze.
- **Run ID:** `20261110T161912Z-359958f0`.
- **Task:** T1.
- **Architettura:** `hierarchical-adaptive`.
- **Commit sorgente e harness:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

I piloti e tentativi precedenti al fix B12 restano evidenze storiche e non
contribuiscono alla nuova calibrazione.

## Parametri ed esito

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- massimo deleghe: 3;
- stato: `completed`;
- `agentTaskDurationMs`: 24.974;
- `totalTokens`: 38.216;
- deleghe richieste/avviate/completate: 0/0/0.

Uso osservato: 8 token input non in cache, 22.676 cache read, 11.092 cache
creation e 4.440 output. L'uso autorevole è completo.

## Verifiche dell'harness

- `MCP_TASK_ID=T1` presente nella configurazione registrata;
- modello, server e strumenti conformi;
- violazioni, errori strumento e righe malformate: 0;
- typecheck e test pubblici completati con exit code 0;
- differenze nei materiali protetti: 0;
- stderr vuoto e UTF-8 valido;
- directory di sessione vuota;
- processi residui dell'orchestratore o del server MCP: 0.

La scelta di non delegare è ammessa per la gerarchia adattiva.

## Valutazione indipendente

Il valutatore fidato ha prodotto outcome `passed` ed exit code 0:

- build superata;
- test pubblici: 31/31;
- test riservati: 3/3;
- test mancanti, inattesi o saltati: 0;
- input originale, archivio e manifest delle suite integri;
- diagnostica vuota;
- durata della valutazione: 3.068 ms.

## Evidenze e nuova allowlist

- harness: `C:\AgenticAIResults\20261110T161912Z-359958f0`;
- workspace: `C:\AgenticAIRuns\20261110T161912Z-359958f0`;
- evaluator:
  `<trusted-evaluator-root>\runs\20261110T161912Z-359958f0`.

| File | SHA-256 |
| --- | --- |
| `run-record.json` | `7b928e0fe6cc3a3672a81c87c2753f07c467145815f454b0bf8aee8de3cfc1fc` |
| `evaluation-summary.json` | `bd4c70c419dcc7a07f5a77eaa676abc385ec348f39b46922c649792aeeb7a19e` |

La nuova allowlist contiene esclusivamente:

1. `20261110T161912Z-359958f0` — T1 adattiva.

Restano cinque slot da eseguire prima della calibrazione.
