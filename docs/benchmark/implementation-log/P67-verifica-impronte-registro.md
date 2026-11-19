# P67 — Verifica delle impronte nell'evidenza di registro

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** verificare gli hash annidati nel documento prodotto da P66,
  oltre all'hash esterno verificato dal registro P38.
- **Componenti:** output P66, riferimenti di evidenza P38, nuovo controllo Node
  in sola lettura, pilota reale e fixture sintetica alterata.
- **Comportamento:** richiedere hash esterno atteso, identificativi e cinque
  riferimenti distinti; confrontare ogni SHA-256 e i dati identificativi nei
  file, rifiutando difformità. Non modificare file o input congelati.
- **Accettazione:** prova positiva su output P66 del pilota B26 e prova negativa
  con un hash interno alterato; test dello strumento e verifica sintattica.
  Nessuna run, chiamata LLM o modifica dei risultati dei piloti.

## Comando e prove

`scripts/current/Test-RunEvidenceBundle.cjs` verifica il file di evidenza P66
contro un hash esterno atteso, legge i cinque riferimenti e ne confronta gli
hash e alcune identità. Il documento P66 può essere conservato come evidenza
di un tentativo nel registro P38/P65: dopo il controllo P38 dell'hash esterno,
eseguire anche questo comando per controllare i riferimenti annidati.

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Test-RunEvidenceBundle.cjs `
  '<percorso-assoluto-evidenza.json>' '<sha256-atteso>' '<runId>' '<taskId>'
```

La verifica sintattica `node --check scripts/current/Test-RunEvidenceBundle.cjs`
è passata, exit code 0. Con Node congelato, `node --test
tests/run-evidence-bundle.test.cjs tests/official-audit-anchor.test.cjs
tests/official-audit.test.cjs tests/report-official-results.test.cjs` ha
riportato 4 test passati, 0 falliti, exit code 0. Il test nuovo ha generato
in temporaneo l'output P66 del pilota B26, verificato cinque riferimenti,
rifiutato hash esterno e task errati e rifiutato un hash interno alterato
nel solo documento temporaneo, senza modificare i file del pilota.

## Limiti e lavoro rimanente

Il controllo confronta i byte correnti con le impronte registrate, ma non
certifica la provenienza dell'hash esterno, la veridicità della classificazione
o la correttezza dell'evaluator. Non ricalcola l'albero della submission:
P66 lo verifica al momento della produzione del documento. Non verifica gli
stream e non dimostra assenza di letture esterne. Il comando non è invocato
automaticamente da P38, P65 o P62: va eseguito nella procedura, mantenendo
evidenza e anchor in custodia fidata. Gli errori con valutazione mancante
richiedono un'altra evidenza e la classificazione conservativa B34.
