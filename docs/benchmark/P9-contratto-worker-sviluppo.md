# P9 — Contratto del worker per build e test di sviluppo

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** contratto progettuale definito; worker e isolamento di processo non
  ancora implementati né verificati.
- **Obiettivo:** fissare composizione, comandi, input, output e confini del worker
  che fornirà feedback di sviluppo all'agente senza esporre una shell generale.

Questo worker non è il valutatore ufficiale descritto in B3. Non riceve test
riservati, non produce il punteggio finale e non può essere considerato un
confine sicuro finché il meccanismo di isolamento del processo non supera le
prove di accettazione previste in questo documento.

## Decisione sulla composizione

Ogni invocazione usa una nuova area effimera, esterna alla root MCP dell'agente:

```text
worker-root/<invocation-id>/
├── project/                         directory di lavoro del processo
│   ├── src/                         copia congelata da workspace/src
│   ├── tests/
│   │   ├── public/                  copia da public-reference/tests/public
│   │   └── agent/                   copia da agent-tests/tests/agent
│   ├── package.json                 copia dalla workspace
│   ├── package-lock.json            copia dalla workspace
│   └── tsconfig.json                copia dalla workspace
├── toolchain/                       copia fidata delle dipendenze bloccate
├── config/                          configurazioni generate dal controllore
└── output/                          solo report e log previsti
```

La composizione avviene in questo ordine:

1. il controllore verifica l'integrità di `public-reference/`;
2. copia `workspace/` in `project/`, rifiutando collegamenti e percorsi vietati;
3. copia `public-reference/` preservandone i percorsi relativi, quindi
   `public-reference/tests/public/...` diventa `project/tests/public/...`;
4. copia `agent-tests/` preservandone i percorsi relativi, accettando soltanto
   `tests/agent/...`;
5. aggiunge toolchain e configurazioni da copie fidate, mai tramite junction,
   symlink o riferimenti alla loro posizione originale;
6. registra gli hash dell'input composto prima di avviare il processo.

La conservazione del prefisso `tests/public/` è necessaria: i test canonici
esistenti importano `src/index.js` con percorsi relativi calcolati da quella
posizione. La stessa convenzione rende esplicita la destinazione dei test creati
dall'agente: nell'area MCP devono essere salvati sotto
`agent-tests/tests/agent/`.

In caso di collisione tra le tre sorgenti la preparazione fallisce. Nessun file
della workspace può sostituire test o configurazioni aggiunti dal controllore.

## Superficie MCP prevista

Il server non espone un comando generico, argomenti arbitrari o una shell. Gli
strumenti candidati sono operazioni distinte:

- `run_typecheck`;
- `run_build`;
- `run_public_tests`;
- `run_agent_tests`.

Ogni strumento seleziona internamente eseguibile, argomenti, configurazione,
directory di lavoro, timeout e schema del risultato. Stringhe provenienti
dall'agente non vengono concatenate in una command line.

Il controllore invoca direttamente le versioni fissate di Node.js, TypeScript e
Vitest. Non esegue script npm dalla `package.json` modificabile e non installa o
aggiorna dipendenze durante una run. `run_public_tests` usa esclusivamente la
configurazione generata dal controllore per la suite comune e per il solo task
della run. `run_agent_tests` seleziona esclusivamente `tests/agent/` e il suo
esito non contribuisce alla valutazione ufficiale.

## Risultati restituiti all'agente

Ogni chiamata restituisce un risultato strutturato con almeno:

- tipo dell'operazione;
- stato `completed`, `failed`, `timeout` oppure `infrastructure-error`;
- exit code, se il processo è stato avviato;
- durata;
- conteggi dei test per le due operazioni Vitest;
- diagnostica standardizzata e limitata in dimensione.

I percorsi assoluti vengono rimossi dalla diagnostica. Il risultato non include
variabili d'ambiente, command line completa, percorsi fidati, contenuto di file
esterni o dettagli dei test riservati. stdout e stderr grezzi rimangono negli
output controllati soltanto se la prova dimostra che possono essere acquisiti
senza divulgare dati dell'host.

## Confine di esecuzione obbligatorio

La validazione MCP dei percorsi non confina Node.js né il codice importato da
Vitest. Directory di lavoro, copia effimera e Job Object non costituiscono da
soli un confine di sicurezza.

Il processo e tutti i figli devono quindi essere avviati con un'identità o token
Windows distinto che:

- possa leggere soltanto runtime e copia effimera necessari;
- possa scrivere soltanto nei percorsi temporanei autorizzati;
- non possa leggere repository, root delle run, risultati persistenti, profilo
  utente, configurazioni Claude, credenziali o copie fidate originali;
- non disponga di rete;
- riceva un ambiente costruito da allowlist, senza segreti;
- sia racchiuso in un limite di tempo e in un gruppo di processi terminabile per
  intero.

Il meccanismo Windows concreto non viene scelto per assunzione. La prossima
iterazione deve confrontare le capacità disponibili e provare il candidato
minimo con sentinelle sintetiche. Se richiede installazioni, servizi, utenti
persistenti o modifiche di sistema, sarà necessaria un'approvazione separata.

## Criteri di accettazione dell'implementazione futura

### Composizione e determinismo

1. La stessa combinazione di input produce lo stesso manifest della composizione.
2. I test pubblici mantengono `tests/public/...` e risolvono gli import di `src`.
3. Soltanto `tests/agent/...` proviene dall'area dei test dell'agente.
4. Collisioni, symlink, junction e percorsi vietati fanno fallire la preparazione.
5. Configurazioni e toolchain provengono da copie fidate con hash registrati.

### Superficie di esecuzione

6. L'inventario MCP contiene soltanto le operazioni approvate.
7. Non è possibile scegliere eseguibile, argomenti, working directory o config.
8. I quattro comandi usano versioni fissate e non eseguono script npm.
9. Timeout e processi figli vengono terminati e classificati correttamente.
10. Fallimento del test e errore infrastrutturale restano distinti.

### Isolamento

11. Codice controllato nella submission non legge sentinelle in repository,
    risultati, profilo, configurazioni o area fidata.
12. Lo stesso codice non raggiunge tali aree tramite percorso assoluto, traversal,
    junction, symlink o processo figlio.
13. Connessioni di rete in uscita e risoluzione DNS falliscono.
14. Ambiente e processi figli non espongono credenziali o variabili sensibili.
15. L'unico contenuto persistito è quello previsto dallo schema di output.
16. Una nuova invocazione non vede file o risultati della precedente.

## Evidenze raccolte in questa iterazione

Sono stati ispezionati:

- `BenchmarkTarget/package.json` e le versioni esatte già bloccate;
- `BenchmarkTarget/tsconfig.json`;
- tutte le configurazioni `vitest.public*.config.ts`;
- gli import dei test sotto `BenchmarkTarget/tests/public/`;
- il contratto di separazione e valutazione in B3;
- la policy dei percorsi della preparazione A1.

È stato osservato che i test comuni importano da `../../src/index.js` e i test
specifici da `../../../src/index.js`; entrambi richiedono il ripristino sotto
`project/tests/public/`. Questa osservazione giustifica la composizione scelta,
ma non è ancora una prova di esecuzione del worker.

## Prossimo passo

Eseguire una ricognizione circoscritta delle primitive di isolamento già
disponibili sull'host Windows e costruire una prova con sentinelle sintetiche.
Nessuno strumento MCP di esecuzione deve essere aggiunto prima che un candidato
impedisca realmente accesso ai percorsi vietati, rete e segreti anche dal codice
eseguito e dai suoi processi figli.
