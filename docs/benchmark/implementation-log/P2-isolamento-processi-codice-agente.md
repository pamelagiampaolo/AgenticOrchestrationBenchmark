# P2 — Ricognizione per l'isolamento dei processi che eseguono codice agente

## Stato e perimetro

Ricognizione eseguita il 5 novembre 2026. Questo documento propone il confine per
una futura prova, ma non lo implementa e non certifica isolamento del sistema
operativo. Non sono stati installati componenti, abilitate feature, modificati
account, ACL o firewall, né introdotti valutatore o strategie multi-agente.

## Obiettivo, ambito e criteri

### Obiettivo

Individuare il meccanismo proporzionato con cui eseguire build, test e relativi
processi figli quando il loro codice può essere modificato dall'agente, impedendo
loro di raggiungere riservati, risultati, credenziali e configurazioni fidate.

### Ambito

Stato locale di Windows, WSL e Windows Sandbox; confronto tra isolamento tramite
identità Windows, WSL 2 e Windows Sandbox; topologia proposta; specifica della
futura prova con sentinelle fittizie.

### Criteri oggettivi di questa ricognizione

1. registrare capacità locali osservate senza cambiare il sistema;
2. confrontare confine file, processi figli, rete, automazione e prerequisiti;
3. assegnare esplicitamente la collocazione di CLI, MCP e processi non fidati;
4. definire una prova futura automatizzabile con esiti positivi e negativi;
5. distinguere fatti verificati, proposta, prerequisiti e limiti;
6. formulare una decisione da sottoporre all'utente.

## Fatti locali verificati

### Host Windows

- edizione registrata: Windows Professional, display version `25H2`, build
  `26200.9711`, architettura a 64 bit;
- hypervisor presente;
- 20 processori logici, 31,8 GiB di RAM e 902,2 GiB liberi sull'unità `C:`;
- `docker.exe` e `podman.exe` non sono disponibili.

Il valore legacy `ProductName` riporta “Windows 10 Pro”, mentre build e display
version identificano il sistema effettivo. La proposta usa i valori di build,
edizione e capacità, non il nome legacy.

### WSL

- `wsl.exe` è disponibile;
- versione WSL `2.7.8.0`, kernel `6.18.3.33.1-1`, default WSL 2;
- nessuna distribuzione installata;
- WSL 1 non è supportato dalla configurazione corrente perché la relativa
  feature opzionale non è abilitata.

La documentazione Microsoft conferma che WSL monta normalmente i dischi Windows
sotto `/mnt` e usa per default una rete NAT. WSL 2 non costituisce quindi, nella
configurazione standard, un confine sufficiente contro lettura del filesystem
host o rete.

### Windows Sandbox

- l'edizione Professional e le risorse hardware osservate soddisfano i
  prerequisiti pubblicati da Microsoft;
- `wsb.exe`, `WindowsSandbox.exe` e il pacchetto AppX `WindowsSandbox` non sono
  disponibili nella sessione corrente;
- lo stato delle feature `Containers-DisposableClientVM`,
  `VirtualMachinePlatform`, Hyper-V e WSL non è interrogabile dall'utente corrente:
  DISM richiede una console amministrativa anche per la query;
- pertanto Windows Sandbox **non è utilizzabile ora** e non è dimostrato se la
  sola feature sia disabilitata o se manchino anche componenti/app aggiornati.

Microsoft documenta che Windows Sandbox può disabilitare la rete e mappare
cartelle host in sola lettura tramite configurazione `.wsb`. La rete è invece
abilitata per default, così come il clipboard, quindi una sessione senza
configurazione esplicita non sarebbe accettabile.

## Confronto delle opzioni

| Opzione | File host e processi figli | Rete | Stato locale | Valutazione |
| --- | --- | --- | --- | --- |
| Identità Windows dedicata + ACL NTFS + firewall | Il token limitato si propaga ai figli; richiede ACL corrette su tutte le aree e nessuna credenziale nel profilo. | Richiede regole firewall robuste per identità/programmi arbitrari e verifica di LAN, loopback e figli. | Meccanismi di base presenti, ma servono nuovo account, ACL e policy. | Possibile fallback, invasivo sul sistema e più facile da configurare in modo incompleto. |
| WSL 2 dedicato | VM leggera, ma `/mnt/c` espone normalmente l'host; servono distro importata, automount e interop disabilitati e rootfs controllato. | NAT disponibile per default; servono firewall Hyper-V/WSL e prove dedicate. | Runtime presente, nessuna distro. | Non proporzionato: richiede installazione e hardening su più superfici. |
| Windows Sandbox configurata | Confine VM usa-e-getta; solo cartelle esplicitamente mappate, input mappabile read-only, figli confinati nella stessa VM. | `<Networking>Disable</Networking>` offre una policy semplice da provare. | Prerequisiti host plausibili, ma eseguibile/CLI non disponibili. | Soluzione raccomandata, subordinata ad abilitazione/installazione futura e prova della CLI `wsb`. |

