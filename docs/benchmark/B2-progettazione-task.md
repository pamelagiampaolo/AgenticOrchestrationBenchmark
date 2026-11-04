# B2 — Progettazione dei task sperimentali T1–T6

## Registro

- **Data:** 2 novembre 2026
- **Fase:** 2, blocco B, progettazione dei task
- **Stato:** progettazione e specifiche approvate dall'utente il 2 novembre 2026;
  nessuna implementazione ancora autorizzata
- **Origine:** consolidamento delle decisioni, della proposta preliminare e delle
  specifiche concrete approvate
- **Integrazione T1:** firma, tipo del risultato e codice di errore approvati il
  4 novembre 2026

## Decisioni progettuali approvate

### Dominio e forma dell'applicazione

`BenchmarkTarget` sarà un piccolo gestionale di ordini, prodotti e magazzino,
realizzato come libreria TypeScript verificabile tramite test automatici. In
questa fase non sono previste una CLI o un'API HTTP.

La logica applicativa resterà separata da eventuali interfacce, così da non legare
il dominio a uno specifico canale di utilizzo e permettere l'aggiunta di una
componente web dopo la consegna della tesi.

### Conservazione dei dati

I dati applicativi saranno conservati in memoria mediante oggetti e collezioni
TypeScript. Ogni esecuzione partirà da uno stato controllato e non condividerà
dati applicativi modificabili con le altre esecuzioni.

Questa scelta riguarda soltanto i dati del gestionale. Log, metriche, risultati
dei test e altri dati sperimentali saranno salvati persistentemente
dall'infrastruttura sperimentale.

### Baseline funzionale minima

La baseline comprenderà soltanto:

- catalogo dei prodotti;
- quantità disponibili in magazzino;
- creazione degli ordini con righe prodotto;
- aggiornamento delle scorte alla conferma di un ordine;
- consultazione di prodotti, scorte e ordini;
- dati iniziali deterministici;
- test pubblici essenziali.

La baseline resterà il più semplice possibile e non includerà in anticipo le
soluzioni di T1–T6. Ogni esecuzione partirà dallo stesso commit pulito e da
condizioni iniziali equivalenti.

### Contratto pubblico approvato per il primo incremento

Il primo incremento della baseline espone dall'entry point `src/index.ts`:

```ts
type Product = {
  productId: string;
  name: string;
};

type InitialProduct = Product & {
  availableQuantity: number;
};

class InventoryManagement {
  constructor(initialProducts: InitialProduct[]);
  listProducts(): Product[];
  getAvailableQuantity(productId: string): number;
}
```

Ogni istanza conserva in memoria copie proprie dei dati iniziali.
`listProducts` restituisce nuove copie contenenti soltanto `productId` e `name`,
ordinate per identificatore testuale crescente. `getAvailableQuantity`
restituisce la scorta corrente oppure solleva un errore con proprietà stabile
`code` uguale a `PRODUCT_NOT_FOUND` quando il prodotto non esiste. I risultati e
i dati forniti al costruttore non espongono riferimenti modificabili allo stato
interno. Questo incremento non introduce stato globale o repository e non
comprende ancora ordini né le funzionalità riservate a T1–T6.

### Contratto pubblico approvato per il secondo incremento

Il secondo incremento aggiunge all'entry point pubblico i tipi `OrderLine` e
`Order` e le seguenti operazioni a `InventoryManagement`:

```ts
type OrderLine = {
  productId: string;
  quantity: number;
};

type Order = {
  orderId: string;
  status: "draft" | "confirmed";
  lines: OrderLine[];
};

class InventoryManagement {
  createOrder(lines: OrderLine[]): Order;
  getOrder(orderId: string): Order;
  listOrders(): Order[];
  confirmOrder(orderId: string): Order;
}
```

Gli identificatori sono `O-001`, `O-002` e successivi, generati da un contatore
proprio dell'istanza. Un ordine nasce `draft`, contiene almeno una riga e conserva
righe duplicate nell'ordine ricevuto. Ogni riga deve riferirsi a un prodotto
esistente e avere una quantità positiva per cui `Number.isSafeInteger` è vero.
Una creazione non valida non registra l'ordine né consuma un identificatore e la
creazione valida non modifica le scorte.

