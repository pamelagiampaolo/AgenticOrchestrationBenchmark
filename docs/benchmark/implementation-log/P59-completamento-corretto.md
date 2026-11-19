# P59 — Completamento corretto per run

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** derivare la correttezza funzionale e il completamento corretto
  B5 dai dati già collegati, senza inventare metriche o modificare gli input.
- **Componenti:** valutazione P56, tassi P58, uso P55, record e limiti B32;
  funzione esterna con prove sintetiche.
- **Comportamento:** valutazione affidabile e tassi verificati determinano la
  correttezza funzionale; `correctCompletion` richiede anche limiti ufficiali
  coerenti, stato e misure affidabili. Un limite dichiarato produce `false`;
  dati mancanti o contraddittori restano `null` con diagnostica.
- **Accettazione:** provare successo, fallimenti test e build, limiti, errore
  evaluator, misure mancanti, configurazione di budget errata e record
  contraddittori. Nessuna run o chiamata LLM.

## Implementazione e verifiche

`scripts/current/Compute-OfficialCompletion.cjs` usa i tassi già calcolati e
la valutazione collegata per `functionalSubmissionCorrect`. Se i budget del
record coincidono con B32 e non ci sono differenze protette, uno stato di
limite produce `correctCompletion: false`; altrimenti richiede durata e uso
verificati entro i budget. Stati non riconosciuti e contraddizioni producono
`null`. Per `cli-failed` la funzione applica la definizione B5 sulla submission:
se i test passano e le misure sono entro i limiti, il completamento funzionale
può essere vero; lo stato di errore rimane nel record per l'analisi separata.

Comandi con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Compute-OfficialCompletion.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/compute-official-completion.test.cjs tests/compute-official-pass-rates.test.cjs tests/attach-official-evaluation.test.cjs tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0: 9 test passati, 0 falliti. La nuova prova copre successo,
fallimenti funzionali, tre stati di limite, errore CLI, dati mancanti,
budget errati e incoerenze. La lettura del controllore mostra che il budget
osservato può attivare l'arresto al raggiungimento; lo stato esplicito di
limite prevale anche quando il consumo finale non è disponibile. Due ricerche
iniziali di file in directory inesistenti sono fallite; il file corretto è
stato trovato con `rg --files` e letto successivamente.

## Limiti e lavoro rimanente

La funzione presuppone input già verificati da P54–P58 e non ne certifica
provenienza, hash o legame alla submission; la prova della nuova logica usa
fixture in memoria e non dimostra l'intera catena operativa. Gli esiti
`cli-failed` con submission funzionalmente corretta sono mantenuti distinguibili
per l'analisi; un'eventuale interpretazione diversa richiede una modifica
esplicita del protocollo, non una riclassificazione silenziosa. Restano
aggregazione di correttezza, integrazione operativa e controllo pre-avvio.
Input congelati invariati, nessun pilota ripetuto, nessuna run ufficiale.
