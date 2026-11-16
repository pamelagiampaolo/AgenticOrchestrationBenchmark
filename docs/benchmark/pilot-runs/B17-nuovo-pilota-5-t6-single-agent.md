# B17 — Nuovo pilota 5: T6 single-agent

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6 dopo il fix B12.
- **Ordine:** quinto dei sei piloti del nuovo freeze.
- **Run ID:** `20261110T163437Z-dc161db1`.
- **Task:** T6.
- **Architettura:** `single-agent`.
- **Commit sorgente e harness:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

## Parametri ed esito

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- stato: `completed`;
- `agentTaskDurationMs`: 31.941;
- `totalTokens`: 64.072;
- exit code: 0;
- turni: 12.

L'uso autorevole è completo e coincide con quello ricostruito dallo stream:
58.340 token di input complessivi e 5.732 token di output. L'agente non ha
creato test aggiuntivi.

## Verifiche dell'harness

- `MCP_TASK_ID=T6` presente nella configurazione registrata;
- violazioni di configurazione, errori strumento e righe malformate: 0;
- typecheck completato con exit code 0;
- test pubblici completati con exit code 0: 32 riportati, 26 superati, 0
  falliti e 6 pending;
- i 6 pending sono i test comuni non applicabili a T6, esclusi dal filtro
  canonico introdotto da B12; non rappresentano fallimenti;
- differenze nei materiali protetti: 0;
- stderr vuoto e stream finale completo;
- processi residui dell'orchestratore o del server MCP: 0.

Il primo tentativo di invocazione di `run-single-agent` è stato rifiutato dal
parser prima dell'avvio perché mancavano i percorsi obbligatori di Node, Vitest
e TypeScript. Nell'area erano presenti soltanto `manifest.json` e
`prepared-run.json`: non era stata creata alcuna sessione né effettuata alcuna
chiamata al modello. Il comando è stato quindi ripetuto sulla stessa area con i
tre percorsi espliciti. Questo errore operativo non costituisce una run e non ha
alterato condizioni, limiti o input del pilota registrato.

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
- durata della valutazione: 4.700 ms.

## Evidenze e nuova allowlist

- harness:
  `C:\AgenticAIResults\20261110T163437Z-dc161db1\session-record.json`;
- workspace: `C:\AgenticAIRuns\20261110T163437Z-dc161db1`;
- evaluator:
  `<trusted-evaluator-root>\runs\20261110T163437Z-dc161db1\evidence\evaluation-summary.json`.

| File | SHA-256 |
| --- | --- |
| `session-record.json` | `d76632d6909e2e0e38155e27c0d0fecd433fe88a319e0cc7bc5782d97c223e4b` |
| `evaluation-summary.json` | `f968bc2ac6d87f479148b0e5b21f42353c80e66fecec09848a6de5c518afe93b` |

La nuova allowlist contiene esclusivamente:

1. `20261110T161912Z-359958f0` — T1 adattiva;
2. `20261110T162200Z-914660f7` — T1 statica;
3. `20261110T162624Z-2e6886a2` — T1 single-agent;
4. `20261110T162926Z-e66b80f6` — T6 statica;
5. `20261110T163437Z-dc161db1` — T6 single-agent.

Resta soltanto T6 adattiva prima della calibrazione definitiva.
