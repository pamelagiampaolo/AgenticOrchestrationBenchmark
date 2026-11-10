# B9 — Pilota 3: T1 single-agent

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6.
- **Ordine:** terzo dei sei piloti congelati.
- **Run ID:** `20261110T152614Z-c438ecc5`.
- **Task:** T1.
- **Architettura:** `single-agent`.
- **Commit sorgente:** `748e09f6b3d315027e554dd2af7035ad0fc78a96`.

Il pilota usa la condizione approvata con test pubblici specifici del task. La
condizione senza tali test verrà progettata separatamente.

## Parametri ed esito del runner

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- stato: `completed`;
- `agentTaskDurationMs`: 25.522;
- `totalTokens`: 30.980;
- exit code del processo: 0.

Uso osservato:

| Categoria | Token |
| --- | ---: |
| input non in cache | 6 |
| cache read | 13.635 |
| cache creation | 12.929 |
| output | 4.410 |
| totale | 30.980 |

L'uso autorevole è completo.

## Verifiche dell'harness

- modello richiesto e osservato coincidenti;
- configurazione MCP conforme;
- violazioni di configurazione: 0;
- errori strumento: 0;
- righe stream malformate: 0;
- stderr vuoto;
- differenze nei materiali protetti: 0;
- file UTF-8 non validi: 0;
- directory di sessione vuota al termine;
- processi residui dell'orchestratore o del server MCP: 0.

L'audit contiene typecheck e test pubblici completati con exit code 0. L'agente
non ha creato test aggiuntivi.

## Valutazione indipendente

Il valutatore fidato, commit
`eb573d5a53985abe0bc809299c64a35f300443fd`, ha prodotto outcome `passed` ed
exit code 0:

- build superata;
- test pubblici: 31 attesi, 31 eseguiti, 31 superati;
- test riservati: 3 attesi, 3 eseguiti, 3 superati;
- test mancanti, inattesi o saltati: 0;
- input originale e submission archiviata invariati;
- manifest delle suite verificati;
- diagnostica vuota;
- durata della valutazione: 5.951 ms.

La baseline canonica e la toolchain sono le stesse verificate per B7 e B8. La
copia di lavoro temporanea dell'evaluator è stata rimossa al termine.

## Evidenze e allowlist

Evidenze dell'harness:
`C:\AgenticAIResults\20261110T152614Z-c438ecc5`.

Workspace congelata:
`C:\AgenticAIRuns\20261110T152614Z-c438ecc5`.

Evidenze private dell'evaluator:
`C:\repos\TesiAgenticAI-TrustedEvaluator\runs\20261110T152614Z-c438ecc5`.

Hash dei riepiloghi principali:

| File | SHA-256 |
| --- | --- |
| `session-record.json` | `c30614bcaa858282f43be3cedd6012647c90f08742ae576962161315a5b8005b` |
| `evaluation-summary.json` | `9f82e1635f924169393728514ff7b1531dc63a9138b79c495d8bfa91f579668c` |

Dopo questo passo l'allowlist dei piloti contiene esattamente:

1. `20261109T195256Z-9fc50de5` — T1 adattiva;
2. `20261109T203833Z-bdd6ac20` — T1 statica;
3. `20261110T152614Z-c438ecc5` — T1 single-agent.

Le altre directory presenti nelle root operative sono prove tecniche storiche e
non fanno parte dei piloti B6.

Questa prova certifica il solo slot T1 single-agent. I tre piloti T1 sono ora
completi, ma i limiti ufficiali non possono essere derivati prima dei tre slot
T6 rimanenti.
