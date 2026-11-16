# P23 — Integrazione del worker MCP nel launcher

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** rendere disponibili i quattro comandi di sviluppo MCP nelle
  sessioni single-agent e gerarchiche reali.
- **Componenti:** profilo Claude CLI, comandi di avvio, runner single-agent e
  gerarchico, server MCP, istruzioni comuni e test.
- **Accettazione:** nove strumenti MCP esatti, toolchain esplicita, configurazione
  verificata all'avvio, probe reale verde e nessun uso di Windows Sandbox.

## Decisioni implementate

I comandi `run-single-agent` e `run-adaptive-hierarchy` richiedono ora i percorsi
assoluti `--node`, `--vitest` e `--typescript`. Il launcher non cerca componenti
nel `PATH` e non eredita implicitamente una toolchain variabile.

La configurazione MCP generata passa al server le directory scrivibili, la
toolchain fissata, una worker root e una directory di audit sotto i risultati.
Ogni processo MCP crea `mcp-command-audit-<pid>.jsonl`: coordinatore e lavoratori
paralleli non scrivono quindi nello stesso file.

L'allowlist della CLI contiene esattamente i cinque strumenti file e i quattro
comandi `run_public_tests`, `run_typecheck`, `run_build`, `run_agent_tests`.
Le istruzioni comuni limitano questi comandi alla verifica del progetto Node.js;
il loro hash continua a essere registrato nei record della run.

## Verifiche osservate

- merge di `feat/single-agent`: automatico, nessun conflitto, fermato prima del
  commit;
- build: 0 avvisi, 0 errori;
- test mirati di launcher, sessioni e gerarchia: 53/53;
- probe MCP reale: inventario esatto di nove strumenti;
- test pubblici: 27/27; test agente sintetico: 1/1;
- typecheck e build: exit code 0;
- parametri arbitrari rifiutati;
- quattro record di audit, zero residui worker, stderr server vuoto.

Durante la verifica è emersa una differenza tra identità sandbox e proprietario
di `%TEMP%`. Il supporto dei test ora individua il profilo che contiene realmente
la temp directory; la policy usata dalle run non è stata indebolita.

## Limite dichiarato

Il worker è completo rispetto al contratto operativo approvato, ma non è un
confine di sicurezza del processo Node. Prompt, allowlist, configurazioni fisse,
copie effimere e audit permettono controllo e rilevazione; non impediscono a
codice intenzionalmente ostile di usare permessi concessi all'identità Windows.

## Passo successivo

Eseguire una prova completa non ufficiale del single-agent con area A2, CLI reale
e i quattro comandi MCP, quindi ispezionare record, audit, integrità e workspace
prima di autorizzare i piloti B4.
