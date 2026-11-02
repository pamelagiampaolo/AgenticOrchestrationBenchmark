# B5 — Definizione completa delle metriche sperimentali

## Registro

- **Data:** 2 novembre 2026
- **Fase:** 2, blocco B, passo B5
- **Obiettivo:** definire dati grezzi, metriche derivate e regole di aggregazione
  per confrontare single-agent, gerarchia statica e gerarchia adattiva.
- **Perimetro:** 6 task, 3 architetture e 3 ripetizioni per ogni coppia
  task–architettura, per un totale pianificato di 54 esecuzioni ufficiali.
- **Stato:** proposta progettuale da revisionare; nessuna implementazione o
  esecuzione sperimentale autorizzata da questo documento.
- **Vincoli ereditati:** criteri di successo e valutazione indipendente di B3;
  equità, budget globale, contabilizzazione dei token e formula di costo di B4.

## Scopo e principi

Le metriche devono rendere osservabili correttezza, risorse impiegate e costi di
coordinamento senza presupporre la superiorità di una architettura. Le stesse
definizioni sono applicate alle tre condizioni; le metriche specifiche della
delega sono riportate soltanto quando il comportamento esiste e può essere
osservato direttamente.

Si adottano i seguenti principi:

1. i record prodotti dai processi e dal valutatore sono **dati grezzi**;
2. somme, tassi, indicatori, costi e riepiloghi sono **metriche derivate**;
3. dati misurati, stime economiche e interpretazioni restano distinti;
4. un valore mancante non viene sostituito con zero e non viene stimato senza una
   regola approvata;
5. ogni metrica conserva provenienza, completezza e formula verificabile;
6. prove pilota e run ufficiali sono identificate esplicitamente e non vengono
   aggregate insieme;
7. il numero ridotto di ripetizioni richiede riepiloghi descrittivi, non
   conclusioni statistiche forti.

## Unità di osservazione e identificatori

L'unità primaria è la **run ufficiale**, identificata da `runId`, task,
architettura e indice di ripetizione. Ogni run comprende l'intera architettura:
nel caso multi-agent include coordinatore, lavoratori, retry, correzioni e
chiamate fallite per le quali sia osservabile un consumo.

Ogni record elementare di uso LLM deve contenere almeno `runId`, `agentId`, ruolo,
processo, sessione e `callId`. I risultati dei test sono legati a `runId`, hash
della submission congelata, versione del manifest e configurazione del
valutatore. Questi collegamenti impediscono di sommare dati appartenenti a run o
submission differenti.

## Esecuzione valida ai fini dell'analisi

### Validità generale

Una run è valida per l'analisi principale quando:

1. è una delle run ufficiali previste dal piano, non una prova pilota;
2. identità di task, architettura, ripetizione, baseline e prompt sono note;
3. le condizioni sperimentali richieste da B4 sono state applicate;
4. non sono presenti violazioni d'integrità;
5. i dati necessari alla specifica metrica sono completi e affidabili.

La validità non richiede il completamento corretto. Restano quindi valide e non
possono essere escluse perché sfavorevoli:

- run con `test-failures` o `build-failure` attribuibili alla soluzione;
- run fermate da `time-limit-exceeded`, `token-limit-exceeded` o
  `resource-limits-exceeded`;
- run con errori di pianificazione, delega, coordinamento o uso degli strumenti
  attribuibili all'architettura.

Queste run contribuiscono ai conteggi, ai tassi di successo, alle distribuzioni
di consumo e, quando il costo è calcolabile, al numeratore del costo per
completamento corretto.

Non sono interpretabili come prestazione dell'architettura le run con violazione
d'integrità, `evaluator-error`, guasto esterno documentato o errore di misura che
impedisca di ricostruire la metrica in esame. Non vengono eliminate
silenziosamente: conservano record, causa, stato di validità e decisione di
ripetizione. Una ripetizione è consentita soltanto per le cause infrastrutturali
definite prima delle run ufficiali. La run sostitutiva riceve un nuovo `runId` e
un riferimento alla run invalidata; soltanto la sostitutiva occupa la cella del
disegno, mentre entrambe restano nell'audit trail.

