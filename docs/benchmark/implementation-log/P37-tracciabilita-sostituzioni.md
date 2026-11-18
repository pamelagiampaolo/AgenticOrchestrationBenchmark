# P37 — Prova su registro persistente simulato

## Obiettivo e stato

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali.
- **Stato:** struttura di registro simulata e casi verificati; procedura reale
  e controllo operativo non ancora implementati o provati.
- **Chiamate LLM e run ufficiali:** nessuna.

Obiettivo: mostrare come conservare il collegamento tra slot, tentativi e
decisioni B34 in un artefatto persistente distinto dai record della run.
Componenti: fixture JSON, verificatore indipendente di prova e regole B34.
Criteri: conservare tentativi e collegamenti, distinguere causa ignota e
fallimento funzionale, documentare autorizzazione per la sostituzione e
riconoscere l'esaurimento del tentativo disponibile.

## Fixture e verifica

La fixture versionata `docs/benchmark/evidence/B34-fixture-audit-sostituzioni.json`
contiene quattro casi **simulati**, non risultati di run ufficiali: guasto
infrastrutturale con sostituzione valida, causa ignota, fallimento funzionale
e sostituzione esaurita. Contiene identificativi artificiali, evidenze e
autorizzazioni artificiali, seed, posizione e metadati dello slot, oltre a
`runId`, numero tentativo, collegamento al precedente, classificazione,
decisione e disponibilità delle metriche.

Una verifica Node in sola lettura della fixture ha verificato numero massimo
di tentativi, identificativi distinti, collegamento, evidenza di autorizzazione
simulata e causa infrastrutturale per una sostituzione. Ha controllato gli
esiti dei quattro casi e creato in memoria cinque varianti anomale: terzo
tentativo, sostituzione dopo causa ignota, sostituzione dopo fallimento
funzionale, collegamento errato e autorizzazione assente. Il verificatore ha
respinto tutte le varianti. Output osservato:

```text
Fixture persistente: 4 casi conformi; 5 anomalie respinte (terzo tentativo, causa ignota, fallimento funzionale, collegamento, autorizzazione)
```

Exit code 0. Le varianti non sono state scritte su disco. Il verificatore è
stato definito nella stessa invocazione di prova: è un controllo della fixture,
non una funzione di validazione applicata automaticamente alle run reali.

## Applicazione operativa ancora necessaria

Prima delle run occorre definire e provare come il controllore fidato crea e
aggiorna un registro reale fuori dal workspace agente, senza sovrascrivere
eventi o record originali; deve collegare ogni slot al piano B33 e ogni
evidenza e autorizzazione reali a riferimenti verificabili. Deve inoltre
impedire o rilevare una sostituzione non consentita e mantenere la cella
incompleta se necessario. La fixture non costituisce tale registro e non
autorizza sostituzioni o chiamate LLM.

Non sono state eseguite prove delle metriche B4/B5 o dell'isolamento. Questo
passo non certifica l'audit trail operativo né la readiness delle 54 run.
