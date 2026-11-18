# B32 — Proposta dei limiti dai sei piloti del freeze finale

## Stato e obiettivo

- **Data:** 18 novembre 2026.
- **Fase:** calibrazione dopo i sei piloti B25.
- **Stato:** valori calcolati, **in attesa dell'approvazione esplicita**;
  non sono ancora limiti ufficiali congelati.
- **Chiamate LLM:** nessuna in questa iterazione.

Obiettivo: applicare la formula predefinita in B25 ai sei piloti validi, senza
separare le architetture. Componenti: record dell'harness, riepiloghi
dell'evaluator fidato e documenti B26–B31. Criteri: sei coppie univoche secondo
l'ordine previsto, stessa baseline, metriche complete non troncate, integrità e
valutazioni coerenti, massimi e arrotondamenti riproducibili.

## Osservazioni

| Posizione | Task | Architettura | Run ID | Durata (ms) | Token |
| ---: | --- | --- | --- | ---: | ---: |
| 1 | T1 | `hierarchical-adaptive` | `20261118T172146Z-77c1a569` | 24820 | 38503 |
| 2 | T1 | `hierarchical-static` | `20261118T174844Z-c22ba9c4` | 81767 | 198857 |
| 3 | T1 | `single-agent` | `20261118T181009Z-260b2323` | 24963 | 36298 |
| 4 | T6 | `hierarchical-static` | `20261118T185448Z-52ec0b68` | 118531 | 352308 |
| 5 | T6 | `single-agent` | `20261118T190522Z-ebd7cd95` | 33311 | 58905 |
| 6 | T6 | `hierarchical-adaptive` | `20261118T191206Z-104ed5e9` | 30175 | 46049 |

Tutte le valutazioni hanno outcome `passed`. La validità dei singoli piloti e
l'errore di un'invocazione di test agente nel pilota 4 sono documentati in
B26–B31; tale errore non è stato classificato come guasto infrastrutturale.
Questi valori sono osservazioni di calibrazione, non risultati delle 54 run.

## Calcolo secondo B25

Entrambi i massimi provengono dallo slot 4, T6 gerarchia statica:

- massimo durata: `118531 ms`; raddoppio `237062 ms`; arrotondamento al minuto
  superiore: **240 secondi**;
- massimo `totalTokens`: `352308`; raddoppio `704616`; arrotondamento ai
  100000 superiori: **800000 token**.

La proposta è quindi un limite comune di **240 secondi e 800000 token** per
ogni run ufficiale. Il parametro adattivo `--max-delegations 10` rimane quello
previsto dal protocollo; non deriva dal calcolo dei massimi.

## Verifiche eseguite

Uno script PowerShell in sola lettura ha confrontato per ciascuno dei sei slot:

1. SHA-256 dei record e dei riepiloghi con gli hash in B26–B31;
2. run ID, task, architettura, posizione, seed, ripetizione, classificazione
   `pilot` e commit sorgente con il piano B25;
3. stato `completed`, misurazione token completa, tempo e token inferiori ai
   tetti pilota, assenza di differenze protette e corrispondenza tra token
   osservati e uso autorevole totale;
4. identità, outcome `passed`, integrità e versione dell'evaluator nel riepilogo.

Output osservato: sei righe corrispondenti alla tabella; `maxMs=118531`,
`doubledMs=237062`, `roundedSeconds=240`, `maxTokens=352308`,
`doubledTokens=704616`, `roundedTokens=800000`; exit code 0. Il controllo usa
le classificazioni documentate per i singoli piloti e non ripete tutte le
rispettive verifiche pre-run e post-run.

Un precedente tentativo di ricerca con glob passato a `rg` come percorso è
fallito per sintassi Windows; non ha modificato dati e non è stato usato nel
calcolo.

## Passi successivi

Sottoporre la proposta all'utente. Solo dopo approvazione esplicita i valori
potranno essere registrati come limiti ufficiali congelati; prima delle 54 run
restano piano deterministico, controlli pre-avvio e autorizzazione esplicita.
Questo documento non autorizza run o chiamate LLM.
