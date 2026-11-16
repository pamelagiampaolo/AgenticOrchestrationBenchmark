# P25 — Conferma reale del round-trip UTF-8

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** confermare attraverso Claude CLI reale la correzione UTF-8 di
  P24 e il ciclo di sviluppo MCP completo.
- **Baseline:** commit `7ed95a014dd4259fd0ca089b74b95126455e68c3`,
  task T1, run `20261109T135133Z-f5a9eff4`.
- **Limiti:** 600 secondi, 1.000.000 token, modello `claude-sonnet-5-5`.
- **Classificazione:** prova tecnica non ufficiale, non pilota B4.

## Esito osservato

La sessione è terminata con exit code 0 e stato `completed` in 29.544 ms.
Il record contiene 36.124 token autorevoli, coincidenti con lo stream, nessuna
violazione di configurazione, nessun errore strumento e nessuna differenza nei
materiali protetti.

L'inizializzazione ha dichiarato:

- esattamente i nove strumenti MCP approvati;
- un solo server `benchmark_workspace`, connesso;
- nessun agente, skill o comando slash aggiuntivo.

Chiamate osservate:

- `list_files`: 1;
- `read_file`: 1;
- `write_file`: 1;
- `run_typecheck`: 1;
- `run_public_tests`: 1.

Typecheck e suite pubblica sono terminati con exit code 0; la suite comprendeva
31 test e l'agente ha riportato 31 test superati. L'audit persistente contiene
le operazioni `typecheck` e `public-tests`, entrambe `completed`.

## Verifica UTF-8

`agent-root/workspace/src/index.ts` è stato decodificato con UTF-8 rigoroso dopo
la sessione. Sono state osservate tutte le proprietà attese:

- contiene `quantità`;
- non contiene `quantit├á`;
- non contiene il carattere sostitutivo Unicode;
- il trasporto `read_file` → modello → `write_file` ha quindi conservato i
  caratteri non ASCII.

## Pulizia e integrità

- differenze nei materiali protetti: zero;
- elementi residui nella worker root: zero;
- elementi nella session directory: zero;
- processi `McpReadOnlyPrototype.exe` residui: zero;
- stderr della CLI: vuoto;
- errori MCP: zero;
- righe stream malformate: zero.

Le evidenze sono in
`C:\AgenticAIResults\20261109T135133Z-f5a9eff4`; l'area modificata è in
`C:\AgenticAIRuns\20261109T135133Z-f5a9eff4`.

## Conclusione

La correzione UTF-8 è confermata anche nel percorso reale Claude CLI ↔ MCP.
Il single-agent dispone ora del ciclo di sviluppo previsto con typecheck e test
pubblici. Prima dei piloti B4 restano da congelare protocollo, limiti e testo
definitivo delle istruzioni, e da decidere se eseguire una valutazione riservata
delle prove tecniche conservate.
