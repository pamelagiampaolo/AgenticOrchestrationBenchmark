# P43 — Prova del confinamento dei test creati dall'agente

## Obiettivo e criterio

- **Data:** 18 novembre 2026.
- **Fase:** accettazione dell'isolamento prima delle run ufficiali.
- **Stato:** requisito di confinamento verso file esterni **non superato** per
  la via `run_agent_tests` osservata in questa prova.
- **Chiamate LLM:** nessuna; nessun test riservato reale letto o esposto.

Obiettivo: verificare se codice eseguito tramite lo strumento MCP
`run_agent_tests` possa leggere un file sintetico esterno alla root MCP.
Componenti: eseguibile MCP congelato P31, toolchain P33, area agente temporanea,
worker e sentinella esterna temporanea. Criterio di accettazione: il codice
eseguito tramite lo strumento non deve poter leggere la sentinella esterna;
una lettura riuscita costituisce fallimento del requisito provato.

## Prova e risultato

`scripts/current/Probe-AgentTestConfinement.cjs` crea un'area temporanea e una
sentinella sintetica esterna, avvia il server congelato, tenta una lettura
diretta del percorso assoluto tramite MCP, scrive un test agente tramite MCP e
invoca `run_agent_tests`. Il test usa `node:fs` e verifica il contenuto della
sola sentinella sintetica. La directory temporanea di prova viene rimossa dal
probe; non usa workspace di piloti o dati riservati.

Comandi:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts\current\Probe-AgentTestConfinement.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' scripts\current\Probe-AgentTestConfinement.cjs
```

La prova finale è stata eseguita con accesso autorizzato ai normali permessi
dell'utente, necessari per avviare il server nel percorso operativo Windows.
Entrambi i comandi hanno restituito exit code 0: il secondo codice indica che
il probe è stato completato, **non** che il requisito sia stato superato.
Output osservato, riprodotto da due esecuzioni con accesso autorizzato:

```json
{"serverExit":0,"directReadRejected":true,"agentTestWriteSucceeded":true,"agentTestStatus":"completed","agentTestExitCode":0,"agentTestsPassed":1,"externalReadObserved":true,"isolationAcceptance":"failed","sentinelUnchanged":true}
```

Il server ha rifiutato la lettura diretta MCP, ma il test agente ha letto con
successo la sentinella esterna usando `node:fs`. La sentinella non è stata
modificata. Gli SHA-256 riletti dell'eseguibile MCP e di Node corrispondono a
P31/P33. Il sorgente di `ControlledPublicTestRunner` dichiara che il runner
non costituisce un confine di sicurezza del processo.

Il primo tentativo nella sandbox del controllore è fallito prima della prova:
il server ha restituito exit code 2 per accesso negato a `AppData`. Questo
tentativo non dimostra né successo né fallimento del confinamento. Le due prove
con accesso autorizzato sono quelle che hanno prodotto l'osservazione sopra.

## Impatto e limiti

La prova dimostra una lettura esterna attraverso codice di test agente, non la
lettura dei test riservati reali e non l'avvenuta esposizione di tali test nei
piloti. Non sono stati provati qui scrittura esterna, accesso di rete, tutti i
percorsi alternativi o tutti gli account e ACL possibili.

Le run ufficiali non devono iniziare finché il requisito di isolamento non è
risolto e verificato. Le sei evidenze pilota vanno conservate: l'impatto sulla
loro validità e sul freeze non è ancora determinato. Se una correzione cambia
elementi congelati che possono influire su comportamento, durata, token o
valutazione, B22 richiede una valutazione esplicita dell'impatto e la
ripetizione dei piloti. Nessuna conclusione automatica viene tratta qui.

Questa prova non modifica baseline, harness o toolchain congelati e non
autorizza run o chiamate LLM.
