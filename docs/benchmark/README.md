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
proseguire con i documenti B nella stessa directory. La politica corrente per
completare la readiness, congelare la configurazione e ricalibrare i limiti è
descritta in
[`B22 — Politica di freeze finale e ricalibrazione`](benchmark-design/B22-politica-freeze-e-ricalibrazione.md).
L'algoritmo che materializzerà i 54 slot dopo il freeze è definito in
[`B23 — Ordine riproducibile delle run ufficiali`](benchmark-design/B23-ordine-run-ufficiali.md).
Il commit sorgente designato per il freeze corrente è registrato in
[`B24 — Designazione del commit di freeze finale`](benchmark-design/B24-designazione-commit-freeze-finale.md).
Il protocollo corrente dei sei nuovi piloti è in
[`B25 — Protocollo dei sei piloti sul freeze finale`](benchmark-design/B25-protocollo-sei-piloti-freeze-finale.md).
La precedente calibrazione è conservata come evidenza storica in
[`B19 — Calibrazione storica dei limiti`](benchmark-design/B19-calibrazione-limiti-ufficiali.md);
la neutralità delle istruzioni e la toolchain precedente sono documentate in
[`B20 — Neutralità delle istruzioni`](benchmark-design/B20-neutralita-istruzioni-agenti.md)
e [`B21 — Toolchain congelata e protocollo dei piloti`](benchmark-design/B21-toolchain-congelata-e-protocollo-piloti.md).
La registrazione dell'identità effettiva della toolchain e la relativa prova
completa non ufficiale sono documentate in
[`P26 — Identità della toolchain`](implementation-log/P26-identita-toolchain-e-prova-completa.md).

Le specifiche pubbliche effettivamente consegnate agli agenti sono in
[`task-specifications/`](task-specifications/). I test riservati non appartengono
a questo repository e non devono essere collocati in un workspace accessibile
agli agenti.

I documenti in `implementation-log/` e le evidenze associate descrivono anche
percorsi architetturali superati, tra cui Windows Sandbox. Devono essere letti
come registro storico: l'architettura operativa corrente è quella definita in
[`AGENTS.md`](../../AGENTS.md).
