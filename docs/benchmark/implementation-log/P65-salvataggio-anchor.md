# P65 — Salvataggio del registro con anchor separato

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** collegare il salvataggio P39 a una copia separata dell'hash
  di ogni revisione, mantenendo la verifica dell'ultimo hash fidato fornito
  dall'operatore.
- **Componenti:** piano B33, script P39, directory di snapshot e directory
  anchor distinte, nuovo comando esterno e prove con file simulati.
- **Comportamento:** rifiutare cronologie o anchor discordanti; salvare uno
  snapshot e il suo anchor con creazione esclusiva; se la seconda scrittura
  fallisce fermarsi e conservare lo snapshot per revisione, senza cancellazioni.
- **Accettazione:** due revisioni simulate, confronto anchor, rifiuto di hash
  errato o anchor mancante e preservazione dei file precedenti. Nessuna run,
  chiamata LLM o scrittura nelle aree ufficiali.

## Comando e verifiche

`scripts/current/Save-OfficialAuditWithAnchor.cjs` richiede due directory già
esistenti e non annidate. Confronta ogni snapshot con il relativo anchor e
richiede l'hash fidato dell'ultima revisione (`none` per la prima); poi usa
P39 per validare e salvare la nuova revisione e scrive un anchor esclusivo in
un file separato. Interfaccia:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Save-OfficialAuditWithAnchor.cjs '<piano-B33.json>' `
  '<directory-registro>' '<directory-anchor>' '<candidato.json>' '<hash-fidato-precedente-o-none>'
```

Il JSON restituito identifica snapshot, revisione, SHA-256 e percorso anchor.
L'hash più recente va conservato anche mediante un riferimento fidato separato
e fornito alla chiamata successiva: non va ricavato automaticamente dalle sole
directory che si vogliono controllare.

Verifiche con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Save-OfficialAuditWithAnchor.cjs
$env:OFFICIAL_PLAN_PATH = 'C:\AgenticAIResults\official-plan-aee8d1162bb496895990a00095cc14c2651ff752.json'
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/official-audit-anchor.test.cjs tests/official-audit-workflow.test.cjs tests/official-audit-save.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0: 4 test passati, 0 falliti, nessuno saltato. Le prove
coprono due revisioni simulate, hash precedente errato, anchor alterato o
mancante, directory coincidenti, sostituzione simulata e rilevamento di
rollback. I file temporanei appartengono ai test; non sono stati creati
registro o anchor ufficiali.

## Limiti e lavoro rimanente

Due directory distinte sullo stesso computer non garantiscono custodia
indipendente da chi può modificarle entrambe. Una riscrittura concordata della
cronologia e degli anchor è rilevabile soltanto confrontando un hash fidato
conservato separatamente. Le due scritture non sono atomiche: se il processo
si interrompe dopo lo snapshot e prima dell'anchor, la chiamata successiva si
ferma per incongruenza; conservare i file e verificare manualmente l'accaduto.
Il comando non prepara il candidato, non garantisce autenticità o sufficienza
delle evidenze e non verifica il collegamento con la submission. La scelta e
la custodia effettiva del riferimento fidato restano da stabilire durante la
procedura. Non è una prova d'isolamento né autorizzazione alle run.