Win32 App Isolation non è raccomandata: è una tecnologia preview che richiede
packaging e capability specifiche e non offre vantaggi proporzionati per eseguire
un toolchain Node.js arbitrario.

## Proposta raccomandata

### Confine di fiducia

Sul **trusted host Windows** restano:

- orchestratore .NET;
- Claude CLI e autenticazione;
- server MCP controllato e sua configurazione;
- prompt, manifest, hash, log autorevoli e risultati dell'orchestratore;
- archivio originale e, in futuro, materiali riservati, che non vengono mai
  mappati nella sandbox.

Dentro una **Windows Sandbox nuova per ogni run sperimentale** entrano soltanto:

- snapshot dei materiali autorizzati della workspace agente;
- runtime e dipendenze esatte preassemblate dal controllore;
- directory scratch della run;
- build, test pubblici e ogni processo figlio da essi avviato.

Claude CLI non entra nella sandbox: necessita della rete e dell'autenticazione e
non deve condividere il confine del codice non fidato. Il server MCP resta sul
trusted host; il futuro strumento build/test accetta un'operazione fissa, non un
comando host arbitrario, prepara lo snapshot e usa il controllo `wsb` per
eseguire il comando nella VM.

### Trasferimento dei dati

1. Il controllore crea una directory input dedicata alla run con soli file
   autorizzati e manifest di hash.
2. La directory input è mappata **read-only** nella sandbox.
3. All'interno della VM il worker copia l'input nel proprio disco effimero e
   lavora soltanto sulla copia.
4. Una directory output host nuova e vuota è l'unico mapping scrivibile. Non è la
   directory dei risultati fidati: contiene soltanto output non affidabili della
   stessa invocazione.
5. Dopo l'arresto della VM, il controllore valida dimensioni, nomi, formato e hash
   degli output ammessi e produce separatamente i propri record autorevoli.
6. Configurazioni fidate, risultati precedenti, profilo utente, repository
   originale e aree riservate non sono mappati e non sono copiati nel guest.

Un mapping scrivibile non va considerato un canale fidato: il codice agente può
fabbricarne il contenuto. Per questo l'orchestratore conserva separatamente exit
code, timeout, configurazione e log del controllo VM.

### Credenziali e ambiente

Il processo guest riceve un ambiente costruito da allowlist, senza ereditare
`CLAUDE*`, `ANTHROPIC*`, token, proxy, credenziali Git/npm o directory home del
trusted host. Dipendenze e toolchain sono preparati prima della run; il guest non
esegue download o package restore online.

### Rete

La policy proposta è **nessuna rete per build, test e figli**:

- `.wsb` con `<Networking>Disable</Networking>`;
- clipboard, audio/video, stampanti e vGPU disabilitati; protected client
  abilitato quando compatibile;
- nessun proxy o credenziale di rete nel guest;
- controllo host-guest tramite CLI `wsb`, non tramite una porta di rete del
  worker.

La configurazione deve essere esplicita perché Microsoft documenta rete e
clipboard come abilitate per default. Se la CLI `wsb` non consente controllo
deterministico, exit code e arresto forzato, Windows Sandbox non supera i
prerequisiti e si rivaluta il fallback Windows con identità dedicata.

## Specifica della prova P2.1

### Fixture fittizie

Sul trusted host, tutte prive di dati reali:

- `authorized-input/internal.txt` con sentinella interna;
- `trusted-config/config-sentinel.txt`;
- `simulated-reserved/reserved-sentinel.txt`;
- `trusted-results/previous-result-sentinel.txt`;
- variabile ambiente host fittizia `SIMULATED_HOST_CREDENTIAL`;
- endpoint HTTP host fittizio che registra ogni richiesta senza contenuti
  sensibili.

Nel guest sono predisposti scratch e output della singola run. Nessuna sentinella
esterna è inserita in prompt, argomenti o messaggi di errore.

### Operazioni consentite attese

1. leggere la sentinella interna dalla copia guest;
2. creare, modificare ed eliminare file nello scratch guest;
3. avviare un processo figlio controllato;
4. eseguire versione, build e test pubblici con toolchain preassemblata;
5. scrivere un report fittizio nella sola directory output autorizzata.

### Operazioni negate attese

Il processo principale e un figlio separato tentano di:

- elencare, leggere e copiare ciascuna directory host non mappata;
- usare percorsi assoluti host, traversal, junction/symlink e percorsi UNC;
- leggere profili, variabili o file di credenziali del trusted host;
- modificare il mapping input read-only;
- raggiungere output di altre run;
- avviare un processo Windows host tramite interop o shell esterna.

Ogni tentativo deve fallire senza restituire la sentinella. L'assenza delle
stringhe esterne viene verificata su stdout, stderr, file output e log MCP.

### Prove di rete

Processo principale e figlio tentano separatamente:

- risoluzione DNS di un nome pubblico;
- connessione TCP a un indirizzo pubblico controllato;
- connessione all'endpoint sentinella sul trusted host;
- connessione a un indirizzo LAN di prova.

Tutti devono fallire entro timeout brevi e l'endpoint host deve registrare zero
richieste. Un test solo verso Internet non certifica isolamento dal trusted host
o dalla LAN.

