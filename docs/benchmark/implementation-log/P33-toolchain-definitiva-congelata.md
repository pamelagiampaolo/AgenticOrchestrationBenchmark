# P33 — Toolchain Node definitiva congelata

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Commit sorgente:** `aee8d1162bb496895990a00095cc14c2651ff752`.
- **Stato:** toolchain creata, doppia installazione verificata e probe MCP
  completato.
- **Chiamate LLM:** nessuna.

## Collocazione e identità

```text
C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e
```

- Node: `v24.17.0`;
- SHA-256 `node.exe`: `c6335d08331c23d68b9f2b18adb102002d76ef150b47248e954c507e0d033664`;
- npm usato per l'installazione: `11.13.0`;
- Vitest: `5.1.0`;
- TypeScript: `7.0.2`;
- SHA-256 lockfile congelato: `586b704e70cb3fcf9e99a889d627fae98c6880ce042247cd4d37471594b5e739`.

## Creazione e verifica

`New-FrozenNodeToolchain.ps1` estrae `package.json` e `package-lock.json`
direttamente dal commit, copia `node.exe`, esegue due `npm ci --no-audit
--no-fund` indipendenti e confronta percorso, dimensione e SHA-256 di ogni file.
La destinazione viene creata soltanto dopo il confronto positivo.

Risultati osservati:

- entrambe le installazioni: `added 37 packages`;
- file nel manifest: `1148`;
- hash aggregato: `8ac2873da4b0fdd5e6ad8911f862e0ff50619988290b9b8aeba5ab77c5aa0938`;
- SHA-256 `toolchain-manifest.json`:
  `b2644f8c0500e13f8b80da2828bd3b18f3a5423f5c394df1fcc2b641ca424231`;
- file non in sola lettura: `0`;
- directory temporanee residue: `0`.

Il primo controllo manuale del lockfile tramite pipeline PowerShell aveva
prodotto un hash diverso perché la pipeline ne alterava codifica o fine riga. La
generazione definitiva usa `git archive` e preserva i byte dei blob.

## Prova MCP con la toolchain definitiva

`Invoke-McpPublicTestsProbe.ps1` accetta ora percorsi opzionali espliciti per
Node, Vitest e TypeScript. Il probe è stato eseguito indicando esclusivamente i
tre percorsi della nuova toolchain e ha osservato:

- protocollo MCP `2025-06-18` e inventario esatto di nove strumenti;
- parametri arbitrari rifiutati per tutti i comandi controllati;
- test pubblici comuni: `27/27` superati;
- typecheck e build: exit code `0`;
- test agente: `1/1` superato;
- quattro record di audit;
- zero residui nella worker root;
- stderr del server vuoto.

## Limiti e lavoro rimanente

La sola lettura protegge da modifiche accidentali; l'integrità rispetto al
manifest deve essere verificata prima di ogni pilota e run ufficiale. Restano da
congelare Claude CLI, evaluator e test riservati. Questo passo non autorizza
chiamate LLM o run.
