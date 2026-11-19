# P55 — Collegamento del consumo agli slot ufficiali

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** collegare i record già selezionati da P54 al controllo di
  completezza P51, mantenendo nulli i consumi non verificabili.
- **Componenti:** selezione P54, funzione `checkRun` P51, nuova funzione
  esterna e test sintetici con stream su file temporanei.
- **Comportamento atteso:** ciascuno slot valido usa il proprio record e il
  proprio stream; se P51 non passa o non può leggere i dati, consumo `null`
  e diagnostica, senza trasformare il dato in zero. Slot senza record restano
  senza consumo e non richiedono una directory.
- **Accettazione:** prova integrata con selezione P54 per misura completa,
  categoria assente, stream non leggibile e slot non avviati; i record e i
  file sorgente restano invariati. Nessuna run o chiamata LLM.

## Implementazione e verifiche

`scripts/current/Attach-OfficialUsage.cjs` applica `checkRun` a ogni record
valido già selezionato, usando la directory fornita dal chiamante. Produce
`usageComplete`, `totalTokens` e `usageIssues` per slot. Un controllo fallito,
uno stream illeggibile o una directory non risolvibile producono `null` e
diagnostica; gli slot senza record valido non vengono letti.

Comandi eseguiti con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Attach-OfficialUsage.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0; 5 test passati, 0 falliti. La nuova prova integra P54 e
P51 su 54 slot sintetici, inclusi tre record validi con stream completo,
categoria mancante o stream assente, e verifica la gestione di un errore del
resolver e l'assenza di mutazione della selezione. Usa file temporanei rimossi
dal test; nessun dato dei piloti è stato modificato.

## Limiti e lavoro rimanente

La funzione presuppone selezioni già validate e una risoluzione fidata dei
percorsi: non verifica hash o autenticità dei record né la custodia dell'anchor.
P51 non dimostra che la CLI abbia emesso tutti i consumi o che la lista degli
agenti sia completa. Non sono ancora collegati i riepiloghi dell'evaluator,
né calcolati correttezza e aggregati; il collegamento non è integrato in una
CLI o nel flusso delle run. Il passo non certifica la readiness e non avvia
run ufficiali; gli input congelati non sono cambiati.
