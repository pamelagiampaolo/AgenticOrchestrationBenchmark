# P31 — Binari dell'harness congelati

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Commit sorgente:** `aee8d1162bb496895990a00095cc14c2651ff752`.
- **Stato:** due pubblicazioni indipendenti identiche; harness definitivo creato
  e verificato.
- **Chiamate LLM:** nessuna.

## Obiettivo e criteri

Produrre una sola copia fidata dei binari usati da piloti e run ufficiali,
compilandola esclusivamente dal commit designato in B24. I criteri erano:

- due estrazioni e pubblicazioni Release indipendenti;
- identità di percorso relativo, dimensione e SHA-256 per ogni file;
- presenza di apphost `.exe` e assembly `.dll` di entrambi i progetti;
- manifest completo e verifica indipendente contro la copia definitiva;
- file in sola lettura e nessun residuo temporaneo.

## Collocazione approvata

```text
C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752
```

La cartella è esterna al repository, alle aree delle run e alle directory dei
risultati. Non viene esposta agli agenti tramite gli strumenti MCP.

## Implementazione

È stato aggiunto `scripts/current/New-FrozenHarness.ps1`. Lo script rifiuta una
destinazione già esistente, estrae due copie del commit tramite `git archive`,
esegue restore e publish dei progetti `AgenticExperimentRunner` e
`AgentWorkspaceMcpServer`, confronta tutti i file e crea la destinazione soltanto
dopo il confronto positivo.

Parametri di publish rilevanti:

- configurazione `Release`;
- framework-dependent (`--self-contained false`);
- `ContinuousIntegrationBuild=true` e `Deterministic=true`;
- simboli e PDB esclusi;
- `PathMap` dei differenti percorsi temporanei verso `/_/`.

Il `PathMap` è necessario perché il primo tentativo, pur avendo completato tutte
le build, ha prodotto output differenti tra le due root temporanee. Il controllo
ha interrotto l'operazione e la destinazione definitiva non è stata creata. Dopo
la normalizzazione, il secondo tentativo è risultato identico byte-per-byte.

## Risultati osservati

Comando:

```powershell
& .\scripts\current\New-FrozenHarness.ps1 `
  -Commit aee8d1162bb496895990a00095cc14c2651ff752 `
  -DestinationRoot C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752
```

- .NET SDK: `10.0.403`;
- MSBuild: `18.9.15+5a2162bea`;
- RID host: `win-x64`;
- file pubblicati: `16`;
- hash aggregato: `fa2c3278e4fec4ca51fae8eb4e8f970d083aded011c03554d923ed9afb934737`;
- SHA-256 di `harness-manifest.json`:
  `2ae6e4d9c69e4d54e2430f574b7bea58654d07eca469a853e3f54d5b3db40b2c`.

File principali:

| File | Byte | SHA-256 |
| --- | ---: | --- |
| `AgenticExperimentRunner.exe` | 162304 | `c0c93c8ff965080965e7af3e2ce7f1aa4145cc07ca2475e64ee1eb5b7c859a63` |
| `AgenticExperimentRunner.dll` | 336896 | `4dbb4bea509bc7c35e8eddf099a7372c7d24d2a1204a8a1d7aebf850a9501326` |
| `AgentWorkspaceMcpServer.exe` | 162304 | `dac6a223cd6411364cd16e45f76688d998b5c41f0588e85122855fb7b40b7661` |
| `AgentWorkspaceMcpServer.dll` | 88064 | `ecdb33a63d81a8afb0114f5afe00c64cb549791e78935d378c5c69500117f545` |

La verifica successiva sul contenuto definitivo ha osservato:

- `16/16` file presenti e conformi al manifest;
- zero mismatch di dimensione o SHA-256;
- quattro file principali presenti;
- zero file senza attributo di sola lettura;
- zero directory temporanee `frozen-harness-*` residue.

## Limiti e lavoro rimanente

L'attributo di sola lettura protegge da modifiche accidentali, non da un processo
con i permessi dell'utente che lo rimuova. L'integrità deve essere ricontrollata
contro `harness-manifest.json` prima dei piloti e delle run ufficiali.

Questo passo non congela ancora le configurazioni runtime generate, la toolchain
Node/Vitest/TypeScript, Claude CLI, evaluator o test riservati. Non autorizza
piloti o run ufficiali.