La distinzione deve basarsi su evidenze strutturate e riproducibili, non
sull'esito sfavorevole della run. Un guasto è indipendente dall'architettura
soltanto quando riguarda un componente esterno al suo perimetro — per esempio
orchestratore, valutatore, ambiente comune o indisponibilità documentata del
servizio — ed è attestato da stato, codice di errore e log del componente
responsabile. Un errore originato da codice, comandi, processi, strumenti,
coordinamento o deleghe dell'architettura resta invece attribuito all'esecuzione,
anche quando impedisce il completamento. In caso di causa non determinabile la
run viene marcata non classificabile e sottoposta alla regola conservativa
approvata prima della sperimentazione; non può essere dichiarata
infrastrutturale mediante valutazione discrezionale successiva.

Non è mai consentita una sostituzione per errori funzionali, build failure della
soluzione, problemi di esecuzione, coordinamento, delega o ripianificazione,
oppure superamento del budget temporale o di token. Prima delle run ufficiali
devono essere congelati sia l'elenco dei codici infrastrutturali sostituibili sia
un numero massimo di tentativi sostitutivi per ciascuna cella del disegno. Ogni
tentativo, valido o invalidato, conserva `runId`, ordine, causa, log, dati
disponibili e collegamento alla cella e agli altri tentativi.

Se il limite viene raggiunto senza ottenere una run valida, la cella resta
esplicitamente incompleta. Non viene riempita riutilizzando una prova pilota,
riclassificando un tentativo invalido, trasferendo una run da un'altra cella o
modificando i denominatori pianificati. I riepiloghi mostrano run previste,
tentativi effettuati, run valide e motivo dell'assenza; le metriche non
calcolabili restano `null` e qualsiasi confronto interessato è dichiarato
incompleto.

### Validità per metrica

La validità è anche specifica della metrica. Per esempio, una run con valutazione
funzionale affidabile ma uso token incompleto può contribuire ai tassi dei test,
ma non alle aggregazioni di token o costo. Si registrano almeno:

- `analysisValidityStatus` e `analysisInvalidReason` per la run;
- `evaluationComplete`, `usageComplete`, `costComplete` e `integrityValid`;
- per ogni aggregato, numero di run previste, valide e con dato disponibile.

Questa regola evita sia di perdere risultati utilizzabili sia di costruire
confronti economici su dati incompleti. Se dopo le sostituzioni previste una cella
task–architettura contiene meno di tre run valide per una metrica, il riepilogo è
mostrato con il denominatore effettivo e marcato incompleto.

## Dati grezzi obbligatori

### Identità e condizioni della run

- `runId`, task, architettura, ripetizione e indicatore ufficiale/pilota;
- commit della baseline, hash del task prompt e delle istruzioni architetturali;
- modello esatto osservabile, versione Claude CLI e configurazione;
- versioni di ambiente, valutatore, suite e manifest;
- seed e posizione nell'ordine di esecuzione;
- budget temporale e budget token approvati;
- orari di inizio e fine e hash della submission congelata.

La fonte è l'orchestratore fidato. Campi mancanti che impediscono di dimostrare
la comparabilità rendono la run non valida; gli altri sono segnalati senza
inventare valori.

### Uso LLM elementare

Per ogni record autorevole Claude CLI:

- `input_tokens`;
- `output_tokens`;
- `cache_read_input_tokens`;
- `cache_creation_input_tokens`;
- `duration_ms` dichiarato dalla CLI;
- identificatori di agente, ruolo, processo, sessione e chiamata;
- esito e numero del tentativo, quando disponibile;
- JSON originale o copia tecnica verificabile.

La fonte è il JSONL Claude CLI acquisito dall'orchestratore. I duplicati sono
rifiutati, non sommati. Record cumulativi vengono trasformati in delta; record
padre e figli non vengono conteggiati insieme se descrivono lo stesso consumo.
Un record necessario senza usage è marcato `usage-missing` e rende incompleta la
relativa misura, non pari a zero.

### Tempi

Il controllore esterno registra con cronometro monotono:

- `agentTaskDurationMs`;
- `evaluationDurationMs`;
- eventuali durate separate delle suite pubblica e riservata;
- preparazione, arresto forzato e archiviazione come overhead infrastrutturali
  separati.