La conferma valida decrementa le scorte per ogni riga e imposta lo stato a
`confirmed`. Una conferma con scorta insufficiente lascia ordine e magazzino
invariati; una seconda conferma fallisce senza ulteriori decrementi. Le letture
restituiscono copie profonde delle righe e `listOrders` ordina per `orderId`
testuale crescente. Dati, contatori e collezioni restano isolati per istanza,
senza stato globale o repository.

I codici di errore stabili di questo contratto sono `PRODUCT_NOT_FOUND`,
`ORDER_NOT_FOUND`, `EMPTY_ORDER`, `INVALID_QUANTITY`, `INSUFFICIENT_STOCK` e
`ORDER_ALREADY_CONFIRMED`.

Per preservare il difetto intenzionale oggetto di T3, la conferma controlla tutte
le righe separatamente rispetto alle scorte correnti prima di qualsiasi
decremento e non aggrega quantità duplicate. Soltanto dopo i controlli decrementa
la scorta per ogni riga. Di conseguenza, con scorta `5` per `P-002` e due righe
`P-002 × 3`, l'ordine risulta `confirmed` e la scorta diventa `-1`. Una singola
riga superiore alla disponibilità fallisce invece prima di ogni mutazione.

## Scopo e limiti

Questo documento traduce la struttura preliminare in comportamenti osservabili e
casi di verifica indipendenti. Non contiene soluzioni, non assegna il lavoro agli
agenti e non presume che un'architettura debba ottenere risultati migliori.

I nomi delle operazioni indicati nelle specifiche sono proposte necessarie a
rendere i contratti non ambigui. Saranno allineati e congelati quando verrà
approvata l'API della baseline, senza cambiare i comportamenti qui descritti.

## Condizioni comuni a tutti i task

1. Ogni task parte da una copia pulita dello stesso commit della baseline.
2. I task sono indipendenti e non cumulativi.
3. Le tre architetture ricevono lo stesso testo, gli stessi file, gli stessi test
   pubblici e gli stessi limiti sperimentali.
4. I test pubblici di riferimento sono immutabili durante l'esecuzione. Gli agenti
   possono creare test aggiuntivi separati.
5. I test riservati sono eseguiti esternamente dopo il termine e non sono
   accessibili agli agenti.
6. Ogni test costruisce un'istanza nuova della libreria. Non esiste stato globale
   applicativo condiviso.
7. Gli errori richiesti devono essere osservabili dal chiamante. I test verificano
   il tipo o codice stabile definito dalla futura API, non il testo descrittivo del
   messaggio.
8. Un'operazione non valida non deve lasciare mutazioni parziali.
9. Gli ordinamenti richiesti sono deterministici e basati sull'identificatore
   testuale crescente.
10. Nessun task richiede rete, database, filesystem applicativo o dipendenze
    infrastrutturali aggiuntive.

## Stato controllato di riferimento

I casi descritti usano questa fixture proposta, ricreata prima di ogni test:

| Prodotto | Nome | Quantità iniziale |
| --- | --- | ---: |
| `P-001` | Tastiera | 0 |
| `P-002` | Mouse | 5 |
| `P-003` | Monitor | 10 |

La fixture serve a rendere espliciti input e risultati attesi. La sua adozione
definitiva sarà decisa con la baseline; i test riservati useranno anche dati
diversi per verificare la generalità della soluzione.

## Interpretazione delle stime

Per ogni task sono riportate due ipotesi distinte:

- **difficoltà ipotizzata:** quantità e complessità del ragionamento e delle
  modifiche tecniche previste;
- **coordinamento ipotizzato:** possibilità ragionevole di dividere il lavoro in
  parti con interfacce chiare.

Queste valutazioni servono a costruire un benchmark equilibrato. Non sono misure,
non sono risultati sperimentali e non implicano che usare più agenti sia utile.

## Quadro riassuntivo

