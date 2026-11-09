# A6 — Piano meccanico della gerarchia statica

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** fissare in codice ruoli e sequenza della terza architettura,
  prima di collegarla alle sessioni Claude CLI.
- **Componenti:** piano statico, schedulatore sequenziale, istruzioni dei ruoli e
  test automatici.
- **Comportamento atteso:** avviare sempre `analyst`, `implementer`, `verifier`,
  in quest'ordine e una sola volta ciascuno.
- **Accettazione:** ordine deterministico, massimo un ruolo attivo, arresto al
  primo fallimento e riepilogo obbligatorio per ogni successo.

## Decisione approvata

La gerarchia statica usa tre lavoratori obbligatori:

1. **analyst:** legge specifica, codice e test pubblici; non modifica file e
   produce il piano per l'implementer;
2. **implementer:** modifica il workspace e usa i comandi di sviluppo MCP;
3. **verifier:** esegue typecheck e test pubblici, corregge i difetti osservati e
   ripete le verifiche.

I tre ruoli condividono modello, workspace e budget globale. Sono sequenziali e
non possono delegare. Il nome registrato dell'architettura è
`hierarchical-static`.

## Implementazione

`StaticHierarchyPlan` espone il solo piano approvato con identificatori stabili e
file di istruzioni versionati. `StaticHierarchyScheduler` invoca un passaggio
alla volta. Non decide dinamicamente ruoli, numero o ordine.

Un passaggio dichiarato riuscito senza riepilogo viene normalizzato come
fallimento. Un fallimento senza motivo riceve una diagnostica esplicita. Dopo il
primo fallimento nessun ruolo successivo viene avviato.

Le istruzioni sono separate in:

- `Instructions/static-analyst.md`;
- `Instructions/static-implementer.md`;
- `Instructions/static-verifier.md`.

## Verifiche osservate

- formattazione completata;
- build: 0 avvisi, 0 errori;
- test mirati: 4/4;
- ordine osservato: `analyst`, `implementer`, `verifier`;
- massimo passaggi contemporanei: 1;
- fallimento dell'implementer: verifier non avviato;
- riepilogo vuoto su successo: esecuzione classificata fallita.

## Limiti e prossimo passo

Questa iterazione certifica il piano e lo schedulatore, non una run statica. Non
sono ancora implementati il comando `run-static-hierarchy`, l'avvio dei tre
processi Claude CLI, il passaggio dei riepiloghi tra ruoli né il record finale.

La prossima iterazione collegherà lo schedulatore al runner gerarchico condiviso,
applicando un solo controllore di tempo e token a tutti e tre i lavoratori.
