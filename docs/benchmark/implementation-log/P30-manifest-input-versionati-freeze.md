# P30 — Manifest degli input versionati del freeze

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Commit sorgente:** `aee8d1162bb496895990a00095cc14c2651ff752`.
- **Tree sorgente:** `7543e5dddc103dfbc7250566c26b765c370fbda0`.
- **Stato:** manifest creato e rigenerazione indipendente verificata.
- **Chiamate LLM:** nessuna.

## Obiettivo e criteri

Registrare in modo deterministico gli input versionati che possono influire sul
comportamento dell'agente o dell'harness, leggendo i blob dal commit designato in
B24 e non dal working tree. I criteri erano:

- SHA-256 e dimensione di ogni file;
- categorie separate per benchmark, specifiche pubbliche, istruzioni e harness;
- hash aggregato con serializzazione canonica documentata;
- nessun test riservato o artefatto generato;
- seconda generazione byte-per-byte identica.

## Implementazione

È stato aggiunto `scripts/current/New-FrozenSourceManifest.ps1`. Lo script:

1. richiede un object ID completo e verifica che risolva esattamente al commit;
2. seleziona i percorsi tramite `git ls-tree`;
3. estrae i blob con `git archive`, neutralizzando la conversione dei fine riga;
4. calcola dimensione e SHA-256 dei file estratti;
5. ordina i percorsi con confronto ordinale;
6. calcola l'aggregato SHA-256 sulle righe UTF-8
   `categoria NUL percorso NUL dimensione NUL sha256 LF`;
7. scrive JSON UTF-8 senza BOM e con fine riga LF, rifiutando di sovrascrivere
   un output esistente.

Le istruzioni, contenute anche nella directory generale dell'orchestratore,
mantengono la categoria specifica `instructions` e non vengono duplicate.

## Comando e risultati osservati

```powershell
& .\scripts\current\New-FrozenSourceManifest.ps1 `
  -Commit aee8d1162bb496895990a00095cc14c2651ff752 `
  -OutputPath docs\benchmark\evidence\final-freeze-source-manifest.json
```

Risultato:

- file: `docs/benchmark/evidence/final-freeze-source-manifest.json`;
- file registrati: `88`;
- benchmark: `19`;
- specifiche pubbliche: `6`;
- istruzioni: `8`;
- harness fidato: `55`;
- hash aggregato: `82dc5305d6a268f12342b0e736778b78dd4f0f3d4e59a7944d95128fe9fc1836`;
- SHA-256 del JSON: `5e1941cc35d0be9e09841a331d36bfd38d381f3edbdc9bd3b95be58812a9edcc`;
- percorsi duplicati: `0`;
- percorsi sotto `tests/reserved`: `0`.

Una seconda generazione in un file temporaneo ha prodotto lo stesso hash del
JSON (`5e1941cc…9edcc`) e lo stesso hash aggregato (`82dc5305…1836`). Il confronto
byte-per-byte tramite SHA-256 è quindi risultato vero.

## Problemi osservati e correzioni

Durante la prima esecuzione il controllo delle categorie ha rilevato la
sovrapposizione attesa delle istruzioni con `src/AgenticExperimentRunner`; è
stata mantenuta la prima categoria più specifica. Windows PowerShell non espone
gli overload moderni a due argomenti di `Path.GetFullPath` e statico
`SHA256.HashData`; sono state usate API equivalenti compatibili. La sandbox ha
negato la prima scrittura nella directory versionata: la generazione è stata
ripetuta con autorizzazione limitata alla destinazione prevista. Nessuno di
questi tentativi ha creato un manifest parziale.

## Limiti e lavoro rimanente

Questo manifest congela esclusivamente gli input versionati. Esclude
intenzionalmente:

- eseguibili compilati e configurazioni MCP/runtime generate;
- toolchain Node/Vitest/TypeScript;
- evaluator fidato e test riservati;
- output di build, `node_modules`, risultati e cronologia Git.

Questi elementi devono essere congelati e verificati nei sotto-passi successivi
prima dei nuovi piloti. Il manifest non autorizza run o chiamate LLM.