| Task | Categoria principale | Difficoltà ipotizzata | Coordinamento ipotizzato |
| --- | --- | --- | --- |
| T1 | Modifica semplice e circoscritta | Bassa | Basso |
| T2 | Nuova funzionalità | Medio-bassa | Basso |
| T3 | Correzione di un difetto verificabile | Media | Basso |
| T4 | Modifica trasversale a più moduli | Medio-alta | Medio |
| T5 | Refactoring con compatibilità obbligatoria | Alta | Medio |
| T6 | Funzionalità complessa e scomponibile | Alta | Medio-alto |

Difficoltà e coordinamento sono ipotesi distinte. Un task difficile non richiede
necessariamente molti agenti e un eventuale vantaggio del single-agent nei task
più circoscritti costituisce un risultato sperimentale valido.

---

## T1 — Ricerca dei prodotti sotto scorta

### Funzionalità richiesta

Aggiungere il tipo e l'operazione pubblici seguenti:

```ts
type ProductStockSnapshot = {
  productId: string;
  name: string;
  availableQuantity: number;
};

class InventoryManagement {
  findProductsBelowStock(threshold: number): ProductStockSnapshot[];
}
```

`ProductStockSnapshot` contiene soltanto `productId`, `name` e
`availableQuantity`. L'operazione restituisce questi snapshot quando la quantità
è strettamente inferiore alla soglia.

### Requisiti

- `threshold` deve essere un intero maggiore o uguale a zero.
- Valori negativi, decimali e non numerici devono produrre un errore di
  validazione con proprietà stabile `code` uguale a `INVALID_THRESHOLD`, secondo
  la convenzione degli errori della baseline. Il messaggio testuale non è parte
  del contratto.
- Un prodotto con quantità uguale alla soglia è escluso.
- Il risultato è ordinato per `productId` crescente.
- Una chiamata senza corrispondenze restituisce una collezione vuota.
- La query non modifica alcuno stato e i risultati non consentono di modificare lo
  stato interno della libreria.

### Criteri di successo

Tutti i test pubblici della baseline continuano a passare e tutti i casi T1
pubblici e riservati producono i valori o gli errori previsti, senza variazioni
delle scorte osservabili dopo la query.

### Verifica indipendente proposta

| ID | Visibilità | Input sulla fixture | Risultato atteso |
| --- | --- | --- | --- |
| `T1-P01` | Pubblico | soglia `6` | `P-001`, `P-002`, in quest'ordine |
| `T1-P02` | Pubblico | soglia `0` | collezione vuota |
| `T1-P03` | Pubblico | soglia `-1` | errore; stato invariato |
| `T1-P04` | Pubblico | soglia `2.5` | errore; stato invariato |
| `T1-R01` | Riservato | soglia `5` | soltanto `P-001` |
| `T1-R02` | Riservato | catalogo vuoto | collezione vuota |
| `T1-R03` | Riservato | risultato poi mutato dal test | stato interno invariato |

I test sono prevalentemente di integrazione tra catalogo e magazzino; la
validazione della soglia può essere verificata anche unitariamente.

### Ipotesi separate

- **Difficoltà ipotizzata:** bassa, per il perimetro ristretto e i pochi casi
  limite.
- **Coordinamento ipotizzato:** basso; la divisione tra più agenti potrebbe avere
  un costo superiore al lavoro utile.

---

## T2 — Carico manuale di magazzino

### Funzionalità richiesta

Aggiungere l'operazione pubblica proposta
`receiveStock(productId, quantity)`, che incrementa la scorta di un prodotto e
restituisce la nuova quantità disponibile.

### Requisiti

- `productId` deve identificare un prodotto esistente.
- `quantity` deve essere un intero strettamente positivo.
- Più carichi sullo stesso prodotto si sommano.
- Un errore non modifica catalogo, magazzino o ordini.
- Un carico modifica soltanto la scorta del prodotto indicato.
- I dati di due istanze della libreria rimangono indipendenti.

### Criteri di successo

Tutti i test pubblici della baseline continuano a passare; ogni carico valido
produce la quantità esatta e ogni input non valido produce un errore senza
mutazioni osservabili.

### Verifica indipendente proposta

