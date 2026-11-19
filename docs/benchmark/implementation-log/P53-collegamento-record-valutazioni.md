# P53 — Collegamento tra record e valutazioni dei piloti

## Obiettivo e criteri

- **Data:** 19 novembre 2026.
- **Fase:** preparazione delle run ufficiali; nessuna chiamata LLM.

Obiettivo: verificare sui sei piloti esistenti un collegamento concreto tra
record dell'harness, valutazione indipendente e controllo di consumo P51.
Componenti: documenti B26–B31, record nelle aree fidate, riepiloghi evaluator
e `Check-RunUsage.cjs`. Comportamento atteso: per ogni pilota identità `runId`
e `taskId` concordanti, natura `pilot` esplicita e controllo P51 senza errori.
Accettazione: sei coppie lette, sei identità corrispondenti e sei controlli
usage completi; ogni risultato è descritto senza includere piloti tra le run
ufficiali. I file osservati non vengono modificati.

## Verifiche eseguite

PowerShell ha estratto i sei `runId` dai documenti B26–B31, letto per ognuno
l'unico `session-record.json` o `run-record.json` sotto `C:\AgenticAIResults`,
e `evaluation-summary.json` sotto la directory `runs/<runId>/evidence`
dell'evaluator. Ha confrontato entrambi i `runId`, i `taskId` e `runKind`,
eseguendo per ogni cartella `Check-RunUsage.cjs` con Node congelato. Il
conteggio finale è stato verificato uguale a sei. Output, exit code 0:

```text
20261118T172146Z-77c1a569 task=T1 kind=pilot evaluation=passed usage=complete
20261118T174844Z-c22ba9c4 task=T1 kind=pilot evaluation=passed usage=complete
20261118T181009Z-260b2323 task=T1 kind=pilot evaluation=passed usage=complete
20261118T185448Z-52ec0b68 task=T6 kind=pilot evaluation=passed usage=complete
20261118T190522Z-ebd7cd95 task=T6 kind=pilot evaluation=passed usage=complete
20261118T191206Z-104ed5e9 task=T6 kind=pilot evaluation=passed usage=complete
matched=6
```

## Limiti e lavoro rimanente

Questa ricognizione usa percorsi e identificativi dei piloti, non il registro
ufficiale. Non controlla hash di questi file, autenticità, submission, selezione
di sostituzioni, calcolo della correttezza o aggregazione. Il campo `pilot` è
stato osservato: non è stato verificato un filtro automatizzato che escluda i
piloti. L'integrazione operativa di P51 e l'aggregazione dei risultati reali
restano aperte; P51 non dimostra che la CLI abbia emesso tutti i consumi.
Nessun pilota è stato ripetuto e nessun input congelato è stato cambiato.
