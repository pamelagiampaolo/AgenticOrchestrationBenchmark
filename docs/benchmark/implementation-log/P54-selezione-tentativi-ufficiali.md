# P54 — Selezione dei tentativi ufficiali

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** preparare una selezione verificabile degli slot per la futura
  aggregazione, senza alterare gli input sperimentali.
- **Componenti:** piano, validatore del registro, record di run e nuova funzione
  esterna con test sintetici.
- **Comportamento:** un solo tentativo valido per slot, dopo validazione del
  registro; i tentativi invalidati restano esclusi, gli slot senza tentativo
  valido restano presenti senza record, i record pilota e quelli con identità
  incoerente vengono rifiutati. La selezione non classifica le singole metriche.
- **Accettazione:** test su sostituzione valida, slot incompleto e in revisione,
  fallimento funzionale valido, record pilota, identità diverse e registro
  non valido. Nessuna run né chiamata LLM.

## Implementazione e verifiche

`scripts/current/Select-OfficialAttempts.cjs` espone una funzione richiamabile:
valida il registro tramite P38, restituisce una riga per ciascuno slot del
piano e legge soltanto il tentativo finale valido. Per quel tentativo confronta
schema, `runId`, task, architettura, ripetizione, posizione, seed e `runKind`
con piano e registro. Gli slot mai avviati, incompleti o in revisione hanno
record `null`. Errori del registro e record discordanti causano un'eccezione.

Comandi eseguiti con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Select-OfficialAttempts.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/select-official-attempts.test.cjs tests/official-audit.test.cjs tests/official-audit-save.test.cjs tests/check-run-usage.test.cjs
```

Entrambi exit code 0; 5 test passati, 0 falliti. Le fixture includono 54 slot,
sostituzione autorizzata simulata, fallimento funzionale, slot assente,
incompleto e in revisione, pilot e identità incoerenti. Il test del registro
usa evidenze simulate; gli altri test esistenti verificano anche file reali
temporanei. La ricerca iniziale della semantica del seed con glob come percorsi
ha avuto errori di sintassi Windows; la ricerca nei sorgenti ha individuato la
definizione `OrderSeed` e il relativo parametro CLI.

## Limiti e lavoro rimanente

La funzione non è una CLI e non è integrata nel flusso di esecuzione o nel
report. Presuppone che il chiamante verifichi hash e provenienza del piano e
del registro, custodisca l'anchor e fornisca una lettura fidata dei record;
la verifica strutturale di P38 non dimostra autenticità o sufficienza delle
evidenze. Non confronta hash della submission o del record, non legge
l'evaluator, non invoca P51 e non calcola correttezza, metriche o aggregati.
Non prova che la CLI abbia emesso tutti i consumi. Restano quindi da collegare
e verificare quelle fasi prima di considerare completa l'elaborazione.
Nessun input congelato è stato modificato e nessun pilota è stato ripetuto.
