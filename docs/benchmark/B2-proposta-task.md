# B2 — Proposta dei task sperimentali T1–T6

## Registro del passo

- **Data:** 2 novembre 2026
- **Fase:** 2, blocco B, passo B2
- **Obiettivo:** proporre esattamente 6 task indipendenti, progressivi e
  verificabili per il gestionale di ordini, prodotti e magazzino.
- **Stato:** proposta da revisionare e approvare prima di implementare la
  baseline o redigere le specifiche operative definitive.

## Regole comuni

- Ogni task viene eseguito separatamente partendo dallo stesso commit pulito
  della baseline; i task non sono cumulativi.
- Le tre architetture ricevono la stessa specifica, lo stesso codice iniziale e
  gli stessi test pubblici di riferimento.
- I test pubblici di riferimento possono essere eseguiti, ma non modificati o
  eliminati. Gli agenti possono aggiungere propri test.
- La valutazione finale usa anche test riservati esterni alla copia modificabile.
- I dati applicativi restano in memoria e ogni test crea uno stato iniziale
  controllato, senza dati condivisi tra esecuzioni.
- Non sono richiesti database, rete, API HTTP o nuove dipendenze
  infrastrutturali.
- I nomi definitivi delle API e dei file saranno fissati durante la progettazione
  della baseline. I requisiti comportamentali descritti qui non dipendono da tali
  nomi.

## Quadro riassuntivo

| Task | Categoria principale | Complessità stimata | Coordinamento atteso |
| --- | --- | --- | --- |
| T1 | Modifica semplice e circoscritta | Bassa | Limitato |
| T2 | Nuova funzionalità | Medio-bassa | Limitato |
| T3 | Correzione di un difetto verificabile | Media | Limitato |
| T4 | Modifica trasversale a più moduli | Medio-alta | Moderato |
| T5 | Refactoring con compatibilità obbligatoria | Alta | Moderato |
| T6 | Funzionalità complessa e scomponibile | Alta | Elevato |

Le stime indicano la difficoltà progettuale prevista, non risultati osservati.
La presenza di task con coordinamento limitato permette di misurare anche
l'eventuale overhead delle architetture multi-agente, senza favorirle
artificialmente.

## T1 — Ricerca dei prodotti sotto scorta

### Obiettivo funzionale

Aggiungere una query che restituisca i prodotti la cui quantità disponibile è
inferiore a una soglia fornita dal chiamante.

### Requisiti precisi

- La soglia è un numero intero maggiore o uguale a zero.
- Una soglia negativa o non intera deve produrre un errore esplicito senza
  modificare lo stato.
- Sono inclusi soltanto i prodotti con quantità strettamente minore della soglia.
- Il risultato contiene i dati identificativi del prodotto e la quantità
  disponibile.
- Il risultato è ordinato in modo deterministico per identificatore del prodotto.
- La query non modifica catalogo, magazzino o ordini.

### Moduli potenzialmente interessati

Query di catalogo/magazzino, tipi pubblici restituiti dalla libreria e relativi
test. Non sono previste modifiche al flusso degli ordini.

### Condizioni iniziali

La baseline contiene prodotti con quantità diverse, inclusi almeno un prodotto
con quantità zero e uno esattamente uguale alla soglia usata dai test pubblici.

### Criteri oggettivi di completamento

- inclusione ed esclusione rispettano il confronto strettamente minore;
- ordinamento stabile e deterministico;
- validazione della soglia corretta;
- assenza di effetti collaterali.

### Test richiesti

- **Unitari:** validazione di soglie negative, decimali, zero e positive.
- **Integrazione:** query su catalogo e quantità del magazzino.
- **Accettazione:** risultato completo, ordinato e senza mutazioni dello stato.
- **Riservati:** catalogo vuoto, più prodotti a pari quantità e valori di confine.

### Stima e motivazione

**Bassa.** È una modifica circoscritta, con una regola di filtro e pochi casi
limite. Offre poche opportunità utili di parallelizzazione.

### Sottotask e dipendenze

1. definire il contratto del risultato;
2. validare la soglia;
3. implementare filtro e ordinamento;
4. aggiungere test dell'agente.

Le attività 2 e 3 dipendono dal contratto; non esistono dipendenze esterne.

## T2 — Carico manuale di magazzino

### Obiettivo funzionale

Aggiungere l'operazione con cui registrare l'arrivo di nuove unità di un prodotto
e incrementarne la quantità disponibile.

### Requisiti precisi

