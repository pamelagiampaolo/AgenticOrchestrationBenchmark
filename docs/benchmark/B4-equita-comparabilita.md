# B4 — Equità e comparabilità sperimentale

## Registro

- **Data:** 2 novembre 2026
- **Fase:** 2, blocco B, passo B4
- **Obiettivo:** definire condizioni equivalenti e regole uniformi per il
  confronto tra single-agent, gerarchia statica e gerarchia adattiva.
- **Stato:** progettazione metodologica approvata dall'utente il 2 novembre 2026;
  nessuna implementazione o prova pilota avviata.
- **Criterio approvato:** budget complessivo condiviso per esecuzione, comprendente
  tutti gli agenti e il coordinatore.

## Principio di equità

Le tre condizioni devono ricevere lo stesso problema, la stessa baseline e lo
stesso budget complessivo. Non devono invece essere rese artificialmente uguali
nella loro organizzazione interna: numero di agenti, delegazione e coordinamento
sono caratteristiche dell'architettura studiata.

L'esperimento non presume che più agenti siano migliori. Nei task semplici il
costo del coordinamento può costituire uno svantaggio reale e scientificamente
significativo.

## Elementi identici in ogni confronto

Per una stessa combinazione task–ripetizione devono essere uguali:

- commit della baseline e stato iniziale dei dati;
- insieme autorizzato dei materiali copiati nell'ambiente agente, preparato senza
  `.git`, collegamenti al repository originale o credenziali riservate secondo B3;
- specifica ufficiale del task e test pubblici canonici;
- modello Claude Sonnet e identificatore esatto della versione disponibile;
- configurazione del modello esposta e controllabile;
- strumenti, privilegi, accesso alla rete e permessi sui file;
- macchina, sistema operativo e versioni di Node.js, npm e dipendenze;
- limite massimo di tempo del task;
- budget totale di token dell'esecuzione;
- regola di interruzione e acquisizione della submission;
- valutatore, test riservati e criterio di successo;
- politica di persistenza di log, metriche e risultati.

Il task prompt comune sarà separato dalle istruzioni specifiche
dell'architettura. Entrambi saranno versionati e identificati tramite hash. Le
istruzioni architetturali potranno differire soltanto per realizzare single-agent,
gerarchia statica o gerarchia adattiva, non per fornire suggerimenti aggiuntivi
sulla soluzione.

Non sarà sufficiente registrare il nome generico “Claude Sonnet”. Per ogni run
saranno conservati l'identificatore esatto del modello restituito o accettato
dalla Claude CLI, la versione della CLI e la configurazione effettiva. Se il
fornitore non consente di fissare o verificare un identificatore più specifico,
questa limitazione sarà registrata e sottoposta ad approvazione prima delle run
ufficiali; non verrà sostituita con un valore presunto né descritta come versione
controllata.

## Differenze architetturali ammesse

- **Single-agent:** un solo agente svolge pianificazione e implementazione.
- **Gerarchia statica:** ruoli, numero massimo di agenti e schema di delegazione
  sono fissati prima dell'esecuzione.
- **Gerarchia adattiva:** il coordinatore può decidere se e come delegare entro i
  limiti approvati.

Tutti i processi e tutte le chiamate LLM appartenenti alla condizione, inclusi
coordinatore, lavoratori, retry e correzioni, consumano lo stesso budget globale
della rispettiva esecuzione. Non esiste un budget aggiuntivo per singolo agente.

## Metriche Claude CLI effettivamente osservate

L'ispezione di `AgenticExperimentRunner/Program.cs` del 2 novembre 2026 mostra che
l'orchestratore attuale legge da `usage` nel JSON di Claude CLI:

- `input_tokens`;
- `output_tokens`;
- `cache_read_input_tokens`;
- `cache_creation_input_tokens`.

Legge inoltre `duration_ms` al livello principale. Il codice calcola già:

