# P52 — Riconciliazione sintetica dei dati mancanti

## Obiettivo e criteri

- **Data:** 18 novembre 2026.
- **Fase:** preparazione delle run ufficiali.
- **Stato:** verifica circoscritta su dati sintetici; nessuna run o chiamata LLM.

Obiettivo: verificare con un esempio calcolabile a mano le regole B5 per tre
slot di una coppia task–architettura, distinguendo valutazione e consumo token.
Componenti: B5, P40, P51 e fixture in memoria. Comportamento atteso: un
fallimento funzionale valido conta nel denominatore; una misura token mancante
non diventa zero e non impedisce l'uso di una valutazione affidabile; riepilogo
token incompleto se disponibili meno di tre misure.

Criteri di accettazione: sulle tre righe sintetiche, due completamenti corretti
su tre valutazioni affidabili; token disponibili 100 e 300 con mediana 200,
minimo 100, massimo 300, media 200 e una misura mancante. La somma delle due
misure disponibili è 400, ma non rappresenta il totale completo delle tre run.
La prova non usa né modifica gli input congelati.

## Verifica eseguita e risultato

Una singola invocazione PowerShell ha costruito tre oggetti in memoria con
`Correct/Tokens` rispettivamente `true/100`, `false/null`, `true/300`, filtrato
esplicitamente i soli token non nulli e confrontato conteggi e statistiche con
i valori attesi sopra. Output osservato, exit code 0:

```text
correct=2/3; token_n=2/3; missing=1; min=100; max=300; mean=200; median=200; partial_sum=400; incomplete=True
```

La correttezza delle tre valutazioni e la loro validità sono presupposti della
fixture, non ricavati da record reali. Il valore 400 è una somma parziale,
non un totale completo; la copertura va sempre riportata accanto al valore.

## Limiti e lavoro rimanente

P40 non aveva individuato un aggregatore verificato; le ricerche di questa
iterazione non ne hanno dimostrato l'esistenza nel runner o negli script
`scripts/current`. La prova calcola un esempio, non verifica un processo reale
di aggregazione, il filtro piloti, la selezione dei tentativi, l'integrazione
di P51, la propagazione di errori evaluator o i riepiloghi su 18 run. Restano
da definire e verificare su record e valutazioni l'elaborazione effettiva e la
procedura operativa. Non viene dichiarata raggiunta la readiness completa.

Una ricerca iniziale con glob passati come percorsi a `rg` è fallita per la
sintassi Windows; le ricerche successive con `-g` e la lettura diretta di B5 e
P40 sono riuscite. Non sono state ripetute prove pilota.
