# A5 — Esecuzione della gerarchia adattiva

- **Data:** 8 novembre 2026
- **Fase:** realizzazione delle architetture sperimentali (dopo A4, canale di coordinamento)
- **Branch:** `feat/single-agent` (worktree `C:\repos\AgenticOrchestrationBenchmark-agent`)
- **Stato:** completata; test automatici e due prove reali eseguiti (risultati sotto)

## Decisioni dell'utente (8 novembre 2026)

1. **Numero massimo di deleghe:** tetto tecnico alto, parametro della run
   (`--max-delegations`, valore proposto 10), registrato nel record. Oltre il tetto
   la delega viene rifiutata (`rejected`) e il coordinatore può proseguire da solo;
   il limite effettivo resta il budget globale.
2. **Istruzioni di coordinatore e lavoratore:** bozze scritte da Claude, neutre
   (solo ruolo e funzionamento della delega, nessun suggerimento sulla soluzione),
   da approvare dall'utente prima di qualunque pilota.
3. **Prova reale:** autorizzata a fine A5. L'utente ricorda che il server MCP non
   offre ancora l'esecuzione dei test: la prova verifica il meccanismo, non la
   capacità dell'agente di risolvere il task.

## Evidenza raccolta prima del progetto

Documentazione ufficiale delle variabili d'ambiente di Claude Code
(<https://code.claude.com/docs/en/env-vars>, consultata l'8 novembre 2026):

- `CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS`: dopo 120000 ms (predefinito) una chiamata
  MCP ancora in corso viene spostata in un task in background; `0` disattiva lo
  spostamento. Vale dalla versione 2.1.212; la CLI usata è la 2.1.312.
- `CLAUDE_CODE_MCP_TOOL_IDLE_TIMEOUT`: per i server stdio una chiamata senza
  risposta né notifiche di avanzamento viene interrotta dopo 1800000 ms
  (predefinito). Un `timeout` per server di almeno 1000 ms alza la finestra di
  inattività di quel server almeno a quel valore.
- `MCP_TOOL_TIMEOUT`: timeout complessivo predefinito di 100000000 ms (circa 28 ore);
  il `timeout` per server lo sostituisce per quel server.

Una delega dura quanto la sessione di un lavoratore. Senza contromisure il
coordinatore riceverebbe dopo due minuti un task in background invece dell'esito,
e oltre trenta minuti la chiamata verrebbe interrotta. Contromisure previste:

- `CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS=0` nelle impostazioni fidate, uguali per
  tutte le architetture (per il single-agent non cambia nulla: nessuna sua
  chiamata MCP dura minuti);
- `timeout` del server `benchmark_coordination` pari al limite di tempo della run.

Entrambe sono ipotesi da documentazione finché la prova reale non le conferma.

## Obiettivo

Eseguire una run della gerarchia adattiva: un coordinatore che può delegare,
tramite il canale di A4, lavoratori avviati dall'orchestratore come sessioni CLI
separate, con un unico controllore di token e tempo su tutti i processi e un record
della run con identità e ruolo di ogni agente e gli eventi di delega (B5).

## Componenti coinvolti

- `SessionExecution/RunLimitController.cs` (nuovo): budget globale di token e
  limite di tempo condivisi da tutti gli agenti di una run; segnala la fermata a
  tutti i processi.
- `SessionExecution/AgentProcess.cs` (nuovo): esecuzione di una singola sessione
  CLI (avvio, stdin, lettura dello stream, controllo di `system/init`,
  terminazione dell'albero di processi). Estratto da `SingleAgentSessionRunner`,
  che viene riscritto sopra questi due componenti senza cambiare comportamento.
- `SessionExecution/ClaudeLaunchProfile.cs`: strumenti consentiti e server attesi
  parametrici; configurazione MCP del coordinatore con due server;
  `CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS=0`.
- `SessionExecution/StreamUsageTracker.cs`: testo finale del `result`, restituito
  al coordinatore come esito della delega.
- `Coordination/WorkerDelegationHandler.cs`, `Coordination/HierarchicalRunRunner.cs`,
  `Coordination/HierarchicalRunRecord.cs`, `Coordination/RunAdaptiveHierarchyCommand.cs`
  (nuovi), `Program.cs`.
- `Instructions/coordinator.md`, `Instructions/worker.md` (bozze nuove).
- Test in `AgenticExperimentRunner.Tests/` con una CLI finta capace di delegare.

## Comportamento atteso

1. **Comando:** `run-adaptive-hierarchy --prepared-run --claude --mcp-server
   --model --time-limit-seconds --token-budget --max-delegations`, stesse
   convenzioni di `run-single-agent` (stdout JSON, exit 0/1/2). Il relay è lo
   stesso eseguibile dell'orchestratore.
2. **Precondizioni:** area integra rispetto al manifest, `session/` vuota,
   directory dei risultati con i soli `manifest.json` e `prepared-run.json`.
3. **Coordinatore:** prompt = specifica del task (come il single-agent);
   system prompt = istruzione comune + `coordinator.md`; strumenti = cinque del
   workspace + `mcp__benchmark_coordination__delegate_task`; `system/init` deve
   dichiarare esattamente questi strumenti e i due server connessi.
4. **Lavoratore:** prompt = testo del compito; system prompt = istruzione comune +
   `worker.md`; solo i cinque strumenti del workspace; stessa area agente e stessa
   directory di sessione; nessuna delega possibile.
5. **Deleghe:** una alla volta; oltre `maxDelegations` o a limiti già raggiunti la
   delega è `rejected` senza avviare processi; lavoratore concluso con successo →
   `completed` con il suo riepilogo finale; altrimenti `failed` con il motivo.
6. **Limiti:** token di tutti gli agenti sommati in tempo reale; al superamento
   del budget o del tempo, o a una configurazione non conforme di qualunque agente,
   tutti i processi vengono terminati.
7. **Stato della run:** `configuration-rejected` se un agente ha una configurazione
   non conforme; poi limiti di tempo/token (anche con il totale autorevole oltre il
   budget); poi `usage-measurement-error` se un agente non terminato dal
   controllore non ha l'uso autorevole; infine l'esito del coordinatore
   (`completed` o `cli-failed`).
8. **Registrazione** in `<resultsRoot>/<runId>/`: `run-record.json` (totali, uso
   per agente e ruolo, eventi di delega con istanti, conteggi richieste/avviate/
   completate/fallite/rifiutate, hash di tutte le condizioni incluse le istruzioni
   e la descrizione dello strumento di delega) e `agents/<agentId>/` con stream,
   stderr, configurazione MCP, system prompt e prompt del lavoratore.

## Criteri di accettazione

- Test esistenti del single-agent invariati e verdi dopo l'estrazione dei
  componenti comuni.
- Test unitari del controllore dei limiti.
- Test di integrazione con CLI finta che delega attraverso il relay reale:
  run completata con una delega; delega rifiutata oltre il tetto; lavoratore
  fallito; budget di token superato dalla somma di coordinatore e lavoratore;
  limite di tempo durante una delega; configurazione non conforme di un
  lavoratore; area con risultati già presenti.
- Prova reale T1 con Sonnet, budget di prova dichiarato prima nel documento,
  e prova reale di una delega più lunga di due minuti con gestore ritardato.
- `dotnet format --verify-no-changes`, build senza avvisi, suite completa verde.

## Parametri delle prove reali (dichiarati prima dell'esecuzione)

Valori di prova, non i limiti B4 (che si fissano dopo i piloti). Modello
`claude-sonnet-5-5` per tutti gli agenti, CLI `C:\Users\Utente\.local\bin\claude.exe`,
server MCP `McpReadOnlyPrototype.exe` e relay `AgenticExperimentRunner.exe` dalla
build del commit indicato nei risultati.

1. **Prova R1 — delega lunga.** Test esplicito
   `RealCliHierarchyProbes.LongDelegation_IsAwaitedByRealCoordinator`: baseline
   sintetica in un repository temporaneo, specifica di prova che chiede una sola
   delega; l'orchestratore ritarda la delega di 150 s (punto d'aggancio
   `DelegationDecorator`, solo per prove) e poi avvia il lavoratore reale. Limiti:
   15 minuti, 300 000 token, al massimo 10 deleghe. Criterio: delega `completed`
   con durata ≥ 150 s e run `completed`; se la CLI sposta la chiamata in background
   o la interrompe, la prova fallisce.
2. **Prova R2 — T1 reale.** `prepare-run` su T1 dal commit del branch, poi
   `run-adaptive-hierarchy` con 15 minuti, 1 000 000 token, `--max-delegations 10`.
   Osservazione, non criterio: se e come il coordinatore delega. L'agente non può
   eseguire i test (manca lo strumento): la qualità della soluzione non viene
   valutata.

## Realizzazione

Commit: `a2656fc` (implementazione), `497bdce`, `ec48d97`, `6889525` (prova R1).

- **Estrazione senza cambiamenti di comportamento.** `AgentProcess` (una sessione
  CLI), `RunLimitController` (budget e tempo della run) e `RunInputs` (lettura dei
  documenti della run, istruzioni, hash) sono stati estratti da
  `SingleAgentSessionRunner`, sceso da 548 a 306 righe. I 21 test del single-agent
  sono passati senza modifiche dopo ogni passo dell'estrazione.
- **Controllore unico.** Ogni agente riporta il proprio uso cumulativo; il
  controllore somma gli agenti e, al budget, al tempo o a una configurazione non
  conforme di qualunque agente, annulla `Stopping`: ogni `AgentProcess` termina il
  proprio albero di processi. L'annullamento avviene in modo sincrono fuori dal
  lock, così nessun processo continua dopo che la fermata è decisa.
- **Lavoratore terminato a fine coordinatore.** Se il coordinatore termina mentre
  un lavoratore è attivo (per esempio se la CLI spostasse la chiamata in
  background), la chiusura dell'host termina il lavoratore e la delega è
  registrata `failed` con motivo esplicito.
- **Concorrenza misurata.** `maxConcurrentWorkers` è calcolato dalle sovrapposizioni
  reali degli intervalli dei lavoratori, non assunto dalla regola.
- **Testo finale del `result`.** Aggiunto a `SessionResult` (`resultText`); è il
  riepilogo restituito al coordinatore. Il record single-agent acquisisce il campo.
- **Impostazioni comuni.** `CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS=0` è nelle
  impostazioni fidate di tutte le architetture: l'hash di `cli-settings.json` del
  single-agent cambia rispetto alla run di A3.
- **Punto d'aggancio per le prove.** `HierarchicalRunRunner.DelegationDecorator` è
  `internal`, non è mai impostato dal comando ed è usato solo dalla prova R1.
- **Istruzioni (bozze da approvare):** `Instructions/coordinator.md` e
  `Instructions/worker.md`. Non indicano il numero massimo di deleghe: il
  coordinatore lo scopre da un eventuale rifiuto.

## Verifiche eseguite

```powershell
dotnet format AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --verify-no-changes --no-restore
dotnet build  AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-restore
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore
dotnet test   AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build --no-restore --filter-class "*HierarchicalRunRunnerTests"
```

- `dotnet format`: codice di uscita 0 (dopo `dotnet format whitespace` su due righe
  di un test);
- build: `Avvisi: 0`, `Errori: 0`;
- suite completa: 271 superati, 1 ignorato (la prova reale esplicita), 0 falliti;
- test della gerarchia con CLI finta, relay e pipe reali: 9/9, ripetuti 3 volte;
- controllo di sensibilità: con la segnalazione dell'uso al controllore rimossa,
  `RunAsync_BudgetReachedBySumOfAgents_StopsWorkerAndCoordinatorPromptly` fallisce;
  ripristinato il codice, la suite torna verde.

| Criterio | Test |
| --- | --- |
| una delega completata, totali, prompt, configurazioni, hash | `RunAsync_OneDelegation_CompletesAndRecordsAgentsDelegationAndTotals` |
| rifiuto oltre il tetto | `RunAsync_DelegationsBeyondMaximum_AreRejectedWithoutStartingWorkers` |
| lavoratore fallito, misura incompleta | `RunAsync_WorkerWithoutResult_DelegationFailsAndMeasurementIsIncomplete` |
| budget superato dalla somma degli agenti | `RunAsync_BudgetReachedBySumOfAgents_StopsWorkerAndCoordinatorPromptly` |
| tempo scaduto durante una delega | `RunAsync_TimeLimitDuringDelegation_StopsAllAgents` |
| configurazione non conforme di un lavoratore / del coordinatore | `RunAsync_WorkerWithUnexpectedConfiguration_RejectsRunAndStopsCoordinator`, `RunAsync_CoordinatorWithoutDelegateTool_IsRejected` |
| risultati già presenti | `RunAsync_ResultsDirectoryAlreadyUsed_IsRefusedWithoutStarting` |
| controllore, profilo, comando | `RunLimitControllerTests`, `CoordinatorLaunchProfileTests`, `RunAdaptiveHierarchyCommandTests`, `MaxConcurrent_*` |

## Risultati delle prove reali

CLI `2.1.312 (Claude Code)`, modello dichiarato in `system/init`:
`claude-sonnet-5-5` per tutti gli agenti. Nessuna violazione di configurazione.

### R1 — delega lunga

Tre esecuzioni, tutte con lo stesso comportamento della CLI; le prime due sono
fallite per errori del test, non dell'harness:

| Esecuzione | Commit | Esito del test | Causa |
| --- | --- | --- | --- |
| `a5-probe-long-delegation-20261108T211446Z` | `497bdce` | fallito | gli istanti dell'evento sono presi dal gestore interno, dopo il ritardo: misurava 3,6 s |
| `a5-probe-long-delegation-20261108T211844Z` | `ec48d97` | fallito | confronto testuale con il JSON serializzato da .NET (backtick come `\u0060`) |
| `a5-probe-long-delegation-20261108T212205Z` | `6889525` | **superato** | — |

Prima della terza esecuzione la nuova verifica è stata applicata alle evidenze delle
prime due: in entrambe un solo `tool_result`, identico al riepilogo del lavoratore.
Un primo lancio del comando di test con `--diagnostic-output-directory` era stato
rifiutato dalla piattaforma di test senza eseguire alcun test né chiamate LLM.

Esecuzione superata (`C:\AgenticAIResults\a5-probe-long-delegation-20261108T212205Z\`):

- run `completed`, `agentTaskDurationMs` 157885;
- una delega `completed`; durata vista dal relay ≥ 150 s (asserzione superata);
  coordinatore 157879 ms, lavoratore 3647 ms (una `read_file`);
- il coordinatore ha ricevuto come `tool_result` esattamente il riepilogo del
  lavoratore e lo ha riportato; nello stream "background" compare solo nelle
  statistiche dei subagenti, tutte a zero;
- token: coordinatore 8853, lavoratore 7564, totale 16432; stream e `result`
  coincidono per entrambi gli agenti.

### R2 — T1 reale

Run `20261108T212457Z-2ea4895d` (commit `6889525`), risultati in
`C:\AgenticAIResults\20261108T212457Z-2ea4895d\`:

- stato `completed`, `agentTaskDurationMs` 25030, 4 turni;
- **nessuna delega**: il coordinatore ha usato `list_files`, `read_file` e
  `write_file` su `workspace/src/index.ts` e ha concluso da solo;
- token 35174 (input 8, cache read 21854, cache creation 8777, output 4535),
  stream uguale al `result`; costo riportato dalla CLI 0,0827 USD;
- materiali protetti invariati; nel riepilogo l'agente dichiara di non aver potuto
  eseguire i test.

Interpretazione (non misura): su un task semplice come T1 il coordinatore adattivo
può scegliere di non delegare; per B5 zero deleghe è un valore significativo della
condizione adattiva, non un errore. Una sola run non dice nulla sulla frequenza.

## Limiti e punti non verificati

- **Contromisure non isolate.** R1 mostra che, con `CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS=0`
  e il `timeout` per server, una delega di circa 154 s viene attesa. Non è stata
  eseguita una prova di controllo senza queste impostazioni: non è dimostrato che
  senza di esse la chiamata verrebbe spostata in background, né che la finestra di
  inattività di 30 minuti venga davvero alzata (nessuna delega ha superato 30 minuti).
- **Terminazione forzata con la CLI reale** non provata: budget, tempo e
  configurazione non conforme sono verificati solo con la CLI finta.
- **Istruzioni di coordinatore e lavoratore**: bozze, da approvare dall'utente;
  ogni modifica cambia gli hash. Anche la descrizione di `delegate_task` va rivista.
- **Esecuzione dei test** ancora assente: nessuna run vale come pilota B4.
- **Confine della pipe** (A4): limitata all'utente corrente, non ai processi della
  run.
- **Gerarchia statica** non realizzata: userà `HierarchicalRunRunner` con un'altra
  politica di delega e altre istruzioni (lavoro di Codex sul livello condiviso).

## Prossimo passo proposto

- revisione e approvazione delle istruzioni (single-agent, coordinatore,
  lavoratore) e della descrizione di `delegate_task`;
- integrazione dello strumento di test di Codex appena disponibile, che sblocca i
  piloti B4;
- eventuale prova di controllo senza le contromisure MCP, se si vuole dimostrarne la
  necessità.
