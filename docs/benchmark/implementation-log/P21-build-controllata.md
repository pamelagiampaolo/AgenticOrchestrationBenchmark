# P21 - Build TypeScript controllata

## Data e obiettivo

- Data: 2026-11-08.
- Obiettivo: aggiungere `run_build` senza shell, script npm o artefatti nella
  workspace assegnata all'agente.

## Comportamento

Il comando riusa il motore effimero P20, ma omette `--noEmit` e registra
`operation: build`. Il comando effettivo è fissato dal controllore:

```text
<typescript>/tsc.js --project <worker>/project/tsconfig.json
```

La build genera `dist/` soltanto nella copia effimera, che viene eliminata al
termine. La workspace originale resta invariata. Il launcher abilita il tool con
`MCP_BUILD_NODE`, `MCP_BUILD_TSC`, `MCP_BUILD_WORKER_ROOT`,
`MCP_COMMAND_AUDIT_PATH` e, facoltativamente,
`MCP_BUILD_TIMEOUT_SECONDS`.

## Evidenze

- build reale della baseline: `completed`, exit code 0;
- `dist/` assente nella workspace originale dopo la build;
- parametro MCP arbitrario: rifiutato con `-32602`;
- inventario MCP: otto strumenti, incluso `run_build`;
- test pubblici: 27/27;
- typecheck: exit code 0;
- audit condiviso: tre record per test, typecheck e build;
- worker root: nessun residuo;
- stderr server: vuoto.

## Limiti

La build esegue il compilatore sulla configurazione modificabile presente nella
workspace. Comando, toolchain, directory e timeout restano fissati. Il processo
host non è isolato, secondo il rischio operativo accettato in P15.

Rimane da implementare `run_agent_tests` per completare i quattro comandi
previsti dal contratto P9. Deve usare esclusivamente
`agent-tests/tests/agent/**` e non deve contribuire alla valutazione ufficiale.
