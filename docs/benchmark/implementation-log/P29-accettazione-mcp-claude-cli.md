# P29 — Accettazione MCP tramite Claude CLI

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** readiness precedente al freeze finale e ai nuovi piloti.
- **Commit di partenza:** `27c430b3feaec714f12c0bcbcdbed2817d3fe58f`.
- **Stato:** prova Claude CLI completata con esito positivo.
- **Chiamate LLM:** due sessioni tecniche autorizzate esplicitamente dall'utente;
  nessuna costituisce un pilota sperimentale.

## Obiettivo e criteri

Verificare il percorso operativo MCP visto da Claude CLI, il controllo degli hook
e il confinamento degli strumenti prima del freeze. La prova doveva:

- richiedere esplicitamente il modello congelato `claude-sonnet-5-5`;
- esporre soltanto i cinque strumenti MCP di accesso al workspace;
- osservare le operazioni consentite e il rifiuto di quelle protette o esterne;
- dimostrare con un controllo positivo che un hook fidato configurato può
  eseguirsi, e che nella sessione confinata gli hook restano disabilitati;
- terminare senza errori, divulgazione del sentinel esterno o scritture esterne.

## Modifica necessaria prima della prova

L'ispezione preventiva ha rilevato che lo script non passava `--model` alle due
invocazioni Claude CLI. Il probe avrebbe quindi potuto usare il modello
predefinito della CLI, in contrasto con l'invariante B22. È stata introdotta la
costante `$frozenModel = 'claude-sonnet-5-5'` e il relativo argomento `--model`
sia nel controllo positivo degli hook sia nella verifica MCP principale.

Prima della chiamata LLM sono stati eseguiti il parser PowerShell e
`git diff --check`: entrambi hanno restituito exit code `0`.

## Comando e risultato

Dalla radice del repository:

```powershell
& .\scripts\current\Invoke-AgentWorkspaceMcpServerProbe.ps1 -Mode Claude
```

Il comando ha completato restore e publish del server, quindi è terminato con
exit code `0`. L'area transitoria osservata è stata:

```text
C:\Users\Utente\AppData\Local\Temp\agent-workspace-mcp-server-5028338b09354dc085642a95d24b082f
```

### Controllo positivo degli hook

- modello riportato dall'evento di inizializzazione: `claude-sonnet-5-5`;
- sentinel fidato: `HOOK_POSITIVE_CONTROL_EXECUTED`;
- hook di progetto non eseguito;
- costo riportato dalla CLI: `0.012936000000000001` USD;
- token riportati: input `2`, output `4`, cache creation `3223`, cache read `0`.

### Verifica MCP confinata

- modello richiesto e modello riportato: `claude-sonnet-5-5`;
- Claude Code: `2.1.312`;
- exit code registrato: `0`;
- controlli: `31/31` veri;
- unico server MCP: `benchmark_workspace`, stato `connected`;
- inventario: `list_files`, `read_file`, `write_file`, `delete_file`,
  `move_file`, tutti nel namespace MCP previsto;
- slash command, skill e subagent disponibili: nessuno;
- 16 chiamate MCP previste osservate;
- letture e modifiche consentite riuscite;
- spostamento/eliminazione dei materiali protetti, accessi assoluti, traversal e
  attraversamento della junction rifiutati;
- sentinel esterno non divulgato e nessun file esterno aggiunto;
- hook di progetto e hook fidato disabilitato non eseguiti;
- stderr vuoto.

Metriche riportate dalla CLI per la sessione principale:

- durata totale: `15161` ms;
- durata API: `15003` ms;
- turni: `17`;
- costo: `0.0328404` USD;
- token input: `18`;
- token output: `1572`;
- token cache creation: `3389`;
- token cache read: `35284`.

Il costo complessivo riportato per le due sessioni tecniche è `0.0457764` USD.
Queste misure descrivono esclusivamente il probe e non concorrono alla
calibrazione dei piloti o delle run ufficiali.

## Integrità delle evidenze transitorie

Gli hash SHA-256 osservati sono:

| File | SHA-256 |
| --- | --- |
| `claude-checks.json` | `26a8c3a99cf61b93bb6998b8df2bf84e862107706b6bf51fb6bc52db6d305e32` |
| `run-manifest.json` | `3085ea9b06d8916f8bcb1c2d197ef9b13f00a3b1d559a872ff0dea3d861dd220` |
| `claude-stream.jsonl` | `11ccaa1f875474dcc48bbb2e0e12885b583302835d5861b38c7a727d3039efdb` |
| `hook-positive-control-stream.jsonl` | `21a62b31f44af631b95bc5da41dbe49e7c616e5c2c0a3eeab3b5c9046449a98f` |

## Limiti e lavoro rimanente

La prova certifica soltanto le proprietà direttamente osservate sul probe
temporaneo. Non certifica ancora evaluator, test riservati, manifest definitivo
o toolchain congelata e non sostituisce i sei nuovi piloti.

Con questa prova è chiusa la lacuna real-CLI registrata in P27. Prima dei piloti
restano da completare il freeze del commit e degli artefatti, la designazione
della toolchain definitiva, il freeze dell'evaluator e dei materiali riservati,
e la definizione del protocollo minimo dei nuovi piloti.
