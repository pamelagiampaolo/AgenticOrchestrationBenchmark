# P13 — Spike del pool Vitest in-process

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** spike completato; soluzione respinta.
- **Obiettivo:** verificare se un pool in-process basato esclusivamente sulle API
  pubbliche di Vitest 5.1.0 elimina il timeout osservato in P12 senza cambiare
  runner, casi o reporter.

Lo spike non modifica la baseline né la distribuzione Vitest. Non introduce
strumenti MCP e non viene considerato una valutazione valida dei task.

## API pubbliche verificate

Il pacchetto installato esporta pubblicamente:

- `PoolRunnerInitializer`, `PoolWorker` e `startVitest` da `vitest/node`;
- `init`, `runBaseTests` e `setupEnvironment` da `vitest/worker`.

È quindi possibile costruire senza import interni un `PoolWorker` locale con un
trasporto a eventi. Il worker implementato nello spike:

- usa un solo processo e un solo pool;
- inoltra richieste e risposte con `EventEmitter` e `queueMicrotask`;
- delega raccolta ed esecuzione a `runBaseTests`;
- delega l'ambiente a `setupEnvironment`;
- mantiene il reporter JSON canonico.

## Due prove distinte

### Configurazione CLI

Il pool è stato prima fornito tramite configurazione ESM fidata. Il processo è
andato in timeout prima che il worker ricevesse una richiesta.

### Launcher programmatico

Per eliminare completamente parsing CLI e caricamento della configurazione è
stato creato un launcher fidato che invoca:

```text
startVitest(filters, options, { configFile: false })
```

Le opzioni inline fissano root, due file pubblici comuni, ambiente Node, pool
in-process, un solo worker, timeout e reporter JSON. Anche questa variante va in
timeout senza stdout, stderr o report e prima che il trasporto del pool osservi
messaggi.

## Risultato

Lo spike non supera il criterio di accettazione. Il blocco avviene nella
preparazione comune `startVitest`/Vite, prima dell'avvio del pool. Sostituire il
pool non può quindi risolvere la compatibilità AppContainer osservata.

Per scendere sotto `startVitest` sarebbe necessario ricostruire manualmente
server, trasformazione, raccolta, RPC e stato del runner oppure importare chunk
interni con nomi hashati. Entrambe le opzioni:

- dipendono da dettagli non pubblici della versione;
- rischiano di divergere dal runner canonico;
- rendono più difficile dimostrare comparabilità ed equivalenza dei 27 casi;
- violano il criterio di abbandono stabilito in P12.

La strada del pool personalizzato viene quindi chiusa e non va integrata nel
worker sperimentale.

## Evidenza osservata

La prova programmatica finale ha mantenuto positivi tutti i controlli non
Vitest già osservati in P12:

- typecheck e build con exit code `0`;
- CLI/versione Vitest disponibile;
- Worker thread Node e processo figlio funzionanti;
- accessi esterni e junction negati;
- DNS, TCP e HTTP negati;
- ambiente allowlist e baseline invariata.

La chiamata `startVitest` è stata terminata dal Job Object al timeout con exit
code `124`; il report pubblico non è stato prodotto.

Evidenza non versionata:

```text
%TEMP%\p12-run-64b94a7ee8ca40fda755a2df1b93d10c\evidence\p12-results.json
```

## Decisione necessaria

Il worker confinato non può usare direttamente AppContainer con Vitest 5.1.0
nella configurazione verificata. Restano due direzioni tecnicamente coerenti:

1. **HCS/container Windows:** confine più forte e Vitest non modificato; maggiore
   complessità di preparazione e gestione dell'immagine.
2. **Identità Windows dedicata:** utente temporaneo o preconfigurato, ACL
   deny-by-default, ambiente allowlist, Job Object e blocco rete; più semplice da
   integrare ma richiede dimostrare che nessun percorso leggibile dell'host o
   servizio locale rimanga raggiungibile.

La raccomandazione è provare prima HCS, perché conserva il runner canonico e offre
un confine di filesystem e rete più completo. Questa scelta costituisce però un
cambiamento architetturale significativo e richiede approvazione esplicita prima
di implementazione o modifiche di sistema.
