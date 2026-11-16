/** Descrive un prodotto esposto dal catalogo pubblico. */
export type Product = {
  /** Identificatore testuale stabile del prodotto. */
  productId: string;
  /** Nome visualizzato del prodotto. */
  name: string;
};

/** Descrive un prodotto e la relativa scorta durante l'inizializzazione del sistema. */
export type InitialProduct = Product & {
  /** Quantità inizialmente disponibile in magazzino. */
  availableQuantity: number;
};

/** Descrive la quantità richiesta di un prodotto in un ordine. */
export type OrderLine = {
  /** Identificatore del prodotto richiesto. */
  productId: string;
  /** Numero di unità del prodotto richieste. */
  quantity: number;
};

/** Descrive un'istantanea di un ordine esposta dall'API pubblica. */
export type Order = {
  /** Identificatore sequenziale dell'ordine, locale all'istanza. */
  orderId: string;
  /** Stato corrente del ciclo di vita dell'ordine. */
  status: "draft" | "confirmed";
  /** Quantità dei prodotti conservate nel loro ordine originale. */
  lines: OrderLine[];
};

type ErrorCode =
  | "PRODUCT_NOT_FOUND"
  | "ORDER_NOT_FOUND"
  | "EMPTY_ORDER"
  | "INVALID_QUANTITY"
  | "INSUFFICIENT_STOCK"
  | "ORDER_ALREADY_CONFIRMED";

class InventoryManagementError extends Error {
  public readonly code: ErrorCode;

  public constructor(code: ErrorCode, message: string) {
    super(message);
    this.name = "InventoryManagementError";
    this.code = code;
  }
}

/**
 * Fornisce accesso in memoria al catalogo dei prodotti e alle scorte correnti.
 * Ogni istanza possiede copie indipendenti di tutti i dati forniti.
 */
export class InventoryManagement {
  private readonly products: Product[];
  private readonly availableQuantities: Map<string, number>;
  private readonly orders = new Map<string, Order>();
  private nextOrderNumber = 1;

  /**
   * Crea un inventario inizializzato con copie dei prodotti forniti.
   *
   * @param initialProducts Prodotti e quantità usati come stato iniziale.
   */
  public constructor(initialProducts: InitialProduct[]) {
    this.products = initialProducts.map(({ productId, name }) => ({
      productId,
      name,
    }));
    this.availableQuantities = new Map(
      initialProducts.map(({ productId, availableQuantity }) => [
        productId,
        availableQuantity,
      ]),
    );
  }

  /**
   * Restituisce istantanee dei prodotti ordinate per identificatore testuale crescente.
   *
   * @returns Nuovi oggetti prodotto che non espongono lo stato interno.
   */
  public listProducts(): Product[] {
    return this.products
      .map(({ productId, name }) => ({ productId, name }))
      .sort((left, right) => compareProductIds(left.productId, right.productId));
  }

  /**
   * Restituisce la quantità attualmente disponibile di un prodotto.
   *
   * @param productId Identificatore del prodotto da esaminare.
   * @returns La quantità disponibile in magazzino.
   * @throws Un errore con codice `PRODUCT_NOT_FOUND` quando il prodotto è assente.
   */
  public getAvailableQuantity(productId: string): number {
    const availableQuantity = this.availableQuantities.get(productId);

    if (availableQuantity === undefined) {
      throw new InventoryManagementError(
        "PRODUCT_NOT_FOUND",
        `Product '${productId}' was not found.`,
      );
    }

    return availableQuantity;
  }

  /**
   * Crea un ordine in bozza senza modificare le quantità in magazzino.
   *
   * @param lines Quantità dei prodotti da conservare nell'ordine in cui sono state fornite.
   * @returns Un'istantanea indipendente dell'ordine in bozza appena creato.
   * @throws Un errore con codice `EMPTY_ORDER`, `PRODUCT_NOT_FOUND` oppure
   * `INVALID_QUANTITY` quando le righe fornite non sono valide.
   */
  public createOrder(lines: OrderLine[]): Order {
    this.validateOrderLines(lines);

    const orderId = formatOrderId(this.nextOrderNumber);
    const order: Order = {
      orderId,
      status: "draft",
      lines: copyOrderLines(lines),
    };

    this.orders.set(orderId, order);
    this.nextOrderNumber += 1;

    return copyOrder(order);
  }

