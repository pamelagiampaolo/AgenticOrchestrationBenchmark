# P57 — Aggregazione del consumo token

## Iterazione e criteri

- **Data:** 19 novembre 2026.
- **Obiettivo:** riepilogare i token P55 per coppia task–architettura e per
  architettura, conservando il denominatore pianificato e i dati mancanti B5.
- **Componenti:** selezione P54, consumo P55, nuova funzione esterna e test
  sintetici. Nessuna modifica a runner, evaluator o input congelati.
- **Comportamento:** per ciascun gruppo mostrare slot individuali, previsti,
  validi e con misura disponibile, conteggi mancanti, mediana, minimo, massimo,
  media, somma delle sole misure disponibili e somma completa solo con tutte le
  misure. Nessun dato assente deve diventare zero; gruppi incompleti sono marcati.
- **Accettazione:** prove di gruppi da 3 e da 18, zero reale, dato mancante,
  esito funzionale sfavorevole valido, duplicati e dimensioni errate, oltre a
  verifica della sintassi. Nessuna run o chiamata LLM.

## Implementazione e verifiche

`scripts/current/Summarize-OfficialUsage.cjs` richiede esattamente una riga
ufficiale per ciascuna combinazione di 6 task, 3 architetture e 3 ripetizioni.
Produce i 18 riepiloghi per coppia e i 3 per architettura. `availableSum` è la
somma parziale delle misure disponibili; `completeSum` è `null` se manca anche
una sola misura pianificata. Se non è disponibile alcun valore anche la somma
parziale e le statistiche restano `null`.

Comandi eseguiti con Node congelato:

```powershell
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --check scripts/current/Summarize-OfficialUsage.cjs
& 'C:\AgenticAIToolchain\node-v24.17.0-lock-586b704e\node\node.exe' --test tests/summarize-official-usage.test.cjs tests/attach-official-usage.test.cjs tests/select-official-attempts.test.cjs tests/check-run-usage.test.cjs tests/attach-official-evaluation.test.cjs tests/official-audit.test.cjs
```

Entrambi exit code 0: 7 test passati, 0 falliti. La nuova prova sintetica copre
54 slot, riepiloghi completi e incompleti da 3 e da 18, zero effettivo,
misura assente, un record con esito funzionale sfavorevole classificato come
valido, gruppi senza misure, input duplicato o malformato e assenza di mutazioni.
Per il gruppo di tre con valori `0`, `null`, `300` ha verificato disponibilità
2/3, mediana 150, somma parziale 300 e somma completa `null`.

## Limiti e lavoro rimanente

La funzione presuppone righe già validate da P54/P55 e non verifica provenienza,
hash e integrità dei file. La prova sintetica dell'aggregazione non costituisce
una prova integrata della catena completa su record reali. Non calcola metriche
di correttezza, durata o altre categorie token, non collega automaticamente
l'evaluator e non è integrata nel flusso operativo delle run. Rimangono da
verificare tali aspetti e il controllo finale pre-avvio. Gli input congelati
non sono cambiati e non sono stati ripetuti piloti.
