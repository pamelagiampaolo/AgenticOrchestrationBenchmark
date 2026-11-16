# B6 — Protocollo minimo approvato per i piloti

## Stato

- **Data:** 9 novembre 2026.
- **Stato:** protocollo approvato e congelato dall'utente il 9 novembre 2026;
  i piloti sono autorizzati, ma non ancora avviati.
- **Obiettivo:** calibrare un unico limite di tempo e token per le tre
  architetture senza duplicare le 54 run ufficiali.

## Workload e numero di prove

Il protocollo usa due estremi già congelati del benchmark:

- **T1:** modifica semplice e circoscritta;
- **T6:** modifica complessa, trasversale e scomponibile.

Ogni workload viene eseguito una volta con ciascuna architettura. Sono quindi
previsti **6 piloti validi**:

| Task | Single-agent | Gerarchia statica | Gerarchia adattiva |
| --- | ---: | ---: | ---: |
| T1 | 1 | 1 | 1 |
| T6 | 1 | 1 | 1 |

Le prove tecniche A3, P25 e A10 non vengono riutilizzate: precedono il freeze e
non includono tutte le condizioni.

## Ordine e seed

Seed approvato: `d9dc66a`, commit che ha esposto la terza architettura.

L'ordine base si ottiene ordinando lessicograficamente gli SHA-256 di
`<seed>|<architecture>`:

1. `hierarchical-adaptive` — `3492ec0a...`;
2. `hierarchical-static` — `4686608b...`;
3. `single-agent` — `a8fbdcc2...`.

T1 usa l'ordine base; T6 applica una rotazione sinistra di una posizione:

1. T1 adaptive;
2. T1 static;
3. T1 single-agent;
4. T6 static;
5. T6 single-agent;
6. T6 adaptive.

Ogni architettura occupa due posizioni diverse. La procedura e gli hash rendono
l'ordine riproducibile senza sceglierlo dopo aver osservato risultati.

## Parametri iniziali dei piloti

- modello richiesto: `claude-sonnet-5-5`;
- limite tecnico per pilota: **900 secondi**;
- budget tecnico per pilota: **1.000.000 totalTokens**;
- massimo deleghe adattive: **3**;
- gerarchia statica: esattamente 3 deleghe;
- periodo tecnico di arresto: **0 secondi**; al limite il controllore termina
  immediatamente l'intero albero di processi;
- stessa macchina, commit, toolchain e configurazione MCP;
- sessioni e aree nuove, nessun riuso di workspace o conversazioni.

I limiti iniziali sono tetti di calibrazione, non i limiti ufficiali. Se un
pilota li raggiunge, la calibrazione è inconclusiva e non vengono aumentati
automaticamente dopo aver visto il risultato.

## Regola approvata per i limiti ufficiali

La regola viene approvata prima dei piloti e applicata una sola volta ai sei
piloti validi:

```text
timeLimit = arrotondaPerEccesso(max(agentTaskDurationMs) × 1,25, a 30 secondi)
tokenBudget = arrotondaPerEccesso(max(totalTokens) × 1,25, a 50.000 token)
```

Il massimo è calcolato congiuntamente sulle tre architetture e sui due task. Non
si usano medie, risultati ufficiali o limiti separati per architettura.

Un pilota è valido per la calibrazione se infrastruttura, configurazione,
integrità e misurazione sono valide, anche se la submission non è corretta. Un
errore funzionale è un risultato dell'architettura e non autorizza la ripetizione.

## Ripetizioni ammesse

Un tentativo viene ripetuto soltanto per uno di questi eventi osservabili:

- rate limit o indisponibilità del fornitore;
- processo non avviato per errore dell'host;
- server MCP o relay non connesso per guasto infrastrutturale;
- record o metriche mancanti per errore dell'harness;
- violazione d'integrità non causata dall'agente.

È consentita al massimo una ripetizione per slot. Sei piloti validi richiedono
quindi da 6 a un massimo di 12 tentativi. Entrambi i tentativi vengono conservati
e il primo resta classificato come errore infrastrutturale.

Timeout, token esauriti, configurazione rifiutata per comportamento effettivo
della CLI, errori del modello e test falliti non autorizzano una ripetizione.

## Valutazione e non contaminazione

- Durante la run l'agente vede soltanto test pubblici e propri.
- Il valutatore riservato viene eseguito dopo il congelamento della submission.
- Nessun dettaglio riservato viene restituito agli agenti o inserito nei prompt
  successivi.
- Submission, manifest, stream, audit e record vengono conservati separatamente.
- Cache e posizione nell'ordine vengono registrate, non interpretate come nuovo
  input.

## Istruzioni congelate

La revisione non ha trovato suggerimenti funzionali specifici dei task. Le
differenze descrivono soltanto l'architettura. Hash SHA-256 congelati:

| File | SHA-256 |
| --- | --- |
| `common-environment.md` | `4bdf3fa045e78e478f5f2dfa90537f0d994f227d48277d9ec1d93c211a6b5cb4` |
| `single-agent.md` | `307cee03bf688fb1eccb48b0eb94f951cc498c2c464fd94b18652a72a3af8222` |
| `coordinator.md` | `90c5d9c8925e8b5b70e292c3887573fba0e4a845d36463ca7f1e0642e8b0358d` |
| `worker.md` | `f792ce4e580e66552c7acec42cb32ec91c52ae5cf3adba9b88d02e962aac9e36` |
| `static-coordinator.md` | `6cb60e00ff7c95da3f408e10937f2484e616280acc5d6d7605358fc333425cf1` |
| `static-analyst.md` | `71ddc9a65162f00073f63864f134c053eefd2537d60a906af3af82867a73af8d` |
| `static-implementer.md` | `ac82d463091f5e5dcb45f18c82e86e93f1479e7562ba442df578e0f189edbdca` |
| `static-verifier.md` | `816f5db0256cabad8ea1509bed3b46c06bad6f02b95be23eb27e6d64ce2a0ac6` |

## Decisioni approvate e congelate

L'utente ha approvato insieme, prima del primo pilota:

1. 6 piloti validi su T1 e T6;
2. ordine e seed sopra indicati;
3. tetti iniziali 900 secondi e 1.000.000 token;
4. massimo 3 deleghe adattive;
5. margine del 25% e arrotondamenti;
6. una sola ripetizione per errore infrastrutturale;
7. freeze degli otto file di istruzioni elencati;
8. periodo tecnico di arresto nullo e schema degli stati già implementato
   (`completed`, limiti singoli o congiunti, configurazione rifiutata,
   misurazione incompleta, fallimento CLI).

Qualunque modifica successiva a questi elementi invalida il freeze e richiede
una nuova approvazione prima di continuare.