`duration_ms` della CLI resta un dato tecnico per chiamata e non sostituisce
`agentTaskDurationMs`. Timestamp di parete servono all'audit, non al calcolo
principale delle durate. Una durata assente o un cronometro non affidabile rende
non disponibile la relativa metrica.

### Valutazione indipendente

Il valutatore fidato registra:

- esito della compilazione;
- per suite, casi attesi, superati, falliti, saltati e `todo`;
- identificatore ed esito normalizzato di ogni caso;
- stato e codice di uscita del valutatore;
- esito dei controlli d'integrità;
- hash della submission e dei manifest usati.

I conteggi attesi provengono dal manifest canonico. I test aggiuntivi creati dagli
agenti non entrano nelle metriche ufficiali. Un errore del valutatore non viene
convertito in test falliti.

### Eventi ed errori

Ogni evento osservabile contiene almeno timestamp monotono, `runId`, componente,
categoria, codice stabile, severità, operazione e risultato. Messaggi liberi sono
diagnostici e non costituiscono da soli una categoria analitica.

Le categorie minime sono:

- **funzionale:** build o test canonici non superati;
- **limite di risorse:** tempo, token o entrambi;
- **esecuzione/strumento:** comando, processo o strumento dell'agente fallito;
- **coordinamento:** comunicazione, sincronizzazione o integrazione del lavoro;
- **delega:** assegnazione rifiutata, non avviata, fallita o non restituita;
- **ripianificazione:** revisione esplicita del piano dopo un insuccesso o cambio
  di strategia;
- **integrità:** alterazione o incongruenza di artefatti protetti;
- **infrastrutturale:** valutatore, orchestratore, servizio o ambiente esterno.

Un medesimo evento può avere una categoria primaria e codici secondari, ma viene
conteggiato una sola volta per ciascuna metrica. Le tassonomie e i punti di
emissione devono essere congelati prima delle run ufficiali. Errori presenti
soltanto nel testo libero e non classificabili automaticamente sono conservati e
riportati qualitativamente, non trasformati retroattivamente in conteggi scelti
dopo aver visto i risultati.

## Metriche derivate per run

### Correttezza e completamento

| Metrica | Definizione e unità | Raccolta e fonte | Dato mancante |
| --- | --- | --- | --- |
| `correctCompletion` | Booleano. Vero solo se la submission compila, l'integrità è valida, il valutatore conclude senza guasti, tutti i test pubblici e riservati passano, nessuno è saltato/`todo` e la run termina entro entrambi i budget. | Derivata da valutatore, controllore e stati B3–B4. | `null` se valutazione o integrità non sono affidabili; mai inferito dai log dell'agente. |
| `functionalSubmissionCorrect` | Booleano descrittivo: la submission congelata soddisfa tutti i criteri funzionali del valutatore, indipendentemente dallo stato dei budget. | Valutatore indipendente. Serve a descrivere submission dopo un limite, senza convertirle in completamenti corretti. | `null` se il valutatore non conclude validamente. |
| `publicPassRate` | Rapporto in `[0,1]`: `publicPassed / publicExpected`. | Conteggi del manifest e report canonico. | `null` per errore del valutatore; `0` per build failure, come stabilito in B3. |
| `reservedPassRate` | Rapporto in `[0,1]`: `reservedPassed / reservedExpected`. | Conteggi del manifest e report canonico. | Stessa regola dei test pubblici. |
| `canonicalPassRate` | Rapporto in `[0,1]`: `(publicPassed + reservedPassed) / (publicExpected + reservedExpected)`. Ogni caso pesa uno. | Valutatore indipendente. | `null` se la valutazione non è affidabile; `0` per build failure. |

I tassi vengono visualizzati anche come percentuali, ma memorizzati come rapporti
non arrotondati. I test saltati o `todo` sono inclusi nel denominatore e non nel
numeratore. `correctCompletion` è la metrica primaria di successo; i tassi sono
risultati parziali e non abbassano la soglia approvata.

### Stato finale e classificazione dei fallimenti

Ogni run conserva separatamente:

- `executionStatus`: conclusa, limite tempo, limite token, entrambi o errore di
  misura;
- `evaluationStatus`: completata, test falliti, build fallita, timeout del
  valutatore, violazione d'integrità o errore del valutatore;
