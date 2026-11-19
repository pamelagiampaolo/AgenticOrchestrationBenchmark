# P63 — Conteggi canonici per il riepilogo

## Obiettivo e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** ottenere i conteggi attesi delle suite per il comando P62 dai
  manifest evaluator congelati, senza leggere i test riservati.
- **Componenti:** manifest pubblici e riservati, hash P35, riepiloghi dei sei
  piloti e file JSON dei conteggi nel repository benchmark.
- **Comportamento:** usare i conteggi per task dichiarati dai manifest con
  hash corrispondenti a P35; verificare i conteggi dei task già valutati nei
  piloti, senza considerarli risultati ufficiali.
- **Accettazione:** sei task con numeri positivi e confronto positivo per i sei
  piloti; confronto SHA-256 dei due manifest e controllo del JSON prodotto.
  Nessuna run, chiamata LLM o modifica degli input congelati.

## Metodo e risultati

Gli hash SHA-256 calcolati con `Get-FileHash -Algorithm SHA256` coincidono con
P35: pubblico `dc5f7dbb2d4e8d8811e6e5335391f76199b7b1c1c1ba0635059672317abc7e5b`,
riservato `ce05f45cb2e93ee0e499abd86102cf8e8c2505f7029c83b099c8ddeecb8bad09`.
La funzione `Get-PublicScopeNames` del valutatore specifica che T1–T4 usano
gli scope `common` e task, mentre T5 e T6 usano soltanto lo scope del task,
che include già i test comuni. Sono stati letti i soli conteggi nei manifest.

| Task | Pubblici | Riservati |
| --- | ---: | ---: |
| T1 | 31 | 3 |
| T2 | 31 | 3 |
| T3 | 30 | 3 |
| T4 | 31 | 3 |
| T5 | 30 | 3 |
| T6 | 26 | 4 |

Una verifica PowerShell ha confrontato gli attesi nei sei riepiloghi pilota
con i conteggi derivati: `matchedPilots=6`, exit code 0. I piloti coprono
soltanto T1 e T6. I valori T2–T6 coincidono anche con quelli presenti nel codice
dei test di accettazione evaluator consultato; quei test non sono stati
rieseguiti in questo passo. Il file
`docs/benchmark/benchmark-design/official-canonical-counts-aee8d11.json`
contiene i sei conteggi nel formato richiesto da P62.
Una seconda lettura ha confrontato tutte le sei voci JSON con i manifest:
`counts=6 verified`, exit code 0; SHA-256 del file conteggi
`9deeb1b3b950172420c11d5c4bce3beaa62f56737d44158c00f67f6923e643fe`.
Con Node congelato, `node --test tests/report-official-results.test.cjs
tests/compute-official-pass-rates.test.cjs` ha riportato 2 test passati,
0 falliti, exit code 0; `git diff --check` non ha segnalato errori.

Il primo confronto, con exit code 1, sommava erroneamente solo lo scope del
task e ha rilevato una discordanza su T1. La lettura delle regole evaluator ha
permesso di correggere la formula prima di scrivere il file. Una ricerca con
glob di percorso Windows è fallita ed è stata ripetuta con `-g`.

## Limiti e lavoro rimanente

La corrispondenza degli hash accerta i byte dei manifest rispetto a P35, non
riesegue le suite né verifica automaticamente il contenuto di tutti i file
referenziati. Le valutazioni reali dei piloti non coprono T2–T5. Il comando
P62 riporta l'hash del file conteggi ma non ne autentica automaticamente la
provenienza; è necessario verificare il file al momento dell'uso. Resta la
verifica della procedura operativa e il controllo finale degli altri freeze.
