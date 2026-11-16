# T3 — Correzione degli ordini con righe duplicate

## Registro

- **Data:** 4 novembre 2026
- **Stato:** contratto e pacchetto di test pubblici approvati
- **Perimetro:** specifica pubblica e tre casi T3; nessuna soluzione inclusa

## Contratto pubblico

T3 corregge il comportamento esistente di:

```ts
confirmOrder(orderId: string): Order;
```

Prima di modificare lo stato, `confirmOrder` deve determinare la quantità totale
richiesta per ciascun `productId` e verificare la disponibilità rispetto a tale
totale. Tutte le verifiche devono terminare prima della prima mutazione. Una
conferma valida decrementa ogni scorta una sola volta della quantità totale e
imposta l'ordine a `confirmed`.

Se la disponibilità è insufficiente, l'operazione produce un errore con proprietà
stabile `code` uguale a `INSUFFICIENT_STOCK`, non modifica alcuna scorta e lascia
l'ordine `draft`. Lo stesso comportamento si applica quando il totale delle
quantità duplicate supera `Number.MAX_SAFE_INTEGER`: il superamento deve essere
riconosciuto senza perdita di precisione, senza imporre una specifica
implementazione.

La rappresentazione pubblica dell'ordine conserva tutte le righe originali nel
loro ordine, senza sostituirle con righe aggregate. Il valore restituito è uno
snapshot indipendente e non espone riferimenti modificabili allo stato interno.
Gli ordini senza duplicati e tutti gli altri comportamenti della baseline restano
invariati, inclusi tipi, validazioni e codici di errore esistenti.

## Fixture pubblica

| Prodotto | Nome | Quantità iniziale |
| --- | --- | ---: |
| `P-001` | Tastiera | 0 |
| `P-002` | Mouse | 5 |
| `P-003` | Monitor | 10 |

Ogni caso costruisce una nuova istanza dalla fixture.

## Casi pubblici

| ID | Righe dell'ordine | Risultato atteso |
| --- | --- | --- |
| `T3-P01` | `P-002 × 2`, `P-002 × 3` | ordine `confirmed`; scorta `P-002` pari a `0` |
| `T3-P02` | `P-002 × 3`, `P-002 × 3` | errore `INSUFFICIENT_STOCK`; stato precedente alla conferma invariato; ordine `draft` |
| `T3-P03` | `P-002 × 2`, `P-003 × 4` | ordine `confirmed`; scorte rispettivamente `3` e `6` |

Il task è completato soltanto se continuano a passare anche tutti i test pubblici
comuni della baseline. I test riservati non fanno parte di questo pacchetto.
