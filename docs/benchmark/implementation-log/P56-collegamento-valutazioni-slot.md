# P56 — Collegamento delle valutazioni agli slot ufficiali

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** associare ai record P54 i riepiloghi del valutatore fidato,
  senza interpretare guasti o valutazioni mancanti come test falliti.
- **Componenti:** selezione P54, riepiloghi evaluator, funzione esterna e test
  sintetici. Il contenuto dei test riservati non viene letto.
- **Comportamento:** accettare il collegamento solo con `runId`, `taskId`,
  schema, stato/codice e indicatori d'integrità coerenti; gli altri casi
  restano non disponibili con diagnostica. Timeout e stati non conclusivi
  richiedono revisione e non sono convertiti in fallimenti funzionali.
- **Accettazione:** provare associazione, errori evaluator, integrità,
  identificativi discordanti, dati mancanti e slot non avviati con P54;
  non modificare record o input congelati. Nessuna chiamata LLM.

## Implementazione e verifiche

`scripts/current/Attach-OfficialEvaluation.cjs` collega un riepilogo a uno
slot già selezionato quando identità, schema, esito e codice concordano e i
tre indicatori d'integrità sono veri. Accetta come conclusi `passed`,
`test-failures` e `build-failure`; gli altri stati sono lasciati in revisione.
Restituisce `evaluationLinked`, il riepilogo o `null` e la diagnostica.

Comandi con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Attach-OfficialEvaluation.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/attach-official-evaluation.test.cjs tests/select-official-attempts.test.cjs tests/attach-official-usage.test.cjs tests/check-run-usage.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0: 6 test passati, 0 falliti. La nuova prova collega P54 a
riepiloghi sintetici e verifica tre esiti conclusi, errori, timeout,
integrità non confermata, identità discordanti, assenza dei dati e slot non
avviati. Verifica inoltre che la selezione non sia mutata.

## Limiti e lavoro rimanente

Il collegamento non certifica il riepilogo: il chiamante deve fornirlo da una
fonte fidata e verificarne provenienza, hash e associazione alla submission.
La funzione non valida il contenuto dei conteggi né il manifest, non calcola
correttezza o aggregati, non integra l'esecuzione in una CLI e non dimostra
la completa indipendenza dei test riservati. Gli esiti non conclusivi sono
segnalati per revisione senza stabilirne automaticamente la causa. Gli input
congelati restano invariati, i piloti non sono stati ripetuti.

Una prima applicazione della modifica a questa documentazione è fallita per
un refuso nel contesto; la successiva è riuscita.
