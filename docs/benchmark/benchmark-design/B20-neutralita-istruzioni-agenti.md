# B20 — Revisione delle istruzioni architetturali

- **Data:** 9 novembre 2026
- **Fase:** verifica di readiness prima dei piloti B4
- **Branch:** `feat/single-agent` (worktree `C:\repos\AgenticOrchestrationBenchmark-agent`)
- **Stato:** completata; modifiche approvate dall'utente

## Obiettivo

Rendere le istruzioni delle tre condizioni neutrali e simmetriche, come richiede
B4: le istruzioni architetturali possono differire soltanto per realizzare
single-agent, gerarchia statica o gerarchia adattiva, non per dare informazioni o
suggerimenti diversi.

## Problemi rilevati

1. **Limiti comunicati in modo asimmetrico.** Solo il coordinatore era informato
   che tempo e token sono limitati; il single-agent no. È una differenza di
   informazione che può cambiare il comportamento e non è necessaria a realizzare
   l'architettura.
2. **Contraddizione nel testo comune.** "Operi esclusivamente tramite gli strumenti
   MCP del server `benchmark_workspace`" era falso per il coordinatore, che usa
   anche `benchmark_coordination`.
3. **Formule diverse per il ruolo.** Il single-agent riceveva una sequenza di
   lavoro ("analizzi, pianifichi, implementi e controlli"), il coordinatore no.

## Decisioni dell'utente (9 novembre 2026)

- Limiti: una frase nel testo comune, uguale per tutti gli agenti, senza valori
  numerici; il coordinatore aggiunge soltanto che il limite comprende anche i
  lavoratori.
- Testo comune: "Lavori esclusivamente tramite strumenti MCP. Il server
  `benchmark_workspace` espone i file e i comandi di verifica del progetto…".
- Single-agent: "Lavori come unico agente e sei responsabile del completamento del
  task, senza delegare ad altri agenti", stessa formula del coordinatore.

## File modificati

- `AgenticExperimentRunner/Instructions/common-environment.md`
- `AgenticExperimentRunner/Instructions/single-agent.md`
- `AgenticExperimentRunner/Instructions/coordinator.md`

`worker.md` e la descrizione di `delegate_task` (`CoordinationMcpServer`) sono
invariati.

## Hash dei testi dopo la revisione

SHA-256 del contenuto UTF-8 con fine riga normalizzati a LF, come li calcolano i
runner (`RunInputs.ReadInstructions`):

| File | SHA-256 |
| --- | --- |
| `common-environment.md` | `91d28494ce8bff89139a975f95897c42e447bee2f925117ae6eb6b5cccf2ddd3` |
| `single-agent.md` | `b2ea314300b579c10d280c3f25e1fc1676b538127d2094c7ef9feaee9697a7d0` |
| `coordinator.md` | `ae2ff6c8730eeb0e25d547d0ad96637e056fc5502eade2bbbf66908aea413f79` |
| `worker.md` | `a657cdfabb0ac967ae3e14ddb72596ce1a7ab3aa7f0d45037c2bbbded9e10775` |

Le run precedenti (A3, A5, P26) hanno usato le versioni anteriori: i loro record
riportano hash diversi e non sono confrontabili con run successive su questo
punto.

## Verifiche

- `dotnet build`: 0 avvisi, 0 errori.
- `dotnet test` (suite completa): 285 superati, 1 ignorato (prova reale esplicita),
  0 falliti; i test dei runner verificano intestazioni e ruolo nei system prompt.
- Nessuna chiamata LLM in questa iterazione.

## Limiti e punti aperti

- I testi non sono stati provati con il modello dopo la revisione.
- Le istruzioni della gerarchia statica non esistono ancora; dovranno seguire le
  stesse regole di simmetria (testo comune invariato, solo il ruolo cambia).
- Restano da decidere prima dei piloti: collocazione della toolchain congelata e
  protocollo dei piloti B4.
