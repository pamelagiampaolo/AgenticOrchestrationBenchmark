# B35 — Limitazione di isolamento accettata per la fase corrente

## Decisione e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle 54 run ufficiali.
- **Decisione:** l'utente, informato del risultato P43, ha scelto di rinviare a
  sviluppo futuro gli interventi sull'isolamento e di proseguire con la
  configurazione sperimentale corrente.
- **Stato:** limitazione documentata; non costituisce prova di confinamento né
  autorizzazione all'avvio delle 54 run o alle relative chiamate LLM.

Obiettivo: registrare trasparentemente una deviazione dai criteri di isolamento
prima di ulteriori decisioni sulle run. Componenti: evidenza P43, baseline e
harness congelati, piloti e piano ufficiale. Criteri: non dichiarare risolto il
problema, conservare le evidenze, distinguere decisione di rinvio e avvio delle
run, non attribuire una probabilità numerica non misurata.

## Limitazione e conseguenze

P43 dimostra che un test agente eseguito tramite `run_agent_tests` ha letto una
sentinella sintetica esterna alla root MCP, mentre la lettura diretta tramite
MCP era stata rifiutata. P46 riporta metadati ACL della directory riservata;
P47 chiarisce che l'audit di lettura effettivamente disponibile non è stato
accertato. Non è dimostrata né l'inaccessibilità dei test riservati, né la loro
lettura da parte dei piloti. Non è stata misurata una probabilità di accesso.

Proseguire così significa accettare una **deviazione dal requisito precedente
di inaccessibilità verificata**. La limitazione deve accompagnare protocollo,
risultati e interpretazione della tesi: una contaminazione eventuale potrebbe
compromettere l'indipendenza della valutazione, e l'assenza di eventi registrati
dall'harness corrente non dimostra che non siano avvenute letture esterne.
Non si modifica la policy sperimentale per invitare o istruire gli agenti a
cercare test riservati.

Gli interventi di prevenzione o audit dettagliato dell'accesso ai test
riservati sono rinviati a lavoro futuro per scelta dell'utente. Le evidenze
P43–P47 restano conservate. Non si modificano baseline, istruzioni, harness,
toolchain o evaluator per questa decisione; pertanto non si ripetono i piloti
per la sola accettazione della limitazione. Qualunque futura modifica agli
input congelati resta soggetta alla valutazione di impatto B22.

## Prerequisiti ancora distinti

Il rinvio riguarda l'isolamento discusso sopra, non dimostra automaticamente
gli altri requisiti B4/B5 o la completezza delle metriche. Restano da valutare
in modo proporzionato i punti non legati all'isolamento già censiti in P40,
inclusi retry, dati mancanti, aggregazione e procedura del registro. Il piano
B33 e i limiti B32 restano quelli approvati. Occorre un'autorizzazione esplicita
separata prima di avviare le run ufficiali e le chiamate LLM.

## Verifica della decisione

Sono stati riletti P40, P43, B22, B32–B34 e lo stato Git prima della modifica;
nessuna prova funzionale o run è stata eseguita in questa iterazione. Questa
registrazione non cambia il risultato negativo della prova P43.
Un controllo testuale iniziale non ha trovato alcune frasi a causa delle
maiuscole o dei fine riga; il controllo corretto è passato. `git diff --check`
non ha riportato errori.
