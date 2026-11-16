# Documentazione del benchmark

Questa directory raccoglie progettazione, implementazione, specifiche pubbliche,
piloti ed evidenze dell'esperimento. I prefissi A, B, P e T sono conservati nei
nomi dei documenti per mantenere la tracciabilità cronologica delle iterazioni.

## Sezioni

| Directory | Contenuto |
| --- | --- |
| [`architecture/`](architecture/) | Evoluzione dell'orchestratore e delle tre condizioni sperimentali, documenti A1-A10. |
| [`benchmark-design/`](benchmark-design/) | Struttura del benchmark, task, valutazione, metriche, protocollo e freeze dei limiti. |
| [`pilot-runs/`](pilot-runs/) | Tentativi pilota, inclusi quelli invalidati e le relative osservazioni. |
| [`implementation-log/`](implementation-log/) | Diario tecnico delle iterazioni P sull'MCP, sul confinamento e sulla toolchain controllata. |
| [`task-specifications/`](task-specifications/) | Specifiche pubbliche T1-T6 consegnate agli agenti sperimentali. |
| [`evidence/`](evidence/) | Riepiloghi JSON versionati delle prove tecniche storiche. |

## Percorso di lettura

Per comprendere il disegno sperimentale, partire da
[`B1 — Struttura concettuale`](benchmark-design/B1-struttura-concettuale.md) e
proseguire con i documenti B nella stessa directory. La configurazione e i
limiti da congelare prima delle run ufficiali sono descritti in
[`B19 — Calibrazione e freeze dei limiti ufficiali`](benchmark-design/B19-calibrazione-limiti-ufficiali.md).

Le specifiche pubbliche effettivamente consegnate agli agenti sono in
[`task-specifications/`](task-specifications/). I test riservati non appartengono
a questo repository e non devono essere collocati in un workspace accessibile
agli agenti.

I documenti in `implementation-log/` e le evidenze associate descrivono anche
percorsi architetturali superati, tra cui Windows Sandbox. Devono essere letti
come registro storico: l'architettura operativa corrente è quella definita in
[`AGENTS.md`](../../AGENTS.md).
