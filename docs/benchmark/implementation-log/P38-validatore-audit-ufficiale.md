# P38 — Controllo indipendente del registro delle run

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali.
- **Stato:** validatore in sola lettura e test mirati implementati; registro
  operativo e integrazione nel processo di esecuzione ancora da realizzare.
- **Chiamate LLM e run ufficiali:** nessuna.

Obiettivo: rilevare incongruenze tra registro persistente, piano B33 e politica
B34 senza modificare l'harness congelato. Componenti: script Node separato,
piano ufficiale, file JSON del registro ed evidenze su disco. Criteri: hash del
piano corretto, metadati coerenti con lo slot, al massimo due tentativi,
collegamento e autorizzazione della sostituzione, classificazione coerente e
hash delle evidenze verificati. Un controllo positivo non attesta che una
classificazione o un'autorizzazione siano sostanzialmente vere.

## Interfaccia e contratto

Comando, in sola lettura:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' `
  scripts\current\Test-OfficialAudit.cjs `
  'C:\AgenticAIResults\official-plan-aee8d1162bb496895990a00095cc14c2651ff752.json' `
  '<percorso-assoluto-registro.json>'
```

Il JSON del registro ha `schemaVersion: 1`, `seed` uguale al piano e `slots`,
eventualmente vuoto mentre la raccolta non è iniziata. Ogni slot registrato ha
`slotId`, `orderPosition`, `taskId`, `architecture`, `repetition`, `attempts`
e `slotOutcome`. Ogni tentativo ha `runId`, `attempt`, `previousRunId`,
`classification`, `decision`, `metricsAvailable` e `evidence`. Un riferimento
di evidenza è `{ "path": "<percorso assoluto>", "sha256": "<hash minuscolo>" }`;
la sostituzione richiede anche `authorizationEvidence` con lo stesso formato.
I percorsi devono puntare a file leggibili e non a collegamenti simbolici finali.

Classificazioni di ricercatore accettate: quattro codici `INFRA_*` di B34 con
decisione `invalid`; `non-classificabile` o `integrity-error` con decisione
`review`; `completed`, `test-failures`, `build-failure`,
`time-limit-exceeded`, `token-limit-exceeded`, `resource-limits-exceeded` o
`architecture-failure` con decisione `valid`. Queste etichette del registro
non sostituiscono gli stati originali dell'harness o dell'evaluator. L'ultimo
tentativo determina `slotOutcome`: rispettivamente `incomplete`, `review` o
`valid`. Per una sostituzione il primo tentativo deve avere classificazione
infrastrutturale e decisione `invalid`.

Il comando restituisce exit code 0 se i controlli strutturali e gli hash sono
coerenti, 1 per incoerenze del registro o evidenze, 2 per errori di input o hash
del piano errato. Un registro vuoto è strutturalmente valido e non indica che
siano state eseguite o completate le 54 run.

## Verifiche osservate

- `node --check scripts/current/Test-OfficialAudit.cjs`: exit code 0.
- `node --test tests/official-audit.test.cjs` con Node congelato: 1 test passato,
  0 falliti. Il test scrive e rilegge file temporanei e verifica sostituzione
  collegata, causa ignota, fallimento funzionale, slot incompleto e rifiuto di
  terzo tentativo, causa errata, link o autorizzazione mancanti, `runId`
  duplicato, posizione errata ed evidenza alterata.
- Interfaccia CLI con il piano ufficiale e un registro temporaneo vuoto: exit
  code 0, `Audit structure and evidence hashes are consistent with the official
  plan.` Il file temporaneo è stato rimosso.
- `git diff --check`: nessun errore osservato.

## Limiti e lavoro rimanente

Il validatore rileva incongruenze, ma non crea o aggiorna il registro, non
impedisce sovrascritture e non dimostra che file di evidenza e autorizzazione
siano autentici o sufficienti: queste decisioni restano al ricercatore. Non
controlla che tutti gli slot siano presenti, né impone che i percorsi delle
evidenze siano esterni al workspace; il registro effettivo deve essere tenuto
nell'area fidata. Non verifica la disponibilità di ogni singola metrica né
realizza il collegamento automatico ai record originali. Il piano è vincolato
all'hash B33; una modifica richiede valutazione esplicita.

Restano da creare e provare la procedura di registrazione persistente senza
sovrascritture, l'integrazione nel flusso ufficiale e l'audit B4/B5. Questo
passo non certifica readiness o isolamento e non autorizza run o chiamate LLM.
