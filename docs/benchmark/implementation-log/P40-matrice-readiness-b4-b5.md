# P40 — Matrice preliminare dei requisiti B4/B5

## Stato e metodo

- **Data:** 18 novembre 2026.
- **Fase:** audit preliminare prima delle run ufficiali.
- **Stato:** evidenze censite; la readiness completa non è certificata.
- **Chiamate LLM e run ufficiali:** nessuna in questa iterazione.

Obiettivo: individuare verifiche riutilizzabili e lacune senza ripetere
indiscriminatamente i piloti. Componenti: requisiti B4/B5, prove e test
documentati, record pilota e evaluator, strumenti audit P38/P39. Criteri:
associare ogni requisito a evidenze o dichiararlo aperto, distinguere test
esistenti da risultati effettivamente osservati e non estendere la portata
delle prove oltre quanto riportato.

Le evidenze storiche sono richiamate, non rieseguite in questa iterazione.
`Parziale` significa che esiste evidenza pertinente ma non sufficiente a
certificare l'intero requisito. I nomi dei test indicano copertura progettata;
il loro solo reperimento non dimostra che siano stati eseguiti oggi.

## Requisiti B4

| N. | Requisito sintetico | Evidenza reperita | Stato e residuo |
| --- | --- | --- | --- |
| 1 | Commit e prompt comuni per architetture | B24, manifest P30, record B26–B31, P32 | Parziale: commit e prompt registrati nei piloti; serve confronto sistematico per ogni task e configurazione ufficiale. |
| 2 | Materiali autorizzati, senza `.git`, collegamenti o credenziali | P27 e preparatore/test di preparazione | Parziale: censire prove effettive per tutte le proprietà e percorsi, incluse credenziali. |
| 3 | Modello, strumenti e permessi coerenti | P29, P32 e record dei piloti | Parziale: confrontare profili effettivi di tutte le condizioni e differenze autorizzate. |
| 4 | Nessuno strumento o percorso alternativo non previsto | P27, P29, P32 | Aperto: P27 limita esplicitamente la portata; la sola configurazione non dimostra assenza di percorsi alternativi. |
| 5 | Budget globale coordinatore e worker | `RunLimitControllerTests`, `RunAsync_BudgetReachedBySumOfAgents_StopsWorkerAndCoordinatorPromptly` | Parziale: test disponibili; verificare evidenze e applicabilità al binario congelato. |
| 6 | Nessun doppio conteggio padre/figli/cumulativi | `StreamUsageTrackerTests`, `RunLimitControllerTests`, record B26–B31 | Parziale: deduplicazione e riconciliazione locale coperte da evidenze; audit completo di retry e figli ancora necessario. |
| 7 | Cache separata e inclusa nel totale | P26 e record B26–B31 | Parziale: categorie osservate nei piloti; verificare anche i casi di errore e record incompleti. |
| 8 | Arresto uniforme su limiti per tutti i processi | test single-agent e gerarchici su limiti e P32 | Parziale: test mirati presenti; riconciliare esiti, submission ed eccedenze con i requisiti B5. |
| 9 | Workspace invariato durante valutazione | riepiloghi B26–B31; test evaluator `Test-Evaluation.ps1` | Parziale: integrità osservata nei piloti e casi di alterazione presenti nei test; non equivale a garanzia continua. |
| 10 | Metriche mancanti riconosciute come non valide | test `RunAsync_WorkerWithoutResult_DelegationFailsAndMeasurementIsIncomplete` | Parziale: stato di misura incompleta coperto da test; classificazione per metrica e aggregazione restano da provare. |
| 11 | Ordine e seed registrati e riproducibili | B23, B33 | Coperto per il piano generato: 54 slot e riproducibilità verificati; registrazione effettiva resta da controllare per ogni run. |
| 12 | Limiti derivati dai piloti documentati | B26–B32 | Coperto per i valori approvati: sei record riletti, massimi e formula verificati in B32. |

