# B16 — Nuovo pilota 4: T6 gerarchia statica

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6 dopo il fix B12.
- **Ordine:** quarto dei sei piloti del nuovo freeze.
- **Run ID:** `20261110T162926Z-e66b80f6`.
- **Task:** T6.
- **Architettura:** `hierarchical-static`.
- **Commit sorgente e harness:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

## Parametri ed esito

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- stato: `completed`;
- `agentTaskDurationMs`: 103.647;
- `totalTokens`: 186.811;
- exit code: 0;
- deleghe richieste/avviate/completate: 3/3/3;
- deleghe fallite o rifiutate: 0;
- parallelismo massimo dei worker: 1.

L'uso autorevole è completo. I token per ruolo sono 48.063 per il
coordinatore, 53.574 per l'analista, 54.787 per l'implementatore e 30.387 per
il verificatore. L'agente non ha creato test aggiuntivi.

## Verifiche dell'harness

- `MCP_TASK_ID=T6` presente nella configurazione registrata;
- tutti i quattro agenti terminati con exit code 0;
- violazioni di configurazione, errori strumento e righe malformate: 0;
- typecheck, build e test pubblici eseguiti due volte, sempre con exit code 0;
- in entrambe le esecuzioni pubbliche: 32 test riportati, 26 superati, 0
  falliti e 6 pending;
- i 6 pending sono i test comuni non applicabili a T6, esclusi dal filtro
  canonico introdotto da B12; non rappresentano fallimenti;
- differenze nei materiali protetti: 0;
- stderr non vuoti, sessioni residue e sequenze UTF-8 non valide: 0;
- processi residui dell'orchestratore o del server MCP: 0.

Questa è la prima accettazione in un pilota del fix B12 sul task T6: il runner
pubblico ha selezionato i 21 test comuni compatibili e i 5 test specifici T6,
senza includere come fallimenti i 6 test comuni non applicabili.

## Valutazione indipendente

Il valutatore fidato, versione
`eb573d5a53985abe0bc809299c64a35f300443fd`, ha prodotto outcome `passed` ed
exit code 0:

- build superata;
- test pubblici attesi/eseguiti/superati: 26/26/26;
- test pubblici falliti, saltati, mancanti o inattesi: 0;
- test riservati: 4/4;
- input originale, archivio e manifest delle suite integri;
- diagnostica vuota;
- durata della valutazione: 2.672 ms.

## Evidenze e nuova allowlist

- harness:
  `C:\AgenticAIResults\20261110T162926Z-e66b80f6\run-record.json`;
- workspace: `C:\AgenticAIRuns\20261110T162926Z-e66b80f6`;
- evaluator:
  `C:\repos\TesiAgenticAI-TrustedEvaluator\runs\20261110T162926Z-e66b80f6\evidence\evaluation-summary.json`.

| File | SHA-256 |
| --- | --- |
| `run-record.json` | `ea77a33cda9023c5780b12a59878319c08ef95b7d464a81123e8107f76afc4cf` |
| `evaluation-summary.json` | `89a042a62d49b33d3af8afbc770e6a12a77cd05c8ba49f93575882d90cc42d98` |

La nuova allowlist contiene esclusivamente:

1. `20261110T161912Z-359958f0` — T1 adattiva;
2. `20261110T162200Z-914660f7` — T1 statica;
3. `20261110T162624Z-2e6886a2` — T1 single-agent;
4. `20261110T162926Z-e66b80f6` — T6 statica.

Restano T6 single-agent e T6 adattiva prima della calibrazione definitiva.
