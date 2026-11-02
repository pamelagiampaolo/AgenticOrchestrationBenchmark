# B2 — Decisioni progettuali approvate

## Registro

- **Data:** 1 novembre 2026
- **Fase:** 2, blocco B, preparazione del passo B2
- **Stato:** decisioni approvate; progettazione dei task non ancora iniziata

## Dominio applicativo

`BenchmarkTarget` sarà un piccolo gestionale di ordini, prodotti e magazzino.

## Forma dell'applicazione

Il gestionale sarà realizzato come libreria TypeScript verificabile tramite test
automatici. In questa fase non sono previste una CLI o un'API HTTP.

La logica applicativa dovrà rimanere separata da eventuali interfacce, in modo da
non legare il dominio a uno specifico canale di utilizzo e da permettere
l'aggiunta di una componente web dopo la consegna della tesi.

## Conservazione dei dati applicativi

I dati applicativi saranno conservati in memoria mediante oggetti e collezioni
TypeScript. Questa scelta mantiene il benchmark semplice, deterministico e
riproducibile ed evita dipendenze infrastrutturali non necessarie.

Ogni esecuzione sperimentale dovrà partire da uno stato iniziale controllato. Le
esecuzioni non dovranno condividere dati applicativi modificabili, così da evitare
che una prova influenzi le successive.

Questa decisione riguarda esclusivamente i dati applicativi del gestionale. Log,
metriche, risultati dei test e altri dati sperimentali dovranno essere salvati in
modo persistente dall'infrastruttura sperimentale.

## Baseline funzionale minima approvata

La baseline iniziale dovrà comprendere soltanto le funzionalità necessarie a
costituire un gestionale minimo ma operativo:

- catalogo dei prodotti;
- gestione delle quantità disponibili in magazzino;
- creazione degli ordini con righe prodotto;
- aggiornamento delle scorte alla conferma di un ordine;
- consultazione di prodotti, scorte e ordini;
- dati iniziali deterministici;
- test pubblici essenziali.

La baseline dovrà rimanere il più semplice possibile e non dovrà includere
funzionalità prive di una necessità sperimentale. Prima della sua implementazione
definitiva saranno definiti i 6 task T1–T6, così da verificare che le funzionalità
iniziali siano sufficienti senza anticipare il lavoro richiesto agli agenti.

Ogni esecuzione sperimentale dovrà partire dallo stesso commit pulito della
baseline e da condizioni iniziali equivalenti.

## Decisioni non introdotte in questo aggiornamento

Questo documento non stabilisce ancora:

- struttura interna, entità, attributi o API della libreria;
- contenuto e difficoltà dei task T1–T6;
- implementazione definitiva della baseline funzionale;
- framework e organizzazione dei test;
- formato o percorso di persistenza dei dati sperimentali;
- eventuale tecnologia della futura componente web.

Questi aspetti richiederanno decisioni esplicite nei rispettivi passi, senza
anticipare l'implementazione.
