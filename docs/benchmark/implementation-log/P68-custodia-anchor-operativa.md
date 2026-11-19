# P68 — Custodia operativa dell'anchor

## Obiettivo e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** fissare una procedura proporzionata di custodia del registro
  P65 e del suo ultimo hash, senza richiedere servizi aggiuntivi.
- **Componenti:** directory distinte per snapshot e anchor, hash restituito da
  P65, comunicazione nella conversazione e procedure di arresto.
- **Accettazione:** percorsi distinti e non annidati verificati, prova esistente
  di due revisioni e alterazioni rieseguita, limiti e recupero espliciti.
  Nessuna run, chiamata LLM o creazione di directory ufficiali in questo passo.

## Procedura designata

Per questa esecuzione sono designate le directory:

- snapshot: `C:\AgenticAIResults\official-audit-aee8d11`;
- copie locali degli anchor: `C:\AgenticAIResults\official-anchors-aee8d11`.

Non sono directory annidate. Al momento dell'esecuzione dovranno essere create
come directory nuove e verificabili; se esistono già, fermarsi e ispezionare
prima di usarle, senza sovrascrivere o eliminare nulla. Conservare le evidenze
fuori dall'area agente, nella directory risultati fidata.

Per la prima revisione usare `none` come hash precedente. Dopo ogni salvataggio
P65 riuscito, l'operatore riporta in questa conversazione **revisione, SHA-256
e percorso dell'anchor**, e usa quell'hash riportato come riferimento per la
chiamata successiva. Per riprendere dopo un'interruzione, confrontare l'ultimo
hash riportato nella conversazione con l'anchor e lo snapshot, quindi passarlo
a P65; non promuovere automaticamente a riferimento fidato un valore letto
soltanto dalle due directory locali. Se l'ultimo hash non è recuperabile,
se manca una scrittura o se i valori differiscono, fermarsi e conservare le
evidenze per revisione. Nessuna nuova esecuzione o sostituzione automatica.

## Limiti

Le due directory sono sullo stesso volume e non costituiscono archivi
indipendenti. La conversazione è una copia esterna al filesystem locale, ma
non è una firma crittografica né una garanzia di disponibilità o immutabilità:
il controllo dipende dal recupero affidabile del messaggio e dalla protezione
dell'account. P65 non può garantire che il processo non si interrompa tra
snapshot e anchor. La procedura non certifica isolamento o autenticità delle
classificazioni, né modifica la limitazione B35 accettata dall'utente.

## Verifiche eseguite

Un controllo PowerShell in sola lettura ha confrontato i percorsi assoluti e
la directory padre; output `distinct=True auditExists=False anchorExists=False`,
exit code 0. Non ha creato le directory; la scrittura in queste aree esterne
al repository dipende dai permessi disponibili al momento dell'esecuzione.

Con Node congelato e `OFFICIAL_PLAN_PATH` impostato al piano B33, il comando
`node --test tests/official-audit-anchor.test.cjs
tests/official-audit-workflow.test.cjs tests/official-audit-save.test.cjs
tests/official-audit.test.cjs` ha dato 4 test passati, 0 falliti e 0 saltati,
exit code 0. Le prove usano file temporanei simulati, non dimostrano la
disponibilità futura della conversazione né la custodia effettiva di anchor
ufficiali. L'ultima verifica degli hash e dell'ambiente rimane da eseguire
vicino all'avvio delle run.
