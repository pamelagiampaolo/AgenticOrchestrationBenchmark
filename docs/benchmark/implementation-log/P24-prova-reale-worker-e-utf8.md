# P24 — Prova reale del worker e correzione UTF-8

## Iterazione

- **Data:** 9 novembre 2026.
- **Obiettivo:** verificare una sessione single-agent reale con i quattro comandi
  di sviluppo MCP disponibili.
- **Baseline:** commit `4fe52354012ee8cfd1d29bd56024878b1841b21d`,
  task T1, run `20261109T134416Z-f3712490`.
- **Limiti dichiarati:** 600 secondi, 1.000.000 token, modello
  `claude-sonnet-5-5`.
- **Stato:** prova tecnica non valida come pilota B4; ha identificato un difetto
  di trasporto poi corretto.

## Risultati osservati

La sessione è terminata con exit code 0 e stato `completed` in 29.428 ms.
Il record riporta 36.858 token autorevoli, coincidenti con lo stream, nessuna
violazione di configurazione, nessun errore strumento e nessuna differenza nei
materiali protetti.

L'inizializzazione esponeva esattamente i nove strumenti MCP previsti, un solo
server `benchmark_workspace` connesso e nessun agente, skill o comando slash.
L'agente ha chiamato:

- `list_files`: 2;
- `read_file`: 1;
- `write_file`: 1;
- `run_typecheck`: 1, exit 0;
- `run_public_tests`: 1, exit 0, 31 test superati.

L'audit contiene le due operazioni Node controllate. Worker root e session
directory erano vuote al termine; non restavano processi MCP. Le evidenze sono
in `C:\AgenticAIResults\20261109T134416Z-f3712490` e l'area modificata in
`C:\AgenticAIRuns\20261109T134416Z-f3712490`.

## Difetto rilevato

Il parametro `write_file` registrato nello stream conteneva correttamente
`quantità`, mentre il file scritto conteneva `quantit├á`. Preparazione, lettura
MCP e generazione del modello conservavano quindi UTF-8; la corruzione avveniva
nella decodifica dello stdin del processo MCP con la code page OEM Windows.

Il server imposta ora esplicitamente stdin a UTF-8 con controllo dei byte non
validi e stdout a UTF-8 senza BOM. Un test di integrazione avvia il vero server
su stdio, invia `quantità, perché, è così` dentro una richiesta JSON-RPC e
verifica byte e testo del file risultante.

Il primo avvio del nuovo test sotto `%TEMP%` è stato rifiutato correttamente
dalla sandbox per accesso a `C:\Users\Utente\AppData`; la fixture è stata
spostata sotto `TestResults` nel workspace e la prova è stata ripetuta.

## Conclusione

La prova dimostra che il ciclo typecheck/test è raggiungibile dalla CLI reale,
ma la run che ha rivelato la corruzione non può essere promossa a pilota. Dopo
la correzione serve una nuova prova tecnica su un'area pulita per osservare il
round-trip UTF-8 corretto anche attraverso Claude CLI.
