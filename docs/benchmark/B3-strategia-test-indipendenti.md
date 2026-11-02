# B3 — Strategia dei test pubblici e riservati

## Registro

- **Data:** 2 novembre 2026
- **Fase:** 2, blocco B, passo B3
- **Obiettivo:** progettare una valutazione automatica, indipendente e identica
  per single-agent, gerarchia statica e gerarchia adattiva.
- **Stato:** proposta da revisionare; nessun test o pacchetto implementato.
- **Framework approvato:** Vitest.

## Principio di indipendenza

L'agente sviluppa la soluzione in un ambiente di esecuzione isolato. Un processo
fidato separato, chiamato **valutatore esterno**, riceve la soluzione soltanto al
termine e avvia i test canonici. L'agente non esegue il valutatore e non accede
alla sua directory, configurazione privata o identità di sistema.

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

### 3. Test riservati

Sono conservati nell'ambiente protetto del valutatore e non sono copiati,
collegati, montati o inclusi nell'ambiente dell'agente. Verificano gli stessi
requisiti ufficiali con dati, combinazioni e casi limite diversi da quelli
pubblici.

Gli agenti non ricevono nomi dei file, sorgenti, elenco dettagliato dei casi o
output diagnostico dei test riservati. Il risultato strutturato viene salvato in
una directory persistente non accessibile all'esecuzione sperimentale.

## Separazione fisica degli ambienti

La struttura logica proposta è la seguente; i percorsi reali saranno scelti e
verificati durante l'implementazione:

```text
area-fidata-valutatore/                 accesso: solo valutatore
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

area-risultati/<run-id>/                accesso: orchestratore/valutatore
├── submission-manifest.json
├── public-results.json
├── reserved-results.json
└── evaluation-summary.json
```

L'area fidata non deve essere una sottodirectory raggiungibile dall'identità che
esegue gli agenti. Su Windows la separazione dovrà essere applicata con una
identità dedicata e ACL NTFS: lettura/esecuzione soltanto dove necessarie, nessun
permesso sull'area fidata e nessuna scrittura sui test pubblici esposti.

La sola proprietà “sola lettura” del file non è considerata sufficiente, perché
può essere rimossa dal proprietario. Prima dell'esperimento sarà quindi
obbligatorio verificare realmente i permessi usando la stessa identità e gli
stessi strumenti concessi agli agenti.

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
9. Il valutatore crea una propria area temporanea fidata e vi importa la
   submission senza importare configurazioni di test non autorizzate.
10. Esegue nuovamente i test pubblici canonici con la configurazione fidata.
11. Esegue i test riservati con la configurazione fidata.
12. Salva output JSON grezzi e riepilogo normalizzato nell'area persistente dei
    risultati.
13. Registra exit code, errori del valutatore e stato finale dell'esecuzione.
14. Elimina o archivia l'area temporanea secondo la politica che verrà approvata,
    senza riutilizzarla per un'altra prova.

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
  "durationMs": 0,
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

## Tipi di test e loro ruolo

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
3. una modifica ai test pubblici viene rilevata dagli hash;
4. il valutatore usa le proprie configurazioni anche se la submission ne contiene
   altre;
5. due valutazioni della stessa soluzione producono gli stessi esiti;
6. il JSON generato è valido rispetto allo schema approvato;
7. un errore infrastrutturale è distinto da un test fallito;
8. nessun dato di una esecuzione raggiunge quella successiva.

Queste sono verifiche pianificate, non ancora eseguite.

## Rischi metodologici e mitigazioni

| Rischio | Mitigazione progettata |
| --- | --- |
| Agente modifica i test pubblici | ACL di sola lettura, identità limitata e verifica hash |
| Agente scopre i test riservati | area fisicamente separata e non accessibile all'identità agente |
| Configurazione diversa tra prove | config versionata, hash e valutatore unico |
| Aggiornamento involontario dipendenze | versione esatta, lock file e `npm ci` |
| Test dipendenti dall'ordine | istanze nuove, stato controllato e ordine non assunto |
| Leakage tra ripetizioni | ambienti nuovi e nessun dettaglio riservato restituito |
| Reporter cambia formato | versione Vitest fissata e normalizzatore con schema proprio |
| Guasto del valutatore confuso con errore agente | stati finali e codici di errore distinti |

## Decisioni ancora da approvare

Prima dell'implementazione devono essere approvati:

1. modello a tre insiemi di test;
2. separazione con identità dedicata e ACL NTFS;
3. flusso del valutatore esterno;
4. campi minimi del riepilogo JSON;
5. classificazione degli esiti anomali;
6. scelta successiva della versione esatta di Vitest;
7. percorsi fisici definitivi e politica di conservazione delle aree temporanee.

L'approvazione di B3 non autorizza automaticamente installazioni o
implementazioni.
