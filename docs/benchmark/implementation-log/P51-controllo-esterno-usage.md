# P51 — Controllo esterno dei campi di consumo

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali; seguito di P50.
- **Stato:** controllo esterno implementato e verificato su fixture e piloti.
- **Chiamate LLM sperimentali:** nessuna.

Obiettivo: verificare dopo una run la presenza delle quattro categorie grezze
`usage`, senza trattare un campo mancante come zero. Componenti: record e stream
JSONL della run, controllo esterno Node e test sintetici. Criteri: segnalare
campi assenti, non numerici o negativi, confrontare le categorie e i totali
autorevoli con i record, verificare i flag di completezza e riconciliare le
somme degli agenti; nessuna modifica all'harness congelato.

## Comando e risultato

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Check-RunUsage.cjs '<directory-risultati-run>'
```

Il controllo legge `session-record.json` e `stream.jsonl` per single-agent,
oppure `run-record.json` e lo stream indicato da ciascun agente per le
gerarchie. Richiede uno e un solo evento `result` per agente e quattro valori
interi non negativi. Verifica le somme e i totali registrati. Produce JSON con
`usageComplete` e `issues`: exit code 0 indica controlli passati, 1 dati
incompleti o incoerenti, 2 errore di input. L'assenza di una categoria viene
segnalata anche se la CLI potrebbe considerarla opzionale: è una regola
conservativa di questo controllo, non un'affermazione sul contratto del
fornitore.

## Verifiche osservate

- `node --check scripts/current/Check-RunUsage.cjs`: exit code 0.
- `node --test tests/check-run-usage.test.cjs`: 2 test passati, 0 falliti;
  coperti zero presente, campo assente, oggetto vuoto, flag incompleto, totale
  incoerente, JSON non valido, somma gerarchica, risultato mancante e percorso
  agente esterno ai risultati.
- Esecuzione dello script sui sei record pilota B26–B31: 6/6 con exit code 0,
  `usageComplete: true`, nessun problema segnalato; agenti controllati
  rispettivamente 1, 4, 1, 4, 1 e 1.

## Limiti e lavoro rimanente

Il controllo legge file che devono essere conservati nell'area fidata; non
attesta la loro autenticità, non ricostruisce richieste o consumi che la CLI non
ha emesso, non prova che l'elenco degli agenti sia completo e non riconcilia
tutti i messaggi individuali con il fornitore. Gli esiti di errori, timeout o
risultati multipli possono essere segnati incompleti e richiedono revisione,
non valori zero. L'integrazione sistematica nel processo delle run e nelle
aggregazioni resta da verificare.

Il controllo è esterno agli input sperimentali: i piloti non sono stati
ripetuti. Questo passo non autorizza run o chiamate LLM.
