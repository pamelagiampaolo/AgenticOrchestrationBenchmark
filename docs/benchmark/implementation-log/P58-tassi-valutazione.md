# P58 — Tassi dei test di valutazione

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** calcolare i tassi pubblici, riservati e canonici B5 per gli
  slot già collegati a valutazioni affidate dal chiamante.
- **Componenti:** collegamento P56, conteggi dell'evaluator, conteggi attesi
  canonici forniti dal chiamante, funzione esterna e test sintetici.
- **Comportamento:** usare solo conteggi coerenti con gli attesi, includere
  i test saltati nel denominatore, non trasformare dati incerti in zero;
  per `build-failure` confermata applicare la regola B5 di tassi zero.
- **Accettazione:** verificare suite complete, fallimenti, test saltati,
  build failure, valori mancanti e conteggi discordanti, oltre a slot non
  valutati. Nessuna chiamata LLM, modifica di input congelati o lettura del
  contenuto dei test riservati.

## Implementazione e verifiche

`scripts/current/Compute-OfficialPassRates.cjs` calcola i tre rapporti solo
per una valutazione collegata da P56 e dimensioni canoniche positive fornite
dal chiamante. Per valutazioni con suite eseguite controlla le fasi, i conteggi,
gli attesi, l'assenza di casi mancanti o inattesi e la riconciliazione fra
eseguiti, falliti, superati e saltati. Un'incoerenza lascia tutti i tre tassi
`null`. Per `build-failure` richiede fase build `failed` e restituisce zero,
secondo B5. La prova non verifica direttamente la provenienza delle dimensioni.

Comandi con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Compute-OfficialPassRates.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/compute-official-pass-rates.test.cjs tests/attach-official-evaluation.test.cjs tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0; 8 test passati, 0 falliti. Le fixture verificano tassi
1/1/1, una suite con test fallito e saltato e tassi 1/3, 1 e 3/5, build
failure con tassi zero, dati mancanti, conteggi incoerenti e guasto evaluator.
La lettura del codice di coerenza evaluator ha chiarito che `executed` equivale
a `passed + failed`, escludendo i saltati. Una ricerca preliminare ha incluso
un percorso inesistente; la successiva lettura del modulo corretto è riuscita.

## Limiti e lavoro rimanente

I conteggi canonici, il riepilogo e il suo legame alla submission devono essere
verificati e forniti dal chiamante; il collegamento P56 non ne garantisce
l'autenticità. La funzione non calcola `correctCompletion`, non aggrega i tassi
tra run, non prova la catena end-to-end né si integra nel flusso operativo.
Resta il controllo finale prima dell'avvio. Non sono stati modificati input
congelati o ripetuti piloti; nessuna run ufficiale è stata avviata.
