# A4 — Canale di coordinamento per le architetture gerarchiche

- **Data:** 8 novembre 2026
- **Fase:** realizzazione delle architetture sperimentali (dopo A3, single-agent)
- **Branch:** `feat/single-agent` (worktree `C:\repos\TesiAgenticAI-agent`)
- **Stato:** completata; canale verificato con test automatici, nessuna chiamata LLM

## Decisioni dell'utente (8 novembre 2026)

B4 rinvia a un passo successivo la progettazione degli strumenti di coordinamento.
Prima di iniziare l'utente ha deciso:

1. **Delega tramite processi separati.** Ogni lavoratore è una sessione Claude CLI
   distinta, avviata dall'orchestratore fidato con lo stesso profilo isolato di A3.
   Il coordinatore delega tramite uno strumento MCP di coordinamento. I subagenti
   nativi della CLI restano disattivati come verificato in P1.1.
2. **Livello di coordinamento condiviso.** Il meccanismo è costruito una sola volta
   in `AgenticExperimentRunner` ed è comune a gerarchia statica e adattiva; la
   gerarchia statica ne è una configurazione con ruoli e deleghe prefissati.
3. **Un lavoratore alla volta.** La delega è bloccante per il coordinatore e al
   massimo un lavoratore è attivo. Si evitano scritture concorrenti nello stesso
   workspace e attribuzioni ambigue di tempo e token.

## Progetto complessivo delle gerarchie

Questa sezione descrive la direzione; ogni elemento sarà realizzato e verificato
in un'iterazione propria e potrà essere corretto dalle evidenze.

- **Ruoli.** Un coordinatore e zero o più lavoratori, tutti con lo stesso modello
  e lo stesso profilo di avvio isolato di A3.
- **Strumenti.** Il coordinatore dispone dei cinque strumenti del workspace
  (`benchmark_workspace`) più `delegate_task` (`benchmark_coordination`). I
  lavoratori hanno soltanto i cinque strumenti del workspace: la profondità della
  gerarchia è quindi uno.
- **Workspace.** Coordinatore e lavoratori operano sulla stessa area agente con
  le stesse directory scrivibili; la sequenzialità evita conflitti.
- **Budget.** Un solo controllore esterno somma in tempo reale i token di tutti i
  processi (stream con `--include-partial-messages`, verificato esatto in A3) e il
  tempo dell'intera architettura; al superamento termina tutti i processi (B4).
- **Differenza statica/adattiva.** Solo istruzioni architetturali e politica di
  delega dell'orchestratore (numero massimo di deleghe, eventuali ruoli fissati).
  La semantica degli strumenti resta identica.
- **Registrazione.** Record per agente con identità, ruolo, uso per chiamata ed
  eventi di delega richiesti da B5.

### Canale tra coordinatore e orchestratore

La CLI avvia i server MCP come processi figli stdio. Lo strumento di delega deve
però essere servito dall'orchestratore, che è l'unico a poter avviare lavoratori e
applicare il budget globale. Il canale è quindi diviso in due parti:

```text
CLI coordinatore ──stdio MCP──> relay `coordination-server` ──named pipe──> orchestratore
                                (processo figlio della CLI)                 (avvia il lavoratore)
```

- Il **relay** è una modalità dello stesso eseguibile `AgenticExperimentRunner`
  (`coordination-server --pipe <nome>`): parla MCP su stdio, valida i parametri e
  inoltra la delega sulla pipe. Non avvia processi e non accede a file.
- L'**host** di coordinamento nell'orchestratore crea la pipe con nome casuale per
  run, riservata all'utente corrente (`PipeOptions.CurrentUserOnly`), accetta una
  sola connessione e passa ogni delega a un gestore. Le deleghe sono eseguite una
  alla volta nell'ordine di arrivo.

Alternativa scartata: un server MCP HTTP ospitato dall'orchestratore su localhost.
Eviterebbe il relay, ma richiede il trasporto HTTP di MCP scritto a mano ed espone
una porta TCP a ogni processo locale; il trasporto stdio è invece quello già
verificato con la CLI in P1–P7.

## Iterazione A4

### Obiettivo

Realizzare e verificare il canale di delega da solo: relay MCP stdio, protocollo
sulla pipe e host nell'orchestratore con un gestore sostituibile. L'avvio dei
lavoratori, il budget globale e la sessione del coordinatore sono fuori ambito
(A5).

### Componenti coinvolti

- `AgenticExperimentRunner/Coordination/` (nuovo): protocollo MCP dello strumento,
  messaggi sulla pipe, relay, host e interfaccia del gestore delle deleghe.
- `AgenticExperimentRunner/Program.cs`: instradamento del comando
  `coordination-server`.
- `AgenticExperimentRunner.Tests/Coordination/` (nuovo): test unitari e di
  integrazione.

