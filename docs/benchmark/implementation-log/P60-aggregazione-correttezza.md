# P60 — Aggregazione del completamento corretto

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** riepilogare la metrica primaria P59 per le 18 coppie e le
  tre architetture, preservando denominatori e risultati non disponibili.
- **Componenti:** righe prodotte da P54–P59, aggregazione esterna e test.
  Nessuna modifica ai componenti sperimentali congelati.
- **Comportamento:** mostrare slot individuali, run previste, valide e con
  metrica disponibile, successi, insuccessi, mancanti e tasso sui soli valori
  disponibili, segnalando incompleta ogni copertura inferiore al previsto.
- **Accettazione:** verificare gruppi da 3 e 18, esito falso, dato nullo,
  denominatore zero, slot non validi e duplicati, con tassi riconciliati e
  nessuna mutazione delle righe. Nessuna run o chiamata LLM.

## Implementazione e verifiche

`scripts/current/Summarize-OfficialCompletion.cjs` richiede tutte le 54
combinazioni ufficiali senza duplicati e rifiuta una metrica presente per uno
slot non valido. Produce 18 gruppi da tre e tre gruppi da 18. Il `rate` usa il
denominatore `available`; quando una run valida non ha metrica disponibile,
`validRuns` resta visibile separatamente e il gruppo è marcato incompleto.
Con nessuna metrica disponibile il tasso è `null`.

Comandi con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Summarize-OfficialCompletion.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/summarize-official-completion.test.cjs tests/compute-official-completion.test.cjs tests/compute-official-pass-rates.test.cjs tests/attach-official-evaluation.test.cjs tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0: 10 test passati, 0 falliti. La prova nuova verifica una
coppia con risultati `true`, `false`, `null`: tre run valide, due metriche
disponibili, un successo e tasso 1/2 con copertura incompleta. Per la stessa
architettura verifica 16 successi su 17 metriche disponibili e 18 slot previsti;
verifica inoltre gruppi vuoti, duplicati e input non validi.

## Limiti e lavoro rimanente

La funzione presuppone righe P54–P59 già verificate e non accerta l'autenticità
dei record. Le prove di ciascun componente sono sintetiche e non costituiscono
ancora una verifica dell'intera procedura operativa, della custodia del
registro o di tutte le metriche B5. Restano l'integrazione e la verifica
pre-avvio. Gli input congelati non sono cambiati e i piloti non sono ripetuti.
