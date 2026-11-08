# A3 — Sessione single-agent controllata

## Registro

- **Data:** 8 novembre 2026
- **Fase:** architettura single-agent, iterazione A3
- **Branch di lavoro:** `feat/single-agent`, allineato a `main` in `8bc2ca3`
  (strumenti MCP `delete_file` e `move_file`)
- **Stato:** pianificata; questa sezione è scritta prima delle modifiche al codice

## Contesto e decisioni

- L'utente ha approvato l'avvio di A3, comprese le chiamate LLM necessarie alla
  verifica, e ha deciso l'8 novembre 2026 che il **launcher operativo delle run è
  costruito da Claude in C#** nell'orchestratore. Il profilo di avvio riprende
  senza modifiche le opzioni già verificate da Codex in P1, P1.1 e P7; lo script
  `scripts/Invoke-McpReadOnlyPrototype.ps1` resta la prova di accettazione
  dell'isolamento e non viene modificato.
- B4 fissa le regole dei limiti (budget globale di token `totalTokens`, tempo
  `agentTaskDurationMs`, controllore esterno, stati di superamento) ma rinvia i
  valori numerici ai piloti: in A3 sono **parametri obbligatori** del comando.
- Lo stream della prova P7 (`%TEMP%\mcp-readonly-prototype-e2fe…\evidence\claude-stream.jsonl`,
  letto senza nuove chiamate LLM) mostra che:
  - ogni messaggio `assistant` riporta `message.usage`; più eventi con lo stesso
    `message.id` ripetono lo stesso uso e vanno contati una volta;
  - le somme per messaggio di `input_tokens` (20), `cache_read_input_tokens`
    (36542) e `cache_creation_input_tokens` (2732) coincidono con il `result`;
  - la somma degli `output_tokens` per messaggio (304) **non** coincide con il
    `result` (1355): nello stream l'output per messaggio è parziale;
  - quella prova non specificava `--model` e ha usato `claude-opus-5-5`, mentre B4
    richiede Sonnet con identificatore esatto.

## Obiettivo dell'iterazione

Eseguire su un'area preparata da `prepare-run` una sessione single-agent della
CLI con profilo isolato, sotto il controllo esterno di tempo e token previsto da
B4, registrando nella directory dei risultati tutto ciò che serve a B5:
configurazione effettiva con hash, stream grezzo, uso per chiamata, totali,
stato finale e integrità dei materiali protetti.

## Componenti coinvolti

- `AgenticExperimentRunner/SessionExecution/` (nuovo):
  - `StreamUsageTracker`: interpretazione pura dello stream (`system/init`, uso
    per messaggio deduplicato per `message.id`, output finale da
    `message_delta` se presente, evento `result`, chiamate agli strumenti);
  - `ClaudeLaunchProfile`: argomenti della CLI, impostazioni fidate e
    configurazione MCP;
  - `ChildProcessEnvironment`: rimozione delle variabili ereditate;
  - `SessionRecord`: documento finale della sessione;
  - `SingleAgentSessionRunner`: coordinamento, limiti e terminazione;
  - `RunSingleAgentCommand`: comando `run-single-agent`.
- `AgenticExperimentRunner/Instructions/`: `common-environment.md` (identico per
  le tre architetture) e `single-agent.md` (istruzione architetturale breve).
- `RunPreparation/PrepareRunCommand.cs`: scrive anche
  `<resultsDirectory>/prepared-run.json`, uguale allo stdout.
- `Program.cs`: instradamento del nuovo comando.
- Test in `AgenticExperimentRunner.Tests/SessionExecution/`.

Non vengono modificati il server MCP, gli script o i documenti `P*`.

## Comportamento atteso

### Comando

```text
AgenticExperimentRunner run-single-agent
    --prepared-run <assoluto>\prepared-run.json
    --claude <assoluto>\claude.exe --mcp-server <assoluto>\McpReadOnlyPrototype.exe
    --model <id esatto> --time-limit-seconds <n> --token-budget <n>
```

Exit `0` se la sessione è stata eseguita e registrata (qualunque sia lo stato
sperimentale, che è nel record), `2` argomenti non validi, `1` impossibile
avviare o registrare la sessione.

### Prima dell'avvio

1. Legge `prepared-run.json` e il manifest; la verifica d'integrità in ambito
   completo deve essere vuota, altrimenti la sessione non parte.
2. Il record della sessione non deve esistere (una sola sessione per area).
3. Scrive nella directory dei risultati, fuori da `agent-root`:
   `cli-settings.json`, `mcp-config.json` e `system-prompt-append.md`.
4. Registra versione della CLI (`claude --version`, non è una chiamata LLM) e
   SHA-256 di: specifica (prompt), istruzione comune, istruzione architetturale,
   testo aggiunto al system prompt, impostazioni, configurazione MCP ed
   eseguibile del server MCP.

### Profilo della CLI