- Il prodotto deve esistere nel catalogo.
- La quantità ricevuta deve essere un numero intero strettamente positivo.
- L'operazione restituisce la nuova quantità disponibile.
- In caso di prodotto inesistente o quantità non valida, lo stato resta invariato
  e viene prodotto un errore esplicito.
- Carichi successivi sullo stesso prodotto si sommano correttamente.
- L'operazione non modifica prodotti o ordini.

### Moduli potenzialmente interessati

Servizio di magazzino, accesso al catalogo, contratto pubblico della libreria e
test del magazzino.

### Condizioni iniziali

La baseline permette di consultare un prodotto e la relativa quantità, ma non
offre un'operazione pubblica di carico del magazzino.

### Criteri oggettivi di completamento

- incremento esatto della quantità;
- validazione completa prima della mutazione;
- errore osservabile e stato invariato nei casi non validi;
- risultato isolato tra istanze indipendenti della libreria.

### Test richiesti

- **Unitari:** validazione della quantità.
- **Integrazione:** verifica congiunta di catalogo e magazzino.
- **Accettazione:** più carichi consecutivi e consultazione della nuova scorta.
- **Riservati:** prodotto inesistente, quantità zero, decimale e carichi su
  istanze separate.

### Stima e motivazione

**Medio-bassa.** Introduce una nuova operazione di dominio e una mutazione
controllata, ma coinvolge pochi componenti e non richiede coordinamento ampio.

### Sottotask e dipendenze

1. definire errori e contratto dell'operazione;
2. verificare esistenza del prodotto e quantità;
3. aggiornare atomicamente la scorta;
4. aggiungere test dell'agente.

L'aggiornamento dipende dalle validazioni; non esistono dipendenze esterne.

## T3 — Correzione degli ordini con righe duplicate

### Obiettivo funzionale

Correggere il difetto per cui un ordine contenente più righe dello stesso prodotto
può superare la scorta disponibile o produrre un aggiornamento errato.

### Requisiti precisi

- Prima della conferma, le quantità delle righe con lo stesso identificatore di
  prodotto devono essere sommate.
- La disponibilità deve essere verificata sulla quantità totale richiesta per
  ciascun prodotto.
- Se almeno un prodotto non ha scorta sufficiente, la conferma fallisce e nessuna
  quantità di magazzino viene modificata.
- Se tutte le quantità sono disponibili, ogni scorta viene ridotta esattamente
  della quantità totale richiesta.
- Il comportamento degli ordini privi di righe duplicate resta invariato.
- L'ordine memorizzato conserva la rappresentazione prevista dal contratto
  pubblico della baseline; il task non autorizza altri cambiamenti dell'API.

### Moduli potenzialmente interessati

Validazione e conferma degli ordini, aggiornamento del magazzino e test di
integrazione ordine–magazzino.

### Condizioni iniziali

La baseline contiene intenzionalmente una gestione non corretta delle righe
duplicate. I normali ordini con una sola riga per prodotto continuano a
funzionare.

### Criteri oggettivi di completamento

- aggregazione corretta delle quantità duplicate;
- conferma atomica rispetto alle scorte;
- nessuna regressione per gli ordini ordinari;
- nessuna mutazione parziale in caso di errore.

### Test richiesti

- **Unitari:** aggregazione di righe duplicate.
- **Integrazione:** conferma e variazione delle scorte.
- **Accettazione:** ordine valido e ordine non valido con più prodotti e duplicati.
- **Riservati:** duplicati non adiacenti, insufficienza sull'ultimo prodotto e
  verifica completa dell'assenza di mutazioni parziali.

### Stima e motivazione

**Media.** La modifica può essere breve, ma richiede diagnosi del difetto,
comprensione del flusso esistente e garanzia di atomicità.

### Sottotask e dipendenze

1. riprodurre il difetto con un test aggiuntivo;
2. individuare il punto della validazione errata;
3. aggregare e validare tutte le quantità prima delle mutazioni;
4. verificare regressioni e atomicità.

La correzione dipende dalla diagnosi; analisi e preparazione dei casi di test
possono procedere separatamente.

## T4 — Sospensione di un prodotto dal catalogo

### Obiettivo funzionale

Permettere di sospendere e riattivare un prodotto, coordinando catalogo, creazione
degli ordini e consultazioni senza cancellare dati storici.

### Requisiti precisi

- Ogni prodotto possiede uno stato attivo o sospeso; i prodotti della baseline
  iniziano attivi.
