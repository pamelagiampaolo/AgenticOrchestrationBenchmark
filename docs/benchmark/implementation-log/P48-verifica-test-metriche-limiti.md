# P48 — Verifica mirata dei test su metriche e limiti

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** verifiche B4/B5 prima delle run ufficiali.
- **Stato:** test esistenti mirati eseguiti; requisiti residui indicati sotto.
- **Chiamate LLM sperimentali:** nessuna.

Obiettivo: eseguire i test già disponibili per deduplicazione degli eventi,
misurazione e limiti senza cambiare l'harness congelato. Componenti: test dello
stream, controllore dei limiti e runner single-agent e gerarchici. Criteri:
test mirati completati senza fallimenti, output e limiti osservati registrati,
distinzione tra casi coperti e requisiti non provati.

## Comando e risultato

Con accesso autorizzato alle directory temporanee richieste dai test:

```powershell
dotnet test .\tests\AgenticExperimentRunner.Tests\AgenticExperimentRunner.Tests.csproj --no-build `
  --filter-class '*StreamUsageTrackerTests' `
  --filter-class '*RunLimitControllerTests' `
  --filter-class '*SingleAgentSessionRunnerTests' `
  --filter-class '*HierarchicalRunRunnerTests'
```

Output: **41 test totali, 41 passati, 0 falliti, 0 ignorati**, exit code 0;
durata riportata 21,612 secondi. I casi includono eventi ripetuti dello stesso
messaggio, categorie cache, errori degli strumenti, budget per più agenti,
timeout, misura incompleta e rifiuto di configurazioni inattese. P42 riporta
separatamente la riconciliazione dei record dei sei piloti.

`git diff --name-only` tra il commit sorgente congelato
`aee8d1162bb496895990a00095cc14c2651ff752` e HEAD, limitato a
`StreamUsageTracker.cs`, `RunLimitController.cs`, `SingleAgentSessionRunner.cs`
e `HierarchicalRunRunner.cs`, non ha restituito differenze versionate.

## Limiti e lavoro rimanente

Il comando usa `--no-build` e l'assembly di test Debug già presente; non è una
nuova compilazione né una prova byte-per-byte del binario Release congelato.
Il confronto Git riguarda solo i quattro sorgenti indicati e non prova da solo
la provenienza dell'assembly di test. I casi utilizzano sostituti della CLI,
non nuove chiamate al modello.

I test passati non dimostrano che ogni retry o chiamata LLM fallita con consumo
sia visibile e conteggiata una sola volta. Non verificano da soli la propagazione
dei dati mancanti agli aggregati, l'esclusione dei piloti, la custodia operativa
del registro o l'isolamento. Tali aspetti restano distinti nella matrice P40.
L'utente ha deciso separatamente in B35 di rinviare la correzione della
limitazione di isolamento; questa verifica non modifica tale decisione.

Nessun input sperimentale congelato è stato modificato e non sono stati
ripetuti piloti. Questo documento non autorizza run o chiamate LLM.
