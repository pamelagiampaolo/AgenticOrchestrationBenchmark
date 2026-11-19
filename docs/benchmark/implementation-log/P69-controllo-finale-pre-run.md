# P69 — Controllo finale prima delle run

## Obiettivo e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** confrontare piano, sorgente, harness, toolchain, CLI,
  evaluator e condizioni operative con il freeze prima della decisione di
  avviare le run ufficiali.
- **Criteri:** hash e versioni corrispondenti ai documenti di freeze, confronto
  dei file dei manifest, verifica del primo slot e dei test pertinenti; ogni
  controllo non eseguibile deve restare esplicitamente aperto.
- **Confine:** nessuna area ufficiale, run o chiamata LLM. L'avvio richiede
  un'autorizzazione esplicita separata e, se differito, nuovi controlli vicino
  all'esecuzione. Si applica la limitazione di isolamento accettata B35.

## Risultati osservati

- Il commit sorgente risolve al tree B24
  `7543e5dddc103dfbc7250566c26b765c370fbda0`; il manifest P30 ha l'hash
  previsto. L'evaluator è al commit
  `a2f1626a50e696d2930bfc72d28b6cb339dc9d9e` e `git status --porcelain=v1
  --untracked-files=all` non ha mostrato modifiche o file non tracciati.
- Il piano B33 ha hash previsto, 54 ID e combinazioni uniche, posizioni
  consecutive e primo slot `official-t5-hierarchical-adaptive-r3`.
  `generate-experiment-plan --seed aee8d1162bb496895990a00095cc14c2651ff752`
  eseguito con l'harness congelato è terminato con exit 0 e stdout
  byte-identico al piano salvato.
- Manifest e file confrontati tramite lettura e SHA-256: harness **16/16**,
  toolchain **1148/1148**, baseline fidata **19/19**, nessuna differenza.
  Anche CLI, suo manifest, manifest pubblico e riservato, schema evaluator e
  file conteggi P63 hanno gli hash attesi. `claude.exe --version` ha restituito
  `2.1.312 (Claude Code)` e `node.exe --version` `v24.17.0`.
- `Test-PublicManifest.ps1` ha restituito `Manifest coerente (test verificati:
  True)`, exit 0; `Test-ReservedManifest.ps1` ha restituito `Manifest coerente
  (identificatori dei test verificati: True)`, exit 0. Questi comandi verificano
  i manifest, non eseguono le run ufficiali.
- La suite Node dei controlli di report, evidenze e audit ha dato **16 test
  passati, 0 falliti, 0 saltati**, exit 0, usando anche il piano reale e il
  pilota B26 in sola lettura. I test sono quelli di P54–P67 più il flusso P41.
- Node `fs.statfsSync` ha rilevato `968878747648` byte disponibili su C: su
  `2047330590720` totali. Non è stata stimata una soglia di sufficienza per
  tutte le run. Le tre directory radice di run, risultati ed evaluator esistono;
  le directory di registro e anchor designate in P68 non esistono ancora.
  `Get-Process` non ha mostrato processi visibili con i nomi del runner,
  server MCP o Claude CLI.

## Comandi e problemi risolti

I confronti file sono stati eseguiti con Node congelato tramite `node -e`,
leggendo ogni voce dei manifest, dimensione, SHA-256 e numero di file effettivi;
la baseline è stata verificata con `Get-Item` e `Get-FileHash -Algorithm
SHA256` per le 19 voci `benchmark` del manifest P30. Per il piano lo script
Node ha inoltre confrontato lo stdout del generatore congelato con i byte del
file. Per l'evaluator è stato usato `git -c
safe.directory=C:/repos/AgenticOrchestrationEvaluator -C
C:\repos\AgenticOrchestrationEvaluator status --porcelain=v1
--untracked-files=all` e `rev-parse HEAD` con la stessa opzione locale.

Test eseguiti con Node congelato e `OFFICIAL_PLAN_PATH` impostato al piano:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/report-official-results.test.cjs tests/official-report-pipeline.test.cjs tests/summarize-official-completion.test.cjs tests/compute-official-completion.test.cjs tests/compute-official-pass-rates.test.cjs tests/attach-official-evaluation.test.cjs tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/run-evidence-bundle.test.cjs tests/official-audit-anchor.test.cjs tests/official-audit-workflow.test.cjs tests/official-audit.test.cjs tests/official-audit-save.test.cjs
```

Il primo `git` sull'evaluator ha rifiutato la diversa proprietà della directory;
l'opzione `-c safe.directory` ha permesso la lettura senza modificare la
configurazione globale. Il primo confronto della baseline e il controllo del
manifest pubblico sono stati negati dalla sandbox (`Accesso negato`): le 19
letture iniziali non costituiscono differenze dei file. Ripetuti con accesso
autorizzato hanno dato 19/19 e manifest pubblico coerente. `Get-CimInstance`
per lo spazio su disco è stato negato; la misura riportata usa `fs.statfsSync`.

## Limiti e azioni al momento dell'avvio

La verifica è una fotografia del momento: ripetere i controlli necessari
immediatamente prima dell'esecuzione, soprattutto se l'avvio viene rinviato.
La prima area non è stata creata; dopo autorizzazione va preparata nuova e
controllata secondo P64. Le directory P68 e la loro custodia effettiva non sono
state create o provate con run reali; la scrittura fuori dal repository dipende
dai permessi disponibili. La reportistica e l'audit sono stati provati su dati
simulati o piloti, non su run ufficiali; non coprono automaticamente tutte le
metriche B5. La disponibilità futura del servizio e delle credenziali non è
stata verificata con una chiamata LLM. Gli hash non dimostrano il completo
isolamento: resta la limitazione B35 scelta dall'utente, da riportare con i
risultati. Nessun pilota è stato ripetuto e nessuna run ufficiale autorizzata o
avviata in questo passo.
