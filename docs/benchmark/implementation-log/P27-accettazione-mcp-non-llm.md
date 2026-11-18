# P27 — Accettazione non-LLM dell'harness MCP corrente

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** readiness precedente al freeze finale e ai nuovi piloti.
- **Commit verificato:** `dd3118a7ce4b3fab74032c1610b7a793ac0f6c26`.
- **Stato:** entrambe le prove non-LLM completate con esito positivo.
- **Chiamate LLM:** nessuna.

## Obiettivo

Verificare sul percorso operativo corrente alcune proprietà osservabili del
server `AgentWorkspaceMcpServer` e dei worker controllati prima di designare il
commit finale di freeze. La prova riguarda l'inventario degli strumenti,
l'accesso confinato ai file, il rifiuto di percorsi di evasione e l'esecuzione
effimera dei comandi di sviluppo.

## Comandi eseguiti

Dalla radice del repository:

```powershell
& .\scripts\current\Invoke-AgentWorkspaceMcpServerProbe.ps1 -Mode Direct
& .\scripts\current\Invoke-McpPublicTestsProbe.ps1
```

La modalità `Direct` è stata specificata esplicitamente per impedire l'avvio
della parte Claude del primo script.

## Risultati osservati

Il probe diretto è terminato con exit code `0`. Tutte le 20 proprietà registrate
in `direct-results.json` hanno valore `true`, incluse:

- inventario degli strumenti esattamente corrispondente alla configurazione;
- letture interne e pubbliche consentite;
- scritture, spostamenti ed eliminazioni consentiti soltanto nelle aree
  configurate;
- modifica dei materiali protetti rifiutata;
- percorsi assoluti, traversal e attraversamento di junction rifiutati;
- contenuti protetti invariati, sentinel esterno non divulgato e nessun file
  scritto nella root esterna.

La root esterna conteneva al termine soltanto `external.txt`, cioè il sentinel
creato prima della prova. Il file transitorio osservato era:

- percorso:
  `C:\Users\Utente\AppData\Local\Temp\agent-workspace-mcp-server-dfcbe3783bbf4bd2b30da911def700d6\evidence\direct-results.json`;
- dimensione: `43860` byte;
- SHA-256: `ab8c44fe51121441b5b37f8dcb29b95d61853910a249c05e6d352553239dd288`.

Il probe end-to-end dei comandi controllati è terminato con exit code `0` e ha
osservato:

- protocollo MCP `2025-06-18`;
- inventario esatto di nove strumenti;
- parametri arbitrari rifiutati per test pubblici, typecheck, build e test
  agente;
- test pubblici: `27` totali, `27` superati, `0` falliti, `0` pending;
- typecheck, build e test agente completati con exit code `0`;
- test agente: `1/1` superato in esecuzione separata;
- quattro record di audit;
- zero residui nella worker root;
- stderr del server vuoto.

Il secondo probe ha eliminato in `finally` la propria area transitoria sotto
`TestResults`.

## Limiti della prova

Questa evidenza certifica esclusivamente le proprietà direttamente osservate:

- non è stata avviata Claude CLI e non è stato verificato il percorso MCP visto
  da un agente reale;
- non è stato eseguito il controllo positivo degli hook previsto dalla modalità
  `Claude`/`All`;
- il probe usa sentinelle e una root esterna temporanea, non espone né legge test
  riservati reali;
- il superamento non dimostra un isolamento generale del processo o l'assenza
  di ogni possibile percorso alternativo;
- il percorso e il file di evidenza diretta sono transitori; hash e risultati
  osservati sono registrati qui, ma non costituiscono ancora il manifest del
  freeze finale.

## Lavoro rimanente

Prima dei nuovi piloti resta necessaria almeno la prova tramite Claude CLI,
subordinata ad autorizzazione esplicita alle chiamate LLM. Restano inoltre da
completare gli altri passaggi della sequenza B22: freeze finale, manifest,
toolchain definitiva, evaluator e materiali riservati.