Nessun file di proprietà di Codex (`McpReadOnlyPrototype/`, `scripts/`, `P*`) viene
modificato.

### Comportamento atteso

1. `initialize` restituisce protocollo `2025-06-18`, capacità `tools` e
   `serverInfo` `benchmark-coordination`; `ping` restituisce un oggetto vuoto; le
   notifiche non ricevono risposta.
2. `tools/list` espone solo `delegate_task` con un unico parametro obbligatorio
   `task` (stringa) e nessun parametro aggiuntivo.
3. `tools/call` con `task` non vuoto e non più lungo del massimo inoltra la delega
   all'host e restituisce il testo del gestore; `isError` è `true` se la delega non
   è stata completata.
4. Parametri mancanti, aggiuntivi, di tipo errato, vuoti o troppo lunghi danno
   errore JSON-RPC `-32602` senza contattare l'host; strumento sconosciuto
   `-32602`; metodo sconosciuto `-32601`; JSON non valido `-32700`.
5. Il relay risponde a `ping` anche mentre una delega è in corso.
6. Due deleghe inviate insieme sono eseguite una alla volta: il gestore non vede
   mai due deleghe contemporanee.
7. L'host accetta una sola connessione; una seconda connessione non viene servita.
8. Se la pipe non esiste il relay termina con codice 1 e un messaggio su stderr
   entro il tempo di connessione configurato; argomenti non validi danno codice 2
   (stessa convenzione di `run-single-agent`).
9. Se l'host chiude la connessione, le deleghe in attesa ricevono un errore
   JSON-RPC invece di restare bloccate.

### Criteri di accettazione

- Test unitari del protocollo per i punti 1–4.
- Test di integrazione relay–pipe–host nello stesso processo per i punti 3, 5, 6,
  7 e 9.
- Test di processo che avvia `AgenticExperimentRunner.exe coordination-server`
  reale e scambia MCP su stdio per i punti 3 e 8.
- `dotnet format --verify-no-changes`, build senza avvisi e intera suite verde.
- Nessuna chiamata LLM in questa iterazione.

## File realizzati

- `AgenticExperimentRunner/Coordination/DelegationContracts.cs`: messaggi sulla
  pipe (`DelegationRequest`, `DelegationResponse`), esito (`DelegationStatus`:
  `completed`, `failed`, `rejected`), interfaccia `IDelegationHandler`, formato
  JSON su una riga con proprietà sconosciute rifiutate.
- `AgenticExperimentRunner/Coordination/CoordinationMcpServer.cs`: protocollo MCP
  dello strumento `delegate_task`, indipendente dal trasporto.
- `AgenticExperimentRunner/Coordination/DelegationPipeClient.cs`: lato relay della
  pipe; associa le risposte tramite `requestId` e conclude con errore tutte le
  attese aperte se il canale si interrompe.
- `AgenticExperimentRunner/Coordination/CoordinationRelay.cs`: ciclo stdio con
  gestione concorrente delle richieste.
- `AgenticExperimentRunner/Coordination/CoordinationHost.cs`: pipe dell'orchestratore,
  una sola istanza, `CurrentUserOnly`, deleghe sequenziali, nuova validazione dei
  messaggi ricevuti.
- `AgenticExperimentRunner/Coordination/CoordinationServerCommand.cs` e
  `Program.cs`: comando `coordination-server --pipe <nome> [--connect-timeout-ms <n>]`.
- Test in `AgenticExperimentRunner.Tests/Coordination/`: 50 casi.

### Scelte emerse durante la realizzazione

- **Codifica.** Il relay legge e scrive stdin/stdout come UTF-8 senza BOM in modo
  esplicito: la codifica predefinita della console di Windows altererebbe i
  caratteri non ASCII dei compiti. Verificato dal test di processo con `≥` e
  lettere accentate.
- **Relay concorrente, host sequenziale.** Se il relay elaborasse le righe una per
  volta, un `ping` della CLI resterebbe senza risposta per tutta la durata di un
  lavoratore. La sequenzialità delle deleghe è quindi imposta solo dall'host, che
  non legge la delega successiva prima di aver risposto alla precedente.
- **Disconnessione del relay.** Fine del file o pipe interrotta, anche durante la
  scrittura di una risposta, chiudono l'host normalmente: significano che la
  sessione del coordinatore è terminata. Messaggi non validi ed eccezioni del
  gestore fanno invece fallire `Completion`, perché indicano un guasto.
- **Testo dello strumento.** La descrizione di `delegate_task` è visibile al
  modello e fa quindi parte delle condizioni sperimentali; dovrà essere inclusa
  negli hash della sessione in A5. Dice soltanto che il lavoratore è un agente
  separato con gli stessi strumenti, che riceve solo il testo del compito, che la
  chiamata è bloccante e che è attivo un lavoratore alla volta.
