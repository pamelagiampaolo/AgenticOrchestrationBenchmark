# P10 — Ricognizione dell'isolamento del worker

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** ricognizione locale completata; nessun meccanismo dichiarato sicuro.
- **Obiettivo:** individuare la primitiva Windows minima da sottoporre a una prova
  con sentinelle prima di implementare gli strumenti MCP di esecuzione.

Non sono stati creati utenti o profili AppContainer, non sono state modificate
ACL, regole firewall, funzionalità Windows o servizi e non è stato avviato codice
del benchmark.

## Ambiente osservato

- PowerShell `5.1.26100.9711`;
- kernel riportato da `[Environment]::OSVersion.Version`: `10.0.26100.0`;
- prodotto riportato da `Get-ComputerInfo`: `Windows 10 Pro`, versione `2009`;
- identità effettiva: `PAMPAM\codexsandboxoffline`;
- unico privilegio elencato: `SeChangeNotifyPrivilege`;
- gruppi amministrativi assenti dal token osservato.

La sessione corrente può leggere sia il repository sia
`C:\Users\Utente\.claude`. Eseguire Node.js con la stessa identità, anche con una
working directory dedicata, non isolerebbe quindi configurazioni e file leggibili
dalla sessione.

## Capacità rilevate

| Meccanismo | Osservazione locale | Esito per il progetto |
| --- | --- | --- |
| Windows Sandbox | `WindowsSandbox.exe` presente | escluso dall'architettura corrente |
| Docker / Podman | comandi assenti | non disponibile |
| WSL | comando presente, enumerazione negata con `E_ACCESSDENIED` | non utilizzabile dalla sessione verificata |
| HCS / Hyper-V | `hns` e `vmcompute` attivi, `hcsdiag.exe` presente | possibile ma sproporzionato senza una prova più semplice |
| Utente locale separato | cmdlet disponibili, token corrente non amministrativo | richiederebbe identità persistente, ACL e modifiche di sistema |
| Restricted token | API `CreateRestrictedToken` presente | insufficiente da solo: non rimuove automaticamente l'accesso ai file dell'utente |
| AppContainer | API necessarie presenti | candidato minimo per la prossima prova |

La presenza è stata verificata senza invocare operazioni mutanti controllando gli
export locali:

```text
CreateAppContainerProfile                         true
DeriveAppContainerSidFromAppContainerName         true
DeleteAppContainerProfile                         true
CreateRestrictedToken                             true
CreateProcessAsUserW                              true
UpdateProcThreadAttribute                         true
```

Sono inoltre attivi `mpssvc`, `hns` e `vmcompute`; `AppIDSvc` è presente ma
arrestato con avvio manuale. Questi dati descrivono soltanto la disponibilità
locale e non provano che l'identità corrente possa completare ogni operazione.

## Scelta candidata

La prossima prova userà un processo **AppContainer senza capability di rete**, con
accesso concesso esplicitamente soltanto a una fixture temporanea e alle copie del
runtime strettamente necessarie. Il processo sarà inoltre inserito in un Job
Object per timeout, terminazione dell'intero albero e limiti operativi.

La scelta è candidata perché:

- è una primitiva Windows già presente;
- consente di attribuire l'accesso alla fixture a un SID distinto;
- non richiede di esporre una shell o di riutilizzare l'identità dell'agente;
- evita, se la prova riesce, la creazione di un utente locale persistente;
- può essere combinata con un ambiente costruito da allowlist e con il blocco dei
  processi residui tramite Job Object.

Queste sono motivazioni progettuali, non evidenze di isolamento. In particolare
non è ancora dimostrato che Node.js e le dipendenze fissate possano avviarsi nel
contenitore, che nessuna capability di rete venga acquisita implicitamente o che
tutti i figli conservino il confine atteso.

## Alternative non scelte per la prima prova

- **Identità corrente e ACL:** non proteggono i percorsi già leggibili dalla
  sessione.
- **Restricted token da solo:** riduce privilegi e gruppi ma non costituisce un
  confine dei file equivalente a una allowlist di percorsi.
- **Nuovo utente locale:** rimane un fallback possibile, ma richiede gestione di
  account, credenziali, ACL e rete e produce stato persistente sull'host.
- **WSL:** la distribuzione non è enumerabile dall'identità osservata e il suo
  confine di rete e filesystem richiederebbe una progettazione separata.
- **HCS/container personalizzato:** offre un confine più ampio ma introduce
  maggiore complessità e artefatti non ancora necessari.

## Prova P11 richiesta

La prova successiva deve usare esclusivamente sentinelle sintetiche e non i test
riservati reali. Deve:

1. creare una fixture temporanea autorizzata e percorsi esterni vietati;
2. derivare o creare un profilo AppContainer temporaneo con nome univoco;
3. concedere al relativo SID soltanto i diritti minimi sulla fixture;
4. avviare un programma di probe, non il benchmark, tramite
   `PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES`;
5. osservare lettura e scrittura consentite nella fixture;
6. tentare letture di repository, profilo, configurazioni e risultati simulati;
7. tentare percorsi assoluti, traversal e junction verso l'esterno;
8. tentare accesso tramite un processo figlio;
9. tentare DNS, TCP e HTTP verso un endpoint controllato;
10. verificare che l'ambiente contenga soltanto nomi ammessi e nessun segreto;
11. applicare un timeout e dimostrare la terminazione dell'intero albero;
12. rimuovere profilo, ACL e fixture creati dalla prova, verificando l'assenza di
    residui.

La prova deve distinguere `access-denied`, `network-denied`, `timeout` ed errore
infrastrutturale. Un mancato avvio di Node.js non certifica l'isolamento: indica
soltanto che il candidato non è ancora operativo.

## Criterio per proseguire

Gli strumenti MCP `run_*` potranno essere implementati soltanto se P11 dimostra
accessi interni riusciti, tutti gli accessi esterni e di rete negati, propagazione
del confine ai figli, terminazione completa e pulizia senza residui. In caso
contrario si rivaluteranno, nell'ordine, AppContainer con una toolchain preparata
diversamente, identità locale dedicata con ACL e blocco rete, oppure un confine
HCS più forte.