  /**
   * Restituisce un ordine in base all'identificatore.
   *
   * @param orderId Identificatore dell'ordine da esaminare.
   * @returns Un'istantanea indipendente dell'ordine.
   * @throws Un errore con codice `ORDER_NOT_FOUND` quando l'ordine è assente.
   */
  public getOrder(orderId: string): Order {
    return copyOrder(this.requireOrder(orderId));
  }

  /**
   * Restituisce tutti gli ordini ordinati per identificatore testuale crescente.
   *
   * @returns Istantanee indipendenti degli ordini, incluse copie indipendenti delle righe.
   */
  public listOrders(): Order[] {
    return [...this.orders.values()]
      .map(copyOrder)
      .sort((left, right) => compareText(left.orderId, right.orderId));
  }

  /**
   * Conferma un ordine in bozza e decrementa le scorte per ogni riga conservata.
   * La disponibilità viene intenzionalmente verificata riga per riga senza aggregare
   * i prodotti duplicati, preservando il difetto usato dal task sperimentale T3.
   *
   * @param orderId Identificatore dell'ordine in bozza da confermare.
   * @returns Un'istantanea indipendente dell'ordine confermato.
   * @throws Un errore con codice `ORDER_NOT_FOUND`, `INSUFFICIENT_STOCK` oppure
   * `ORDER_ALREADY_CONFIRMED` quando la conferma non può essere completata.
   */
  public confirmOrder(orderId: string): Order {
    const order = this.requireOrder(orderId);

    if (order.status === "confirmed") {
      throw new InventoryManagementError(
        "ORDER_ALREADY_CONFIRMED",
        `Order '${orderId}' is already confirmed.`,
      );
    }

    for (const line of order.lines) {
      const availableQuantity = this.availableQuantities.get(line.productId)!;

      if (availableQuantity < line.quantity) {
        throw new InventoryManagementError(
          "INSUFFICIENT_STOCK",
          `Product '${line.productId}' has insufficient stock.`,
        );
      }
    }

    for (const line of order.lines) {
      const availableQuantity = this.availableQuantities.get(line.productId)!;
      this.availableQuantities.set(
        line.productId,
        availableQuantity - line.quantity,
      );
    }

    order.status = "confirmed";
    return copyOrder(order);
  }

  private validateOrderLines(lines: OrderLine[]): void {
    if (lines.length === 0) {
      throw new InventoryManagementError(
        "EMPTY_ORDER",
        "An order must contain at least one line.",
      );
    }

    for (const line of lines) {
      if (!this.availableQuantities.has(line.productId)) {
        throw new InventoryManagementError(
          "PRODUCT_NOT_FOUND",
          `Product '${line.productId}' was not found.`,
        );
      }

      if (!Number.isSafeInteger(line.quantity) || line.quantity <= 0) {
        throw new InventoryManagementError(
          "INVALID_QUANTITY",
          "Order line quantity must be a positive safe integer.",
        );
      }
    }
  }

  private requireOrder(orderId: string): Order {
    const order = this.orders.get(orderId);

    if (order === undefined) {
      throw new InventoryManagementError(
        "ORDER_NOT_FOUND",
        `Order '${orderId}' was not found.`,
      );
    }

    return order;
  }
}

function compareProductIds(left: string, right: string): number {
  return compareText(left, right);
}

function compareText(left: string, right: string): number {
  if (left < right) {
    return -1;
  }

  if (left > right) {
    return 1;
  }

  return 0;
}

function formatOrderId(orderNumber: number): string {
  return `O-${orderNumber.toString().padStart(3, "0")}`;
}

function copyOrderLines(lines: OrderLine[]): OrderLine[] {
  return lines.map(({ productId, quantity }) => ({ productId, quantity }));
}

function copyOrder(order: Order): Order {
  return {
    orderId: order.orderId,
    status: order.status,
    lines: copyOrderLines(order.lines),
  };
}
