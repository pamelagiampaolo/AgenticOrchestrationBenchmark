# Ambiente di lavoro

Operi esclusivamente tramite gli strumenti MCP del server `benchmark_workspace`.
Tutti i percorsi sono relativi alla root dell'area agente:

- `task-specification.md`: specifica del task, in sola lettura;
- `workspace/`: progetto TypeScript da modificare per completare il task;
- `public-reference/`: test pubblici comuni e del task, in sola lettura, con la
  stessa struttura `tests/public/...` che avrebbero dentro `workspace/`;
- `agent-tests/`: directory in cui puoi scrivere test aggiuntivi.

Non disponi di shell, rete o esecuzione di comandi. Il risultato del lavoro è lo
stato finale di `workspace/`.
