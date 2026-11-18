# B25 — Protocollo dei sei piloti sul freeze finale

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione dei nuovi piloti, prima di osservare i risultati.
- **Stato:** protocollo documentato secondo la combinazione proposta e accettata
  dall'utente; nessun nuovo pilota eseguito in questa iterazione.
- **Autorizzazione operativa:** la definizione del protocollo non autorizza
  chiamate LLM; occorre un'autorizzazione esplicita prima dell'avvio.

Calibrare un unico limite di tempo e un unico budget di token per le tre
architetture. Componenti coinvolti: piano dei sei slot, harness e input congelati,
raccolta delle metriche ed evaluator fidato. Criteri di accettazione del
protocollo: copertura esatta di T1 e T6 per tutte le architetture, ordine
riproducibile, parametri comuni espliciti, criteri di validità e formula di
calibrazione fissati prima dei risultati, salvaguardia dei test riservati.

Questo documento definisce la combinazione corrente di decisioni: sei slot come
B22, ordine di B6, tetti e arrotondamenti di B21, regola 2×, massimo 10 deleghe
adattive e arresto senza aumento automatico al raggiungimento di un tetto. Le
parti incompatibili dei protocolli precedenti restano evidenze storiche: in
particolare non si applicano né il margine 1,25× di B6 né il piano a nove slot e
la ripetizione automatica con tetto raddoppiato di B21.

## Slot e ordine

Seed testuale `d9dc66a`: ordinare lessicograficamente gli SHA-256 delle stringhe
UTF-8 `<seed>|<architecture>`, ottenendo adaptive, static, single-agent. T1 segue
quest'ordine; T6 lo ruota a sinistra di una posizione, come in B6.

| Posizione | Identificativo dello slot | Task | Architettura |
| ---: | --- | --- | --- |
| 1 | `pilot-t1-hierarchical-adaptive-1` | T1 | `hierarchical-adaptive` |
| 2 | `pilot-t1-hierarchical-static-1` | T1 | `hierarchical-static` |
| 3 | `pilot-t1-single-agent-1` | T1 | `single-agent` |
| 4 | `pilot-t6-hierarchical-static-1` | T6 | `hierarchical-static` |
| 5 | `pilot-t6-single-agent-1` | T6 | `single-agent` |
| 6 | `pilot-t6-hierarchical-adaptive-1` | T6 | `hierarchical-adaptive` |

Ogni record usa `run-kind=pilot`, `repetition=1`, `order-seed=d9dc66a` e la
posizione della tabella. L'identificativo di slot è distinto dal `runId` generato
per ogni tentativo. Una sostituzione conserva i metadati dello slot, riceve un
nuovo `runId` e mantiene il collegamento al tentativo precedente.

## Input e limiti comuni

- Commit sorgente e istruzioni: `aee8d1162bb496895990a00095cc14c2651ff752`;
  manifest sorgente e binari secondo P30–P32 e P35.
- Toolchain definitiva secondo P33 e CLI dedicata secondo P34; modello esatto
  `claude-sonnet-5-5`. Evaluator: commit
  `a2f1626a50e696d2930bfc72d28b6cb339dc9d9e` e manifest secondo P35.
- Limite tecnico per ogni pilota: **2700 secondi**; budget: **3000000
  `totalTokens`**, inclusi input, cache read, cache creation e output.
- Solo per la gerarchia adattiva: `--max-delegations 10`. La gerarchia statica
  usa il proprio piano fisso di tre deleghe; non accetta quel parametro.
- Periodo tecnico di arresto nullo come in B6. I limiti pilota non sono i limiti
  ufficiali e sono uguali per tutti gli slot.
- Run sequenziali, stessa macchina e configurazione congelata, sessione nuova e
  area pulita preparata dal commit sorgente per ogni tentativo.

## Validità, arresto e ripetizioni

Un pilota è valido per calibrare se infrastruttura, configurazione effettiva,
integrità e misurazione sono valide e le metriche necessarie sono complete.
Una submission funzionalmente errata resta un risultato sperimentale e non
giustifica una ripetizione. Classificare separatamente errori infrastrutturali,
errori della soluzione, rifiuti di configurazione e misurazione incompleta.

Se viene raggiunto un limite di tempo o token, la calibrazione è inconclusiva:
fermarsi e sottoporre l'accaduto all'utente. Non aumentare automaticamente il
tetto e non includere una misura troncata come massimo completo. Anche in caso
di violazione del freeze o di esaurimento delle ripetizioni consentite, fermarsi
e valutare l'impatto prima di proseguire.

È ammessa al massimo una ripetizione per slot soltanto per gli errori
infrastrutturali osservabili previsti da B6: indisponibilità o rate limit del
fornitore, mancato avvio per errore host, guasto di connessione MCP o relay,
record o metriche mancanti per errore dell'harness, violazione d'integrità non
causata dall'agente. Conservare entrambi i tentativi e la motivazione; quello
invalidato non entra nel calcolo. Timeout, esaurimento token, configurazione
rifiutata per il comportamento effettivo della CLI, errori del modello e test
falliti non autorizzano una ripetizione. Massimo 12 tentativi complessivi.
Eventuali chiamate LLM, incluse le ripetizioni, richiedono autorizzazione
esplicita dell'utente prima dell'avvio.

## Valutazione e calcolo dei limiti

Dopo ogni run congelare la submission e valutarla indipendentemente con
l'evaluator fidato, suite pubbliche e riservate, verificando manifest e integrità.
Non esporre test o dettagli riservati agli agenti o a prompt successivi.
Conservare record, stream, audit e valutazione; controllare la completezza di
tempo, token e cache, chiamate LLM, deleghe, errori, overhead e processi residui.
Le run pilota sono escluse dalle 54 run ufficiali.

Con esattamente uno slot valido per ciascuna delle sei coppie e nessuna misura
troncata, calcolare congiuntamente sulle tre architetture e sui due task:

```text
timeLimitSeconds = 60 × ceil((2 × max(agentTaskDurationMs)) / 60000)
tokenBudget      = 100000 × ceil((2 × max(totalTokens)) / 100000)
```

Registrare i massimi non arrotondati, gli slot che li determinano e il calcolo.
Non derivare limiti separati per architettura. Presentare i valori all'utente e
congelarli soltanto dopo la sua approvazione esplicita.

## Controlli prima dell'avvio e limiti del documento

Prima di ogni tentativo verificare gli input congelati, l'integrità della
baseline, le identità dell'evaluator e dei manifest, la CLI secondo P34, e
l'isolamento delle aree e dei materiali riservati; controllare dopo la run gli
hash delle configurazioni effettive nei record secondo P32. Le verifiche P35
sono una fotografia precedente e non sostituiscono i controlli pre-run. Il
presente protocollo non certifica l'isolamento dell'intero sistema e non
autorizza run ufficiali.

## Verifica della presente iterazione

Uno script di controllo eseguito con il Node congelato ha ricalcolato gli
SHA-256 dell'ordine e controllato sei identificativi univoci, posizioni 1–6,
copertura delle sei coppie, parametri e identità dell'evaluator: esito OK.
Ha inoltre verificato l'assenza di spazi finali in questo documento. Il primo
tentativo equivalente con `python` non è stato eseguito perché il comando non
è disponibile nel PATH; è stato sostituito dal controllo Node. `git diff --check`
non ha riportato errori per i file già tracciati. Non sono stati eseguiti test
funzionali o piloti: questa modifica riguarda soltanto la documentazione.