- `failureDomain`: funzionale, risorse, architettura/esecuzione, integrità oppure
  infrastruttura;
- `primaryFailureCode` e lista ordinata degli eventi secondari.

La causa primaria è determinata con una precedenza congelata prima delle run e
non cancella le cause concorrenti. I conteggi per categoria e la matrice degli
stati costituiscono metriche descrittive; non si sommano categorie sovrapposte per
ottenere un inesistente “totale errori” senza deduplicazione.

### Durate

```text
agentTaskDurationMs = fine architettura o prima rilevazione timeout
                      - istante immediatamente precedente all'avvio

evaluationDurationMs = acquisizione e validazione completa dei report
                       - congelamento della submission
```

Entrambe sono millisecondi. `agentTaskDurationMs` include pianificazione,
chiamate LLM, strumenti, coordinamento, build e test avviati dagli agenti. Esclude
preparazione, valutazione canonica e archiviazione. È la misura di velocità
dell'architettura. `evaluationDurationMs` misura stabilità e costo operativo del
valutatore e non viene sommata al tempo degli agenti né imputata al loro budget.

### Token

Dopo selezione dei record autorevoli e deduplicazione:

```text
nonCachedInput(r) = Σ input_tokens
cacheRead(r)      = Σ cache_read_input_tokens
cacheCreation(r)  = Σ cache_creation_input_tokens
output(r)         = Σ output_tokens

totalInput(r)  = nonCachedInput(r) + cacheRead(r) + cacheCreation(r)
totalTokens(r) = totalInput(r) + output(r)
```

L'unità è il token dichiarato dalla CLI. Le quattro componenti e i due totali
sono riportati per ogni run. Nel multi-agent la somma comprende tutti i ruoli;
nel single-agent comprende ogni chiamata, retry e correzione dell'unico agente.
I subtotali per ruolo sono diagnostici e devono riconciliarsi esattamente con il
totale della run. `totalTokens` è la quantità usata dal budget tecnico.

### Costo economico stimato

Con tariffe autorevoli normalizzate nella stessa valuta per token:

```text
estimatedCost(r) = nonCachedInput(r) × inputRate
                 + cacheCreation(r) × cacheCreationRate
                 + cacheRead(r) × cacheReadRate
                 + output(r) × outputRate
```

La metrica conserva valuta, unità delle tariffe, fonte, data del listino e modello
applicabile. Se una tariffa è espressa per milione di token viene prima divisa per
1.000.000. Il risultato non è il costo fatturato dall'abbonamento Claude Pro, ma
una stima analitica comparabile. Uso incompleto, modello ambiguo o tariffa non
applicabile producono `estimatedCost = null` e `costComplete = false`.

### Uso ed eccedenza dei budget

```text
timeBudgetUtilization(r)  = agentTaskDurationMs / timeBudgetMs
tokenBudgetUtilization(r) = totalTokens(r) / tokenBudget

timeOverageMs(r) = max(0, agentTaskDurationMs - timeBudgetMs)
tokenOverage(r)  = max(0, totalTokens(r) - tokenBudget)
```

Le utilizzazioni sono rapporti e possono superare `1` per rendere visibile una
eccedenza. Sono accompagnate dagli indicatori booleani `timeLimitExceeded` e
`tokenLimitExceeded`. L'intero consumo dell'ultima chiamata che supera il budget
resta incluso. Il periodo tecnico di arresto è overhead infrastrutturale e non
entra in `agentTaskDurationMs` oltre il primo rilevamento del timeout.

### Errori osservabili

Per run si derivano conteggi per codice e categoria e gli indicatori
`hasExecutionError`, `hasCoordinationError`, `hasDelegationError` e
`hasReplanningEvent`. I conteggi misurano eventi registrati, non difficoltà
latenti. Un retry è conteggiato come retry; diventa errore soltanto se possiede un
evento di errore associato. Il numero di ripianificazioni conta revisioni
esplicite osservabili, non inferenze dal testo del ragionamento.

## Metriche di coordinamento e applicabilità

Le metriche comuni alle tre condizioni sono durata complessiva, quattro
categorie di token, totale, costo, retry, errori di strumento e utilizzo dei
budget. Nel single-agent pianificazione e lavoro appartengono allo stesso ruolo:
non si attribuisce artificialmente una quota di “coordinamento”.