### Criteri oggettivi di accettazione P2.1

1. configurazione `.wsb`, toolchain, input e launcher hanno hash registrati;
2. inventario dei mapping contiene soltanto input read-only e output dedicato;
3. tutte le operazioni interne attese riescono;
4. tutti gli accessi file esterni falliscono sia nel processo principale sia nel
   figlio e nessuna sentinella esterna compare negli output;
5. l'ambiente guest contiene soltanto le variabili allowlisted;
6. tutte le prove di rete falliscono e il server sentinella host osserva zero
   richieste;
7. timeout e arresto terminano l'intera sandbox, non il solo processo padre;
8. dopo la chiusura, una nuova sandbox non contiene il marker della precedente;
9. nessun file fuori dall'output dedicato cambia sul trusted host;
10. gli stessi artefatti e la stessa policy sono riutilizzabili nelle tre
    condizioni sperimentali.

Una prova parziale certifica soltanto il vettore verificato. In particolare,
assenza di una sentinella nei log non sostituisce la prova positiva che il
tentativo sia stato realmente eseguito.

## Prerequisiti e limiti

Prima di P2.1 occorre, con autorizzazione separata:

1. verificare da console amministrativa lo stato di
   `Containers-DisposableClientVM`, `VirtualMachinePlatform` e Hyper-V;
2. decidere se abilitare/installare Windows Sandbox e l'app/CLI `wsb` richiesta;
3. verificare che `wsb start`, `wsb exec`, exit code, timeout, stop e mapping
   read-only funzionino sulla build installata;
4. congelare runtime Node/npm e dipendenze da copiare nel guest;
5. definire limiti di CPU, memoria, spazio, output e durata del worker.

Windows Sandbox consente una sola istanza concorrente secondo la documentazione
Microsoft corrente; questo può limitare parallelismo e strategie future, ma non
impedisce le prove sequenziali previste. Le prestazioni e l'overhead di copia non
sono ancora misurati.

## Decisione richiesta

**Proposta da approvare:** adottare Windows Sandbox come confine candidato per
build/test e figli, autorizzando in un passo successivo soltanto la verifica
amministrativa delle feature e, se necessario, l'abilitazione/installazione dei
componenti ufficiali Windows Sandbox e della CLI `wsb`.

Nessuna modifica al sistema è stata eseguita in P2. Se la proposta non viene
approvata, il passo alternativo è progettare e provare identità Windows dedicata,
ACL NTFS e firewall; non va adottato implicitamente perché modifica più stato
persistente del computer e richiede una matrice di prove più ampia.

## Esito della verifica di disponibilità P2.0

La proposta Windows Sandbox è stata approvata e la feature
`Containers-DisposableClientVM` è stata abilitata il 6 novembre 2026, con
riavvio manuale completato prima delle verifiche successive.
`WindowsSandbox.exe` è ora presente e l'aggiornamento ufficiale Microsoft Store
ha fornito `wsb` `0.8.107.0`: il ciclo vuoto start/list/stop è stato completato
con identificazione e arresto verificati. Comandi, versioni, esiti e limiti
sono documentati in [P2.0 — Disponibilità e gestione di Windows
Sandbox](P2.0-disponibilita-windows-sandbox.md).

## Esito della prova P2.1

P2.1 è stata eseguita separatamente il 6 novembre 2026 con sentinelle fittizie,
un progetto C# minimo offline e tre istanze nuove: prova principale, arresto per
timeout e controllo di non persistenza. Nove criteri risultano superati; il
riutilizzo effettivo degli stessi artefatti nelle tre condizioni sperimentali
resta non dimostrato perché agenti e strategie multi-agente erano fuori ambito.

Comandi, artefatti, distinzione tra output guest ed evidenze PC, esiti e limiti
sono documentati in [P2.1 — Prova di isolamento in Windows
Sandbox](P2.1-prova-isolamento-windows-sandbox.md). La sintesi stabile è in
[`evidence/P2.1-isolation-summary.json`](../evidence/P2.1-isolation-summary.json).

## Esito della verifica della baseline Node.js

Una successiva iterazione circoscritta ha eseguito la baseline pubblica Node.js
in una nuova Sandbox con la stessa policy di P2.1. Build, typecheck e suite comune
sono terminati con exit code zero e Vitest ha riportato 27 test superati su 27.
L'input host è rimasto integro e l'istanza è stata arrestata. Il commit corrente
è registrato come sorgente della prova, senza designarlo autonomamente come
baseline sperimentale definitiva. Dettagli e limiti sono in
[P2.2 — Baseline Node.js in Windows
Sandbox](P2.2-baseline-node-windows-sandbox.md).

## Riferimenti ufficiali

- [Install Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)
- [Configure Windows Sandbox with a WSB file](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)
- [Windows Sandbox command line](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-cli)
- [WSL interop and Windows-drive mounts](https://learn.microsoft.com/en-us/windows/dev-environment/wsl-interop)
- [WSL networking](https://learn.microsoft.com/windows/wsl/networking)
