# T5 — Separazione degli archivi in memoria

## Registro

- **Data:** 4 novembre 2026
- **Stato:** contratto e pacchetto di test pubblici approvati
- **Perimetro:** specifica pubblica e quattro casi T5; nessuna soluzione inclusa

## Contratti pubblici

L'entry point `src/index.ts` deve esportare:

```ts
export interface ProductRepository {
  findById(productId: string): Product | undefined;
  list(): Product[];
}

export interface InventoryRepository {
  getAvailableQuantity(productId: string): number | undefined;
  setAvailableQuantity(productId: string, availableQuantity: number): void;
}

export interface OrderRepository {
  findById(orderId: string): Order | undefined;
  list(): Order[];
  save(order: Order): void;
}
```

Sono inoltre richieste queste implementazioni esportate:

```ts
new InMemoryProductRepository(initialProducts: readonly Product[]);
new InMemoryInventoryRepository(initialProducts: readonly InitialProduct[]);
new InMemoryOrderRepository();
```

La factory esportata è:

```ts
createInventoryManagementWithRepositories(
  productRepository: ProductRepository,
  inventoryRepository: InventoryRepository,
  orderRepository: OrderRepository,
): InventoryManagement;
```

Il costruttore originale `new InventoryManagement(initialProducts)` e tutto il
comportamento pubblico della baseline restano invariati, incluso il difetto
intenzionale T3.

## Responsabilità

I repository conservano i dati ma non applicano validazioni o errori di dominio.
Le ricerche mancanti restituiscono `undefined`. `OrderRepository.save` inserisce
o aggiorna in base a `orderId`. Ogni input viene copiato e ogni lettura restituisce
copie indipendenti; le copie degli ordini includono oggetti riga indipendenti.

La logica applicativa resta responsabile di validazioni, errori, ordinamenti,
generazione degli identificatori e comportamento della conferma. Essa dipende
dai contratti; le implementazioni in memoria sono selezionate soltanto nella
composizione predefinita.

La factory richiede repository prodotti e scorte coerenti e un repository ordini
inizialmente vuoto. Queste sono precondizioni documentate e non introducono nuove
validazioni applicative. La costruzione predefinita crea stato indipendente per
istanza; con la factory, l'indipendenza richiede repository distinti.

T5 parte dalla baseline originale e non comprende le soluzioni T1–T4.

## Casi pubblici

- **T5-P01:** la stessa suite comune di 27 casi viene eseguita senza essere
  copiata o modificata.
- **T5-P02:** due sistemi distinti; confermare `P-002 × 2` nel primo lascia il
  secondo con scorta `5` e nessun ordine.
- **T5-P03:** modificare risultati letti di prodotti e ordini, incluse le righe,
  non cambia le letture successive dello stato interno.
- **T5-P04:** tre repository di prova distinti inizializzati con la fixture e
  ordini vuoti vengono passati alla factory. Creare e confermare `P-002 × 2`
  restituisce l'ordine completo `O-001` `confirmed`, lascia scorta `3`, conserva
  i dati attesi e dimostra l'uso dei tre repository senza vincolare numero o
  sequenza esatti delle chiamate.

I repository di prova sostituiscono soltanto la conservazione dei dati, non la
logica applicativa. Questi test funzionali non dimostrano da soli l'intera
separazione strutturale: una verifica dedicata della dipendenza dai contratti
sarà definita successivamente e non fa parte di questo pacchetto.
