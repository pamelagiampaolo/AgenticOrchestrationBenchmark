# B3 — Strategia dei test pubblici e riservati

## Registro

- **Data:** 2 novembre 2026
- **Fase:** 2, blocco B, passo B3
- **Obiettivo:** progettare una valutazione automatica, indipendente e identica
  per single-agent, gerarchia statica e gerarchia adattiva.
- **Stato:** progettazione approvata dall'utente il 2 novembre 2026; nessun test,
  isolamento o pacchetto ancora implementato e verificato.
- **Framework approvato:** Vitest.

## Principio di indipendenza

L'agente sviluppa la soluzione in un ambiente di esecuzione isolato. La
valutazione usa due ruoli separati:

- il **controllore fidato** conserva configurazioni, test originali e risultati;
- il **worker di valutazione isolato** esegue Vitest e il codice della submission
  su copie temporanee predisposte dal controllore.

L'agente non esegue il controllore e non accede alla sua directory, configurazione
privata o identità di sistema. Il worker non può accedere all'archivio originale,
ai risultati delle altre esecuzioni, alle credenziali dell'utente o alla rete.

La sola separazione tra file o configurazioni Vitest non è sufficiente: i test
riservati devono essere assenti dall'ambiente dell'agente e protetti dai permessi
del sistema operativo.

### Separazione dal repository del benchmark e provenienza

È approvato che tutti i materiali riservati siano conservati e versionati
separatamente, nell'area fidata inaccessibile agli agenti. Non devono mai essere
inseriti nel repository del benchmark, neppure temporaneamente: la loro rimozione
dal solo albero di lavoro non eliminerebbe infatti l'accesso tramite cronologia,
ref od oggetti Git eventualmente disponibili agli agenti.

Il controllore esterno prepara ciascun ambiente agente a partire dal commit
stabilito copiando esclusivamente i materiali autorizzati. La copia non contiene
la directory `.git`, collegamenti al repository originale o credenziali che
consentano di raggiungere l'area riservata. Il controllore registra il commit di
provenienza e gli hash dei materiali copiati; registra inoltre, nella propria area
fidata, gli identificatori di versione e gli hash dei materiali riservati usati
dalla valutazione, senza esporli all'ambiente agente.

Questa è una decisione progettuale approvata, non un meccanismo già disponibile.
L'assenza di `.git` riduce la superficie informativa ma non costituisce da sola
isolamento: restano necessari il confine di esecuzione, i permessi del sistema
operativo e le prove negative previste prima delle run ufficiali.

## Tre insiemi distinti di test

### 1. Test pubblici di riferimento

Sono parte della specifica osservabile del task e forniscono feedback comune alle
tre architetture. Gli agenti possono leggerli ed eseguirli, ma non modificarli,
rinominarli o eliminarli.

La copia canonica è conservata dal valutatore. Nell'ambiente dell'agente viene
esposta una copia identica con permessi di sola lettura. Prima e dopo
l'esecuzione, il valutatore confronta l'hash crittografico di ogni file con il
manifest canonico. Una differenza rende l'esecuzione non valida e viene registrata
come violazione d'integrità, non come test fallito.

### 2. Test aggiuntivi degli agenti

Gli agenti possono creare test in una directory scrivibile separata. Questi test
servono allo sviluppo, ma non contribuiscono direttamente al punteggio ufficiale
e non possono sostituire o mascherare i test di riferimento.

I test aggiuntivi non vengono importati dal valutatore e il loro numero, esito o
copertura non entra nel calcolo della correttezza ufficiale.

### 3. Test riservati

Sono conservati nell'ambiente protetto del valutatore e non sono copiati,
collegati, montati o inclusi nell'ambiente dell'agente. Verificano gli stessi
requisiti ufficiali con dati, combinazioni e casi limite diversi da quelli
pubblici.

Gli agenti non ricevono nomi dei file, sorgenti, elenco dettagliato dei casi o
output diagnostico dei test riservati. Il risultato strutturato viene salvato in
una directory persistente non accessibile all'esecuzione sperimentale.

La valutazione ufficiale considera esclusivamente due suite canoniche: test
pubblici di riferimento e test riservati. I test aggiuntivi degli agenti restano
un terzo insieme distinto, escluso dal punteggio.

## Separazione fisica degli ambienti

La struttura logica proposta è la seguente; i percorsi reali saranno scelti e
verificati durante l'implementazione:

