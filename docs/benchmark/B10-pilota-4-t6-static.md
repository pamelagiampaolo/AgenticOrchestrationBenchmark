# B10 — Pilota 4: T6 con gerarchia statica

## Classificazione

- **Data:** 10 novembre 2026.
- **Stato:** pilota valido per la calibrazione B6.
- **Ordine:** quarto dei sei piloti congelati.
- **Run ID:** `20261110T153951Z-badf5ea7`.
- **Task:** T6.
- **Architettura:** `hierarchical-static`.
- **Commit sorgente:** `748e09f6b3d315027e554dd2af7035ad0fc78a96`.

Il pilota usa la condizione approvata con test pubblici specifici del task. T6 è
partito dalla baseline originale e non include le soluzioni dei task precedenti.

## Parametri ed esito del runner

- modello: `claude-sonnet-5-5`;
- limite di tempo: 900 secondi;
- budget globale: 1.000.000 token;
- piano statico: analyst, implementer e verifier;
- stato: `completed`;
- `agentTaskDurationMs`: 154.001;
- `totalTokens`: 391.059;
- massimo lavoratori concorrenti: 1;
- deleghe richieste/avviate/completate: 3/3/3;
- deleghe fallite o rifiutate: 0.

Uso osservato:

| Categoria | Token |
| --- | ---: |
| input non in cache | 50 |
| cache read | 290.776 |
| cache creation | 76.081 |
| output | 24.152 |
| totale | 391.059 |

Per ruolo: coordinator 49.172 token, analyst 74.822, implementer 198.476 e
verifier 68.589. L'uso autorevole è completo per tutti e quattro gli agenti.

## Verifiche dell'harness

- quattro agenti terminati con exit code 0;
- violazioni di configurazione: 0;
- righe stream malformate: 0;
- differenze nei materiali protetti: 0;
- file UTF-8 non validi: 0;
- stderr non vuoti: 0;
- directory di sessione vuota al termine;
- processi residui dell'orchestratore o del server MCP: 0.

L'audit registra dieci operazioni. Typecheck e build sono passati due volte. I
test pubblici sono falliti nei due tentativi eseguiti durante il ciclo agente. I
test aggiuntivi sono falliti due volte e poi passati due volte; il verifier ha
lasciato `agent-tests/t6-extra.test.ts`. Implementer e verifier hanno
rispettivamente 3 e 1 errori strumento, corrispondenti agli exit code negativi
dei test.

Questi fallimenti non sono guasti infrastrutturali e non autorizzano una
ripetizione. La valutazione indipendente della submission finale stabilisce
l'esito funzionale autorevole.

## Valutazione indipendente

Il valutatore fidato, commit
`eb573d5a53985abe0bc809299c64a35f300443fd`, ha prodotto outcome `passed` ed
exit code 0:

- build superata;
- test pubblici applicabili a T6: 26 attesi, 26 eseguiti, 26 superati;
- test riservati: 4 attesi, 4 eseguiti, 4 superati;
- test mancanti, inattesi o saltati: 0;
- input originale e submission archiviata invariati;
- manifest delle suite verificati;
- diagnostica vuota;
- durata della valutazione: 2.667 ms.

I fallimenti pubblici osservati dall'harness erano quindi tentativi intermedi:
la submission congelata dopo l'ultima correzione è corretta. Il fatto che la
suite pubblica non sia stata rieseguita dopo quella correzione resta un dato del
comportamento di verifica della gerarchia, non un errore dell'evaluator.

La baseline canonica dichiarata dall'evaluator è
`ddf08e8759ef172a5b654168a1d2d6b66c9cd248`. Non risultano differenze rispetto al
commit sperimentale nei file `BenchmarkTarget` o nella specifica pubblica T6.

## Evidenze e allowlist

Evidenze dell'harness:
`C:\AgenticAIResults\20261110T153951Z-badf5ea7`.

Workspace congelata:
`C:\AgenticAIRuns\20261110T153951Z-badf5ea7`.

Evidenze private dell'evaluator:
`C:\repos\TesiAgenticAI-TrustedEvaluator\runs\20261110T153951Z-badf5ea7`.

Hash dei riepiloghi principali:

| File | SHA-256 |
| --- | --- |
| `run-record.json` | `5d8e6c50c2cf96c634bc1e883ab209e4ce7b7708de3f3e6566e6443a6ef1c742` |
| `evaluation-summary.json` | `72c313a244fa087a7e954d9fe619afe666cff12b306079c9747ba8d36ce3f64a` |

Dopo questo passo l'allowlist dei piloti contiene esattamente:

1. `20261109T195256Z-9fc50de5` — T1 adattiva;
2. `20261109T203833Z-bdd6ac20` — T1 statica;
3. `20261110T152614Z-c438ecc5` — T1 single-agent;
4. `20261110T153951Z-badf5ea7` — T6 statica.

Le altre directory presenti nelle root operative sono prove tecniche storiche e
non fanno parte dei piloti B6.

Questa prova certifica il solo slot T6 statico. I limiti ufficiali non possono
essere derivati prima dei due slot rimanenti.