```text
inputTotal = input_tokens
             + cache_read_input_tokens
             + cache_creation_input_tokens

tokenTotal = inputTotal + output_tokens
```

Questa è evidenza sul prototipo single-call attuale, non ancora sul futuro sistema
multi-agente. Prima dell'implementazione dovrà essere verificato se il JSON di
ogni modalità riporta uso della singola chiamata, uso cumulativo della sessione o
uso comprensivo di eventuali processi figli.

## Regola di contabilizzazione dei token

### Valori grezzi obbligatori

Per ogni record Claude CLI devono essere conservati separatamente:

- token di input non serviti dalla cache;
- token di output;
- token di input letti dalla cache;
- token di input usati per creare la cache;
- durata dichiarata dalla CLI;
- output JSON originale o una sua copia tecnica verificabile;
- identificatore di run, agente, ruolo, processo e chiamata.

I valori di cache non vengono incorporati in `input_tokens`: restano categorie
distinte e vengono sommati soltanto nei campi derivati. In questo modo cache read
e cache creation non vengono confuse con nuovo input.

### Unità autorevole e deduplicazione

Ogni utilizzo LLM deve contribuire una sola volta. Prima delle prove ufficiali le
prove pilota stabiliranno quale record sia autorevole:

- se ogni processo restituisce soltanto il proprio uso, si sommano i record
  elementari di coordinatore e lavoratori;
- se un record padre include già i figli, si usa il riepilogo padre oppure i
  record figli, mai entrambi;
- se un valore è cumulativo di sessione, per ogni chiamata si registra il delta
  rispetto al valore cumulativo precedente, non il cumulativo completo;
- retry e chiamate fallite vengono conteggiati quando Claude CLI riporta consumo;
- un record senza metriche non viene stimato: è marcato `usage-missing` e rende la
  misura dei token incompleta.

Ogni record avrà un identificatore univoco. Il normalizzatore rifiuterà duplicati
con la stessa chiave invece di sommarli silenziosamente. Saranno conservati sia i
campi grezzi sia le somme normalizzate, così il calcolo resterà verificabile.

### Totali per esecuzione

Per la run `r`, dopo la deduplicazione:

```text
nonCachedInput(r) = somma degli input_tokens autorevoli
cacheRead(r) = somma dei cache_read_input_tokens autorevoli
cacheCreation(r) = somma dei cache_creation_input_tokens autorevoli
output(r) = somma degli output_tokens autorevoli

totalInput(r) = nonCachedInput(r) + cacheRead(r) + cacheCreation(r)
totalTokens(r) = totalInput(r) + output(r)
```

Il budget approvato usa `totalTokens(r)`, quindi include input, output, cache read
e cache creation di tutti i ruoli. Le quattro componenti saranno comunque
riportate separatamente per consentire analisi trasparenti. Un'eventuale stima dei
costi monetari resterà distinta e verrà calcolata soltanto con prezzi autorevoli.

## Token e costo economico sono metriche distinte

`totalTokens(r)` misura il consumo tecnico e determina il rispetto del budget.
Non rappresenta direttamente il costo economico, perché input non in cache,
creazione cache, lettura cache e output possono avere tariffe differenti.

Quando saranno disponibili tariffe ufficiali applicabili al modello esatto, la
stima sarà calcolata separatamente:

```text
estimatedCost(r) = nonCachedInput(r) × inputRate
                 + cacheCreation(r) × cacheCreationRate
                 + cacheRead(r) × cacheReadRate
                 + output(r) × outputRate
```

Le tariffe saranno normalizzate alla stessa unità, valuta e data di riferimento.
Per ogni stima saranno registrati fonte autorevole, data, modello e versione del
listino. Se le categorie esposte dalla CLI non sono sufficienti ad applicare una
tariffa senza ambiguità, il costo sarà marcato come incompleto anziché inventato.