```text
area-fidata-controllore/                accesso: solo controllore fidato; separata dal repository benchmark
├── config/
│   ├── vitest.public.config.*
│   └── vitest.reserved.config.*
├── manifests/
│   └── suite-manifest.json
├── public-reference/                   copia canonica
├── reserved/                           mai esposta agli agenti
└── evaluator/

area-esecuzione/<run-id>/               accesso: agente
├── workspace/                          sorgenti modificabili
├── public-reference/                   leggibile, non modificabile
└── agent-tests/                        test aggiuntivi modificabili

area-worker/<run-id>/                   accesso: worker isolato, temporanea
├── submission/                         copia congelata della soluzione
├── public-reference/                   copia canonica temporanea
├── reserved/                           copia riservata temporanea
└── output/                              unico percorso scrivibile

area-risultati/<run-id>/                accesso: orchestratore/valutatore
├── submission-manifest.json
├── public-results.json
├── reserved-results.json
└── evaluation-summary.json
```

L'area fidata non deve essere una sottodirectory raggiungibile dall'identità che
esegue gli agenti o dal worker. Su Windows la separazione dovrà usare identità e
ACL NTFS distinte: lettura/esecuzione soltanto dove necessarie, nessun permesso
sull'area fidata e nessuna scrittura sui test pubblici esposti.

La sola proprietà “sola lettura” del file non è considerata sufficiente, perché
può essere rimossa dal proprietario. Prima dell'esperimento sarà quindi
obbligatorio verificare realmente i permessi usando la stessa identità e gli
stessi strumenti concessi agli agenti.

Le ACL usate durante lo sviluppo non proteggono da sole la fase di valutazione,
perché Vitest importa ed esegue codice scritto dagli agenti. Il controllore deve
quindi creare un worker confinato per ogni valutazione. Il worker può leggere le
copie temporanee necessarie all'esecuzione, ma non gli originali; può scrivere
soltanto nella propria area di output, non possiede segreti e non dispone di rete.
Al termine il controllore acquisisce esclusivamente gli output previsti e
distrugge l'area del worker.

### Scelta proporzionata del meccanismo di isolamento

Non viene imposto in anticipo un container o una macchina virtuale. Prima
dell'implementazione saranno controllate le capacità già disponibili su Windows
e sarà scelto il meccanismo più semplice che dimostri tutte queste proprietà:

1. accesso limitato alla sola area temporanea della valutazione;
2. impossibilità di leggere archivio originale e risultati di altre esecuzioni;
3. rete disabilitata durante l'esecuzione della submission;
4. assenza di credenziali e variabili d'ambiente sensibili;
5. eliminazione sicura dell'area temporanea al termine.

ACL e identità separate restano necessarie per i confini dei file, ma sono
accettabili soltanto insieme a un confine di esecuzione che impedisca rete e
accessi esterni. Se gli strumenti già presenti non superano le prove previste, non
verrà installata infrastruttura aggiuntiva senza una nuova approvazione.

### Direzione tecnica approvata per l'harness degli agenti

La direzione approvata è usare Claude CLI come interfaccia con il modello, un
server MCP locale controllato come unica superficie degli strumenti, l'orchestratore
.NET per sessioni, condizioni sperimentali e metriche, e il valutatore esterno
descritto in questo documento. L'elenco completo degli strumenti MCP non è ancora
approvato né congelato e le strategie agentiche non sono implementate.

La configurazione Claude CLI di ogni run dovrà esporre soltanto gli strumenti MCP
autorizzati per quella condizione. Dovranno essere esclusi o neutralizzati almeno:

- strumenti nativi alternativi per file, shell, esecuzione di codice, web e
  delegazione;
- hook, plugin, skill, comandi e agenti personalizzati ereditati;
- server MCP non dichiarati esplicitamente dal controllore;
- configurazioni utente, progetto o locali non previste e persistenza di sessione
  capace di trasferire stato tra run.

Configurazione Claude CLI, configurazione MCP e implementazione del server degli
strumenti sono artefatti fidati esterni alla workspace modificabile dagli agenti.
Il controllore ne registra versioni e hash. Le opzioni della CLI costituiscono una
difesa soltanto dopo che la loro combinazione è stata osservata sulla versione
fissata e sono state escluse altre superfici effettivamente disponibili.