## Requisiti B5

| N. | Controllo sintetico | Evidenza reperita | Stato e residuo |
| --- | --- | --- | --- |
| 1 | Totali riconciliati senza duplicati | B26–B31; test `StreamUsageTrackerTests` | Parziale: uso stream/autorevole confrontato nei piloti; riconciliazione completa per ogni chiamata e ruolo da documentare. |
| 2 | Significato dei record CLI coordinatore e figli | P26, record B27 e B29 | Parziale: ruoli e uso osservati; verificare semantica e completezza anche su errori e retry. |
| 3 | Retry e chiamate fallite conteggiati una volta | test su eventi ripetuti dello stesso messaggio | Aperto: non dimostra tutti i retry/fallimenti con consumo. |
| 4 | Entrambi i limiti, stato, submission ed eccedenza | test `RunLimitControllerTests`, `SingleAgentSessionRunnerTests`, `HierarchicalRunRunnerTests` | Parziale: test disponibili su arresto e stato; confrontare esplicitamente copertura di submission ed eccedenza per entrambe le condizioni. |
| 5 | Formule tassi, build failure e test saltati | `Test-ReportValidation.ps1` e prove evaluator documentate nel freeze evaluator | Parziale: esistono casi di esito e conteggio; formule di aggregazione B5 non verificate qui. |
| 6 | Guasto evaluator produce valori non disponibili, non zeri | `Test-Evaluation.ps1` contiene casi `evaluator-error` | Aperto: provare propagazione fino alle metriche e agli aggregati, non solo esito evaluator. |
| 7 | Sostituzione e audit trail | B34, P37–P39 | Parziale: policy approvata, fixture e script provati; custodia anchor, integrazione e prova operativa restano aperte. |
| 8 | Formule monetarie con listino congelato | B34 | Non applicato per ora: stime economiche non disponibili finché listino e formule non sono congelati e verificati; non trattare come zero. |
| 9 | Aggregati a 3 e 18 riconciliati | Nessuna evidenza di aggregatore e prova individuata in questa ricognizione | Aperto: definire e verificare l'aggregazione contro record di origine. |
| 10 | Esclusione di piloti e invalide dai risultati ufficiali | metadata `runKind`, piano B33, policy B34 | Parziale: identificazione documentata; provare il filtro nell'effettiva elaborazione dei risultati. |

## Capacità e comandi osservati

Node congelato, tramite `fs.statfsSync` sul volume `C:`, ha restituito
`availableBytes=933930115072` e `totalBytes=2047330590720`. È una misura
puntuale, non una previsione del consumo delle 54 run; non è stata applicata una
soglia di sufficienza. Nessuna area ufficiale è stata creata.

Le ricerche `rg` hanno individuato i test e i documenti sopra riportati. Due
ricerche con glob passati come percorsi hanno restituito exit code 1 per sintassi
Windows; i documenti pertinenti sono stati letti o ricercati separatamente. Una
ricerca `rg --files src` eseguita nel repository evaluator è fallita perché lì
non esiste `src`; non dimostra l'assenza di componenti altrove.

## Ordine di lavoro proposto

1. Provare la custodia dell'anchor e l'integrazione del registro con dati reali
   simulati, senza chiamate LLM e senza modificare gli input congelati.
2. Riconciliare requisiti B4/B5 parziali con i test e le evidenze esistenti;
   eseguire solo i casi non coperti, soprattutto retry e misurazione incompleta.
3. Verificare l'assenza dei percorsi alternativi e la protezione dei test
   riservati con prove di accettazione sull'harness corrente.
4. Definire e verificare l'elaborazione dei risultati e delle aggregazioni,
   preservando valori mancanti e distinzione piloti/run ufficiali.

La matrice identifica lacune, non autorizza modifiche sperimentali o run. I
piloti non vanno ripetuti per questa sola ricognizione; si applica B22 se un
futuro intervento cambia elementi congelati che possono influire sull'esperimento.
