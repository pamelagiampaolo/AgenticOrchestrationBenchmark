# B1 — Struttura concettuale del benchmark

## Registro del passo

- **Data:** 1 novembre 2026
- **Fase:** 2, blocco B, passo B1
- **Obiettivo:** separare gli elementi del benchmark prima di progettare i task.
- **Stato iniziale osservato:** `BenchmarkTarget` contiene la configurazione
  TypeScript, il lock file npm e un solo file sorgente che stampa un messaggio.
- **Limite del passo:** questo documento non definisce ancora i 6 task (T1–T6) e
  non ne implementa le soluzioni.

## Scopo della separazione

Il confronto tra single-agent, gerarchia statica e gerarchia adattiva è valido
soltanto se le tre architetture ricevono lo stesso progetto iniziale e sono
valutate con gli stessi criteri. Per questo il benchmark viene diviso in cinque
componenti con responsabilità diverse.

## 1. Baseline tecnica

La baseline tecnica è lo stato iniziale versionato di `BenchmarkTarget` dal quale
parte ogni esecuzione sperimentale. Comprenderà il codice applicativo comune, le
configurazioni, le dipendenze bloccate da `package-lock.json` e gli eventuali test
pubblici comuni.

Ogni prova dovrà partire dallo stesso identificatore di commit e da una copia
pulita e indipendente. Le modifiche prodotte durante una prova non dovranno
raggiungere le prove successive.

Lo stato attuale è una prima versione dell'ambiente tecnico, non la baseline
sperimentale definitiva. Quest'ultima potrà essere dichiarata soltanto dopo
l'approvazione di struttura applicativa, task, criteri di successo e condizioni
iniziali.

## 2. Specifiche dei task

Ogni specifica costituisce la fonte ufficiale dei requisiti e dei criteri di
accettazione del lavoro richiesto. Dovrà contenere almeno:

- obiettivo funzionale e comportamento osservabile;
- requisiti e vincoli espliciti;
- condizioni iniziali;
- criteri oggettivi di completamento;
- test pubblici eseguibili e comandi consentiti;
- eventuali limiti di tempo, strumenti e risorse.

La stessa versione della specifica sarà usata in tutte le condizioni
sperimentali. Le specifiche non conterranno suggerimenti organizzativi che
favoriscano artificialmente una particolare architettura.

Gli agenti potranno inoltre consultare il codice del progetto, la documentazione
autorizzata e i test pubblici disponibili per comprendere il sistema e svolgere il
task. Queste fonti non sostituiscono né modificano i requisiti ufficiali contenuti
nella specifica.

La sperimentazione comprende esattamente 6 task (T1–T6) e 3 ripetizioni per ogni
combinazione task–architettura. Con le tre architetture confrontate, il disegno
prevede quindi 6 × 3 × 3 = 54 esecuzioni complessive.

## 3. Funzionalità sviluppata durante il task

È l'insieme delle modifiche che gli agenti producono nella loro copia di
`BenchmarkTarget`. Il perimetro modificabile sarà dichiarato nella specifica del
task. In generale potrà includere sorgenti, test aggiuntivi creati dagli agenti e
configurazioni necessarie, ma non i test pubblici di riferimento né
l'infrastruttura di valutazione riservata.

La soluzione di ciascuna esecuzione sarà conservata separatamente insieme a diff,
log e metriche. Non entrerà automaticamente nella baseline usata dalle altre
esecuzioni.

## 4. Test pubblici

I test pubblici sono visibili ed eseguibili dagli agenti durante lo sviluppo.
Servono a fornire feedback riproducibile sui requisiti dichiarati e a permettere
diagnosi e correzioni legittime.

I test pubblici di riferimento rimangono invariati durante tutte le esecuzioni:
gli agenti possono eseguirli e creare test aggiuntivi, ma non possono modificare o
eliminare quelli di riferimento. Questo vincolo garantisce condizioni comparabili
tra architetture e ripetizioni.

Comprenderanno, secondo le necessità del task:

- test unitari per singole funzioni o regole di dominio;
- test di integrazione per la collaborazione tra moduli;
- test di accettazione per una parte dei comportamenti esterni richiesti.

I test pubblici non devono rivelare tutti i casi della valutazione finale e non
sono, da soli, prova sufficiente del completamento del task.

## 5. Valutazione riservata indipendente

I test riservati verificano i criteri finali con casi non disponibili agli agenti.
Saranno conservati fuori dalla copia di lavoro modificabile e avviati da un
processo di valutazione controllato. Gli agenti non dovranno poterli leggere,
alterare o dedurre dai risultati dettagliati durante l'esecuzione.

La valutazione userà gli stessi test e la stessa configurazione per tutte le
architetture. I risultati registreranno almeno esito, casi superati e falliti,
durata ed errori osservati. Percorsi, permessi e comando di esecuzione saranno
definiti nel passo dedicato alla strategia dei test riservati.

## Flusso di una singola esecuzione

1. Selezionare il commit della baseline approvata.
2. Creare una copia pulita e indipendente di `BenchmarkTarget`.
3. Consegnare all'architettura la medesima specifica del task.
4. Consentire modifiche soltanto nel perimetro autorizzato e l'uso dei test
   pubblici.
5. Terminare l'esecuzione secondo condizioni comuni e conservare soluzione, log e
   metriche.
6. Eseguire esternamente i test riservati sulla soluzione prodotta.
7. Registrare risultati grezzi separandoli da stime e interpretazioni.

## Confini decisi in B1

- Il repository può contenere strumenti e documentazione del benchmark, ma ogni
  prova riceve una copia isolata del solo perimetro necessario.
- Il commit identifica la baseline; il nome di una cartella o una copia manuale
  non è sufficiente a garantirne l'identità.
- Test pubblici e riservati possono verificare gli stessi requisiti, ma usano casi
  distinti e hanno livelli di accesso differenti.
- Le soluzioni generate dagli agenti sono risultati sperimentali, non aggiornamenti
  automatici della baseline.
- Nessuna architettura riceve informazioni aggiuntive sui casi riservati.

## Decisioni rinviate ai passi successivi

- dominio e struttura applicativa definitiva di `BenchmarkTarget`;
- definizione dettagliata dei 6 task (T1–T6);
- framework di test e relative dipendenze;
- collocazione fisica e isolamento dei test riservati;
- metriche definitive e limiti di risorse;
- commit da designare come baseline sperimentale definitiva.

## Criteri di completamento di B1

Il passo è completo quando i cinque componenti sono definiti, i rispettivi confini
sono espliciti e nessuna soluzione dei task è stata implementata. La progettazione
dei task del passo B2 dovrà rispettare questi confini.