La ricognizione ripetuta il 5 novembre 2026 sulla Claude CLI installata `2.1.312`
ha
osservato nel relativo help locale le opzioni `--tools`, `--allowedTools`,
`--disallowedTools`, `--restricted`, `--safe-mode`, `--strict-mcp-config`,
`--mcp-config`, `--settings`, `--setting-sources`, `--disable-slash-commands`,
`--no-session-persistence` e `--no-chrome`. Sono inoltre presenti funzioni native
per agenti e delegazioni. Questa evidenza rende fattibile un prototipo, ma non
dimostra ancora che la configurazione combinata esponga esclusivamente i due
strumenti previsti. Le impostazioni gestite esternamente possono avere precedenza
e dovranno essere rilevate o escluse prima delle run ufficiali.

L'help locale precisa inoltre tre limiti progettuali. `--safe-mode` disabilita le
personalizzazioni, ma lascia disponibili strumenti e plugin incorporati e
impostazioni amministrate. `--restricted` rimuove gli strumenti incorporati che
eseguono comandi o codice e `WebFetch`, ma conserva strumenti file confinati alle
directory di lavoro salvo ulteriore selezione tramite `--tools`. `--bare` salta
molte fonti implicite, ma non può essere assunto come soluzione per questo
progetto perché non legge autenticazione OAuth o keychain: ammette soltanto
`ANTHROPIC_API_KEY` o `apiKeyHelper` esplicito, in contrasto con la decisione di
conservare l'autenticazione della CLI. La prima iterazione dovrà quindi provare
una combinazione esplicita dei controlli compatibili, senza dichiararla sicura in
base alla sola presenza dei flag.

La documentazione ufficiale conferma che `--strict-mcp-config` limita i server a
quelli passati con `--mcp-config`, mentre le impostazioni hanno più origini e le
impostazioni amministrate hanno precedenza su riga di comando, progetto e utente.
Conferma anche che `~/.claude.json` può contenere configurazioni MCP e decisioni
di fiducia. Di conseguenza, inventario effettivo della sessione, origini caricate
e configurazione amministrata devono essere osservati nella prova; non basta
ispezionare il solo file fornito al launcher.

Fonti consultate il 5 novembre 2026:

