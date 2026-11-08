# P11 — Prova AppContainer per il worker

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** probe sintetico implementato ed eseguito con esito positivo.
- **Obiettivo:** verificare che AppContainer, ACL esplicite, ambiente allowlist e
  Job Object possano costituire il confine di base del futuro worker.

La prova non esegue il benchmark, Node.js, TypeScript, Vitest o test riservati.
Usa soltanto sentinelle sintetiche e un eseguibile .NET controllato.

## Componenti

- `AppContainerIsolationProbe/`: controllore e processo di probe;
- `scripts/Invoke-AppContainerIsolationProbe.ps1`: pubblicazione ed esecuzione
  ripetibile in directory temporanee;
- AppContainer temporaneo privo di capability;
- ACL concessa al SID AppContainer soltanto su fixture autorizzata e copia del
  probe;
- Job Object con `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.

Il controllore crea un nome profilo univoco, avvia il probe tramite
`PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES`, acquisisce il JSON, elimina
profilo e fixture e conserva soltanto le evidenze. Lo script elimina anche la
pubblicazione temporanea.

## Proprietà verificate

Il processo AppContainer:

- legge e scrive nella fixture autorizzata;
- non legge né scrive nella directory esterna;
- non legge la sentinella esterna attraverso una junction NTFS;
- avvia un figlio che conserva il confine e non legge la sentinella esterna;
- non risolve DNS;
- non apre TCP verso un endpoint loopback realmente in ascolto;
- non completa HTTP verso un secondo endpoint loopback realmente in ascolto;
- riceve soltanto le variabili della allowlist;
- viene terminato insieme al figlio quando supera il timeout.

L'allowlist minima osservata come necessaria per avviare il processo contiene:

```text
APPDATA, COMSPEC, LOCALAPPDATA, PATH, SYSTEMROOT, TEMP, TMP, USERPROFILE
```

I valori non concedono accesso ai percorsi: l'accesso rimane determinato dalle
ACL del SID AppContainer. Variabili Codex, Claude, editor, token e credenziali non
sono state propagate.

## Problemi osservati e correzioni

1. `Directory.CreateSymbolicLink` è fallito perché l'identità corrente non
   possiede il privilegio per creare symlink. La fixture usa ora una junction
   NTFS creata dal controllore e rimossa esplicitamente prima della directory.
2. Un blocco ambiente con sole quattro variabili ha prodotto errore Win32 `203`
   prima dell'avvio. L'aggiunta delle quattro variabili strutturali di profilo ha
   consentito l'avvio mantenendo l'allowlist priva di dati del processo Codex.
3. Il primo controllo TCP usava una porta chiusa e non distingueva isolamento da
   rifiuto della connessione. La prova finale usa endpoint TCP e HTTP controllati
   e in ascolto prima dell'avvio del probe.

I tentativi intermedi non sono conteggiati come superamenti. Hanno guidato le
correzioni della fixture e sono descritti per non perdere l'evidenza diagnostica.

## Verifiche eseguite

```powershell
dotnet format AppContainerIsolationProbe\AppContainerIsolationProbe.csproj --verify-no-changes --no-restore
dotnet build AppContainerIsolationProbe\AppContainerIsolationProbe.csproj --no-restore
.\scripts\Invoke-AppContainerIsolationProbe.ps1
```

Risultato finale:

- formattazione superata;
- build con zero errori e zero avvisi;
- probe con exit code `0`;
- 13 controlli veri su 13;
- processo principale terminato normalmente;
- processo di timeout terminato dal Job Object con exit code `124`.

Evidenza non versionata:

```text
%TEMP%\p11-run-5a84e63e31bc427caeb5780979a2b406\evidence\p11-results.json
```

Dopo la prova sono stati osservati:

```text
fixture temporanee presenti       0
pubblicazioni temporanee presenti 0
processi probe presenti           0
profili P11 presenti              0
```

## Limiti

La prova certifica esclusivamente il comportamento osservato del probe .NET
sull'host e sulla versione Windows corrente. Non dimostra ancora che:

- Node.js e la toolchain bloccata si avviino con le stesse ACL;
- TypeScript e Vitest funzionino senza ampliare i permessi;
- la composizione P9 produca build e test pubblici corretti;
- il valutatore riservato sia isolato;
- il comportamento resti identico su un altro host o dopo aggiornamenti di
  Windows e runtime.

## Prossimo passo

P12 deve eseguire una copia fidata di Node.js e della baseline invariata dentro
lo stesso confine, usando la composizione P9. Deve provare typecheck, build e test
pubblici, quindi ripetere dal codice JavaScript gli stessi tentativi vietati.
Nessuno strumento MCP `run_*` deve essere aggiunto prima che questa prova Node
superi accessi, rete, ambiente, figli, timeout e pulizia.
