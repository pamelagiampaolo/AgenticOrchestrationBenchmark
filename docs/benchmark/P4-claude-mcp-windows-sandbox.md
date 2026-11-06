# P4 — Percorso Claude CLI–MCP–Windows Sandbox

## Stato e perimetro

Iterazione eseguita il 6 novembre 2026. Verifica una singola sessione Claude CLI
che usa il solo server MCP controllato P3 per avviare la suite pubblica P2.2 in
Windows Sandbox. Scrittura file tramite MCP, orchestrazione, valutatore, test
riservati e congelamento della baseline restano esclusi.

## Obiettivo, ambito e comportamento atteso

L'obiettivo è osservare l'intero percorso Claude CLI → MCP → Windows Sandbox con
evidenze tecniche. La sessione deve esporre soltanto i tre strumenti MCP previsti,
invocare una volta `run_public_suite` con `{}`, ricevere il risultato 27/27 e
lasciare zero istanze attive. Il riepilogo testuale di Claude non è usato come
prova.

## Configurazione controllata

[`Invoke-ClaudeMcpSandboxProbe.ps1`](../../scripts/Invoke-ClaudeMcpSandboxProbe.ps1)
riusa la combinazione P1.1:

- `--restricted` e `--strict-mcp-config`;
- un solo file MCP fidato con server `readonly`;
- `--setting-sources ''` e file `--settings` fidato;
- `--tools ''`;
- allowlist dei soli `list_files`, `read_file`, `run_public_suite` MCP;
- denylist di Bash, PowerShell, Read, Write, Edit, Glob, Grep, web, Task, Agent
  e Skill;
- slash command e persistenza sessione disabilitati;
- `dontAsk` e nessun permission prompt;
- hook disabilitati e agenti built-in rimossi dalle impostazioni fidate.

Il server pubblicato, configurazione e impostazioni sono in una directory
temporanea fidata esterna alla workspace Claude. Il server riceve all'avvio la
root pubblica e il percorso fisso del runner P2.2. L'autenticazione Claude
esistente viene intenzionalmente conservata.

Il prompt chiede una sola invocazione di `run_public_suite` con oggetto vuoto e
vieta l'uso degli altri strumenti. Il criterio di successo usa esclusivamente
`system/init`, `tool_use`, `tool_result`, osservazioni `wsb` e il report host
P2.2.

## Evidenze osservate

Claude CLI osservata: `2.1.312`.

L'evento `system/init` riportava:

- strumenti: `mcp__readonly__list_files`, `mcp__readonly__read_file`,
  `mcp__readonly__run_public_suite`;
- un solo MCP, `readonly`, `connected`, sorgente `dynamic`;
- `agents`, `skills` e `slash_commands` vuoti;
- tre plugin built-in dichiarati, come già osservato in P1.1.

Lo stream conteneva esattamente:

- un `tool_use` per `mcp__readonly__run_public_suite`;
- input con zero proprietà;
- nessun `tool_use` per `list_files`, `read_file` o altro;
- un `tool_result` non in errore con build, typecheck e test a exit code zero,
  27 test totali, 27 superati, zero falliti/pending e `IsSuccessful: true`.

Durante l'attesa il PC ha osservato una nuova istanza con `wsb list --raw`. È
comparsa una sola nuova directory di evidenze P2.2; il relativo report host
confermava 27/27, integrità riuscita e `sandboxStopped: true`. Al termine la CLI
`wsb` riportava zero istanze attive.

Nessun evento hook e nessuna delegazione sono stati osservati. Queste assenze
riguardano la singola sessione, non ogni comportamento interno possibile.

## Esito per criterio

| # | Stato | Evidenza sintetica |
| --- | --- | --- |
| 1 | **Superato con limite** | Versione e flag registrati senza segreti; le sorgenti effettive non sono esposte da `system/init`. |
| 2 | **Superato** | Inventario esatto di tre strumenti e unico MCP connesso. |
| 3 | **Superato con limite** | Nessun agente, skill, slash command, hook o tool nativo osservato; restano dichiarati tre plugin built-in. |
| 4 | **Superato** | Un solo `tool_use`, nome atteso e input `{}`. |
| 5 | **Superato** | `tool_result` non in errore con 27/27 e successo strutturato. |
| 6 | **Superato** | Nuova Sandbox osservata dal PC durante la chiamata. |
| 7 | **Superato** | Report host autorevole P2.2 con 27/27 e arresto riuscito. |
| 8 | **Superato** | Zero istanze attive al termine. |
| 9 | **Superato** | Nessun altro strumento invocato nello stream. |
| 10 | **Superato** | Evidenze sanificate, parsing, diff e stato finale verificati. |

## Problema incontrato

La prima sessione ha completato correttamente l'intero percorso, ma il controllo
locale del risultato era negativo perché il client serializzava nuovamente il
JSON testuale dell'evento `tool_result`, escapandone le virgolette. Lo stream e il
report host mostravano già 27/27 e zero Sandbox residue. Il client è stato
corretto per analizzare direttamente il payload JSON e l'intera sessione è stata
ripetuta con exit code zero.

## Limiti

`system/init` non espone l'elenco delle sorgenti di configurazione effettivamente
applicate; la prova riusa i controlli e i limiti P1.1 senza ampliarli. I tre
plugin built-in restano dichiarati: non hanno aggiunto superfici osservabili in
questa sessione, ma non è dimostrata la loro completa inattività interna.

La prova conserva autenticazione e stato account esistenti e vale soltanto per
la versione CLI e la singola esecuzione osservate. Non certifica scrittura,
concorrenza, cancellazione, orchestrazione, valutazione riservata o l'intero
sistema sperimentale. La sintesi stabile è in
[`evidence/P4-claude-mcp-sandbox-summary.json`](evidence/P4-claude-mcp-sandbox-summary.json).