- Un prodotto esistente può essere sospeso e successivamente riattivato.
- Sospensione ripetuta, riattivazione ripetuta e prodotto inesistente producono
  errori espliciti senza mutazioni ulteriori.
- Un prodotto sospeso resta consultabile nel catalogo e conserva la propria
  scorta.
- Non è possibile creare o confermare un nuovo ordine contenente un prodotto
  sospeso.
- Gli ordini già confermati restano consultabili e invariati.
- Le query del catalogo possono distinguere prodotti attivi e sospesi in modo
  deterministico.

### Moduli potenzialmente interessati

Modello e servizio prodotti, regole degli ordini, query del catalogo, esportazioni
pubbliche e test di più moduli.

### Condizioni iniziali

La baseline conosce soltanto prodotti utilizzabili e non prevede eliminazione o
sospensione.

### Criteri oggettivi di completamento

- transizioni di stato valide e validate;
- blocco coerente dei nuovi ordini;
- conservazione di scorte e storico;
- comportamento deterministico delle consultazioni.

### Test richiesti

- **Unitari:** transizioni attivo/sospeso e relativi errori.
- **Integrazione:** effetti su catalogo, magazzino e ordini.
- **Accettazione:** sospensione, tentativo di ordine, riattivazione e nuovo ordine.
- **Riservati:** ordini misti, storico preesistente e ripetizione delle
  transizioni.

### Stima e motivazione

**Medio-alta.** Una singola regola di dominio attraversa più moduli e richiede
coerenza tra scritture, letture e dati storici.

### Sottotask e dipendenze

1. estendere il modello del prodotto;
2. implementare le transizioni di stato;
3. applicare la regola agli ordini;
4. aggiornare le query;
5. verificare integrazione e regressioni.

Modello e contratto precedono le altre modifiche; ordini e query possono essere
sviluppati in parallelo dopo tale definizione.

## T5 — Separazione degli archivi in memoria

### Obiettivo funzionale

Riorganizzare il codice introducendo contratti separati per l'accesso a prodotti,
scorte e ordini, mantenendo invariato il comportamento pubblico della baseline.

### Requisiti precisi

- La superficie pubblica usata dai consumatori e dai test della baseline deve
  restare compatibile.
- La logica applicativa non deve accedere direttamente alle collezioni concrete.
- Devono esistere contratti distinti per prodotti, scorte e ordini, con
  implementazioni in memoria.
- Ogni nuova istanza dell'applicazione deve ricevere archivi indipendenti.
- Le letture non devono esporre riferimenti che consentano di mutare internamente
  lo stato senza passare dalle operazioni autorizzate.
- Errori, ordinamenti e regole di dominio esistenti devono rimanere invariati.
- Non devono essere aggiunti database, filesystem o dipendenze esterne.

### Moduli potenzialmente interessati

Composizione della libreria, servizi applicativi, contratti degli archivi,
implementazioni in memoria, tipi esportati e intera suite di regressione.

### Condizioni iniziali

La baseline usa collezioni in memoria direttamente accessibili alla logica
applicativa e offre già un contratto pubblico funzionante.

### Criteri oggettivi di completamento

- completa compatibilità comportamentale con i test della baseline;
- dipendenze verso contratti invece che collezioni concrete;
- isolamento tra istanze;
- impossibilità di modificare lo stato attraverso risultati di sola lettura;
- assenza di infrastrutture non richieste.

### Test richiesti

- **Unitari:** implementazioni in memoria dei tre contratti.
- **Integrazione:** servizi applicativi collegati agli archivi.
- **Accettazione/regressione:** tutti i comportamenti pubblici della baseline.
- **Riservati:** aliasing di oggetti, isolamento tra istanze e sostituzione con
  implementazioni di prova dei contratti.

### Stima e motivazione

**Alta.** Il task modifica l'architettura interna e numerosi collegamenti, ma non
può cambiare il comportamento osservabile. Richiede coordinamento e controllo
accurato delle regressioni.

### Sottotask e dipendenze

1. mappare accessi diretti e API pubblica esistente;
2. definire i tre contratti minimi;
3. implementare gli archivi in memoria;
4. migrare prodotti, magazzino e ordini;
5. aggiornare la composizione senza rompere i consumatori;
6. eseguire test strutturali e di regressione.

I contratti devono precedere la migrazione; i tre archivi possono essere
implementati in parallelo, mentre composizione e regressione dipendono dalla loro
integrazione.

