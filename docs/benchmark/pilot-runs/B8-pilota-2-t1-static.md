# B8 — Pilota 2: T1 con gerarchia statica

## Classificazione

- **Data:** 9 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6.
- **Ordine:** secondo dei sei piloti congelati.
- **Run ID:** `20261109T203833Z-bdd6ac20`.
- **Task:** T1.
- **Architettura:** `hierarchical-static`.
- **Commit sorgente:** `748e09f6b3d315027e554dd2af7035ad0fc78a96`.

Il pilota usa la condizione approvata con test pubblici specifici del task. La
condizione senza tali test non fa parte di B6 e verrà progettata separatamente.

## Parametri ed esito del runner

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- piano statico: analyst, implementer e verifier;
- stato: `completed`;
- `agentTaskDurationMs`: 126.657;
- `totalTokens`: 190.487;
- massimo lavoratori concorrenti: 1;
- deleghe richieste/avviate/completate: 3/3/3;
- deleghe fallite o rifiutate: 0.

Uso osservato:

| Categoria | Token |
| --- | ---: |
| input non in cache | 38 |
| cache read | 133.148 |
| cache creation | 43.707 |
| output | 13.594 |
| totale | 190.487 |

Per ruolo: coordinator 37.327 token, analyst 53.357, implementer 45.459 e
verifier 54.344. L'uso autorevole è completo per tutti e quattro gli agenti.

## Verifiche dell'harness

- quattro agenti terminati con exit code 0;
- violazioni di configurazione: 0;
- errori strumento: 0;
- righe stream malformate: 0;
- stderr non vuoti: 0;
- differenze nei materiali protetti: 0;
- file UTF-8 non validi: 0;
- directory di sessione vuota al termine;
- processi residui dell'orchestratore o del server MCP: 0.

L'audit contiene sette operazioni completate con exit code 0: typecheck, build e
test pubblici eseguiti due volte, oltre ai test aggiuntivi. Il verifier ha creato
`agent-tests/t1-verifier.test.ts`; l'esecuzione dei test aggiuntivi è terminata
con exit code 0.

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
- durata della valutazione: 3.597 ms.

La baseline canonica e la toolchain sono le stesse già verificate per B7. La
copia di lavoro temporanea dell'evaluator è stata rimossa al termine.

## Evidenze e allowlist

Evidenze dell'harness:
`C:\AgenticAIResults\20261109T203833Z-bdd6ac20`.

Workspace congelata:
`C:\AgenticAIRuns\20261109T203833Z-bdd6ac20`.

Evidenze private dell'evaluator:
`<trusted-evaluator-root>\runs\20261109T203833Z-bdd6ac20`.

Hash dei riepiloghi principali:

| File | SHA-256 |
| --- | --- |
| `run-record.json` | `aa567fadcc845017e26ae7038bb0ef51e3c10e0df2a875c5f5224422c891ee4d` |
| `evaluation-summary.json` | `8c96f2928d2b341b68e499c5a9ae503abc91932fae0914aea2236c5db44462aa` |

Dopo questo passo l'allowlist dei piloti contiene esattamente:

1. `20261109T195256Z-9fc50de5` — T1 adattiva;
2. `20261109T203833Z-bdd6ac20` — T1 statica.

Le altre directory presenti nelle root operative sono prove tecniche storiche e
non fanno parte dei piloti B6.

Questa prova certifica il solo slot T1 statico. Non determina ancora i limiti
ufficiali e non certifica i quattro piloti rimanenti.
