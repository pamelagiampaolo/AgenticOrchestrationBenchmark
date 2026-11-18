# P34 — Claude CLI congelata

## Classificazione

- **Data:** 18 novembre 2026.
- **Fase:** freeze precedente ai nuovi piloti.
- **Stato:** copia dedicata creata e verificata.
- **Chiamate LLM:** nessuna; è stato eseguito soltanto `--version` localmente.

## Decisione e collocazione

Per evitare che un aggiornamento dell'installazione utente cambi la CLI tra
piloti e run ufficiali, è stata approvata una copia dedicata:

```text
C:\AgenticAIClaudeCli\2.1.312\claude.exe
```

I nuovi piloti e le run ufficiali devono usare questo percorso, non l'eseguibile
nel profilo utente. Il modello resta un parametro separato e deve essere
esattamente `claude-sonnet-5-5`.

## Identità osservata

- versione: `2.1.312 (Claude Code)`;
- dimensione: `298690208` byte;
- SHA-256:
  `6099672ebe5dcb518b67d1d32dde1e3360bc6bee1dae7f8d28a69c49acc7c865`;
- file autonomo, non collegamento simbolico o junction.

La copia è byte-per-byte identica all'eseguibile allora installato in
`C:\Users\Utente\.local\bin\claude.exe`.

## Manifest e verifica

Accanto all'eseguibile è stato creato `claude-cli-manifest.json`, UTF-8 senza
BOM, contenente versione, nome, dimensione, SHA-256 e percorso di provenienza.

- SHA-256 del manifest:
  `bf8e333af1551fd5fcd640c6311f752a4091054a405a57f26cf0522784ea1d0d`;
- versione letta dalla copia: corrispondente;
- dimensione riletta: corrispondente;
- SHA-256 ricalcolato: corrispondente;
- eseguibile in sola lettura: sì;
- manifest in sola lettura: sì.

## Regola operativa

Prima di ciascun pilota e run ufficiale devono essere verificati manifest,
dimensione, hash e output di `--version`. Qualunque differenza impedisce l'avvio
e richiede una valutazione esplicita dell'impatto secondo B22.

L'attributo di sola lettura protegge da modifiche accidentali, non da un processo
con i permessi dell'utente che lo rimuova. Per questo il controllo crittografico
pre-run resta obbligatorio.

## Lavoro rimanente

Restano da congelare evaluator e test riservati, quindi formalizzare il
protocollo minimo dei sei nuovi piloti. Questo documento non autorizza chiamate
LLM, piloti o run ufficiali.