Claude Pro è un abbonamento e non addebita necessariamente ogni run secondo le
tariffe per token usate nella formula. `estimatedCost` sarà quindi una stima
analitica comparabile, non una dichiarazione dell'importo realmente fatturato. Il
costo dell'abbonamento, se riportato, resterà una voce distinta.

La metrica richiesta dal tutor è definita, per ciascun gruppo di confronto, come:

```text
meanCostPerCorrectCompletion = somma dei costi stimati di tutte le run valide
                               / numero di run completate correttamente
```

Il numeratore include anche il costo delle run valide non completate, così la
metrica rappresenta il costo sostenuto per ottenere un completamento corretto e
non ignora i tentativi falliti. Se nessuna run del gruppo è completata
correttamente, il valore è `null` e viene riportato insieme a denominatore zero.
Come misura secondaria potrà essere riportato il costo medio delle sole run
riuscite, con un nome distinto per evitare ambiguità.

Il valore principale sarà calcolato separatamente per ciascuna architettura sulle
18 run ufficiali che le appartengono. Sarà inoltre possibile riportare il valore
per coppia task–architettura sulle 3 ripetizioni, mantenendo espliciti numeratore,
denominatore e numero di completamenti corretti.

## Tempo del task e tempi esclusi

Il limite temporale si applica a `agentTaskDurationMs`, già distinto in B3 dal
tempo del valutatore:

- inizio: immediatamente prima dell'avvio dell'architettura;
- fine: conclusione dell'architettura oppure prima rilevazione del timeout;
- inclusi: pianificazione, chiamate LLM, uso degli strumenti, coordinamento,
  compilazioni e test avviati dagli agenti;
- esclusi: preparazione della copia iniziale, valutazione canonica e archiviazione
  finale dell'infrastruttura.

Il tempo del valutatore e gli overhead infrastrutturali restano registrati, ma non
consumano il limite del task e non vengono usati come velocità dell'architettura.

## Prove pilota per determinare i limiti

I valori numerici di tempo e token saranno fissati soltanto dopo prove pilota. Le
prove pilota non fanno parte delle 54 esecuzioni ufficiali e non contribuiscono ai
risultati della tesi.

### Regole dei piloti

- Tutte e tre le architetture eseguono gli stessi workload di calibrazione.
- I workload comprendono almeno un caso circoscritto e uno trasversale, ma non
  espongono i test riservati ufficiali.
- Modello, strumenti, macchina e condizioni di cache sono equivalenti.
- L'ordine delle architetture è bilanciato o randomizzato con seed registrato, per
  evitare che una condizione benefici sistematicamente dell'ordine.
- Le sessioni sono nuove e non vengono riprese tra prove.
- Tempi, quattro categorie di token, errori e completezza delle metriche vengono
  registrati per ogni prova.
- Risultati anomali dovuti all'infrastruttura sono ripetuti e motivati, non rimossi
  silenziosamente.

### Derivazione dei limiti

Prima di osservare i risultati dei piloti sarà approvata una regola comune per
trasformare i valori osservati in limiti, per esempio massimo valido più un
margine uniforme. La stessa regola sarà applicata all'insieme delle tre
architetture, non separatamente a ciascuna.

Tempo e token definitivi devono:

- permettere realisticamente di completare i workload;
- non essere calibrati sulla sola architettura più veloce o più economica;
- non garantire risorse illimitate alla condizione più costosa;
- lasciare osservabile l'overhead di coordinamento;
- essere congelati prima delle 54 esecuzioni ufficiali.

Numero di piloti, workload, margine e valori finali saranno documentati e
approvati prima dell'uso. Nessun risultato ufficiale verrà usato per ritarare i
limiti.

### Proporzionalità e limiti Claude Pro

Il piano dei piloti sarà mantenuto minimo rispetto alla scadenza del 2 dicembre:
servirà a verificare la semantica delle metriche e a calibrare valori realistici,
non a duplicare l'intera sperimentazione. Numero massimo e criteri di arresto dei
piloti saranno approvati prima di iniziare, per evitare espansioni opportunistiche.