## T6 — Evasione parziale e ordini in attesa

### Obiettivo funzionale

Gestire ordini che possono essere evasi in più passaggi quando la scorta non è
sufficiente, registrando quantità evase e residue senza perdere coerenza.

### Requisiti precisi

- Un ordine confermato distingue, per ogni prodotto, quantità richiesta, quantità
  evasa e quantità residua.
- L'evasione utilizza al massimo la scorta disponibile e non produce mai quantità
  negative.
- Un ordine è `in attesa` se nessuna quantità è stata evasa, `parzialmente evaso`
  se resta almeno una quantità residua ed `evaso` quando tutte le righe sono
  complete.
- Evasioni successive aggiornano cumulativamente quantità e stato.
- Il task comprende l'operazione minima necessaria per caricare una quantità
  intera positiva nella scorta di un prodotto esistente tra due tentativi di
  evasione; input non validi producono un errore senza mutazioni.
- Un tentativo di evadere nuovamente un ordine già evaso produce un errore
  esplicito senza mutazioni.
- Prodotti e righe inesistenti o quantità non valide producono errori senza
  aggiornamenti parziali.
- La consultazione dell'ordine restituisce un riepilogo deterministico delle
  quantità e dello stato.
- Ordini diversi competono per la scorta secondo l'ordine esplicito delle
  operazioni; non sono introdotti processi concorrenti o casualità.

### Moduli potenzialmente interessati

Modello e stati dell'ordine, servizio di evasione, operazione di carico del
magazzino, query di riepilogo, composizione della libreria e test trasversali.

### Condizioni iniziali

La baseline conferma un ordine in un solo passaggio, non rappresenta quantità
residue o stati di evasione parziale e non offre un'operazione pubblica di carico.

### Criteri oggettivi di completamento

- calcolo corretto delle quantità evase e residue;
- transizioni di stato coerenti;
- aggiornamenti atomici tra ordine e magazzino;
- supporto di più evasione successive;
- risultati deterministici e assenza di regressioni non autorizzate.

### Test richiesti

- **Unitari:** calcolo delle allocazioni e transizioni di stato.
- **Integrazione:** coordinamento tra ordine e scorte in più passaggi.
- **Accettazione:** ordine inizialmente senza scorta, evasione parziale dopo un
  carico e successivo completamento.
- **Riservati:** più prodotti con disponibilità diverse, più ordini sequenziali,
  ripetizione di operazioni e verifica dell'atomicità in caso di errore.

### Stima e motivazione

**Alta.** Introduce un ciclo di vita articolato, calcoli per riga, mutazioni
coordinate e numerosi casi limite. È il task con la maggiore scomponibilità reale.

### Sottotask e dipendenze

1. definire dati e invarianti dell'evasione;
2. definire la macchina a stati dell'ordine;
3. implementare il calcolo delle quantità allocabili;
4. implementare l'operazione minima di carico del magazzino;
5. coordinare aggiornamenti di ordine e magazzino;
6. implementare il riepilogo di consultazione;
7. coprire flussi completi, errori e regressioni.

Dati e invarianti precedono l'integrazione. Macchina a stati, algoritmo di
allocazione e formato del riepilogo possono essere sviluppati separatamente,
quindi integrati e verificati sul flusso completo.

## Controllo rispetto alla baseline

La baseline minima approvata è sufficiente per rendere comprensibili tutti i
task, ma non deve anticiparne la soluzione:

- T1 richiede una query di soglia non presente inizialmente;
- T2 richiede un'operazione pubblica di carico non presente inizialmente;
- T3 richiede che il difetto sulle righe duplicate sia riproducibile;
- T4 richiede uno stato del prodotto assente inizialmente;
- T5 richiede accessi diretti alle collezioni da rifattorizzare;
- T6 richiede un modello di evasione parziale e il carico minimo necessario al
  relativo flusso, entrambi assenti inizialmente.

Questi vincoli guideranno l'implementazione successiva della baseline. Non
costituiscono ancora codice né test eseguibili.

## Elementi da approvare prima del passo successivo

Devono essere approvati:

1. contenuto funzionale di T1–T6;
2. progressione di complessità;
3. presenza intenzionale del difetto di T3 nella baseline;
4. accessi diretti alle collezioni necessari come punto di partenza di T5;
5. criteri di completamento e categorie di test di ciascun task.

Solo dopo l'approvazione sarà possibile definire la struttura concreta della
baseline senza incorporare accidentalmente le soluzioni richieste dai task.
