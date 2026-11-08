# A5 — Esecuzione della gerarchia adattiva

- **Data:** 8 novembre 2026
- **Fase:** realizzazione delle architetture sperimentali (dopo A4, canale di coordinamento)
- **Branch:** `feat/single-agent` (worktree `C:\repos\TesiAgenticAI-agent`)
- **Stato:** pianificato; le sezioni dei risultati sono completate a fine iterazione

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
