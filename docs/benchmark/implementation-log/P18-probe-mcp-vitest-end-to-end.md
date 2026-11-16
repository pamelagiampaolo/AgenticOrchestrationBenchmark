# P18 - Probe MCP Vitest end-to-end

## Data e obiettivo

- Data: 2026-11-08.
- Obiettivo: verificare il contratto di configurazione che il launcher
  single-agent dovrà applicare, senza modificare il branch separato che contiene
  il launcher A3.

## Implementazione

`scripts/Invoke-McpPublicTestsProbe.ps1` costruisce un layout A2 sintetico con:

- `agent-root/workspace` contenente la baseline;
- `agent-root/public-reference/tests/public` contenente i due test comuni;
- `agent-root/agent-tests` vuota;
- worker root e risultati esterni ad `agent-root`.

Lo script configura il server con le variabili P16, invia quattro richieste MCP
tramite stdin e chiude il flusso per terminare il server:

1. `initialize`;
2. `tools/list`;
3. `run_public_tests` con un parametro arbitrario vietato;
4. `run_public_tests` con oggetto vuoto.

Verifica inventario esatto, errore `-32602` sul parametro, conteggi pubblici,
audit, assenza di residui e stderr vuoto. Tutta la fixture viene eliminata nel
blocco `finally`.

## Risultato osservato

- protocollo: `2025-06-18`;
- strumenti: `list_files`, `read_file`, `write_file`, `delete_file`,
  `move_file`, `run_public_tests`;
- inventario esatto: vero;
- parametro arbitrario rifiutato: vero;
- stato: `completed`, exit code 0;
- test: 27 totali, 27 superati, 0 falliti, 0 pending;
- record audit: 1;
- residui worker: 0;
- stderr server: vuoto.

I primi tentativi hanno evidenziato incompatibilità specifiche di Windows
PowerShell: `ProcessStartInfo.ArgumentList` e `Kill(Boolean)` non erano
disponibili e la lettura interattiva asincrona non completava. Il probe finale
usa una pipeline di quattro righe JSON verso stdin e lascia che EOF termini il
server. Una stringa stderr vuota restituita come `null` è stata normalizzata.

## Limiti e passo successivo

Il probe usa `TestResults` nel repository soltanto come fixture sintetica e la
elimina; le run ufficiali restano in `C:\AgenticAIRuns` e i risultati in
`C:\AgenticAIResults`.

Il launcher A3 è presente esclusivamente su `feat/single-agent`. Per evitare
conflitti non è stato modificato da questo worktree. Dopo il merge del branch,
`ClaudeLaunchProfile` dovrà aggiungere `run_public_tests` alla propria allowlist
e inserire nel JSON MCP le cinque variabili obbligatorie P16, usando directory
derivate dalla run preparata e percorsi fidati di Node/Vitest.
