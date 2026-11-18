# P41 — Prova del flusso audit con anchor separato

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali, dopo P40.
- **Stato:** flusso simulato provato; custodia operativa effettiva ancora da
  definire e provare.
- **Chiamate LLM e run ufficiali:** nessuna.

Obiettivo: verificare insieme gli script P38/P39, il piano B33 e un anchor
conservato separatamente. Componenti: piano ufficiale in sola lettura, record
e autorizzazione simulati, snapshot del registro, file anchor e validatore.
Criteri: due revisioni collegate senza sovrascrittura, evidenze coerenti,
validazione del registro e rilevazione della rimozione dell'ultima revisione
quando l'anchor più recente viene conservato.

## Prova eseguita

`tests/official-audit-workflow.test.cjs` usa il vero piano B33 e file temporanei
chiaramente simulati. Crea directory separate per snapshot e anchor, registra un
guasto infrastrutturale e una sostituzione autorizzata simulata, conserva ogni
hash in un nuovo file anchor con creazione esclusiva e verifica:

- collegamento della seconda revisione all'hash della prima;
- validazione della seconda revisione con la CLI P38;
- rifiuto della sovrascrittura di un file anchor;
- rifiuto del salvataggio dopo la rimozione simulata dell'ultima revisione,
  mentre l'anchor separato conserva l'hash più recente.

Comando eseguito:

```powershell
$env:OFFICIAL_PLAN_PATH = 'C:\AgenticAIResults\official-plan-aee8d1162bb496895990a00095cc14c2651ff752.json'
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test `
  tests\official-audit-workflow.test.cjs `
  tests\official-audit-save.test.cjs `
  tests\official-audit.test.cjs
```

Output: `tests 3`, `pass 3`, `fail 0`, `skipped 0`; exit code 0. Il test
elimina soltanto i file temporanei che ha creato. Senza `OFFICIAL_PLAN_PATH` il
test del flusso è esplicitamente saltato; gli altri test non richiedono il piano
esterno.

## Limiti e lavoro rimanente

Le due directory della prova si trovano nello stesso spazio temporaneo e non
dimostrano custodia indipendente da chi può modificarle entrambe. L'anchor viene
scritto dal test, non automaticamente dal programma di salvataggio; un arresto
tra le due scritture richiede riconciliazione manuale, senza sovrascrivere
evidenze. Non sono state utilizzate submission o autorizzazioni reali, né è
stata provata l'integrazione automatica con il runner.

Prima delle run restano da stabilire e provare custodia effettiva e procedura
di riconciliazione, collegamento ai record reali, controlli B4/B5 e prove di
isolamento aperte. La verifica non altera gli input congelati, non richiede la
ripetizione dei piloti e non autorizza run o chiamate LLM.