Per entrambe le architetture multi-agent si raccolgono, se supportati da eventi
strutturati:

- numero di agenti avviati e numero massimo contemporaneamente attivo;
- numero di deleghe richieste, avviate, completate, fallite e annullate;
- conteggi di errori di delega e coordinamento;
- token e chiamate per coordinatore e per insieme dei lavoratori;
- eventi espliciti di ripianificazione;
- lavori avviati ma non restituiti prima della terminazione.

Per la gerarchia statica si verifica inoltre lo scostamento dalla configurazione
prefissata. Per la gerarchia adattiva si registrano numero di decisioni di
creazione/terminazione di lavoratori e variazioni osservabili del piano. Questi
dati descrivono il comportamento, non sono punteggi di qualità.

Non si definisce in B5 un “tempo puro di coordinamento”: senza strumentazione che
separi intervalli mutuamente esclusivi, attività parallele e attese produrrebbero
doppi conteggi. Analogamente non si introducono indici compositi di efficienza,
qualità della comunicazione o complessità del piano. Eventuali durate di eventi
di coordinamento potranno essere diagnostiche, ma non metriche principali finché
la loro semantica non sarà verificata nei piloti.

Per il single-agent i campi specifici di delega sono `not-applicable`, non zero;
zero resta un valore significativo per una run multi-agent nella quale nessuna
delega è avvenuta, possibilità particolarmente rilevante per la condizione
adattiva.

## Aggregazione e confronto

### Coppia task–architettura: tre ripetizioni

Per ciascuna delle 18 coppie si riportano anzitutto le tre run individuali. Il
riepilogo contiene:

- `correctCompletions / validRuns` e relativo tasso descrittivo;
- conteggi degli stati e delle categorie di fallimento;
- per pass rate, durate, token, costo e uso budget: valori individuali, mediana,
  minimo e massimo;
- media aritmetica come valore supplementare, sempre con `n` e senza nascondere i
  valori individuali;
- numero di valori mancanti o non affidabili per ogni metrica;
- somme di token e costi, quando tutte le componenti richieste sono disponibili.

Con tre osservazioni la mediana limita l'influenza di un singolo valore estremo,
mentre minimo e massimo rendono visibile la variabilità. Non si calcolano test di
significatività, intervalli di confidenza o ranking basati su differenze minime.

### Riepilogo per architettura: 18 esecuzioni

Per ogni architettura si riportano:

- completamenti corretti su 18 run previste, complessivi e separati per task;
- distribuzione dei tassi parziali e dei fallimenti;
- durata, token, costo e utilizzo budget con mediana, minimo, massimo e media;
- totali di token e costo sulle run con misure complete;
- frequenza dei superamenti di budget;
- conteggi degli eventi osservabili comuni e, per le condizioni multi-agent,
  metriche di coordinamento applicabili;
- copertura dei dati: run previste, valide e disponibili per ogni metrica.

L'aggregato complessivo assegna implicitamente lo stesso peso a ciascuna delle 18
run. Poiché i task possono avere difficoltà differenti, il risultato per task
rimane sempre visibile e non viene sostituito dalla sola media globale.

I confronti tra architetture sono effettuati sulla medesima granularità: stessa
coppia task–ripetizione quando si esaminano run, stesso task quando si aggregano
le ripetizioni e insieme fisso dei sei task nel riepilogo finale. Differenze di
ordine, cache, valori mancanti o run sostituite sono mostrate come contesto e non
corrette con aggiustamenti post hoc.

### Costo medio per completamento corretto

Per un gruppo `g`, secondo la formula approvata in B4:

```text
meanCostPerCorrectCompletion(g)
    = Σ estimatedCost(r) per tutte le run valide di g
      / numero di run di g con correctCompletion = true
```

Il numeratore include le run valide fallite e quelle fermate dai limiti. Una run
non viene esclusa perché costosa o non corretta. Il valore principale è calcolato
per architettura sulle 18 run ufficiali e, secondariamente, per coppia
task–architettura sulle tre ripetizioni.

