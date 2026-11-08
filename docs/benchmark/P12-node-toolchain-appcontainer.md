# P12 — Toolchain Node nel worker AppContainer

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** prova parziale; 18 controlli su 22 superati, criterio complessivo non
  superato.
- **Obiettivo:** eseguire copia fidata di Node.js, typecheck, build, Vitest e un
  probe JavaScript ostile nello stesso confine verificato in P11.

P12 non autorizza ancora strumenti MCP `run_*`. Il fallimento della suite
pubblica è classificato come problema di compatibilità dell'infrastruttura, non
come fallimento del benchmark.

## Fixture implementata

`scripts/Invoke-NodeAppContainerProbe.ps1` pubblica il probe in una directory
temporanea e gli fornisce:

- baseline copiata fisicamente, senza collegamenti al repository;
- copia fisica dei `node_modules` bloccati dal lock file;
- copia di `node.exe` versione `24.17.0`;
- configurazione Vitest generata dal controllore;
- storage nativo del profilo AppContainer per il worker;
- sentinelle vietate esterne allo storage;
- ambiente costruito da allowlist;
- Job Object e timeout distinti per ogni processo.

Node viene avviato con `--preserve-symlinks` e
`--preserve-symlinks-main`, anche tramite `NODE_OPTIONS`. Senza tali flag il
runtime tenta `lstat` di `C:\` durante la risoluzione del modulo principale e
termina con `EPERM`; non sono state allargate le ACL della radice per aggirare il
diniego.

## Risultati positivi osservati

- Node `24.17.0` avviato dalla copia temporanea;
- TypeScript `7.0.2` typecheck: exit code `0`;
- build TypeScript: exit code `0`;
- CLI Vitest `5.1.0 --version`: exit code `0`;
- Worker thread Node, inclusi stdout e stderr dedicati: exit code `0`;
- processo figlio Node avviato e confinato;
- lettura e scrittura nella fixture autorizzata riuscite;
- lettura, scrittura e junction verso la sentinella esterna negate;
- DNS, TCP e HTTP verso endpoint controllati negati con exit code `3`;
- ambiente privo di nomi fuori allowlist;
- hash della baseline sorgente invariato.

## Requisito non superato

La suite Vitest non completa la fase di inizializzazione entro 15 secondi:

```text
TimedOut      true
JobTerminated true
ExitCode      124
Report JSON   assente
stdout        vuoto
stderr        vuoto
```

Le quattro verifiche false derivano tutte dallo stesso evento:

- `publicTestsSucceeded`;
- `publicTestReportProduced`;
- `publicTestCountExpected`;
- `publicTestsAllPassed`.

Lo stesso comando, con percorsi assoluti equivalenti ma fuori AppContainer,
produce regolarmente il report con 27 test totali e 27 superati. Il problema non
è quindi la configurazione o la baseline.

## Varianti realmente provate

Sono state provate senza successo le combinazioni standard:

- pool `threads`, `vmThreads` e `forks`;
- config loader `bundle`, `runner` e `native`;
- un solo worker e parallelismo file disabilitato;
- configurazione esterna e configurazione sotto `project/.trusted/`;
- worker nella fixture temporanea e nello storage nativo AppContainer;
- propagazione dei flag tramite `execArgv` e `NODE_OPTIONS`;
- disabilitazione sperimentale del Vite Module Runner;
- pool `forks` con una modifica diagnostica della sola copia temporanea per
  ignorare stdout/stderr mantenendo IPC.

La modifica diagnostica di Vitest non ha risolto il timeout ed è stata rimossa.
La versione finale del probe usa la distribuzione Vitest originale.

## Problema identificato

Il confine AppContainer senza capability è compatibile con Node, TypeScript,
Worker thread e processi figli ordinari, ma non con l'infrastruttura interna di
esecuzione/IPC di Vitest 5 osservata. Il blocco avviene prima della produzione di
output del test runner e non è spiegato da file, rete, ambiente, realpath, scelta
del pool o loader della configurazione.

Questa evidenza è sufficiente per escludere l'integrazione diretta di Vitest nel
worker AppContainer attuale. Non è sufficiente per attribuire il blocco a una
specifica API Windows interna non esposta dai log di Vitest.

## Soluzioni proposte

### 1. Pool Vitest fidato in-process

Realizzare un `PoolRunnerInitializer` controllato che esegua il runner Vitest
nello stesso processo Node, evitando l'infrastruttura worker/IPC incompatibile.
Deve usare le API pubbliche della versione fissata, produrre lo stesso report e
superare un confronto caso per caso con il runner canonico fuori isolamento.

Vantaggio: conserva AppContainer e il blocco rete già verificato. Rischio:
implementazione dipendente dalla versione e possibile divergenza semantica dal
runner canonico.

### 2. Confine di processo Windows differente

Eseguire Vitest non modificato in un confine HCS/container oppure con identità
locale dedicata, ACL deny-by-default, ambiente allowlist, Job Object e blocco
rete dimostrato. Questa soluzione conserva il runner canonico ma introduce più
stato e complessità operativa.

Un semplice restricted token o la sola working directory restano insufficienti.

### 3. Capability AppContainer aggiuntiva

Individuare sperimentalmente la capability necessaria a Vitest e ripetere tutte
le prove di rete e accesso. Questa strada è accettabile soltanto se la capability
non riapre loopback, rete privata o altre superfici vietate. Non va concessa per
tentativi indiscriminati.

## Raccomandazione

La prossima iterazione dovrebbe essere un piccolo spike sul pool Vitest
in-process, perché non modifica l'host e mantiene il confine P11. Va abbandonato
immediatamente se richiede API interne o se i risultati non coincidono esattamente
con i 27 casi canonici. In tal caso la scelta corretta diventa HCS o un'identità
dedicata, previa approvazione architetturale.

## Evidenze e pulizia

Comando finale:

```powershell
.\scripts\Invoke-NodeAppContainerProbe.ps1
```

Risultato atteso nello stato corrente: exit code `1`, perché P12 non considera
accettabile una suite terminata dal timeout.

Evidenza non versionata:

```text
%TEMP%\p12-run-bdbe97b86c644247a9e789ea39d00895\evidence\p12-results.json
```

Dopo la prova finale sono stati osservati zero processi probe, zero profili P12,
zero fixture worker e zero directory di pubblicazione. L'area delle evidenze è
conservata intenzionalmente.
