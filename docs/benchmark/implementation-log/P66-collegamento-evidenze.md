# P66 — Collegamento delle evidenze di run e submission

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** rendere verificabile il legame tra record dell'harness,
  riepilogo evaluator e submission archiviata, usando anche un pilota reale.
- **Componenti:** `prepared-run.json`, record di run, metadati e manifest di
  submission evaluator, riepilogo evaluator e funzione di confronto già
  presente nel valutatore congelato; nuovo controllo esterno in sola lettura.
- **Comportamento:** confrontare identificativi, percorso sorgente e impronta
  della submission; confrontare archivio e sorgente corrente con il manifest;
  restituire riferimenti e SHA-256 ai file senza modificarli.
- **Accettazione:** prova su un pilota reale con identità e file corrispondenti,
  confronto con gli hash storici B26 e prova negativa con un'altra valutazione.
  Non aprire
  il contenuto dei test riservati, non avviare run né chiamate LLM.

## Comando e verifiche

`scripts/current/Test-RunEvidence.ps1` legge i file di una run e di una
valutazione e usa le funzioni `New-SubmissionManifestObject` e
`Compare-SubmissionToManifest` del valutatore per confrontare archivio e
sorgente corrente con il manifest. Controlla schema, identità, percorso
sorgente, commit tra preparazione e record e impronta della submission.
Produce su stdout JSON con identità, impronta e hash di preparazione, record,
metadati, manifest e riepilogo; non scrive file.

```powershell
& .\scripts\current\Test-RunEvidence.ps1 `
  -ResultsDirectory 'C:\AgenticAIResults\<runId>' `
  -EvaluationDirectory 'C:\repos\AgenticOrchestrationEvaluator\runs\<runId>'
```

Su B26 è stato osservato:

```text
pilot=20261118T172146Z-77c1a569 identity=matched source=matched archive=matched files=11 B26Hashes=2/2 exit=0
```

Il record e il riepilogo hanno gli stessi hash riportati in B26; l'impronta
submission è `231176ff569698c50ed695faa65849c6a159cf113ad674d9126a898b51331e4f`.
Associando allo stesso record la valutazione di un altro pilota il controllo
ha restituito `Run identity mismatch`, exit code 1. Un primo richiamo positivo
non impostava esplicitamente l'exit code PowerShell; è stato aggiunto `exit 0`
e la prova positiva e negativa è stata rieseguita con i risultati sopra.

## Limiti e lavoro rimanente

Il confronto del sorgente riguarda lo stato corrente: non prova che il file
non sia cambiato e poi tornato identico, né garantisce una verifica continua.
Il controllo usa il modulo evaluator locale e non ne verifica autonomamente
commit e hash; questi restano parte del freeze pre-avvio. Non valida lo schema
completo del riepilogo, la correttezza dell'esito o gli stream LLM e non rileva
letture esterne. Il JSON prodotto non è ancora integrato automaticamente nel
registro: P38 controlla solo l'hash del file di evidenza, non i riferimenti
annidati in esso; le impronte devono essere ricontrollate durante l'analisi.
Non sono state simulate alterazioni dei file della submission. La custodia
effettiva dell'anchor e l'integrazione operativa restano da definire.