La metrica è calcolabile soltanto se il costo è completo per tutte le run valide
del gruppo. Se manca anche un costo, il valore è `null` e si mostrano comunque
somma parziale e copertura, chiaramente etichettate e non usate per il confronto
principale. Se il denominatore è zero, il valore è `null`, con zero completamenti
esplicito. Il costo medio delle sole run riuscite, se mostrato, si chiama
`meanCostOfCorrectRuns` e resta una misura secondaria distinta.

## Interpretazione responsabile

Tre ripetizioni per cella permettono di osservare consistenza e casi anomali, ma
non sostengono affermazioni generali di superiorità statistica. Le conclusioni
devono indicare ampiezza e direzione delle differenze osservate, valori
individuali, variabilità, dati mancanti e possibili eventi infrastrutturali.

Un'architettura può risultare più corretta ma più lenta o costosa, oppure avere
un overhead non compensato nei task semplici. Nessuna metrica composita riduce
questi compromessi a un punteggio unico. Anche svantaggi o assenza di beneficio
della gerarchia adattiva costituiscono risultati validi.

## Stato tecnico reale al 2 novembre 2026

### Già verificato nella Fase 1

- Claude CLI produce le quattro categorie di token usate in B4 e `duration_ms`;
- il prototipo single-call le acquisisce e salva record JSONL;
- il prototipo calcola una somma tecnica di input e token totali.

Questa evidenza riguarda il percorso single-call osservato. Non dimostra ancora
la semantica dei record in sessioni cumulative o con processi padre e figli.

### Da implementare e verificare

- schema persistente completo di run, chiamate, eventi e indicatori di qualità;
- raccolta multi-agent con identità di coordinatore e lavoratori;
- scelta verificata dei record autorevoli, delta e deduplicazione;
- controllore dei budget globali e gestione uniforme delle eccedenze;
- cronometro monotono di `agentTaskDurationMs` e separazione degli overhead;
- valutatore indipendente, isolamento, manifest e report B3;
- classificazione strutturata di errori, deleghe e ripianificazioni;
- acquisizione e validazione di tariffe autorevoli;
- normalizzazione, aggregazioni e report delle 3 e 18 run;
- prove negative e piloti richiesti da B3 e B4.

Nessuna di queste funzionalità viene considerata disponibile soltanto perché è
definita in questo documento.

## Controlli da superare prima delle run ufficiali

1. Riconciliare i totali di ogni run con i record elementari senza duplicati.
2. Provare il significato dei record Claude CLI per coordinatore e figli.
3. Verificare che retry e chiamate fallite con consumo entrino una sola volta.
4. Simulare entrambi i limiti e verificare stato, submission ed eccedenza.
5. Verificare con casi noti formule dei tassi, build failure e test saltati.
6. Provare che guasti del valutatore producano valori non disponibili, non zeri.
7. Provare la regola di sostituzione e l'audit trail delle run invalide.
8. Verificare formule monetarie con un listino congelato e casi manuali noti.
9. Riconciliare aggregati a 3 e 18 run con i record di origine.
10. Confermare che prove pilota e run invalide non entrino nei risultati ufficiali.

## Decisioni da approvare o congelare prima dell'implementazione

B5 propone e sottopone a revisione:

1. la definizione di validità generale e per singola metrica;
2. la regola per cui fallimenti funzionali e limiti restano nelle aggregazioni,
   mentre soli guasti non attribuibili e integrità invalida consentono
   sostituzione predefinita;
3. l'uso di valori individuali, mediana, minimo e massimo come riepilogo primario,
   con media supplementare;
4. la tassonomia minima degli eventi e il valore `not-applicable` per le metriche
   di delega del single-agent;
5. la regola conservativa che rende nullo il costo per completamento corretto se
   il gruppo non ha copertura economica completa.

Restano inoltre da fissare, come già previsto da B3 e B4, lo schema definitivo
degli stati e la precedenza della causa primaria, la politica esatta di
ripetizione dei guasti infrastrutturali, i valori dei budget dopo i piloti, la
granularità autorevole dello usage, il modello esatto e il listino applicabile.

## Criterio di completamento di B5

B5 sarà completato progettualmente soltanto dopo la revisione e l'approvazione
delle definizioni, formule, regole di validità e aggregazioni qui proposte. Tale
approvazione non certificherà l'implementazione: raccolta, valutatore, budget e
report dovranno essere costruiti e verificati in passi successivi.