| ID | Visibilità | Operazione | Risultato atteso |
| --- | --- | --- | --- |
| `T2-P01` | Pubblico | carica `3` su `P-001` | restituisce `3`; scorta `3` |
| `T2-P02` | Pubblico | carica `2`, poi `4`, su `P-002` | risultati `7` e `11` |
| `T2-P03` | Pubblico | carica `0` su `P-003` | errore; scorta ancora `10` |
| `T2-P04` | Pubblico | carica `1` su `P-999` | errore; stato invariato |
| `T2-R01` | Riservato | quantità negativa o decimale | errore; stato invariato |
| `T2-R02` | Riservato | carico su istanza A | istanza B invariata |
| `T2-R03` | Riservato | carico su `P-002` | altre scorte e ordini invariati |

I test combinano validazione unitaria e integrazione tra catalogo e magazzino.

### Ipotesi separate

- **Difficoltà ipotizzata:** medio-bassa, perché introduce una mutazione con
  validazione e atomicità ma coinvolge pochi componenti.
- **Coordinamento ipotizzato:** basso; contratto, implementazione e test sono
  separabili, ma la quantità di lavoro è contenuta.

---

## T3 — Correzione degli ordini con righe duplicate

### Funzionalità richiesta

Correggere la conferma di un ordine affinché sommi le quantità delle righe che
indicano lo stesso prodotto prima di verificare e aggiornare le scorte.

### Difetto iniziale riproducibile

Con scorta `5` per `P-002`, un ordine con due righe `P-002 × 3` può superare la
disponibilità perché le righe vengono verificate separatamente. Il difetto è
intenzionalmente presente nella baseline esclusivamente come oggetto di T3.

### Requisiti

- Le quantità duplicate sono aggregate per identificatore del prodotto.
- La disponibilità è controllata sulla somma richiesta per ogni prodotto.
- Tutte le validazioni terminano prima del primo aggiornamento di magazzino.
- Se un prodotto è insufficiente, nessuna scorta viene modificata e l'ordine non
  risulta confermato.
- Se l'ordine è valido, ogni scorta diminuisce una sola volta della quantità
  aggregata.
- La rappresentazione pubblica delle righe dell'ordine resta quella stabilita
  dalla baseline.
- Gli ordini senza duplicati mantengono il comportamento precedente.

### Criteri di successo

Il caso che riproduce il difetto viene corretto, gli aggiornamenti sono atomici e
l'intera suite di regressione della baseline resta valida.

### Verifica indipendente proposta

| ID | Visibilità | Ordine | Risultato atteso |
| --- | --- | --- | --- |
| `T3-P01` | Pubblico | `P-002 × 2`, `P-002 × 3` | confermato; scorta `0` |
| `T3-P02` | Pubblico | `P-002 × 3`, `P-002 × 3` | errore; scorta `5` |
| `T3-P03` | Pubblico | `P-002 × 2`, `P-003 × 4` | confermato; scorte `3` e `6` |
| `T3-R01` | Riservato | duplicati non adiacenti | aggregazione corretta |
| `T3-R02` | Riservato | primo prodotto valido, ultimo insufficiente | tutte le scorte invariate |
| `T3-R03` | Riservato | tre righe duplicate al limite esatto | conferma e scorta `0` |

I test sono di integrazione ordine–magazzino e di accettazione; un test unitario
dell'aggregazione può essere aggiunto dagli agenti, ma il successo non dipende
dalla struttura interna scelta.

### Ipotesi separate

- **Difficoltà ipotizzata:** media, perché richiede diagnosi e atomicità oltre a
  una modifica algoritmica contenuta.
- **Coordinamento ipotizzato:** basso; analisi e test possono essere separati, ma
  la correzione centrale resta fortemente coesa.

---

## T4 — Sospensione di un prodotto dal catalogo

### Funzionalità richiesta

Aggiungere le operazioni pubbliche proposte `suspendProduct(productId)` e
`reactivateProduct(productId)`. Le consultazioni devono esporre lo stato
`active` oppure `suspended` del prodotto.

### Requisiti

- Tutti i prodotti iniziali sono `active`.
- Solo un prodotto attivo può essere sospeso e solo uno sospeso può essere
  riattivato.
- Prodotto inesistente o transizione ripetuta producono un errore senza mutazioni.
- La sospensione non elimina il prodotto e non modifica la sua scorta.
- Un ordine già confermato resta consultabile e invariato.
- Qualsiasi nuova conferma contenente almeno un prodotto sospeso fallisce
  atomicamente: nessun ordine confermato e nessuna scorta modificata.