- **Lunghezza massima del compito: 20 000 caratteri.** È un limite tecnico contro
  richieste patologiche, non un vincolo sperimentale: il budget di token resta il
  limite effettivo.

## Verifiche eseguite

Ambiente: Windows 11 Pro 10.0.26200, .NET SDK 10.0.403, branch
`feat/single-agent` dopo il merge di `main` (`f462912`).

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build  AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore --filter-class "*Coordination*"
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
```

Output osservato:

- `dotnet format`: codice di uscita 0;
- build: `Avvisi: 0`, `Errori: 0`;
- test di coordinamento: 50/50, ripetuti 5 volte consecutive con 50/50 ogni volta;
- suite completa: 238/238.

Corrispondenza con i criteri:

| Punto | Test |
| --- | --- |
| 1–4 | `CoordinationMcpServerTests` (protocollo, schema, 10 casi di parametri non validi, limite di lunghezza ai due estremi, metodo sconosciuto, JSON non valido, guasto del canale) |
| 3 | `Delegation_RoundTripsThroughPipe_PreservingNonAsciiText`, `RelayProcess_ServesMcpSessionAndDelegatesThroughHost` |
| 4 attraverso il canale | `InvalidToolCall_NeverReachesHost` |
| 5 | `Ping_IsAnsweredWhileDelegationIsInProgress` |
| 6 | `ConcurrentDelegations_AreExecutedOneAtATimeInArrivalOrder` |
| 7 | `Host_AcceptsOnlyOneConnection`, `Start_WithPipeNameAlreadyInUse_Fails` |
| 8 | `RelayProcess_WithoutHost_ExitsWithChannelFailureAndDiagnosticOnStandardError`, `RelayProcess_InvalidArguments_ExitWithUsageError`, `TryParse_*` |
| 9 | `HandlerFailure_ClosesChannelAndPendingCallGetsChannelError`, `HostDisposedDuringDelegation_PendingCallGetsChannelError` |
| messaggi non validi sulla pipe | `Host_InvalidWireMessage_FailsWithoutCallingHandler` (5 casi) |

**Controllo di sensibilità dei test di concorrenza.** Per verificare che i test
rilevino davvero i difetti che descrivono, il codice è stato alterato
temporaneamente in due modi, poi ripristinato:

1. relay sequenziale (ogni riga attesa prima della successiva):
   `CoordinationChannelTests` 2 falliti su 14;
2. host concorrente (deleghe avviate senza attendere la precedente):
   `ConcurrentDelegations_AreExecutedOneAtATimeInArrivalOrder` fallito
   (`Assert.Equal() Failure`).

Dopo il ripristino: test di coordinamento 50/50.

`git status` a fine iterazione mostra soltanto `Program.cs` modificato, le nuove
cartelle `Coordination/` di codice e test e questo documento: nessun artefatto
generato tra i file versionabili.

## Limiti e punti non verificati

- **Nessuna prova con la CLI reale.** Il relay è verificato con un client MCP
  scritto nei test, non con Claude CLI. Restano da osservare in A5: l'avvio del
  relay da `--mcp-config`, la dichiarazione dello strumento in `system/init` con
  due server MCP e, soprattutto, il **timeout delle chiamate di strumento** della
  CLI. Una delega dura quanto un'intera sessione del lavoratore: se la CLI
  interrompe le chiamate MCP lunghe, servirà una configurazione esplicita (per
  esempio la variabile `MCP_TOOL_TIMEOUT`, da verificare) e un test reale.
- **Annullamento lato orchestratore.** Se il coordinatore termina durante una
  delega, l'host se ne accorge solo quando prova a rispondere. In A5 il
  controllore dovrà terminare il lavoratore insieme al coordinatore, senza
  affidarsi alla pipe.
- **Confine della pipe.** `CurrentUserOnly` limita la pipe all'utente corrente,
  non ai soli processi della run: un altro processo dello stesso utente che ne
  conoscesse il nome casuale potrebbe connettersi prima del relay. L'agente non
  dispone di strumenti per aprire pipe; la proprietà va comunque verificata con
  le prove di accettazione dell'harness gerarchico.
- La gerarchia statica e quella adattiva non esistono ancora: A4 fornisce solo il
  canale.

## Prossimo passo proposto (A5)

Collegare il canale a una run gerarchica reale:

1. gestore delle deleghe che avvia ogni lavoratore come sessione CLI con il
   profilo di A3, sulla stessa area agente;
2. controllore unico di token e tempo su coordinatore e lavoratori, con
   terminazione di tutti i processi al superamento;
3. profilo del coordinatore con due server MCP e controllo di `system/init`;
4. record della run con identità e ruolo per agente ed eventi di delega (B5);
5. istruzioni architetturali del coordinatore e del lavoratore e politica di
   delega (numero massimo), da concordare;
6. una prova reale breve, dopo autorizzazione, per osservare il timeout delle
   chiamate MCP lunghe.
