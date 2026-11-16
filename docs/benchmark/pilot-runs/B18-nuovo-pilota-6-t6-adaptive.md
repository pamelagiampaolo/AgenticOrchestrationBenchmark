# B18 — Nuovo pilota 6: T6 gerarchia adattiva

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6 dopo il fix B12.
- **Ordine:** sesto e ultimo pilota del nuovo freeze.
- **Run ID:** `20261110T164117Z-f4ac9e29`.
- **Task:** T6.
- **Architettura:** `hierarchical-adaptive`.
- **Commit sorgente e harness:**
  `2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`.

## Parametri ed esito

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- limite massimo di deleghe: 3;
- stato: `completed`;
- `agentTaskDurationMs`: 31.739;
- `totalTokens`: 58.219;
- exit code del coordinatore: 0;
- deleghe richieste/avviate/completate: 0/0/0;
- deleghe fallite o rifiutate: 0;
- parallelismo massimo dei worker: 0.

L'uso autorevole è completo e coincide con quello ricostruito dallo stream:
52.732 token di input complessivi e 5.487 token di output. L'intero consumo è
del coordinatore.

Il coordinatore ha scelto autonomamente di non delegare. Questo comportamento è
ammesso dalla condizione adattiva: il valore 3 è un limite massimo, non un numero
obbligatorio di deleghe. La scelta è quindi un risultato sperimentale e non una
causa di invalidazione o ripetizione.

## Verifiche dell'harness

- `MCP_TASK_ID=T6` presente nelle configurazioni del coordinatore e dei worker;
- coordinatore con entrambi i server MCP connessi e strumenti attesi;
- violazioni di configurazione, errori strumento e righe malformate: 0;
- typecheck completato con exit code 0;
- test pubblici completati con exit code 0: 32 riportati, 26 superati, 0
  falliti e 6 pending;
- i 6 pending sono i test comuni non applicabili a T6, esclusi dal filtro
  canonico introdotto da B12; non rappresentano fallimenti;
- test aggiuntivi creati dall'agente: 0;
- differenze nei materiali protetti: 0;
- stderr non vuoti: 0;
- processi residui dell'orchestratore o del server MCP: 0.

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
- durata della valutazione: 2.705 ms.

## Evidenze e allowlist completa

- harness:
  `C:\AgenticAIResults\20261110T164117Z-f4ac9e29\run-record.json`;
- workspace: `C:\AgenticAIRuns\20261110T164117Z-f4ac9e29`;
- evaluator:
  `C:\repos\TesiAgenticAI-TrustedEvaluator\runs\20261110T164117Z-f4ac9e29\evidence\evaluation-summary.json`.

| File | SHA-256 |
| --- | --- |
| `run-record.json` | `5159463b9ebea6d94203eed3a06b72f0a280cd5e13222d565fbdcc2e74570ad2` |
| `evaluation-summary.json` | `255e1cef3e24c62f25bad68a605ea884de9344dd1aa7ed14fa5fac7599f73d58` |

La allowlist completa dei sei piloti validi contiene esclusivamente:

1. `20261110T161912Z-359958f0` — T1 adattiva;
2. `20261110T162200Z-914660f7` — T1 statica;
3. `20261110T162624Z-2e6886a2` — T1 single-agent;
4. `20261110T162926Z-e66b80f6` — T6 statica;
5. `20261110T163437Z-dc161db1` — T6 single-agent;
6. `20261110T164117Z-f4ac9e29` — T6 adattiva.

I sei slot previsti da B6 sono completi. Il passo successivo, non eseguito in
questa iterazione, è aggregare i consumi validi e applicare le regole di
arrotondamento approvate per congelare i limiti delle run ufficiali.