- Dopo la riattivazione il prodotto può essere nuovamente ordinato.
- Le query restituiscono stato e ordinamento deterministici.

### Criteri di successo

Le transizioni valide e non valide rispettano il contratto; catalogo, scorte e
storico restano coerenti; nessun comportamento non collegato viene modificato.

### Verifica indipendente proposta

| ID | Visibilità | Sequenza | Risultato atteso |
| --- | --- | --- | --- |
| `T4-P01` | Pubblico | sospendi `P-002` | stato `suspended`; scorta `5` |
| `T4-P02` | Pubblico | sospendi `P-002`, crea ordine con `P-002` | errore; scorta e ordini invariati |
| `T4-P03` | Pubblico | sospendi e riattiva `P-002`, poi ordina `P-002 × 1` | confermato; scorta `4` |
| `T4-P04` | Pubblico | sospendi due volte `P-003` | secondo tentativo in errore |
| `T4-R01` | Riservato | sospendi `P-999` | errore; stato invariato |
| `T4-R02` | Riservato | ordine con prodotto attivo e sospeso | fallimento atomico |
| `T4-R03` | Riservato | ordine storico, poi sospensione | storico invariato e consultabile |

I test sono unitari sulle transizioni e di integrazione/accettazione sui flussi
catalogo–ordini–magazzino.

### Ipotesi separate

- **Difficoltà ipotizzata:** medio-alta, perché la regola attraversa più moduli e
  deve preservare dati storici.
- **Coordinamento ipotizzato:** medio; modello, query e regole degli ordini sono
  separabili dopo aver concordato il contratto comune.

---

## T5 — Separazione degli archivi in memoria

### Funzionalità richiesta

Rifattorizzare la baseline introducendo i contratti proposti
`ProductRepository`, `InventoryRepository` e `OrderRepository`, con relative
implementazioni in memoria. Il comportamento pubblico della libreria deve restare
invariato.

### Requisiti

- I tre contratti hanno responsabilità separate e contengono soltanto operazioni
  realmente richieste dalla baseline.
- La logica applicativa dipende dai contratti, non da `Map`, array o
  implementazioni concrete.
- Le implementazioni in memoria non condividono stato modificabile tra istanze.
- La composizione predefinita della libreria continua a creare un sistema pronto
  all'uso con dati controllati.
- I servizi possono essere costruiti con implementazioni di prova dei contratti.
- Le letture restituiscono snapshot o copie che non espongono lo stato interno.
- API pubblica, errori, regole, ordinamenti e risultati della baseline restano
  compatibili.
- Non vengono introdotti database, file applicativi o nuove dipendenze.

### Criteri di successo

La suite completa della baseline continua a passare; test strutturali confermano
la presenza e l'uso dei tre contratti; test con archivi di prova dimostrano la
sostituibilità; test di isolamento e aliasing non rilevano condivisioni o
mutazioni indirette.

### Verifica indipendente proposta

| ID | Visibilità | Verifica | Risultato atteso |
| --- | --- | --- | --- |
| `T5-P01` | Pubblico | suite completa baseline | tutti i casi passano invariati |
| `T5-P02` | Pubblico | costruzione di due sistemi | stati indipendenti |
| `T5-P03` | Pubblico | mutazione di un risultato letto | stato interno invariato |
| `T5-P04` | Pubblico | servizi con archivi di prova | costruzione e flusso riusciti |
| `T5-R01` | Riservato | controllo dipendenze dei servizi | nessun accesso a collezioni concrete |
| `T5-R02` | Riservato | implementazioni alternative minime | sostituzione riuscita |
| `T5-R03` | Riservato | regressione di errori e ordinamenti | comportamento invariato |

I test includono compilazione TypeScript, regressione, integrazione con test
double e controlli strutturali indipendenti. I controlli strutturali saranno
definiti in modo da verificare il contratto senza imporre dettagli irrilevanti
della soluzione.

### Ipotesi separate

- **Difficoltà ipotizzata:** alta, perché la modifica è trasversale e deve
  preservare integralmente il comportamento.
