# A7 — Runtime delle deleghe statiche

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** collegare il piano A6 al runtime dei processi usato dalla
  gerarchia adattiva, mantenendo ruoli e ordine sotto controllo meccanico.
- **Componenti:** `WorkerDelegationHandler`, policy statica e test con processi
  CLI finti reali.
- **Accettazione:** tre processi con identità e ruoli fissi, un solo controllore
  globale, arresto dopo fallimento e nessun avvio del verifier in quel caso.

## Implementazione

`WorkerDelegationHandler` accetta ora, oltre al comportamento adattivo
preesistente, selettori deterministici di identificatore e ruolo. Il costruttore
originale conserva `worker-01`, `worker-02`, ... e ruolo `worker`, quindi la
gerarchia adattiva non cambia semantica.

`StaticDelegationHandler` configura lo stesso runtime con il piano A6:

| Sequenza | AgentId | Ruolo |
| --- | --- | --- |
| 1 | `analyst` | `analyst` |
| 2 | `implementer` | `implementer` |
| 3 | `verifier` | `verifier` |

Le richieste arrivano ancora dal canale MCP del coordinatore, ma identificatore,
ruolo, numero massimo e prosecuzione non sono scelti dal modello. Dopo un
fallimento o dopo il verifier, richieste ulteriori vengono rifiutate.

Tutti i processi ricevono lo stesso `RunLimitController`: token e tempo restano
quindi globali come richiesto da B4.

## Verifiche osservate

- build iniziale: test verdi ma un warning nullable nel test dei token;
- warning corretto senza soppressioni;
- tre processi finti avviati in ordine con ruoli distinti;
- uso totale osservato: 30 token, somma dei tre lavoratori;
- eventi di delega completati: 3;
- implementer con exit code 3: verifier non avviato e richiesta successiva
  rifiutata;
- test mirati comprensivi della gerarchia adattiva: 15/15.

## Limiti e prossimo passo

Il runtime dei lavoratori è collegato, ma manca ancora il runner completo con il
processo coordinatore, la composizione delle istruzioni specifiche dei ruoli, il
record `hierarchical-static` e il comando `run-static-hierarchy`.

La prossima iterazione realizzerà questi elementi riusando configurazioni MCP,
hash, metriche e controlli di integrità già verificati per A5.
