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
area-fidata-controllore/                accesso: solo controllore fidato
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

1. L'orchestratore seleziona il commit approvato della baseline.
2. Crea una nuova area di esecuzione con identificatore univoco.
3. Copia i sorgenti ammessi e applica lo stato iniziale del task.
4. Espone i test pubblici di riferimento in sola lettura e una directory separata
   per i test aggiuntivi dell'agente.
5. Registra hash, versioni, task, architettura, ripetizione e orario iniziale.
6. Avvia l'architettura sperimentale con accesso limitato alla propria area.
7. Al termine blocca la soluzione e genera il manifest della submission.
8. Controlla integrità dei test pubblici, file consentiti e configurazioni.
9. Il controllore crea una nuova area temporanea e vi copia submission, test
   canonici e configurazioni autorizzate, senza esporre gli archivi originali.
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
3. il codice della submission, eseguito dal worker, non può leggere gli archivi
   originali o i risultati di altre esecuzioni;
4. il worker non può usare la rete né accedere a credenziali o variabili sensibili;
5. una modifica ai test pubblici viene rilevata dagli hash;
6. il valutatore usa le proprie configurazioni anche se la submission ne contiene
   altre;
7. due valutazioni della stessa soluzione producono gli stessi esiti;
8. il JSON generato è valido rispetto allo schema approvato;
9. un errore infrastrutturale è distinto da un test fallito;
10. nessun dato di una esecuzione raggiunge quella successiva.

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
5. scelta proporzionata del meccanismo di isolamento.

Restano attività di implementazione e verifica, non proprietà già dimostrate:

- selezionare e approvare la versione esatta di Vitest;
- individuare il meccanismo Windows più semplice disponibile;
- definire percorsi fisici e politica di eliminazione delle aree temporanee;
- implementare controllore, worker, configurazioni e report;
- eseguire le prove negative di accesso a file, risultati, credenziali e rete;
- dimostrare con test concreti l'isolamento prima della sperimentazione.

L'approvazione conclude B3 soltanto a livello progettuale e non autorizza
installazioni o implementazioni. Le proprietà di isolamento non dovranno essere
descritte come verificate finché le prove pianificate non saranno state eseguite
con esito osservato e registrato.
