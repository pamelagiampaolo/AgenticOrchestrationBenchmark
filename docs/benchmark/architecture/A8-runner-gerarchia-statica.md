# A8 — Runner completo della gerarchia statica

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** integrare coordinatore e tre lavoratori statici nel runner
  gerarchico condiviso.
- **Componenti:** `HierarchicalRunRunner`, policy A7, istruzioni del coordinatore,
  prompt specifici dei ruoli, record e test end-to-end con CLI finta.
- **Accettazione:** quattro processi registrati, tre deleghe obbligatorie, budget
  globale, istruzioni corrette e fallimento se il piano resta incompleto.

## Riutilizzo del runtime

`HierarchicalRunRunner.CreateStatic` configura lo stesso runtime usato dalla
gerarchia adattiva. Restano condivisi:

- preparazione e verifica d'integrità;
- profili CLI e configurazioni MCP;
- controllore globale di token e tempo;
- raccolta di stream, metriche ed eventi;
- terminazione dei processi;
- formato del record gerarchico.

La modalità adattiva conserva il costruttore e il comportamento precedenti. La
modalità statica cambia soltanto architettura, istruzioni e policy di delega.

## Istruzioni e processi

Il coordinatore riceve `static-coordinator.md`, che impone esattamente tre
deleghe bloccanti. Ogni processo lavoratore riceve `common-environment.md` più il
proprio file A6, salvati anche nella sua directory dei risultati come
`system-prompt-append.md`.

Il record statico usa:

- architettura `hierarchical-static`;
- `maxDelegations` pari a 3;
- agenti `coordinator`, `analyst`, `implementer`, `verifier`;
- subtotali token distinti per ruolo;
- gli stessi hash aggregati e deterministici delle condizioni gerarchiche.

Se il coordinatore termina con successo prima di completare i tre ruoli, lo
stato viene corretto in `cli-failed` e `stopReason` indica che il piano statico è
incompleto. Un completamento testuale del coordinatore non può quindi mascherare
un'architettura eseguita solo in parte.

## Verifiche osservate

- build: 0 avvisi, 0 errori;
- test mirati statici e regressione adattiva: 17/17;
- run finta completa: tre deleghe completate, quattro agenti nel record;
- ordine e ruoli: coordinator, analyst, implementer, verifier;
- concorrenza massima dei lavoratori: 1;
- istruzione specifica presente nella directory di ciascun lavoratore;
- coordinatore terminato dopo analyst: stato `cli-failed`, implementer e verifier
  assenti.

## Limiti e prossimo passo

Il runner è disponibile come API ma non è ancora raggiungibile dalla riga di
comando. La prossima iterazione aggiungerà `run-static-hierarchy`, parsing degli
stessi input della modalità adattiva senza `--max-delegations`, instradamento in
`Program.cs` e test del comando.