Opzioni verificate in P1.1/P7, invariate: `-p`, `--output-format stream-json`,
`--verbose`, `--include-hook-events`, `--restricted`, `--strict-mcp-config`,
`--mcp-config`, `--settings`, `--setting-sources ""`, `--tools ""`,
`--allowedTools` con i soli strumenti MCP del server, `--disallowedTools` con gli
strumenti built-in, `--disable-slash-commands`, `--no-session-persistence`,
`--no-chrome`, `--permission-mode dontAsk`, `--permission-prompts none`.
Aggiunte motivate: `--model <id>` (B4), `--include-partial-messages` (output
finale per messaggio, da verificare) e `--append-system-prompt-file`.

- Strumenti MCP consentiti: `list_files`, `read_file`, `write_file`,
  `delete_file`, `move_file` del server `benchmark_workspace`; nessun
  `run_public_suite` (il server viene avviato senza runner).
- Server MCP: `<mcp-server> <agentRoot>` con
  `MCP_WRITABLE_DIRECTORIES=workspace;agent-tests` (dal JSON di `prepare-run`).
- Impostazioni fidate: `disableAllHooks: true`, variabili
  `CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS=1` ed
  `ENABLE_CLAUDEAI_MCP_SERVERS=false`.
- Directory di lavoro: `session/`. Prompt: contenuto esatto di
  `agent-root/task-specification.md`, inviato su stdin. System prompt aggiunto:
  istruzione comune + riga vuota + istruzione single-agent.
- Ambiente del processo: quello dell'orchestratore senza le variabili con
  prefisso `CLAUDE` o `ANTHROPIC`; i soli nomi rimossi vengono registrati.

### Controllo durante la sessione

- **Configurazione effettiva:** al primo `system/init` si verificano modello
  uguale a quello richiesto, strumenti esattamente uguali ai cinque consentiti,
  un solo server MCP `benchmark_workspace` connesso, nessun agente, skill o
  slash command. Una differenza termina subito la sessione con stato
  `configuration-rejected`.
- **Token:** `tokensObserved` = somma, sui messaggi distinti, di input, cache read,
  cache creation e output (finale se disponibile, altrimenti il massimo
  osservato). Appena `tokensObserved ≥ tokenBudget` il processo viene terminato:
  `token-limit-exceeded`.
- **Tempo:** cronometro monotono da subito prima dell'avvio del processo; alla
  scadenza il processo viene terminato: `time-limit-exceeded`. Entrambi nello
  stesso controllo: `resource-limits-exceeded`.
- La terminazione chiude l'intero albero di processi (CLI e server MCP). Il
  periodo tecnico di arresto è nullo: non esiste un segnale di chiusura
  ordinata verificato per la CLI su Windows.

### Dopo la sessione

- Stato: `completed` (evento `result` con `subtype` `success`), `cli-failed`
  (exit diverso da zero o `result` di errore, con `subtype` registrato),
  `usage-measurement-error` (nessun limite superato ma `result` assente) o uno
  degli stati di limite/configurazione.
- Totali autorevoli: `usage` del `result` quando presente (B4); altrimenti i
  valori dello stream, marcati come incompleti. Sono registrati sia gli uni sia
  gli altri e la loro coerenza.
- Verifica d'integrità in ambito protetto: le differenze sono registrate.
- `session-record.json` (schema 1) scritto con `CreateNew`, insieme a
  `stream.jsonl` (stdout grezzo) e `stderr.txt`.

## Criteri oggettivi di accettazione

1. Test unitari del tracker sullo schema osservato: deduplicazione per
   `message.id`, output finale da `message_delta`, `result`, chiamate e errori
   degli strumenti, righe non JSON contate e non fatali.
2. Test del profilo: argomenti esatti, nessun prompt sulla riga di comando,
   configurazione MCP e impostazioni come previsto; test della pulizia
   dell'ambiente.
3. Test di integrazione con una CLI finta (script) che emette stream
   predefiniti: completamento, superamento dei token, superamento del tempo
   (terminazione entro un margine misurato), configurazione rifiutata, `result`
   assente, area alterata prima dell'avvio (sessione non avviata), record già
   esistente, alterazione di `public-reference` durante la sessione registrata.
4. `prepare-run` scrive `prepared-run.json` identico allo stdout.
5. Formattazione, build con 0 avvisi, suite completa superata.
6. **Prova reale (chiamata LLM):** una sessione T1 con Sonnet su un'area appena
   preparata, con limiti di prova dichiarati prima dell'esecuzione. Si verifica
   che il controllo `init` passi, che il record sia completo e che la somma
   degli output finali per messaggio coincida con il `result` (se no, il limite
   viene documentato). La prova non è un pilota B4 e non calibra i limiti.

## Fuori perimetro

- Esecuzione di typecheck e test da parte dell'agente (worker confinato, Codex):
  nella prova reale l'agente può solo operare sui file.
- Valutazione con i test riservati e calcolo della correttezza.
- Valori definitivi dei limiti (piloti B4) e stima dei costi.
- Architetture multi-agente.
