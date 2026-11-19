# P64 — Procedura per la prima run ufficiale

## Obiettivo e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** rendere verificabile la sequenza per il primo slot del piano,
  dalla preparazione alla valutazione e registrazione, senza eseguirla.
- **Componenti:** piano B33, preparatore e runner congelati, evaluator, P38–P39,
  P51, P62 e conteggi P63.
- **Accettazione:** i parametri del primo slot e i percorsi coincidono con piano
  e interfacce osservate; la procedura distingue comandi non eseguiti, verifiche
  e punti che richiedono intervento del ricercatore. Nessuna chiamata LLM.

## Primo slot e sequenza

Il primo slot del piano B33 è `official-t5-hierarchical-adaptive-r3`:
task T5, gerarchia adattiva, ripetizione 3, posizione 1; il seed è
`aee8d1162bb496895990a00095cc14c2651ff752`. I limiti B32 sono 240 secondi,
800000 token e massimo 10 deleghe. Prima dell'uso ricontrollare piano,
input congelati e condizioni operative. I comandi seguenti sono stati
**predisposti, non eseguiti in questo passo**.

1. Preparare un'area nuova dopo il controllo finale; salvare il JSON completo
   restituito e il suo `runId`. Non riutilizzare aree o sessioni precedenti.

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' prepare-run `
  --repository 'C:\repos\AgenticOrchestrationBenchmark' `
  --commit aee8d1162bb496895990a00095cc14c2651ff752 `
  --task T5 --runs-root 'C:\AgenticAIRuns' --results-root 'C:\AgenticAIResults'
```

   Verificare exit code, commit, task, manifest, file presenti e directory
   sessione e test agente vuote. Se un controllo fallisce, fermarsi.

2. **Solo dopo esplicita autorizzazione dell'utente alle run ufficiali e alle
   chiamate LLM**, sostituire `<runId>` con quello della preparazione e avviare:

```powershell
& 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgenticExperimentRunner\AgenticExperimentRunner.exe' run-adaptive-hierarchy `
  --prepared-run 'C:\AgenticAIResults\<runId>\prepared-run.json' `
  --claude 'C:\AgenticAIClaudeCli\2.1.312\claude.exe' `
  --mcp-server 'C:\AgenticAIHarness\aee8d1162bb496895990a00095cc14c2651ff752\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.exe' `
  --node 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  --vitest 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\vitest\vitest.mjs' `
  --typescript 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules\typescript\lib\tsc.js' `
  --model claude-sonnet-5-5 --time-limit-seconds 240 --token-budget 800000 `
  --max-delegations 10 --run-kind official --repetition 3 `
  --order-seed aee8d1162bb496895990a00095cc14c2651ff752 --order-position 1
```

3. Conservare record, stream, diagnostica e stato; eseguire P51 sulla directory
   risultati. Valutare la submission `agent-root\workspace` con l'evaluator
   fidato, usando lo stesso `runId`, T5, baseline e toolchain congelate; il
   riepilogo va conservato fuori dall'area agente. Applicare i codici di uscita
   dell'evaluator e B34: errore di integrità o causa non classificabile richiede
   arresto e revisione, non una sostituzione automatica.

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Check-RunUsage.cjs 'C:\AgenticAIResults\<runId>'
$previousProcessPath = $env:Path
try {
  $env:Path = 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node;' + $previousProcessPath
  & 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -File `
    'C:\repos\AgenticOrchestrationEvaluator\evaluator\Invoke-Evaluation.ps1' `
    -SubmissionPath 'C:\AgenticAIRuns\<runId>\agent-root\workspace' -TaskId T5 `
    -BaselinePath 'C:\repos\AgenticOrchestrationTools\baseline-aee8d11' `
    -ToolsNodeModulesPath 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\packages\node_modules' `
    -RunId '<runId>'
  $evaluationExitCode = $LASTEXITCODE
} finally { $env:Path = $previousProcessPath }
$evaluationExitCode
```

4. Preparare e verificare il candidato del registro secondo P38; salvarlo con
   P39 e conservare separatamente l'hash restituito come anchor per la prossima
   revisione. Verificare i collegamenti a run, valutazione ed evidenze prima di
   procedere allo slot successivo. Eventuali nuove chiamate per sostituzione
   richiedono autorizzazione specifica secondo B34. P62 può produrre il riepilogo
   usando i conteggi P63, previa verifica della loro provenienza.

## Limiti

La sequenza sopra non è un esecutore automatico e non prova che l'anchor abbia
custodia indipendente. La preparazione del candidato audit, la conservazione
dell'anchor e la verifica dei collegamenti sono operazioni ancora da provare
in un flusso concreto. Le limitazioni di isolamento accettate in B35 restano
applicabili. Non si ripetono i piloti per questa documentazione.

## Verifiche eseguite

Una verifica PowerShell in sola lettura ha analizzato con
`System.Management.Automation.Language.Parser` i tre blocchi di comandi,
confrontato SHA-256 del piano con B33 e i metadati del primo slot con i valori
indicati, e controllato l'esistenza di otto eseguibili o file runtime citati.
Output: `syntaxBlocks=3 planHash=matched firstSlot=matched runtimePaths=8/8`,
exit code 0. L'esistenza non verifica gli hash dei binari; l'analisi sintattica
non esegue né convalida semanticamente i comandi con una run reale. I parametri
dell'evaluator e il formato delle aree sono stati confrontati con il relativo
script e con l'area di un pilota esistente. Una lettura iniziale del README
evaluator ha usato un percorso errato; quella successiva è riuscita.