Eventuali limiti di utilizzo, finestre di ripristino o rate limit di Claude Pro
sono vincoli del servizio e non coincidono con tempo e token sperimentali. Saranno
registrati come eventi infrastrutturali. I piloti e le run ufficiali saranno
pianificati per ridurne l'interferenza; una run interrotta dal servizio sarà
classificata separatamente e gestita con la regola predefinita per gli errori
infrastrutturali, senza modificare il budget della singola architettura.

## Controllo uniforme dei limiti

Un controllore esterno all'architettura mantiene un cronometro monotono e il
contatore normalizzato dei token. L'architettura non può modificare questi valori.

### Limite temporale

Al raggiungimento del tempo massimo il controllore:

1. impedisce l'avvio di nuove operazioni;
2. richiede la terminazione di coordinatore e lavoratori;
3. dopo un periodo tecnico fisso di arresto forza la chiusura dei processi ancora
   attivi;
4. congela la workspace nello stato raggiunto;
5. assegna lo stato `time-limit-exceeded`.

Il periodo tecnico di arresto non può essere usato per continuare il task e viene
registrato come overhead infrastrutturale.

### Limite di token

Poiché le metriche disponibili nel prototipo vengono lette dal JSON al termine di
una chiamata Claude CLI, il controllo minimo verificabile avviene tra chiamate:

1. se il totale è già uguale o superiore al budget, non parte una nuova chiamata;
2. al termine di ogni chiamata si acquisisce e normalizza l'uso riportato;
3. se la chiamata porta il totale oltre il limite, non partono altre chiamate,
   l'esecuzione viene terminata e classificata `token-limit-exceeded`;
4. l'intero consumo della chiamata che ha superato la soglia resta registrato.

Questa è una limitazione attuale della Claude CLI osservata nel prototipo: il
controllore non conosce il consumo definitivo mentre la chiamata è in corso. Per
ogni superamento saranno quindi registrati `tokenBudget`, `tokensObserved` e
`tokenOverage = tokensObserved - tokenBudget`. La stessa eccedenza tecnica
inevitabile dell'ultima chiamata viene trattata uniformemente nelle tre
architetture e non autorizza l'avvio di una chiamata successiva.

Un'eventuale capacità futura della CLI di applicare un limite più preciso potrà
essere usata soltanto se verificata e applicata identicamente alle tre condizioni.

### Superamento simultaneo o metriche mancanti

- Se il tempo scade mentre una chiamata è attiva, il controllore applica subito il
  timeout e registra anche i token ottenibili dal processo terminato.
- Se entrambi i limiti risultano superati nello stesso intervallo di controllo,
  lo stato è `resource-limits-exceeded` e conserva entrambi i dettagli.
- Se mancano metriche necessarie al controllo, lo stato è
  `usage-measurement-error`; la run non viene interpretata come confronto valido
  finché la causa infrastrutturale non è risolta.

## Submission e valutazione dopo un limite

Anche dopo il superamento di un limite, la workspace viene congelata e sottoposta
al valutatore esterno quando ciò è tecnicamente sicuro. I tassi parziali dei test
canonici vengono registrati per descrivere lo stato raggiunto.

Una run interrotta per limiti non è però classificata come task completato entro
il budget, anche se la submission parziale supera successivamente tutti i test.
Il risultato mantiene separati:

- stato dell'esecuzione rispetto al budget;
- correttezza funzionale della submission congelata;
- causa e quantità del superamento.

## Ripetizioni, ordine e contaminazione

- Ogni coppia task–architettura ha 3 ripetizioni, per 54 run totali.
- Ogni run usa una copia pulita degli stessi materiali autorizzati della baseline,
  preparata dal commit stabilito secondo B3.
- Non vengono riutilizzate sessioni, memoria conversazionale o file modificati.
- I dettagli dei test riservati non vengono restituiti tra le ripetizioni.
- L'ordine delle condizioni viene bilanciato o randomizzato con seed registrato.
- Cache osservata e posizione nell'ordine vengono registrate per controllare
  possibili effetti residui.