- **Coordinamento ipotizzato:** medio; i tre archivi sono separabili dopo la
  definizione dei contratti, ma migrazione e integrazione restano interdipendenti.

---

## T6 — Evasione parziale e ordini in attesa

### Funzionalità richiesta

Estendere il ciclo di vita degli ordini affinché la conferma assegni la scorta
disponibile, anche se insufficiente, e permetta tentativi successivi tramite
l'operazione proposta `fulfillOrder(orderId)`. Il task include
`receiveStock(productId, quantity)` perché non può dipendere da T2.

### Requisiti

- Per ogni riga sono esposte `requestedQuantity`, `fulfilledQuantity` e
  `remainingQuantity`.
- Alla conferma viene assegnata al massimo la scorta disponibile per ogni riga.
- Lo stato è `waiting` se nulla è stato evaso, `partially-fulfilled` se almeno una
  unità è evasa ma resta una quantità residua, `fulfilled` se tutto è completo.
- La scorta non diventa mai negativa.
- `fulfillOrder` tenta di assegnare le sole quantità residue usando la scorta
  corrente e aggiorna cumulativamente ordine e magazzino.
- `receiveStock` accetta soltanto prodotti esistenti e quantità intere positive;
  gli errori non modificano lo stato.
- Chiamare `fulfillOrder` su un ordine inesistente o già `fulfilled` produce un
  errore senza mutazioni.
- Un errore durante un'operazione non lascia aggiornamenti parziali.
- Le righe del riepilogo sono ordinate secondo il contratto deterministico della
  baseline.
- Ordini diversi consumano la scorta nell'ordine esplicito delle chiamate; non
  sono introdotte concorrenza o casualità.

### Criteri di successo

Quantità e stati sono corretti dopo ogni passaggio; ordine e magazzino restano
coerenti e atomici; più tentativi completano soltanto le quantità residue; i
comportamenti della baseline non incompatibili con il nuovo ciclo di vita non
regrediscono.

### Verifica indipendente proposta

| ID | Visibilità | Sequenza | Risultato atteso |
| --- | --- | --- | --- |
| `T6-P01` | Pubblico | conferma ordine `P-001 × 4` | `waiting`, residuo `4`, scorta `0` |
| `T6-P02` | Pubblico | carica `2`, evadi ordine precedente | `partially-fulfilled`, residuo `2` |
| `T6-P03` | Pubblico | carica altri `2`, evadi | `fulfilled`, residuo `0` |
| `T6-P04` | Pubblico | evadi nuovamente ordine completo | errore; stato invariato |
| `T6-P05` | Pubblico | ordine `P-002 × 3` con scorta `5` | `fulfilled`; scorta `2` |
| `T6-R01` | Riservato | ordine con più prodotti e scorte diverse | quantità e stato globali corretti |
| `T6-R02` | Riservato | due ordini sequenziali sulla stessa scorta | priorità secondo ordine chiamate |
| `T6-R03` | Riservato | carico o ordine non valido | nessuna mutazione parziale |
| `T6-R04` | Riservato | più carichi ed evasioni intermedie | somme cumulative corrette |

I test includono unità per allocazione e macchina a stati, integrazione
ordine–magazzino e accettazione su flussi completi in più passaggi.

### Ipotesi separate

- **Difficoltà ipotizzata:** alta, per ciclo di vita, allocazione, atomicità e
  numerosi stati intermedi.
- **Coordinamento ipotizzato:** medio-alto; modello degli stati, algoritmo di
  allocazione, carico, query e test possono essere separati, ma richiedono un
  contratto condiviso e una fase di integrazione significativa.

## Criterio complessivo di equilibrio

T1–T3 hanno coordinamento ipotizzato basso pur avendo difficoltà differenti;
permettono quindi di osservare l'eventuale costo di coordinamento. T4 e T5
introducono modifiche trasversali con scomposizione moderata. T6 offre una
scomposizione più ampia, senza richiedere o prescrivere l'uso di più agenti.

Questa distribuzione non attribuisce vantaggi a priori. Correttezza, tempo, uso
di risorse e overhead saranno misurati durante le 54 esecuzioni e solo allora
potranno sostenere conclusioni.

## Mappa progettuale di moduli, sottotask e dipendenze

