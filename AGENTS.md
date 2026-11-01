# Istruzioni operative del repository

## Scopo del progetto

Questo repository supporta una tesi sperimentale sull'orchestrazione adattiva di
sistemi multi-agente basati su Large Language Models. Contiene:

- `AgenticExperimentRunner/`: orchestratore .NET che esegue gli agenti e registra le metriche.
- `BenchmarkTarget/`: applicazione TypeScript usata come bersaglio comune dei task.

L'esperimento confronta tre condizioni: single-agent, multi-agent gerarchica
statica e multi-agent gerarchica adattiva. Non assumere che una condizione debba
risultare migliore: il software deve permettere un confronto oggettivo e
riproducibile.

## Priorità attuale

Concentrarsi sulla progettazione e successiva costruzione del benchmark. La
baseline sperimentale definitiva non esiste finché struttura, task, criteri di
successo e condizioni iniziali non sono stati approvati e verificati.

Non implementare in anticipo le soluzioni dei task sperimentali.

## Metodo di lavoro

1. Eseguire un solo passo completo, circoscritto e verificabile alla volta.
2. Prima di intervenire, spiegare in termini generali cosa verrà fatto, perché è
   necessario e come si inserisce nell'esperimento.
3. Ispezionare lo stato reale del repository prima di formulare assunzioni.
4. Dopo ogni modifica, eseguire direttamente tutte le verifiche tecniche
   disponibili e riportare comandi, output osservato ed eventuali errori.
5. Chiedere una verifica manuale all'utente soltanto quando non è tecnicamente
   eseguibile da Codex; in tal caso fornire un comando PowerShell esatto.
6. Fermarsi al termine del passo corrente senza iniziare automaticamente quello
   successivo.
7. Distinguere sempre attività completate, pianificate e ancora da verificare.
8. Non dichiarare superato un test che non sia stato realmente eseguito.
9. Al termine di ogni passo indicare il messaggio di commit consigliato, anche se
   non viene creato alcun commit. Creare effettivamente il commit soltanto dopo
   un'autorizzazione esplicita dell'utente.

## Confini e autorizzazioni

- Non creare commit, effettuare push, pubblicare dati o modificare servizi esterni
  senza autorizzazione esplicita.
- Non introdurre dipendenze, strumenti, servizi a pagamento o cambiamenti
  architetturali importanti senza averne spiegato necessità e alternative.
- Non eliminare dati o eseguire comandi Git distruttivi senza autorizzazione.
- Non usare A2A. Introdurre MCP soltanto se offre un vantaggio necessario e
  verificabile per standardizzare gli strumenti degli agenti.
- Preservare la separazione tra test pubblici, accessibili agli agenti, e test
  riservati usati per la valutazione indipendente.
- Non permettere agli agenti sperimentali di leggere o modificare i test riservati.

## Regole di qualità del codice

Applicare queste regole sia a TypeScript sia a C#, adattandole alle convenzioni
idiomatiche del linguaggio:

- Mantenere funzioni e classi piccole, coese e con una responsabilità chiara.
- Preferire nomi descrittivi a abbreviazioni e commenti compensativi.
- Rendere espliciti input, output, errori attesi e dipendenze.
- Evitare stato globale mutabile e comportamento dipendente dall'ordine quando
  non strettamente necessario.
- Separare logica di dominio, accesso a file/processi e presentazione dei risultati.
- Usare tipi rigorosi; evitare `any` in TypeScript salvo motivazione documentata.
- Gestire gli errori senza nasconderli e produrre messaggi diagnostici utili.
- Favorire risultati deterministici; quando interviene casualità, rendere il seed
  configurabile e registrarlo.
- Non ottimizzare prematuramente e non aggiungere astrazioni prive di un uso reale.
- Mantenere compatibilità con Windows e usare percorsi costruiti tramite le API
  del linguaggio, non concatenazioni dipendenti dal sistema operativo.

## Regole per i commenti nel codice

- Documentare classi, interfacce, tipi pubblici, funzioni e metodi pubblici con il
  formato idiomatico del linguaggio (`/** ... */` in TypeScript, commenti XML
  `///` in C#).
- Per ogni API pubblica spiegare scopo, parametri, valore restituito, errori
  significativi e invarianti quando pertinenti.
- Commentare proprietà o attributi quando il significato, l'unità di misura, il
  formato o il vincolo non sono evidenti dal nome e dal tipo.
- Inserire commenti interni per spiegare decisioni, vincoli sperimentali e passaggi
  algoritmici non ovvi.
- Non scrivere commenti che ripetono letteralmente il codice. Se il codice è
  difficile da spiegare, semplificarlo prima e aggiungere poi il contesto utile.
- Aggiornare o rimuovere i commenti quando cambia il comportamento relativo.

## Standard di verifica

Per ogni modifica applicabile:

1. eseguire formattazione o controlli statici disponibili;
2. compilare il componente interessato;
3. eseguire prima i test mirati e poi quelli più ampi pertinenti;
4. controllare che non siano comparsi artefatti generati tra i file versionabili;
5. registrare il risultato effettivo, non quello atteso.

I criteri di successo devono essere osservabili e automatizzabili. Distinguere:

- test unitari per singole unità di logica;
- test di integrazione per la collaborazione tra componenti;
- test di accettazione per i requisiti esterni del task.

## Validità sperimentale

Ogni confronto deve usare lo stesso commit iniziale, gli stessi requisiti, lo
stesso ambiente, gli stessi test di valutazione e limiti comparabili. Ogni
esecuzione deve partire da una copia pulita della baseline, senza modifiche o
informazioni residue.

Registrare almeno correttezza, test superati, tempo totale, chiamate LLM, token di
input/output e cache quando disponibili, tentativi di correzione, errori e
overhead di coordinamento. Separare sempre dati misurati, stime e interpretazioni.

## Documentazione sperimentale

Per ogni passo significativo registrare data, fase, obiettivo, file modificati,
decisioni e motivazioni, comandi eseguiti, risultati osservati, problemi,
soluzioni e lavoro rimanente. Non inventare risultati, metriche o verifiche.
