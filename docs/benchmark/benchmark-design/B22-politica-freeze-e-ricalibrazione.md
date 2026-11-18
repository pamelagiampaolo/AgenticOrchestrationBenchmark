# B22 — Politica di freeze finale e ricalibrazione

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** readiness precedente alle run ufficiali.
- **Stato:** politica definita; freeze finale e nuovi piloti non ancora eseguiti.
- **Autorizzazione operativa:** questo documento non autorizza chiamate LLM,
  nuovi piloti o run ufficiali.

## Obiettivo

Impedire che le 54 run ufficiali partano da una configurazione diversa da quella
usata per calibrare tempo e token. Il freeze deve avvenire una sola volta, dopo
la chiusura delle attività di readiness e prima dei nuovi piloti.

## Motivo della ricalibrazione

I sei piloti B13–B18 hanno usato il commit
`2d234ec410c89e7fb4999534e4f57ad8e4a4c2af`. I record originali conservati in
`C:\AgenticAIResults` riportano hash di istruzioni differenti da quelli presenti
nello stato ispezionato il 18 novembre 2026:

| Istruzione | Hash nei piloti B13–B18 | Hash ispezionato il 18 novembre 2026 |
| --- | --- | --- |
| Comune | `56ee035ee77ced33e5e474c96167a82ed7e175ede3d004ad62dccabe47673da5` | `91d28494ce8bff89139a975f95897c42e447bee2f925117ae6eb6b5cccf2ddd3` |
| Single-agent | `9a6354e54fd2861e640787d2fd0eb4919d292277b22425f905e51f3b97e8ddab` | `b2ea314300b579c10d280c3f25e1fc1676b538127d2094c7ef9feaee9697a7d0` |
| Coordinatore adattivo | `e3d28a1ce3afe495e1da4deb4d7f2f0a900ee67bd9dc873c814681bd1c393177` | `ae2ff6c8730eeb0e25d547d0ad96637e056fc5502eade2bbbf66908aea413f79` |

Il codice e i test del benchmark TypeScript risultano rinominati senza modifiche
di contenuto rispetto al commit dei piloti, ma istruzioni differenti possono
cambiare strategia, deleghe, durata e token. Il `package-lock.json` ispezionato
ha inoltre SHA-256
`586b704e70cb3fcf9e99a889d627fae98c6880ce042247cd4d37471594b5e739`,
diverso dal lockfile
`1b283562b7e02de0664c74c6f59de9456ab7d3cee211ae7a2bdc6111e333bccd`
registrato dalla toolchain congelata precedente.

Di conseguenza, i valori B19 di 150 secondi e 250.000 token sono evidenze
storiche e non devono essere applicati automaticamente alla configurazione
finale.

## Sequenza obbligatoria

1. Completare le attività di readiness senza modificare in anticipo le soluzioni
   dei task sperimentali.
2. Verificare working tree pulito, build, test e prove di accettazione previste.
3. Designare un singolo commit finale come baseline e harness delle run.
4. Registrare gli hash di benchmark, specifiche T1–T6, istruzioni, eseguibili,
   configurazioni MCP e materiali pubblici.
5. Creare o designare la toolchain definitiva dal lockfile congelato e
   verificarne integralmente il manifest.
6. Congelare evaluator, test riservati, schema dei record e protocollo dei nuovi
   piloti senza esporre materiali riservati agli agenti.
7. Ottenere l'autorizzazione esplicita dell'utente alle chiamate LLM.
8. Eseguire sei nuovi piloti: T1 e T6 per single-agent, gerarchia statica e
   gerarchia adattiva, tutti sulla configurazione finale.
9. Valutare ogni pilota con l'evaluator fidato e verificare completezza delle
   metriche, integrità e comparabilità.
10. Applicare la regola di calibrazione approvata prima dei nuovi piloti e
    documentare i nuovi limiti.
11. Ottenere l'approvazione esplicita dei limiti e soltanto dopo autorizzare le
    54 run ufficiali.

## Invarianti del freeze

- Piloti e run ufficiali devono usare lo stesso commit, istruzioni, modello,
  toolchain, configurazioni MCP, evaluator e materiali di test.
- Il modello richiesto è esattamente `claude-sonnet-5-5`; alias, differenze di
  maiuscole e altri modelli non appartengono alla configurazione congelata e
  devono essere rifiutati prima dell'avvio della CLI.
- Qualunque modifica successiva a un elemento congelato invalida il freeze e
  richiede una valutazione esplicita dell'impatto; se può influire su
  comportamento, durata, token o valutazione, i piloti devono essere ripetuti.
- I risultati B13–B19 restano evidenze storiche e non vengono cancellati o
  reinterpretati come risultati della nuova configurazione.
- Nessun hash del futuro freeze viene anticipato: deve essere ricavato dallo
  stato realmente approvato e usato.

## Criteri di completamento

Questa politica sarà soddisfatta soltanto quando esisteranno evidenze osservate
per tutti i passaggi della sequenza obbligatoria. La sola presenza di documenti,
meccanismi di confinamento o test unitari non certifica la readiness completa.
