# P44 — Analisi delle opzioni dopo la prova P43

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** analisi del confinamento prima delle run ufficiali.
- **Stato:** causa e alternative analizzate; nessuna soluzione implementata o
  certificata e nessuna run ufficiale autorizzata.
- **Chiamate LLM:** nessuna.

Obiettivo: individuare una direzione verificabile per chiudere la via osservata
in P43, senza modificare la baseline o introdurre cambiamenti architetturali in
anticipo. Componenti esaminati: server MCP, esecuzione dei test pubblici e
agente, typecheck/build, documenti storici sui worker e disponibilità locale di
strumenti Windows. Criteri: spiegare il confine mancante, distinguere fatti da
ipotesi e confrontare alternative e impatto sul freeze.

## Causa e superficie

P43 ha osservato che `run_agent_tests` esegue codice scritto dall'agente tramite
Node/Vitest, e quel processo ha letto una sentinella esterna alla root MCP. Il
controllo dei percorsi di `read_file` non si applica alle operazioni `node:fs`
compiute dal processo che esegue il test. Il codice di
`ControlledPublicTestRunner` dichiara esplicitamente che il runner non è un
confine di sicurezza; usa `ProcessStartInfo` e una directory di lavoro, senza
che ciò costituisca di per sé un'identità o un perimetro di accesso separati.

Anche `run_public_tests` usa Vitest su una copia del workspace modificabile e
test pubblici che importano il codice della soluzione. **Per inferenza dal
codice**, questa è un'ulteriore superficie da verificare: rimuovere soltanto
`run_agent_tests` non dimostra di aver chiuso la via. Questa iterazione non ha
eseguito un exploit o una prova di accettazione di `run_public_tests`.
`run_typecheck` e `run_build` usano processi Node separati e configurazioni del
workspace: vanno inclusi nell'analisi e nelle prove, senza affermare che siano
stati dimostrati come vettori equivalenti.

## Alternative

| Direzione | Vantaggio | Limite e verifiche necessarie |
| --- | --- | --- |
| Confine OS per i worker con HCS/container Windows | Potenziale separazione di filesystem e rete mantenendo Vitest canonico. | Cambiamento architetturale; occorre verificare disponibilità reale, immagine, ACL/mount, rete, pulizia e compatibilità della toolchain. La presenza di HCS non è prova di funzionamento o isolamento. |
| Identità Windows dedicata e ACL restrittive | Potrebbe mantenere il runner canonico con un'identità distinta. | Richiede gestione di account/permessi, verifica di tutte le risorse ereditate e blocco rete; non basta cambiare working directory. |
| AppContainer | Confine già esplorato come prova tecnica storica. | P12/P13 riportano che Vitest 5.1.0, nella configurazione allora provata, non ha completato; il pool personalizzato è stato abbandonato. Non assumere che sia utilizzabile senza una nuova prova di compatibilità. |
| Disabilitare strumenti o affidarsi a prompt/configurazione | Minore lavoro apparente. | Cambia il feedback disponibile e quindi le condizioni sperimentali; disabilitare solo i test agente lascia aperta la superficie dei test pubblici inferita sopra. Prompt e allowlist non sono confini OS. |

Windows Sandbox resta fuori dall'architettura operativa corrente. I documenti
P10–P13 sono evidenze storiche utili, non prerequisiti del percorso attuale.

## Ricognizione locale osservata

`Get-Command` ha individuato `hcsdiag.exe`, `wsl.exe` e `runas.exe`; `docker.exe`
non è stato trovato nel PATH. `Get-Service` ha riportato `vmcompute` e `hns` in
stato `Running`. La ricognizione è in sola lettura e non dimostra che un
container possa essere creato, che un'immagine sia disponibile o che la
toolchain e la rete si comportino come richiesto.

## Prossimo passo raccomandato e impatto

Prima di cambiare l'harness, eseguire una ricognizione tecnica circoscritta e
non invasiva sulla fattibilità di un worker HCS: individuare se esiste già
un'immagine e quali prerequisiti effettivi siano disponibili, senza creare
container, account, ACL o servizi. In parallelo alla scelta del confine, il
criterio finale deve coprire sia test agente sia codice workspace eseguito dai
test pubblici, oltre agli altri strumenti, ai materiali riservati e ai percorsi
alternativi. Un confine dei soli worker non certifica automaticamente l'intero
perimetro della CLI e dell'harness.

Un'eventuale integrazione cambierebbe il comportamento operativo dell'harness e
potrebbe influenzare tempi, token e strategie: applicare B22, congelare una
nuova configurazione e ripetere i piloti se l'analisi dell'impatto lo richiede.
La presente analisi non decide che i piloti storici abbiano letto i test
riservati né li elimina; le loro evidenze devono essere conservate.

Nessuna modifica all'harness, account, ACL, servizio o baseline è stata
eseguita in questa iterazione. Questa analisi non autorizza chiamate LLM o run.

Il primo controllo automatico del testo del documento ha cercato una frase
continua spezzata invece da un fine riga ed è terminato con exit code 1; il
controllo corretto dei contenuti e degli spazi finali è terminato con exit code
0. `git diff --check` non ha riportato errori.
