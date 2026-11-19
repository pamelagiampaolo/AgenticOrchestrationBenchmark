# P61 — Prova integrata della catena di aggregazione

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** verificare insieme i componenti P54–P60, dalla scelta del
  tentativo ai riepiloghi di token e completamento corretto.
- **Componenti:** piano e registro sintetici a 54 slot, evidenze con hash,
  record, stream e valutazioni su file temporanei, funzioni già implementate.
- **Comportamento atteso:** selezionare solo il sostituto autorizzato,
  mantenere fallimento e dati mancanti, non includere slot non avviati nei
  denominatori disponibili, lasciare inalterati i file sorgente.
- **Accettazione:** confronto dei risultati attesi per un gruppo di tre e per
  quello da 18; rilevamento di evidenza alterata; test effettivamente eseguiti.
  Nessuna run ufficiale, nessuna chiamata LLM, input congelati invariati.

## Verifiche eseguite

Il test `tests/official-report-pipeline.test.cjs` collega in sequenza la
selezione P54, uso P55, valutazione P56, tassi P58, completamento P59 e i due
riepiloghi P57/P60. I file temporanei includono un primo tentativo annullato
per causa infrastrutturale con sostituzione e autorizzazione simulate, una
build failure e uno stream con categoria token mancante. Verifica anche il
rifiuto quando viene alterata l'evidenza e confronta gli hash dei tre record,
stream e riepiloghi prima e dopo l'elaborazione.

Comandi con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/official-report-pipeline.test.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/official-report-pipeline.test.cjs tests/summarize-official-completion.test.cjs tests/compute-official-completion.test.cjs tests/compute-official-pass-rates.test.cjs tests/attach-official-evaluation.test.cjs tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs tests/official-audit-save.test.cjs
```

Entrambi exit code 0: il test mirato è passato; la suite pertinente ha
registrato 12 test passati e 0 falliti. Per la coppia sintetica: 3 slot
validi, 2 misure token disponibili con somma parziale 18 e somma completa
`null`; completamenti `true`, `false`, `null`, quindi un successo su 2
risultati disponibili. Per l'architettura: 18 previsti, 2 risultati di
completamento disponibili e 16 mancanti.

## Limiti e lavoro rimanente

Il piano, i record, la decisione di sostituzione e la sua autorizzazione sono
simulati: non attestano autorizzazioni reali. Non sono stati verificati la
custodia dell'anchor del registro, l'hash del piano ufficiale, l'autenticità
dei riepiloghi, i manifest canonici o l'associazione con la submission. I
riepiloghi sintetici non sono un test completo dello schema evaluator. La
catena non è ancora collegata a una procedura eseguibile per le run ufficiali;
restano questa integrazione e il controllo finale dei freeze e dell'ambiente.
Non sono stati modificati input congelati, né ripetuti piloti.