- help locale: `claude --version`, `claude --help`, `claude mcp --help`;
- [Claude Code CLI reference](https://code.claude.com/docs/en/cli-reference);
- [Connect Claude Code to tools via MCP](https://code.claude.com/docs/en/mcp);
- [Settings files and precedence](https://code.claude.com/docs/en/settings).

### Esecuzione indiretta di codice modificabile dagli agenti

La validazione dei percorsi nel server MCP protegge le operazioni implementate dal
server, ma non confina automaticamente un processo che esegue codice modificabile
dall'agente. Test pubblici, build, script, plugin e relativi processi figli possono
aprire percorsi direttamente e devono quindi essere eseguiti con un confine che
non possieda permessi di lettura su materiali riservati, configurazioni fidate o
risultati. Non viene presupposta una macchina virtuale: il meccanismo minimo
Windows, l'identità effettiva, le ACL, la politica dei processi figli e l'eventuale
blocco di rete restano scelte tecniche da provare prima di introdurre strumenti
MCP che eseguono codice.

La prova futura dovrà usare codice controllato che tenti accessi diretti e tramite
processi figli a ciascuna area vietata, oltre a rete, credenziali e risultati di
run precedenti. Il successo della prima iterazione sui soli strumenti di lettura
non fornirà evidenza su questa proprietà.

La ricognizione P2 del 5 novembre 2026 è documentata in [P2 — Ricognizione per
l'isolamento dei processi che eseguono codice
agente](P2-isolamento-processi-codice-agente.md). Propone Windows Sandbox come
confine candidato proporzionato, ma la funzionalità e la CLI `wsb` non sono
attualmente disponibili e richiedono una decisione esplicita prima di qualsiasi
abilitazione o installazione. Nessuna prova di isolamento OS è stata ancora
eseguita.

### Prima iterazione pianificata: prototipo MCP di sola lettura

**Stato al 5 novembre 2026:** iterazione eseguita. Risultati, comandi, evidenze e
limiti sono documentati in [P1 — Prototipo Claude CLI con MCP di sola
lettura](P1-prototipo-mcp-sola-lettura.md). Il confinamento dei due strumenti è
stato osservato direttamente e attraverso Claude CLI; l'assenza di ogni
personalizzazione non è stata interamente dimostrata perché la CLI dichiara
agenti e plugin built-in e non espone hook o fonti effettive nell'evento iniziale.
La prova successiva sull'esecuzione indiretta di codice non è stata avviata.
Il chiarimento successivo su agenti, plugin, configurazioni e hook è documentato
in [P1.1 — Limiti residui della configurazione Claude
CLI](P1.1-limiti-configurazione-claude-cli.md): i subagenti sono stati rimossi e
gli hook non managed disabilitati, mentre i plugin built-in restano dichiarati e
le managed settings rimangono un controllo obbligatorio per ambiente.

**Obiettivo:** dimostrare che una sessione Claude CLI può usare soltanto un server
MCP locale fidato con `list_files` e `read_file`, entro una root assegnata dal
controllore.

**Componenti coinvolti:** launcher Claude CLI del prototipo, configurazione CLI
esterna alla workspace, server MCP locale minimale e directory temporanee di prova.
Non sono coinvolti valutatore, strategie multi-agente, test riservati reali o
strumenti che eseguono codice.

**Comportamento atteso:** la CLI vede esclusivamente i due strumenti MCP; elenca e
legge file interni alla root; il server normalizza i percorsi e rifiuta percorsi
assoluti esterni, attraversamenti `..` e collegamenti che risolvono fuori root.

**Criteri oggettivi di accettazione:**

1. l'inventario osservato della sessione contiene soltanto `list_files` e
   `read_file` come strumenti invocabili;
2. non risultano caricati strumenti nativi, hook, plugin, skill, altri MCP o
   delegazioni; la verifica include le origini di configurazione effettivamente
   caricate e l'eventuale policy amministrata;
3. un file interno noto viene elencato e letto con contenuto atteso;
4. tentativi controllati tramite percorso assoluto esterno, `..` e
   symlink/junction esterno vengono rifiutati e registrati senza restituire il
   contenuto;
5. configurazione e server MCP sono esterni e non modificabili dalla workspace;
6. versione CLI, argomenti effettivi, configurazione, chiamate MCP ed esiti sono
   registrati in modo verificabile.

Il superamento di questi criteri certificherà soltanto il confinamento osservato
dei due strumenti di lettura nella configurazione provata.

## Versionamento e configurazione bloccata

Quando Vitest verrà installato:

- `package.json` userà una versione esatta, senza `^`, `~`, `latest` o intervalli;
- `package-lock.json` sarà versionato e non potrà cambiare tra architetture;
- l'installazione riproducibile userà `npm ci`, non `npm install`;
- la versione effettiva di Node.js, npm, TypeScript e Vitest sarà registrata;
- le configurazioni del valutatore saranno versionate e identificate da hash;
- ogni suite pubblica e riservata avrà una versione e un manifest di hash;
- il medesimo commit di configurazione sarà usato per tutte le 54 esecuzioni.

La versione numerica di Vitest non viene scelta in questo passo. Sarà selezionata
e approvata prima dell'installazione, quindi bloccata nel lock file. Questo evita
di dichiarare una versione non ancora installata e verificata.

## Configurazioni Vitest separate

Sono previste configurazioni distinte ma entrambe controllate dal valutatore:

- **configurazione pubblica:** seleziona esclusivamente i test pubblici canonici;
- **configurazione riservata:** seleziona esclusivamente i test riservati.

Le configurazioni fissano almeno ambiente di esecuzione, timeout, ordinamento,
reporter, gestione della concorrenza e pattern dei file. Nessuna configurazione
prodotta o modificata dagli agenti viene usata per il punteggio ufficiale.

L'eventuale parallelismo interno dei test deve essere fissato allo stesso valore
per tutte le prove. I test non devono dipendere dall'ordine di esecuzione e
devono creare istanze applicative nuove.

## Flusso completo di una esecuzione

1. L'orchestratore seleziona il commit approvato della baseline e il controllore
   ne registra la provenienza.
2. Crea una nuova area di esecuzione con identificatore univoco.
3. Copia soltanto i materiali autorizzati, senza `.git`, collegamenti al
   repository originale o credenziali per l'area riservata; applica lo stato
   iniziale del task e registra gli hash della copia.
4. Espone i test pubblici di riferimento in sola lettura e una directory separata
   per i test aggiuntivi dell'agente.
5. Registra hash, versioni, task, architettura, ripetizione e orario iniziale.
6. Avvia l'architettura sperimentale con accesso limitato alla propria area.
7. Al termine blocca la soluzione e genera il manifest della submission.
8. Controlla integrità dei test pubblici, file consentiti e configurazioni.
9. Il controllore crea una nuova area temporanea e vi copia submission, test
   canonici e configurazioni autorizzate, registrandone versioni e hash senza
   esporre gli archivi originali.
10. Avvia un worker isolato senza rete, credenziali o accesso alle altre aree.
11. Il worker riesegue i test pubblici canonici con la configurazione fidata.
12. Il worker esegue i test riservati con la configurazione fidata.
13. Il controllore acquisisce output JSON grezzi e produce il riepilogo
    normalizzato nell'area persistente dei risultati.
14. Registra exit code, errori del worker e stato finale dell'esecuzione.
15. Distrugge l'area temporanea senza riutilizzarla per un'altra prova.

Il valutatore riesegue anche i test pubblici perché il risultato ottenuto durante
lo sviluppo non è sufficiente per il punteggio ufficiale.

## Raccolta strutturata dei risultati

Vitest sarà configurato con un reporter JSON disponibile nella versione fissata.
L'output grezzo non viene sovrascritto: il valutatore produce inoltre un riepilogo
normalizzato con uno schema stabile.

Campi minimi proposti per `evaluation-summary.json`:

```json
{
  "schemaVersion": "1.0",
  "runId": "identificatore-univoco",
  "baselineCommit": "git-sha",
  "taskId": "T1",
  "architecture": "single-agent",
  "repetition": 1,
  "suiteVersion": "versione-suite",
  "suiteManifestHash": "sha256",
  "vitestVersion": "versione-esatta",
  "configurationHash": "sha256",
  "startedAtUtc": "data-ora-ISO-8601",
  "finishedAtUtc": "data-ora-ISO-8601",
  "agentTaskDurationMs": 0,
  "evaluationDurationMs": 0,
  "public": {
    "total": 0,
    "passed": 0,
    "failed": 0,
    "skipped": 0
  },
  "reserved": {
    "total": 0,
    "passed": 0,
    "failed": 0,
    "skipped": 0
  },
  "integrity": {
    "publicTestsUnchanged": true,
    "configurationUnchanged": true
  },
  "evaluatorExitCode": 0,
  "evaluationStatus": "completed"
}
```

Il report di dettaglio conserva anche identificatore del caso, suite, esito,
durata ed errore normalizzato. I dati grezzi sono distinti dalle metriche
aggregate e dalle interpretazioni successive.

## Metriche temporali separate

Il tempo del task e quello della valutazione misurano attività diverse e non
devono essere sommati nella metrica principale:

- `agentTaskDurationMs` inizia immediatamente prima dell'avvio
  dell'architettura e termina quando questa conclude o raggiunge il timeout;
- `evaluationDurationMs` inizia dopo il congelamento della submission e termina
  quando il controllore ha acquisito e validato tutti i report;
- i report grezzi possono inoltre registrare separatamente durata della suite
  pubblica, durata della suite riservata e overhead del controllore.

Preparazione dell'ambiente e archiviazione possono essere registrate come
overhead infrastrutturale separato. Il confronto della velocità delle
architetture usa il tempo del task, mentre il tempo del valutatore serve a
controllare costo e stabilità dell'infrastruttura.

## Tipi di test e loro ruolo

### Regressione applicabile a T6

T6 modifica intenzionalmente forma e stati pubblici degli ordini e il
comportamento della conferma con scorta insufficiente. I 27 casi comuni originali
restano invariati, ma per T6 sono canonici soltanto i 21 casi integralmente
compatibili individuati prima della costruzione del pacchetto.

I sei casi comuni non applicabili a T6 sono:

| Test | Motivo |
| --- | --- |
| `creates and reads a draft order without changing stock` | confronta il vecchio formato delle righe |
| `confirms an order and decrements stock` | richiede lo stato `confirmed` |
| `keeps a failed confirmation atomic` | richiede errore per scorta insufficiente anziché evasione parziale |
| `does not decrement an earlier product when a later product is insufficient` | esclude l'allocazione parziale richiesta da T6 |
| `deeply copies input lines and returned order snapshots` | confronta il vecchio formato delle righe |
| `retains duplicate lines and confirms them when total stock is sufficient` | richiede stato e formato righe precedenti |

La suite pubblica T6 contiene quindi 26 casi applicabili: 21 regressioni comuni e
cinque casi T6. Nessuno di questi 26 casi può essere saltato. Gli eventuali sei
elementi mostrati come `skipped` dal reporter per effetto del filtro sui file
comuni rappresentano esclusioni non applicabili, non casi canonici T6 saltati.
Questa classificazione deve essere identica per tutte le architetture e fissata
prima delle esecuzioni. In questo passo non viene creato un manifest o un
valutatore.

### Test unitari

Verificano regole isolate come validazioni, aggregazione delle righe e transizioni
di stato. Sono rapidi e aiutano a localizzare gli errori, ma non dimostrano da
soli il funzionamento del gestionale.

### Test di integrazione

Verificano la collaborazione tra catalogo, magazzino, ordini e archivi. Sono
necessari per atomicità, isolamento e consistenza tra moduli.

### Test di accettazione

Usano soltanto il contratto pubblico della libreria e verificano i flussi
richiesti dalle specifiche T1–T6. Costituiscono la misura principale della
correttezza funzionale osservabile.

Ogni task userà la combinazione già definita nelle specifiche B2. Il punteggio non
dipenderà dal numero di test scritti dall'agente, ma dagli esiti della suite
canonica pubblica e riservata.

## Criterio univoco di successo

Un task è **completato correttamente** soltanto quando, per quella esecuzione:

1. la submission viene compilata con successo;
2. i controlli di integrità non rilevano violazioni;
3. il valutatore conclude senza errori infrastrutturali;
4. tutti i test canonici pubblici sono superati;
5. tutti i test canonici riservati sono superati;
6. nessun test canonico risulta saltato o `todo`.

I test aggiuntivi degli agenti non influiscono su questa decisione. Un test
canonico saltato vale come **non superato** e impedisce il completamento. Se il
salto dipende da un difetto dimostrato del valutatore o della suite, l'esecuzione
è invece `evaluator-error`, non viene valutata e segue la regola di ripetizione
stabilita prima dell'esperimento.

I risultati parziali sono descrittivi e non cambiano la soglia di completamento:

```text
publicPassRate = publicPassed / publicExpected
reservedPassRate = reservedPassed / reservedExpected
canonicalPassRate = (publicPassed + reservedPassed)
                    / (publicExpected + reservedExpected)
```

`publicExpected` e `reservedExpected` provengono dal manifest canonico e includono
tutti i casi attivi previsti, compresi quelli saltati durante l'esecuzione. Ogni
caso canonico pesa una unità; il numero di singole asserzioni interne non altera
il punteggio. Build failure produce tassi pari a zero. Un errore del valutatore
rende invece il risultato non valido e i tassi non vengono interpretati come
prestazione dell'architettura.

## Regole di comparabilità

- Stesso commit della baseline e stesso task per ogni confronto.
- Stessa versione e configurazione di Vitest.
- Stessi test canonici e stesso manifest di integrità.
- Stesse versioni di runtime e dipendenze.
- Stessa politica di timeout e parallelismo dei test.
- Stesso schema JSON e stesso valutatore.
- Tre ripetizioni per ciascuna coppia task–architettura.
- Nessun risultato riservato dettagliato viene restituito agli agenti durante o
  tra le ripetizioni.
- Stesse regole di completamento, conteggio dei test saltati e calcolo dei tassi
  parziali.

## Classificazione degli esiti anomali

Gli esiti devono distinguere almeno:

- `completed`: valutazione terminata e risultati validi;
- `test-failures`: valutatore funzionante con uno o più test falliti;
- `build-failure`: soluzione non compilabile;
- `timeout`: limite di valutazione superato;
- `integrity-violation`: test o configurazioni protette alterati;
- `evaluator-error`: problema dell'infrastruttura non attribuibile alla soluzione.

Un errore del valutatore non deve essere conteggiato automaticamente come errore
funzionale dell'architettura. La relativa esecuzione deve essere analizzata e, se
necessario, ripetuta secondo una regola stabilita prima dell'esperimento.

## Verifiche obbligatorie prima delle 54 esecuzioni

Prima di dichiarare operativa la strategia sarà necessario dimostrare che:

1. l'identità dell'agente legge ma non modifica o elimina i test pubblici;
2. l'identità dell'agente non può elencare, leggere o raggiungere l'area riservata;
3. l'ambiente agente contiene soltanto i materiali autorizzati e non contiene
   `.git`, collegamenti al repository originale o credenziali per l'area riservata;
4. nessun materiale riservato è presente nella cronologia, nelle ref o negli
   oggetti del repository del benchmark accessibile agli agenti;
5. il codice della submission, eseguito dal worker, non può leggere gli archivi
   originali o i risultati di altre esecuzioni;
6. il worker non può usare la rete né accedere a credenziali o variabili sensibili;
7. una modifica ai test pubblici viene rilevata dagli hash;
8. il valutatore usa le proprie configurazioni anche se la submission ne contiene
   altre;
9. due valutazioni della stessa soluzione producono gli stessi esiti;
10. il JSON generato è valido rispetto allo schema approvato;
11. un errore infrastrutturale è distinto da un test fallito;
12. nessun dato di una esecuzione raggiunge quella successiva.

Queste sono verifiche pianificate, non ancora eseguite.

## Rischi metodologici e mitigazioni

| Rischio | Mitigazione progettata |
| --- | --- |
| Agente modifica i test pubblici | ACL di sola lettura, identità limitata e verifica hash |
| Agente scopre i test riservati | area fisicamente separata e non accessibile all'identità agente |
| Codice della submission legge originali o altri risultati | worker temporaneo senza permessi sulle aree fidate |
| Codice della submission esfiltra dati | rete disabilitata, nessuna credenziale e output controllato |
| Configurazione diversa tra prove | config versionata, hash e valutatore unico |
| Aggiornamento involontario dipendenze | versione esatta, lock file e `npm ci` |
| Test dipendenti dall'ordine | istanze nuove, stato controllato e ordine non assunto |
| Leakage tra ripetizioni | ambienti nuovi e nessun dettaglio riservato restituito |
| Reporter cambia formato | versione Vitest fissata e normalizzatore con schema proprio |
| Guasto del valutatore confuso con errore agente | stati finali e codici di errore distinti |

## Stato di approvazione e verifiche rinviate

Sono approvati a livello progettuale:

1. isolamento a due ruoli tra controllore fidato e worker temporaneo;
2. esclusione dei test aggiuntivi dal punteggio ufficiale;
3. metriche temporali separate;
4. criterio di successo e formule dei risultati parziali;
5. scelta proporzionata del meccanismo di isolamento;
6. conservazione e versionamento separati dei materiali riservati, con divieto di
   inserirli nel repository del benchmark anche temporaneamente;
7. preparazione dell'ambiente agente mediante copia dei soli materiali
   autorizzati, senza `.git`, collegamenti all'originale o credenziali riservate;
8. registrazione esterna di commit di provenienza, versioni e hash dei materiali;
9. direzione tecnica Claude CLI, server MCP locale controllato, orchestratore .NET
   e valutatore esterno, sviluppata per iterazioni verificabili.

Restano attività di implementazione e verifica, non proprietà già dimostrate:

- selezionare e approvare la versione esatta di Vitest;
- individuare il meccanismo Windows più semplice disponibile;
- definire percorsi fisici e politica di eliminazione delle aree temporanee;
- implementare e verificare la copia basata su materiali autorizzati e la
  registrazione di provenienza e hash;
- implementare controllore, worker, configurazioni e report;
- verificare la configurazione esclusiva della Claude CLI e l'assenza di strumenti,
  hook, plugin, MCP e delegazioni alternativi;
- scegliere e provare il confine minimo per gli strumenti MCP che eseguono codice
  modificabile dagli agenti e per tutti i relativi processi figli;
- eseguire le prove negative di accesso a file, cronologia Git, repository
  originale, risultati, credenziali e rete;
- dimostrare con test concreti l'isolamento prima della sperimentazione.

Una prova circoscritta ha successivamente collegato il prototipo MCP al runner
pubblico P2.2 senza coinvolgere Claude o il valutatore. L'operazione sperimentale
`run_public_suite`, priva di parametri, ha avviato una nuova Windows Sandbox e ha
restituito 27 test pubblici superati, lasciando zero istanze residue. Parametri
estranei, timeout e report mancante sono stati riportati come errori. Esiti e
limiti sono documentati in [P3 — Collegamento sperimentale MCP–Windows
Sandbox](P3-collegamento-mcp-windows-sandbox.md); la prova non congela l'API MCP
completa.

L'approvazione conclude B3 soltanto a livello progettuale e non autorizza
installazioni o implementazioni. Le proprietà di isolamento non dovranno essere
descritte come verificate finché le prove pianificate non saranno state eseguite
con esito osservato e registrato.
