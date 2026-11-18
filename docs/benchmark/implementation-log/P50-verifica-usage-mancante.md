# P50 — Verifica dei dati di consumo mancanti

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** verifica delle metriche prima delle run ufficiali.
- **Stato:** comportamenti sintetici osservati e stream pilota controllati;
  gestione dei campi mancanti nelle run future non corretta in questa iterazione.
- **Chiamate LLM sperimentali:** nessuna.

Obiettivo: distinguere un uso non disponibile da un uso pari a zero e verificare
se il caso sintetico riguarda gli stream dei piloti. Componenti: tracciatore,
runner single-agent, test locali e stream conservati dei sei piloti. Criteri:
osservare senza modificare l'harness il trattamento di `usage` mancante o
vuoto; controllare nei piloti la presenza di quattro campi numerici non negativi
nei risultati finali; registrare i limiti della prova.

## Risultati sintetici

Due test aggiunti ai test .NET documentano il comportamento attuale:

1. un risultato `success` con `usage: {}` produce un oggetto `TokenUsage` con
   quattro zeri: la sola presenza dell'oggetto non dimostra la presenza dei
   singoli campi;
2. una sessione single-agent simulata con risultato `success` senza `usage`
   ha stato `completed`, ma `tokenMeasurementComplete=false`, uso autorevole
   `null` e `tokensObserved` derivato dallo stream.

Il secondo stato non certifica la completezza delle metriche: occorre leggere
anche il relativo flag. Il primo caso richiede particolare attenzione perché
il valore autorevole è presente benché i campi originali manchino. Non viene
stabilito qui se la CLI reale possa o debba emettere tale forma in produzione.

## Verifica degli stream pilota

Uno script Node in sola lettura ha esaminato i JSONL dei sei piloti: un evento
`result` per ognuno dei 12 processi agente, con i campi `input_tokens`,
`cache_read_input_tokens`, `cache_creation_input_tokens` e `output_tokens`
presenti come interi non negativi. Output: `resultEvents=12
missingOrInvalid=0`; exit code 0. Questo riscontro non prova che ogni evento
o consumo a monte sia stato emesso e non verifica le run future.

## Comandi di verifica e risultati

- `dotnet format ... --verify-no-changes --no-restore`: exit code 0.
- `dotnet build ... --no-restore`: exit code 0, zero errori e avvisi.
- Il primo `dotnet test` combinava filtro classe e filtro metodo non
  compatibili: nessun test eseguito, exit code 8. Non è contato come successo.
- Ripetizione con filtri di classe `RetryUsageTests` e
  `SingleAgentSessionRunnerTests`: **16/16 passati**, exit code 0.
- Gruppo pertinente comprendente anche stream, limiti e gerarchie:
  **45/45 passati**, exit code 0.

I test usano una CLI simulata e non modificano il codice del runner. I comandi
sono stati eseguiti con accesso autorizzato alle directory temporanee di test.

## Conseguenze e lavoro rimanente

Prima di utilizzare le metriche delle run future serve una regola verificabile
che controlli i campi grezzi e non tratti automaticamente i campi assenti come
zeri. L'eventuale correzione dell'harness congelato richiederebbe valutazione
d'impatto B22; un controllo esterno dei record e degli stream potrebbe essere
valutato separatamente senza attribuirgli proprietà non verificate.

Il riscontro sui piloti non richiede, da solo, di ripeterli. Non sono stati
modificati gli input congelati, né avviate run o chiamate LLM.
