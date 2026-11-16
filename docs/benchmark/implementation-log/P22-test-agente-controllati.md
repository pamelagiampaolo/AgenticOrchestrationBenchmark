# P22 — Test dell'agente controllati e separati

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** completare la superficie di sviluppo P9 con `run_agent_tests`,
  senza esporre shell, argomenti o test riservati.
- **Componenti:** server MCP, composizione Vitest effimera, test automatici e
  probe end-to-end.
- **Comportamento atteso:** eseguire soltanto i file provenienti da
  `agent-tests/tests/agent/**`, restituire conteggi distinti e cancellare la
  composizione dopo ogni invocazione.
- **Accettazione:** inventario MCP esatto, parametri arbitrari rifiutati,
  operazione di audit `agent-tests`, conteggi validati e nessun residuo worker.

## Implementazione

`EphemeralAgentTestRunner` riusa la pipeline effimera Vitest già adottata per i
test pubblici, con configurazione generata dal controllore e include fisso
`tests/agent/**/*.test.ts`. Qualunque contenuto di `agent-tests/` esterno a
`tests/agent/` causa il rifiuto dell'invocazione. La configurazione esclude
esplicitamente `tests/public/**` e `tests/reserved/**`.

Il tool `run_agent_tests` non accetta proprietà JSON. Eseguibile Node, entry
point Vitest, worker root, timeout e audit sono configurati soltanto tramite il
launcher. Il risultato usa l'operazione `agent-tests`: è feedback di sviluppo e
non contribuisce alla valutazione ufficiale.

Variabili richieste quando il tool è abilitato:

- `MCP_AGENT_TEST_NODE`;
- `MCP_AGENT_TEST_VITEST`;
- `MCP_AGENT_TEST_WORKER_ROOT`;
- `MCP_COMMAND_AUDIT_PATH`;
- facoltativamente `MCP_AGENT_TEST_TIMEOUT_SECONDS`, predefinito 300.

## Verifiche osservate

| Verifica | Esito |
| --- | --- |
| Build progetto test | 0 avvisi, 0 errori |
| Test mirati runner Vitest | 7/7 superati |
| Probe MCP end-to-end | inventario esatto di 9 strumenti |
| Parametro arbitrario `run_agent_tests` | rifiutato con `-32602` |
| Test agente sintetico | `completed`, exit 0, 1/1 |
| Suite pubblica nello stesso probe | `completed`, 27/27 |
| Typecheck e build nello stesso probe | entrambi exit 0 |
| Audit | 4 record, uno per ogni comando eseguito |
| Pulizia | 0 elementi residui nella worker root |
| Stderr server | vuoto |

Il primo tentativo di build globale è stato rivolto a un file solution non
presente. Un successivo tentativo di compilare due progetti in parallelo ha
prodotto `CS2012` perché entrambi scrivevano lo stesso output MCP. La verifica è
stata quindi ripetuta correttamente sul progetto test, che include le dipendenze,
in modo seriale e si è conclusa senza errori.

## Limiti

Questi controlli riducono la superficie operativa ma non costituiscono
isolamento del processo Node. Come deciso per il calendario sperimentale,
l'esecuzione rimane governata da comando fisso, prompt, audit e copie effimere;
non è una prova che codice ostile non possa leggere altre risorse accessibili
all'identità del processo.

## Lavoro successivo

Integrare la superficie MCP completa nel launcher ufficiale del single-agent e
verificare una run completa su un'area preparata da A2, mantenendo separati
feedback di sviluppo e valutazione riservata.
