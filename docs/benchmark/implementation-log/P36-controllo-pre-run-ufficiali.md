# P36 — Controllo preliminare delle run ufficiali

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle 54 run ufficiali.
- **Stato:** integrità degli input sotto elencati verificata; readiness completa
  delle run ufficiali non ancora dimostrata.
- **Chiamate LLM e run ufficiali:** nessuna in questa iterazione.

Obiettivo: verificare piano, input congelati e prerequisiti documentati prima
di proporre l'avvio. Componenti: piano B33, baseline, harness, toolchain, CLI,
evaluator e protocollo delle misure. Criteri: hash e identità conformi,
manifest coerenti e punti non verificati o non definiti esplicitati.

## Risultati osservati

- `Get-FileHash` del piano B33: SHA-256
  `441753b52b77cde505af0dd1bb8541c1300288794751a8bb5732e74a731e4573`;
  JSON con 54 slot e seed `aee8d1162bb496895990a00095cc14c2651ff752`.
  Primo slot: `official-t5-hierarchical-adaptive-r3`.
- Manifest sorgente: SHA-256 corrispondente a P30. Confronto di dimensioni e
  SHA-256 della baseline fidata con i 19 file benchmark del manifest: 19/19
  conformi. `Test-PublicManifest.ps1` con toolchain congelata: exit code 0,
  manifest coerente e test verificati.
- Manifest e contenuto dell'harness: 16/16 file conformi, nessun file in più;
  manifest e contenuto della toolchain: 1148/1148 conformi, nessun file in più.
- CLI: hash e dimensione dell'eseguibile, hash del manifest e output
  `2.1.312 (Claude Code)` conformi a P34.
- Evaluator: working tree pulito e HEAD
  `a2f1626a50e696d2930bfc72d28b6cb339dc9d9e`; hash dei manifest pubblico,
  riservato e dello schema conformi a P35. `Test-ReservedManifest.ps1` con
  toolchain congelata: exit code 0, identificatori verificati.
- `C:\AgenticAIRuns` e `C:\AgenticAIResults` esistono. Questo controllo non
  ha creato un'area ufficiale e non prova che un'area futura sarà pulita.

I controlli della baseline protetta sono stati eseguiti con accesso
autorizzato. La precedente generazione e verifica dettagliata dell'ordine del
piano è documentata in B33; qui è stato ricontrollato il relativo hash.

## Punti da chiarire o verificare prima dell'avvio

B5 richiede prima delle run ufficiali una lista congelata di codici
infrastrutturali che consentono sostituzione, un massimo di tentativi per
cella e una regola conservativa per cause non determinabili. La ricerca nel
repository di tali vincoli e dell'audit trail non ha individuato una decisione
successiva che li definisca: B6 e B25 regolano i piloti, non costituiscono da
sole una politica ufficiale. B5 richiede anche una prova della sostituzione e
dell'audit trail. Non si deve supporre che tali requisiti siano soddisfatti.

B5 elenca ulteriori controlli preliminari per metriche e aggregazioni, inclusi
listino e formule economiche: questa iterazione non li ha rieseguiti né ha
dimostrato che siano tutti soddisfatti. Le prove P27/P29 e i piloti sono
evidenze circoscritte; il presente ricontrollo non ripete le prove di isolamento
e non certifica l'assenza di percorsi alternativi non autorizzati. La verifica
dello spazio libero tramite `Get-PSDrive` non ha restituito un valore numerico
utilizzabile e pertanto la capacità disponibile non è stata accertata.

Restano un audit mirato dei requisiti B4/B5 e la definizione e prova delle
politiche mancanti, prima di richiedere l'autorizzazione alle 54 run. Il piano
non è stato eseguito e questo documento non autorizza chiamate LLM.
