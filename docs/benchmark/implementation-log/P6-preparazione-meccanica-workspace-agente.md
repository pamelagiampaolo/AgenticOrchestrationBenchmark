# P6 — Preparazione meccanica della workspace agente

> **Prototipo sostituito da A1/A2.** Lo script PowerShell sperimentale P6 è
> stato ritirato dopo l'integrazione di A1, che costituisce l'unica
> implementazione autorevole della preparazione delle run. A2 estenderà A1 con
> il layout `agent-root/`, `session/`, materiali pubblici separati e manifest
> persistente esterno; al momento di questa nota A2 è pianificato, non ancora
> implementato. Le evidenze e i risultati storici P6 riportati sotto restano
> invariati e non autorizzano l'uso di una root di run interna al repository.

## Stato e perimetro

- **Data:** 8 novembre 2026.
- **Stato:** implementazione e prova diretta completate.
- **Obiettivo:** creare da un commit Git una nuova area agente contenente
  esclusivamente materiali autorizzati, separando sorgenti modificabili, test
  pubblici e test aggiuntivi.

L'iterazione non avvia Claude, non applica ancora ACL e non modifica la policy di
scrittura MCP. Non certifica quindi la protezione completa dei test pubblici.

## Componente introdotto

Il prototipo storico `scripts/New-AgentWorkspace.ps1`, ora ritirato, riceveva:

- root assoluta del repository;
- commit sorgente;
- task da `T1` a `T6`;
- nuova destinazione assoluta;
- percorso assoluto del manifest fidato, esterno alla destinazione agente.

Il contenuto viene estratto con `git archive` dal commit indicato, non copiato
dallo stato eventualmente modificato del worktree. Lo script rifiuta destinazioni
o manifest già esistenti e rimuove soltanto le proprie uscite incomplete in caso
di errore.

## Struttura prodotta

```text
<destination>/
├── workspace/                 sorgenti e configurazioni del benchmark
├── public-reference/          test pubblici separati
├── agent-tests/               inizialmente vuota
└── task-specification.md      specifica pubblica del task selezionato
```

`workspace/` esclude `tests/public`. La copia non contiene `.git`, `node_modules`,
output di build o materiali riservati, perché deriva esclusivamente dai file
versionati selezionati. Dopo l'estrazione e dopo la copia viene inoltre rifiutato
qualsiasi reparse point o collegamento.

Il manifest esterno registra versione dello schema, data UTC, commit completo,
task, destinazione e, per ogni file, percorso relativo, SHA-256 e dimensione.

## Verifiche eseguite

La prova ha usato:

```powershell
.\scripts\New-AgentWorkspace.ps1 `
  -RepositoryRoot <legacy-repository-root> `
  -SourceCommit HEAD `
  -TaskId T1 `
  -DestinationRoot <legacy-repository-root>\TestResults\workspace-preparation-acceptance\run-001 `
  -ManifestPath <legacy-repository-root>\TestResults\workspace-preparation-acceptance\trusted\run-001-manifest.json
```

Risultato osservato:

```text
WORKSPACE_ROOT=<legacy-repository-root>\TestResults\workspace-preparation-acceptance\run-001
MANIFEST_PATH=<legacy-repository-root>\TestResults\workspace-preparation-acceptance\trusted\run-001-manifest.json
SOURCE_COMMIT=7359f9c680a53259c1de5e78d9fc3c9240a0a165
```

Una seconda copia indipendente dallo stesso commit ha prodotto lo stesso insieme
di percorsi, hash e dimensioni. Sono stati osservati:

- 11 file in `workspace`;
- 8 file in `public-reference`;
- 1 specifica del task;
- 0 file iniziali in `agent-tests`;
- `.git` assente;
- zero reparse point;
- test pubblici assenti da `workspace` e presenti in `public-reference`;
- commit completo corretto nel manifest;
- rifiuto di una destinazione già esistente.

Tutti i dodici controlli della prova hanno restituito `true`.

## Limiti e prossimo passo

La separazione fisica in sottodirectory non rende da sola
`public-reference/` immodificabile. Il server MCP P5 consente attualmente a
`write_file` di scrivere ovunque sotto la root assegnata.

Il prossimo passo deve introdurre e verificare una policy MCP esplicita:

- lettura ed elenco consentiti in tutta l'area agente;
- scrittura consentita soltanto sotto `workspace/` e `agent-tests/`;
- scrittura negata in `public-reference/` e `task-specification.md`;
- prove negative dirette e tramite Claude CLI;
- verifica che il manifest fidato resti fuori dalla root visibile all'agente.

Solo dopo tale prova la struttura P6 potrà essere usata come input del primo
agente pilota.
