# P16 - Composizione effimera per Vitest pubblico

## Data e obiettivo

- Data: 2026-11-08.
- Obiettivo: collegare `run_public_tests` al layout A2/P9 senza dipendere dal
  merge del branch single-agent.

## Comportamento implementato

`EphemeralPublicTestRunner` riceve `agent-root`, una worker root esterna, Node e
Vitest fidati, audit e timeout. Per ogni chiamata:

1. calcola l'hash di `public-reference/`;
2. crea una directory di invocazione nuova;
3. copia `workspace/` in `project/`, rifiutando reparse point;
4. sovrappone `public-reference/`, rifiutando collisioni;
5. genera fuori da `project/` una configurazione che seleziona soltanto
   `tests/public/**/*.test.ts`;
6. esegue il comando controllato P15;
7. confronta nuovamente l'hash dei test pubblici;
8. elimina sempre la directory di invocazione.

Worker root e audit devono essere esterni alla root MCP. Il tool viene abilitato
dal launcher con:

- `MCP_PUBLIC_TEST_NODE`;
- `MCP_PUBLIC_TEST_VITEST`;
- `MCP_PUBLIC_TEST_WORKER_ROOT`;
- `MCP_COMMAND_AUDIT_PATH`;
- facoltativamente `MCP_PUBLIC_TEST_TIMEOUT_SECONDS`.

## Criteri ed evidenze

- composizione osservata da un processo Node sintetico: superata;
- eliminazione dell'area di invocazione: superata;
- persistenza dell'audit esterno: superata;
- esecuzione reale Vitest sui due file pubblici comuni della baseline: exit code
  0 e reporter JSON completato;
- test mirati P15/P16: 4/4;
- `dotnet format --verify-no-changes --no-restore`: exit code 0 sui progetti
  interessati;
- build: 0 avvisi, 0 errori;
- suite .NET completa: 70/70;
- `git diff --check`: nessun errore.

Il primo tentativo del test reale cercava il repository nella working directory
del test host; è stato corretto risalendo da `AppContext.BaseDirectory`. Il
secondo si aspettava i conteggi JSON su stdout, mentre Vitest 5 scrive il report
in un file e stampa soltanto il relativo avviso; l'asserzione finale verifica
exit code e completamento del reporter.

## Limiti

La configurazione e l'area effimera sono separate per evitare collisioni
accidentali, ma il processo host non è isolato e può tecnicamente raggiungere
altri percorsi. L'hash successivo rileva modifiche ai test pubblici, non accessi
in sola lettura o traffico di rete. È il rischio operativo già accettato in P15.

Il risultato MCP non espone ancora i conteggi letti dal file del reporter. Il
prossimo passo può acquisire e validare quel report prima della cancellazione,
restituendo soltanto conteggi e diagnostica ripulita dai percorsi assoluti.
