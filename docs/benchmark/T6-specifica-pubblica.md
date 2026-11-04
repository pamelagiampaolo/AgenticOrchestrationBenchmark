# T6 — Evasione parziale e ordini in attesa

## Registro

- **Data:** 4 novembre 2026
- **Stato:** contratto e pacchetto di test pubblici approvati
- **Perimetro:** specifica pubblica, cinque casi T6 e regressione applicabile

## Contratto pubblico

L'input di `createOrder` conserva il tipo esistente:

```ts
type OrderLine = {
  productId: string;
  quantity: number;
};
```

L'entry point esporta il nuovo tipo:

```ts
type FulfillmentOrderLine = {
  productId: string;
  requestedQuantity: number;
  fulfilledQuantity: number;
  remainingQuantity: number;
};
```

Nella soluzione T6, `Order` contiene `FulfillmentOrderLine[]` e usa gli stati
`draft`, `waiting`, `partially-fulfilled` e `fulfilled`. Alla creazione ogni riga
ha quantità richiesta uguale all'input, quantità evasa zero e residuo uguale alla
richiesta.

```ts
confirmOrder(orderId: string): Order;
fulfillOrder(orderId: string): Order;
receiveStock(productId: string, quantity: number): number;
```

`confirmOrder` opera soltanto su un `draft` e assegna la scorta disponibile,
anche parzialmente. `fulfillOrder` opera su `waiting` e
`partially-fulfilled`, assegna soltanto il residuo e aggiorna cumulativamente
l'evaso. Se non trova nuova scorta, restituisce uno snapshot invariato.

Le righe duplicate restano separate e vengono servite progressivamente nel loro
ordine originale. La scorta non diventa mai negativa. Lo stato è `waiting` se
nulla è evaso, `partially-fulfilled` se resta un residuo dopo almeno un'unità
evasa e `fulfilled` se tutto è completo.

`receiveStock` richiede un prodotto esistente, una quantità intera sicura e
strettamente positiva e una somma risultante sicura. Gli errori sono
`PRODUCT_NOT_FOUND` e `INVALID_QUANTITY`.

Una seconda conferma produce `ORDER_ALREADY_CONFIRMED`; l'evasione di un `draft`
produce `ORDER_NOT_CONFIRMED`; l'evasione di un ordine completo produce
`ORDER_ALREADY_FULFILLED`; un ordine inesistente produce `ORDER_NOT_FOUND`.
Ogni errore lascia invariato lo stato. Tutte le letture e i risultati sono
snapshot indipendenti. Ordini diversi consumano scorta secondo l'ordine esplicito
delle chiamate.

T6 parte dalla baseline originale e non comprende le soluzioni T1–T5.

## Casi pubblici T6

Ogni caso costruisce una nuova istanza dalla fixture comune e ricostruisce tutto
il percorso necessario.

- **T6-P01:** `P-001 × 4`, conferma → `waiting`, richiesto `4`, evaso `0`,
  residuo `4`, scorta `0`.
- **T6-P02:** percorso P01, carico `2`, evasione → `partially-fulfilled`, evaso
  `2`, residuo `2`, scorta `0`.
- **T6-P03:** percorso P02, altro carico `2`, evasione → `fulfilled`, evaso `4`,
  residuo `0`, scorta `0`.
- **T6-P04:** percorso fino al completamento, nuova evasione →
  `ORDER_ALREADY_FULFILLED` e intero stato osservabile invariato.
- **T6-P05:** `P-002 × 3`, conferma → `fulfilled`, richiesto `3`, evaso `3`,
  residuo `0`, scorta `2`.

## Regressione applicabile

La suite T6 seleziona positivamente 21 dei 27 casi comuni invariati. Non sono
applicabili, per cambiamento intenzionale del contratto:

- `creates and reads a draft order without changing stock`: vecchio formato righe;
- `confirms an order and decrements stock`: vecchio stato `confirmed`;
- `keeps a failed confirmation atomic`: vecchia semantica di scorta insufficiente;
- `does not decrement an earlier product when a later product is insufficient`:
  incompatibile con l'allocazione parziale;
- `deeply copies input lines and returned order snapshots`: vecchio formato righe;
- `retains duplicate lines and confirms them when total stock is sufficient`:
  vecchio stato e formato righe.

I casi applicabili sono 26: 21 comuni e cinque T6. Nessuno può essere saltato.
Eventuali sei `skipped` mostrati dal reporter sono esclusioni non applicabili, non
casi canonici T6 saltati. In questo passo non viene creato alcun manifest o
valutatore.