- Una run viene ripetuta soltanto per un errore infrastrutturale definito in
  anticipo, mai perché il risultato funzionale è sfavorevole.

## Campi minimi da registrare per la comparabilità

- `runId`, task, architettura e numero di ripetizione;
- commit della baseline e hash dei prompt;
- identificatore esatto di Claude Sonnet, versione CLI e configurazione;
- budget temporale e budget token approvati;
- orari di avvio e fine, `agentTaskDurationMs` e overhead;
- per ogni chiamata: agente, ruolo, identificatore e quattro categorie di token;
- totali deduplicati di input, cache read, cache creation, output e totale;
- tariffe utilizzate, fonte e costo economico stimato separato;
- budget token, consumo osservato ed eventuale eccedenza dell'ultima chiamata;
- stato finale e motivo dell'eventuale interruzione;
- hash della submission congelata;
- esiti pubblici e riservati prodotti da B3;
- indicatori `usageComplete` e `integrityValid`.

## Verifiche richieste prima dell'esperimento

Prima delle 54 run ufficiali dovrà essere dimostrato che:

1. tutte le architetture ricevono realmente lo stesso commit e task prompt;
2. le copie agenti contengono gli stessi materiali autorizzati e sono prive di
   `.git`, collegamenti al repository originale e credenziali riservate;
3. modello, strumenti e permessi coincidono salvo le differenze architetturali;
4. coordinatore e lavoratori consumano lo stesso budget globale;
5. i record di uso non vengono duplicati tra padre, figli o sessioni cumulative;
6. cache read e cache creation restano separate e sono incluse nel totale;
7. timeout e limite token interrompono tutti i processi con la stessa regola;
8. la workspace congelata non cambia durante la valutazione;
9. una run con metriche mancanti viene riconosciuta come non valida;
10. ordine e seed sono registrati e riproducibili;
11. i valori definitivi derivano esclusivamente dai piloti documentati.

Queste sono proprietà progettate, non ancora verificate.

## Rischi e mitigazioni

| Rischio | Mitigazione progettata |
| --- | --- |
| Multi-agent riceve più risorse perché usa più processi | budget globale per run, coordinatore incluso |
| Doppio conteggio tra riepilogo e chiamate figlie | granularità verificata, ID univoci e deduplicazione |
| Cumulativi di sessione sommati più volte | calcolo dei delta tra letture consecutive |
| Cache confusa con input nuovo | quattro componenti grezze conservate separatamente |
| Limiti calibrati su una sola architettura | stessi piloti e regola unica sull'insieme completo |
| Timeout applicato diversamente | controllore esterno e procedura uniforme |
| Singola chiamata supera il budget token | nessuna nuova chiamata e consumo eccedente registrato |
| Costo confuso con volume di token | costo separato e tariffe per categoria documentate |
| Limite Claude Pro confuso con limite sperimentale | evento infrastrutturale distinto e regola predefinita |
| Ordine delle run influenza cache o prestazioni | ordine bilanciato/randomizzato e seed registrato |
| Errore infrastrutturale trattato come fallimento | stato specifico e regola di ripetizione predefinita |

## Stato di approvazione e parametri ancora da fissare

B4 è concluso a livello progettuale. Prima delle prove pilota devono ancora
essere fissati operativamente:

1. workload e numero delle prove pilota;
2. regola e margine usati per derivare i limiti;
3. granularità autorevole delle metriche Claude CLI dopo verifica controllata;
4. periodo tecnico uniforme concesso per arrestare i processi;
5. schema definitivo degli stati di interruzione.

Dopo i piloti devono essere approvati e congelati i valori numerici di tempo e
token. Dovranno inoltre essere registrati il modello Sonnet esatto e le tariffe
autorevoli applicabili. L'approvazione di B4 non autorizza automaticamente
implementazioni o prove.
