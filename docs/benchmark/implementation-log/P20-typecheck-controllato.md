# P20 - Typecheck controllato

## Data e obiettivo

- Data: 2026-11-08.
- Obiettivo: aggiungere `run_typecheck` senza shell, script npm o parametri
  controllabili dall'agente.

## Comportamento

Il comando viene esposto soltanto quando il launcher configura:

- `MCP_TYPECHECK_NODE`;
- `MCP_TYPECHECK_TSC`;
- `MCP_TYPECHECK_WORKER_ROOT`;
- `MCP_COMMAND_AUDIT_PATH`;
- facoltativamente `MCP_TYPECHECK_TIMEOUT_SECONDS`.

Per ogni invocazione il server copia `agent-root/workspace` in una directory
effimera, rifiuta reparse point e invoca direttamente la copia fidata di Node
con gli argomenti fissi:

```text
<typescript>/tsc.js --noEmit --project <worker>/project/tsconfig.json
```

Il processo ha timeout e terminazione dell'albero dei figli. Stato, exit code,
durata e comando vengono aggiunti all'audit JSONL condiviso. La diagnostica è
limitata e ripulita dai percorsi noti; l'area effimera viene sempre eliminata.

## Criteri ed evidenze

- typecheck reale della baseline: `completed`, exit code 0;
- chiamata MCP con parametro arbitrario: errore `-32602`;
- inventario MCP: sette strumenti, incluso `run_typecheck`;
- P18 esteso: test pubblici 27/27 e typecheck completato;
- audit: due record, uno per ciascun comando realmente eseguito;
- worker root: nessun residuo;
- stderr server: vuoto.

Il primo test senza `package.json` ha prodotto TS1287 perché `module: nodenext`
ha classificato la sorgente come CommonJS. La fixture finale replica il layout
A2 includendo `package.json`; il problema non era nel runner.

## Limiti

`tsconfig.json` appartiene alla workspace modificabile e può quindi cambiare
quali sorgenti vengono analizzate. Eseguibile, entry point, opzione `--noEmit`,
working directory e timeout restano fissati dal controllore. Come per P15, il
processo host non è isolato e l'audit non equivale a prevenzione degli accessi.

Il prossimo comando previsto dal contratto P9 è `run_build`; deve essere una
nuova iterazione separata.
