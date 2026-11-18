# B34 — Politica operativa essenziale per la readiness

## Stato e obiettivo

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali dopo P36.
- **Stato:** scelte approvate dall'utente; esercizio su fixture in memoria
  eseguito. L'applicazione operativa e le ulteriori prove restano da verificare.
- **Chiamate LLM e run ufficiali:** nessuna in questa iterazione.

Obiettivo: chiudere i punti aperti di P36 con un insieme piccolo di regole e
verifiche, preservando validità e tracciabilità. Componenti: classificazione e
audit delle run, evaluator, metriche, prove di isolamento e capacità. Criteri:
distinguere cause attribuibili e non attribuibili, limitare le ripetizioni,
conservare ogni tentativo e indicare verifiche osservabili ancora da eseguire.
Questa politica non modifica l'harness congelato e non autorizza run.

## Sostituzioni delle run ufficiali

Usare codici **di classificazione del ricercatore**, distinti dagli stati
effettivi dell'harness e dell'evaluator. Un codice è ammissibile soltanto con
evidenza del componente responsabile e della sua indipendenza dall'architettura:

| Codice | Evento e prova minima | Azione |
| --- | --- | --- |
| `INFRA_PROVIDER` | Indisponibilità o rate limit del servizio attestato dalla risposta o log del fornitore. | Sostituzione ammessa. |
| `INFRA_HOST_START` | Processo non avviato per errore host documentato, non per comando prodotto dall'agente. | Sostituzione ammessa. |
| `INFRA_COMMON_SERVICE` | Guasto del server MCP, relay o ambiente comune documentato nei log del componente, non causato dall'agente. | Sostituzione ammessa. |
| `INFRA_MEASUREMENT` | Dati necessari mancanti o errati per guasto documentato dell'harness, non attribuibile all'architettura. | Sostituzione ammessa se la metrica interessata non è ricostruibile. |

Consentire **al massimo una sostituzione per slot**, con una nuova area e un nuovo
`runId`; conservare entrambi i tentativi, la stessa posizione, task,
architettura, ripetizione e seed. Una sostituzione non è automatica: registrare
prima evidenza e classificazione e ottenere l'autorizzazione esplicita alle
nuove chiamate LLM. Se anche il tentativo sostitutivo non è valido, lasciare
lo slot incompleto e preservare il denominatore pianificato.

Un esito funzionale sfavorevole, build o test falliti della soluzione, errori
di coordinamento o strumenti attribuibili all'agente e raggiungimento dei
limiti restano risultati della run e non autorizzano sostituzione. Stato o
codice isolato non basta a provare una causa infrastrutturale.

Per causa non determinabile: contrassegnare `non-classificabile`, conservare
le evidenze, fermarsi e chiedere revisione; non sostituire o riclassificare per
deduzione. In caso di violazione d'integrità o esito `integrity-error` del
valutatore: fermarsi e verificare l'impatto sul freeze; non ripetere o
sostituire automaticamente.

Per un `evaluator-error` (exit 40) documentato, conservare la submission e i
log; dopo aver verificato integrità e corretto il guasto esterno, proporre una
sola nuova valutazione della **stessa submission**, in una nuova area fidata.
Non è una sostituzione della run LLM. Se l'errore persiste o la causa resta
incerta, fermarsi e marcare le metriche interessate non disponibili. I codici
10, 11 e 12 dell'evaluator hanno significati propri e non provano da soli un
guasto esterno; applicare la distinzione di B5 sulla base delle evidenze.

## Audit e verifiche minime proposte

Per ogni slot conservare un registro fidato con identificativo dello slot,
`runId`, tentativo, collegamento a eventuale precedente, ordine e seed,
classificazione, evidenze, decisione e disponibilità delle metriche. Non
modificare record originali o eliminare tentativi. La prova di accettazione da
eseguire prima delle run deve coprire almeno: un guasto infrastrutturale con
una sostituzione collegata, una causa ignota senza sostituzione e un fallimento
funzionale conservato. Verificare anche che una seconda sostituzione sia
rifiutata e che lo slot resti incompleto quando necessario. Una prova manuale
su fixture o registro di controllo non dimostra un'automazione inesistente.

Per le metriche, costruire una matrice B4/B5 requisito → evidenza esistente →
esito → eventuale prova mancante. Riutilizzare i test e i sei record dei piloti
come evidenze limitate; eseguire soltanto le prove non già dimostrate. La
riconciliazione dei sei record non sostituisce le prove dei limiti, di retry,
fallimenti, valori mancanti e aggregazioni richieste da B5.

Per ridurre lavoro non necessario, riportare prioritariamente i
conteggi di token misurati; non presentare costi monetari come misurati o
fatturati. Se sono richieste stime economiche, congelare prima un listino con
fonte, data, modello, tariffe e formula B5 e verificarlo con casi noti; finché
non disponibile lasciare tali stime non disponibili, senza valori inventati.
Questa scelta non elimina gli altri requisiti di B5.

Per capacità, leggere lo spazio libero reale sul volume delle aree di run,
risultati ed evaluator con un metodo disponibile e registrare metodo e byte;
confrontarlo con una stima documentata, senza assumere soglie non approvate.
Per isolamento, associare ogni requisito alle evidenze P27/P29 e ai piloti,
identificare i percorsi alternativi e le proprietà non coperte ed eseguire le
prove di accettazione mancanti prima di certificare il requisito.

## Evidenze della presente iterazione e limiti

Sono stati consultati P36, B5, gli stati e test dell'harness e gli exit code
dell'evaluator. La ricerca ha rilevato test mirati per limiti, configurazione e
alcuni errori infrastrutturali; qui non sono stati eseguiti. Un tentativo di
lettura di P36 nella directory dell'evaluator è fallito perché il documento è
nel repository benchmark; la successiva lettura nel repository corretto è
riuscita. Una ricerca aggiuntiva senza corrispondenze è terminata con exit 1.

L'utente ha successivamente approvato le scelte sopra riportate. Un esercizio
PowerShell su fixture esclusivamente in memoria ha verificato: collegamento tra
tentativo originale e sostitutivo con stesso slot, posizione e seed e `runId`
diversi; mancata ammissione di causa ignota e fallimento funzionale; rifiuto di
una seconda sostituzione e classificazione dello slot come incompleto. Output:
`Fixture: sostituzione collegata OK; causa ignota senza sostituzione OK; fallimento funzionale senza sostituzione OK; seconda sostituzione negata e slot incompleto OK`.
Exit code 0.

L'esercizio usa una semplice regola di prova scritta nella stessa invocazione e
non verifica una procedura operativa o un registro persistente indipendente:
non certifica l'applicazione della politica, l'audit trail reale, la readiness,
l'isolamento o le aggregazioni. La prova operativa della tracciabilità e l'audit
dei requisiti restano passi separati e circoscritti.
