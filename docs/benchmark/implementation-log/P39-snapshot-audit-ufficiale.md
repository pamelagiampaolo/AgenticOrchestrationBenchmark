# P39 — Snapshot del registro delle run ufficiali

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali.
- **Stato:** salvataggio a snapshot implementato e provato su dati simulati;
  integrazione nel flusso delle run e procedure di custodia ancora da provare.
- **Chiamate LLM e run ufficiali:** nessuna.

Obiettivo: salvare revisioni del registro separatamente dall'harness congelato,
senza sovrascrivere le precedenti. Componenti: validatore P38, piano B33,
snapshot e riferimenti a evidenze. Criteri: creazione esclusiva, collegamento
tramite SHA-256, conservazione dei tentativi precedenti, rispetto della politica
B34 e rilevazione di anchor errato o cronologia alterata.

## Funzionamento

`scripts/current/Save-OfficialAudit.cjs` accetta il piano, una directory già
esistente, un candidato JSON secondo P38 e l'hash fidato dell'ultima revisione
(`none` soltanto per la prima). L'interfaccia è:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Save-OfficialAudit.cjs '<piano-B33.json>' `
  '<directory-registro-fidata>' '<candidato.json>' '<hash-precedente-o-none>'
```

Il programma controlla l'hash del piano, rilegge le revisioni ordinate,
verifica catena e struttura tramite P38 e richiede un'unica aggiunta per volta:
il primo tentativo di uno slot o un nuovo tentativo di uno slot esistente.
I tentativi già registrati e i metadati dello slot devono rimanere identici.
Un file `audit-NNNNNN.json` è creato con modalità esclusiva; il JSON restituito
contiene percorso, revisione e SHA-256 da conservare separatamente come anchor.
Il programma non crea o elimina la directory né modifica record originali.

## Verifiche eseguite

- `node --check scripts/current/Save-OfficialAudit.cjs`: exit code 0.
- `node --test tests/official-audit.test.cjs tests/official-audit-save.test.cjs`:
  2 test passati, 0 falliti. I test usano file persistenti temporanei; coprono
  anche anchor obsoleto, modifica di un tentativo, autorizzazione mancante,
  terza ripetizione e alterazione della cronologia. Le revisioni originali
  restano identiche dopo il salvataggio successivo.
- La CLI è stata eseguita con il vero piano B33 e dati simulati in una directory
  temporanea: revisione 1 creata e riletta, exit code 0. I dati temporanei sono
  stati rimossi; nessun registro di run ufficiali è stato creato.

## Limiti e lavoro rimanente

La creazione esclusiva evita la sovrascrittura da parte di questo programma,
non impedisce a un altro processo autorizzato di modificare o cancellare i
file. La catena rileva modifiche soltanto confrontandola con un anchor fidato
esterno aggiornato dopo ogni salvataggio: la sola catena non impedisce rollback
o riscrittura completa. L'anchor va conservato separatamente; non è ancora
prevista una custodia automatica. Un errore di scrittura può lasciare un file
parziale: fermarsi e conservarlo per diagnosi, senza sostituirlo silenziosamente.

Il validatore controlla la presenza e gli hash delle evidenze, non l'autenticità
o la sufficienza sostanziale della classificazione e dell'autorizzazione; la
revisione resta al ricercatore. I candidati sono preparati esternamente e il
flusso delle run non invoca automaticamente lo script. Restano da stabilire e
provare operativamente custodia dell'anchor, collocazione fidata e integrazione
dei record reali, oltre all'audit B4/B5. Questi strumenti esterni non modificano
gli input sperimentali congelati; non sono state ripetute prove pilota.

Questo passo non certifica la readiness completa e non autorizza run o chiamate
LLM.
