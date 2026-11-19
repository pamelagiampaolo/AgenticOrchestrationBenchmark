# P62 — Comando di riepilogo in sola lettura

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** rendere richiamabile la catena P54–P60 su file fidati,
  mantenendo espliciti i controlli non automatizzati.
- **Componenti:** piano ufficiale, snapshot del registro con anchor fornito dal
  chiamante, risultati, riepiloghi evaluator, conteggi canonici forniti dal
  chiamante e nuovo comando esterno Node.
- **Comportamento:** confrontare hash del piano con B33 e hash dello snapshot
  con l'anchor fornito, validare registro ed evidenze, produrre JSON su stdout
  con aggregati e dati mancanti; non scrivere né avviare processi o run.
- **Accettazione:** prova di comando e catena su file sintetici, rifiuto di
  piano e anchor errati, conservazione degli input e segnalazione esplicita
  delle verifiche di provenienza che restano a carico del chiamante.

## Comando e risultato

`scripts/current/Report-OfficialResults.cjs` legge il piano, lo snapshot e i
file di run, verifica le evidenze tramite P38 e applica P54–P60. Stampa JSON
con riepiloghi, diagnostica per slot validi e hash di piano, registro e file
dei conteggi. Non scrive file né avvia agenti. Interfaccia:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Report-OfficialResults.cjs `
  '<piano-B33.json>' '<snapshot-registro.json>' '<sha256-anchor-fidato>' `
  '<directory-risultati>' '<directory-runs-evaluator>' '<conteggi-canonici.json>'
```

Il file conteggi deve mappare ciascun task, ad esempio
`{"T1":{"public":31,"reserved":3}}`; l'esempio descrive soltanto T1 e
non sostituisce la verifica dei manifest per tutti i task. Se manca una
dimensione, i tassi interessati restano non disponibili.

Comandi di verifica con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Report-OfficialResults.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/report-official-results.test.cjs tests/official-report-pipeline.test.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/report-official-results.test.cjs tests/official-report-pipeline.test.cjs tests/summarize-official-completion.test.cjs tests/compute-official-completion.test.cjs tests/compute-official-pass-rates.test.cjs tests/attach-official-evaluation.test.cjs tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs tests/official-audit-save.test.cjs
```

Tutti exit code 0: 2 test mirati e 13 test nella suite pertinente, nessun
fallimento. Il test CLI usa il piano B33 reale e un registro vuoto sintetico;
controlla rifiuto di anchor e piano errati e l'immutabilità dei file di input.
Il test P61 verifica la catena con record e valutazioni sintetici, ma non
attraverso la nuova CLI con run popolate.

## Limiti e lavoro rimanente

Il confronto dell'hash non rende fidato un anchor fornito dall'utente: servono
custodia separata e verifica della sua provenienza. Il comando controlla lo
snapshot e le evidenze referenziate, non tutta la storia delle revisioni.
Non verifica provenienza o hash dei record e riepiloghi, corrispondenza della
submission né autenticità dei conteggi canonici; segnala tali limiti nel JSON.
Non produce tutte le metriche B5 e non integra l'avvio, la valutazione o la
registrazione automatica delle run. Restano la procedura concreta, la
verifica dei conteggi dai manifest e il controllo finale pre-avvio.
Nessun input congelato è cambiato e nessun pilota è stato ripetuto.
