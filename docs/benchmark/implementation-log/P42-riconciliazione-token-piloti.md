# P42 — Riconciliazione dei token dei sei piloti

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** verifica mirata B4/B5 prima delle run ufficiali.
- **Stato:** riconciliazione dei sei record pilota completata; copertura di
  retry e chiamate fallite resta da verificare separatamente.
- **Chiamate LLM e run ufficiali:** nessuna.

Obiettivo: confrontare le quattro categorie dei messaggi con i totali per
agente e per run, senza ripetere le esecuzioni. Componenti: record pilota
B26–B31 e Node congelato. Criteri: hash dei record invariati, ID messaggio
univoci all'interno di ogni run, output finale, somme per categoria uguali a
uso autorevole e stream di ogni agente, somme degli agenti uguali ai totali di
run, `totalTokens` e `tokensObserved` coerenti e misurazione completa.

## Risultati osservati

`Get-FileHash` ha confrontato i sei record con gli SHA-256 documentati in
B26–B31: **6/6 conformi**. Uno script Node in sola lettura ha verificato i
criteri sopra sui record e ha prodotto i seguenti risultati:

| Run ID | Agenti | Messaggi | Input non cache | Cache read | Cache creation | Output | Totale |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `20261118T172146Z-77c1a569` | 1 | 4 | 8 | 20562 | 13491 | 4442 | 38503 |
| `20261118T174844Z-c22ba9c4` | 4 | 20 | 40 | 143734 | 43062 | 12021 | 198857 |
| `20261118T181009Z-260b2323` | 1 | 4 | 8 | 21003 | 10826 | 4461 | 36298 |
| `20261118T185448Z-52ec0b68` | 4 | 24 | 48 | 270568 | 63173 | 18519 | 352308 |
| `20261118T190522Z-ebd7cd95` | 1 | 5 | 10 | 39288 | 13845 | 5762 | 58905 |
| `20261118T191206Z-104ed5e9` | 1 | 4 | 8 | 26880 | 13701 | 5460 | 46049 |

Output finale: `6/6 reconciled; message IDs unique within each run; no category
mismatches`; exit code 0. Sono stati esaminati 61 messaggi registrati. Nei due
piloti con quattro agenti, la somma degli usi autorevoli dei quattro ruoli
coincide con il totale di run per ogni categoria.

## Limiti e lavoro rimanente

La prova confronta dati già presenti negli stessi record: non verifica
indipendentemente i conteggi presso il fornitore né dimostra che un evento
assente dallo stream sia stato registrato. L'unicità è verificata all'interno
di ciascuna run, non tra run distinte. Non è una prova generale dei retry o
delle chiamate LLM fallite con consumo, né della loro inclusione una sola
volta; tali casi di B5 restano aperti. Non verifica aggregazioni ufficiali,
isolamento o procedura di audit delle sostituzioni.

Non sono stati modificati input congelati o risultati e non sono stati ripetuti
piloti. La verifica non autorizza run o chiamate LLM.