Questa mappa descrive una scomposizione plausibile, non istruzioni organizzative
da consegnare alle architetture sperimentali.

### T1

- **Moduli potenziali:** query di catalogo e magazzino, tipo del risultato e test.
- **Sottotask:** definire il contratto, validare la soglia, filtrare e ordinare,
  verificare i casi limite.
- **Dipendenze:** validazione e query dipendono dal contratto; nessuna dipendenza
  esterna.

### T2

- **Moduli potenziali:** servizio di magazzino, accesso al catalogo, API pubblica
  e test.
- **Sottotask:** definire errori, validare prodotto e quantità, aggiornare
  atomicamente la scorta, verificare isolamento.
- **Dipendenze:** l'aggiornamento dipende dalle validazioni; nessuna dipendenza
  esterna.

### T3

- **Moduli potenziali:** validazione e conferma ordini, aggiornamento magazzino e
  test ordine–magazzino.
- **Sottotask:** riprodurre il difetto, localizzarlo, aggregare prima delle
  mutazioni, verificare atomicità e regressioni.
- **Dipendenze:** la correzione dipende dalla diagnosi; analisi e preparazione dei
  casi di test possono procedere separatamente.

### T4

- **Moduli potenziali:** modello e servizio prodotti, ordini, query del catalogo,
  esportazioni pubbliche e test trasversali.
- **Sottotask:** estendere il modello, implementare le transizioni, applicare la
  regola agli ordini, aggiornare le query, verificare lo storico.
- **Dipendenze:** modello e contratto precedono ordini e query; questi ultimi
  possono poi essere sviluppati separatamente.

### T5

- **Moduli potenziali:** composizione, servizi, contratti degli archivi,
  implementazioni in memoria e suite di regressione.
- **Sottotask:** mappare gli accessi, definire tre contratti minimi, implementare
  gli archivi, migrare i servizi, aggiornare la composizione, verificare la
  compatibilità.
- **Dipendenze:** i contratti precedono la migrazione; gli archivi sono separabili,
  mentre composizione e regressione dipendono dalla loro integrazione.

### T6

- **Moduli potenziali:** modello e stati dell'ordine, allocazione, carico del
  magazzino, query di riepilogo, composizione e test trasversali.
- **Sottotask:** definire invarianti e stati, implementare allocazione e carico,
  coordinare ordine e magazzino, produrre il riepilogo, verificare flussi ed
  errori.
- **Dipendenze:** invarianti e contratti precedono l'integrazione; stati,
  allocazione e riepilogo sono parzialmente separabili.

## Controllo rispetto alla baseline

La baseline è sufficiente a rendere comprensibili i task senza anticiparne le
soluzioni:

- T1 introduce una query di soglia assente inizialmente;
- T2 introduce un'operazione pubblica di carico assente inizialmente;
- T3 usa un difetto intenzionale e riproducibile sulle righe duplicate;
- T4 introduce lo stato del prodotto;
- T5 rifattorizza gli accessi diretti alle collezioni;
- T6 introduce evasione parziale e carico minimo per il relativo flusso.

## Decisioni approvate e registro delle revisioni

Con l'approvazione del documento sono confermati:

1. fixture controllata proposta;
2. nomi e contratti osservabili delle operazioni;
3. casi pubblici e riservati con relativi risultati attesi;
4. semantica concreta di sospensione, atomicità ed evasione parziale;
5. separazione tra difficoltà ipotizzata e coordinamento ipotizzato.

L'approvazione autorizza la successiva progettazione della struttura della
baseline e della strategia di test, non la loro implementazione automatica.

### Cronologia

- **1 novembre 2026:** approvati dominio, libreria TypeScript, dati in memoria e
  baseline funzionale minima.
- **2 novembre 2026:** approvata la struttura preliminare di T1–T6.
- **2 novembre 2026:** approvate le specifiche concrete, la fixture proposta, i
  contratti osservabili e i casi di verifica.
- **2 novembre 2026:** consolidati i tre documenti B2 in questo documento unico,
  senza modificare le decisioni scientifiche approvate.
- **4 novembre 2026:** congelati firma, tipo pubblico del risultato e codice di
  errore di validazione di T1.
