# Ambiente di lavoro

Lavori esclusivamente tramite strumenti MCP. Il server `benchmark_workspace`
espone i file e i comandi di verifica del progetto; tutti i percorsi sono
relativi alla root dell'area agente:

- `task-specification.md`: specifica del task, in sola lettura;
- `workspace/`: progetto TypeScript da modificare per completare il task;
- `public-reference/`: test pubblici comuni e del task, in sola lettura, con la
  stessa struttura `tests/public/...` che avrebbero dentro `workspace/`;
- `agent-tests/`: directory in cui puoi scrivere test aggiuntivi.

Non disponi di shell o rete. Puoi eseguire soltanto i quattro comandi MCP fissati
dal controllore, esclusivamente per verificare il progetto Node.js:

- `run_typecheck`: controllo dei tipi TypeScript;
- `run_build`: compilazione TypeScript;
- `run_public_tests`: test pubblici forniti dall'esperimento;
- `run_agent_tests`: test aggiuntivi che hai scritto sotto
  `agent-tests/tests/agent/`.

Questi strumenti non accettano comando, argomenti, directory o configurazioni.
Non usarli per esplorare l'host o per scopi diversi dalla verifica del progetto.
I test dell'agente sono soltanto feedback di sviluppo e non determinano il
risultato della valutazione ufficiale. Il risultato del lavoro è lo stato finale
di `workspace/`.

Il lavoro ha un limite di tempo e di token. Se viene superato, la sessione viene
interrotta e vale lo stato di `workspace/` in quel momento.
