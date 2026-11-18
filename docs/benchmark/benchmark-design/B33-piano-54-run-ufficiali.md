# B33 — Piano deterministico delle 54 run ufficiali

## Stato e obiettivo

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali dopo la calibrazione B32.
- **Stato:** piano generato e verificato; nessuna run ufficiale avviata.
- **Chiamate LLM:** nessuna in questa iterazione.

Obiettivo: materializzare fuori dall'area agente il piano B23 usando il commit
sorgente B24 come seed. Componenti: generatore e validatore dell'harness
congelato, piano JSON nell'area risultati fidata e controllo indipendente.
Criteri: 54 slot univoci, 18 blocchi da tre architetture, copertura esatta di
6 task × 3 architetture × 3 ripetizioni, ordine secondo B23 e generazione
byte-identica a parità di seed e versione.

## Piano congelato

- Seed e commit sorgente: `aee8d1162bb496895990a00095cc14c2651ff752`.
- Algoritmo: `sha256-blocked-v1`; schema: 1.
- Percorso fidato:
  `C:\AgenticAIResults\official-plan-aee8d1162bb496895990a00095cc14c2651ff752.json`.
- SHA-256: `441753b52b77cde505af0dd1bb8541c1300288794751a8bb5732e74a731e4573`.
- Dimensione: 12823 byte; attributo sola lettura rilevato `True`.
- Primo slot: `official-t5-hierarchical-adaptive-r3`; ultimo:
  `official-t1-hierarchical-adaptive-r3`.

Le run devono usare questo piano come fonte unica di `repetition`, `orderSeed`
e `orderPosition`; i limiti comuni approvati sono registrati in B32. La sola
lettura protegge da modifiche accidentali, non sostituisce il confronto
crittografico prima dell'uso.

## Comando e verifiche osservate

Il comando, eseguito due volte tramite il processo dell'harness congelato, è:

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' generate-experiment-plan `
  --seed aee8d1162bb496895990a00095cc14c2651ff752
```

Entrambe le invocazioni hanno restituito exit code 0 e stdout byte-identico. Il
generatore esegue il validatore della stessa versione prima di serializzare il
piano. Un controllo indipendente Node ha ricalcolato gli hash di ordinamento
dei blocchi e delle architetture e verificato schema, seed, 54 posizioni
consecutive, 18 blocchi, contenuto di ogni slot, `runKind=official` e
identificativi univoci. La scrittura ha usato creazione esclusiva e il file
riletto è risultato byte-identico allo stdout; `Get-FileHash` ha confermato lo
SHA-256 riportato sopra.

Una ricognizione preliminare con `Select-Object -First` ha interrotto la
pipeline dopo le prime righe e mostrato `exit=-1`; non è stata usata per
generare o validare il file. Le due generazioni complete successive sono quelle
con esito 0 e risultato identico descritte sopra.

## Limiti e lavoro rimanente

Il validatore integrato è eseguito sulla struttura generata in memoria; non
esiste qui un comando separato di validazione del JSON salvato. Il controllo
indipendente ha verificato il contenuto generato e la corrispondenza esatta dei
byte scritti. Prima delle run devono essere ricontrollati il piano, gli input
congelati e le condizioni operative; occorre inoltre l'autorizzazione esplicita
alle chiamate LLM e alle run ufficiali. Questo documento non le autorizza.
