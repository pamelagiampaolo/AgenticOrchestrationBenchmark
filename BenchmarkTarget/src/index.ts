/** Describes a product exposed by the public catalog. */
export type Product = {
  /** Stable textual identifier of the product. */
  productId: string;
  /** Display name of the product. */
  name: string;
};

/** Describes a product and its stock when initializing the system. */
export type InitialProduct = Product & {
  /** Quantity initially available in stock. */
  availableQuantity: number;
};

/** Describes one requested product quantity in an order. */
export type OrderLine = {
  /** Identifier of the requested product. */
  productId: string;
  /** Number of product units requested. */
  quantity: number;
};

/** Describes an order snapshot exposed by the public API. */
export type Order = {
  /** Instance-local sequential identifier of the order. */
  orderId: string;
  /** Current lifecycle state of the order. */
  status: "draft" | "confirmed";
  /** Product quantities retained in their original order. */
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
 * Provides in-memory access to the product catalog and current stock.
 * Each instance owns independent copies of all supplied data.
 */
export class InventoryManagement {
  private readonly products: Product[];
  private readonly availableQuantities: Map<string, number>;
  private readonly orders = new Map<string, Order>();
  private nextOrderNumber = 1;

  /**
   * Creates an inventory initialized with copies of the supplied products.
   *
   * @param initialProducts Products and quantities used as initial state.
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
   * Returns product snapshots ordered by ascending textual identifier.
   *
   * @returns New product objects that do not expose internal state.
   */
  public listProducts(): Product[] {
    return this.products
      .map(({ productId, name }) => ({ productId, name }))
      .sort((left, right) => compareProductIds(left.productId, right.productId));
  }

  /**
   * Returns the currently available quantity for a product.
   *
   * @param productId Identifier of the product to inspect.
   * @returns The available stock quantity.
   * @throws An error with code `PRODUCT_NOT_FOUND` when the product is absent.
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
   * Creates a draft order without changing stock quantities.
   *
   * @param lines Product quantities to retain in their supplied order.
   * @returns A detached snapshot of the newly created draft order.
   * @throws An error with code `EMPTY_ORDER`, `PRODUCT_NOT_FOUND`, or
   * `INVALID_QUANTITY` when the supplied lines are invalid.
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
   * Returns an order by identifier.
   *
   * @param orderId Identifier of the order to inspect.
   * @returns A detached snapshot of the order.
   * @throws An error with code `ORDER_NOT_FOUND` when the order is absent.
   */
  public getOrder(orderId: string): Order {
    return copyOrder(this.requireOrder(orderId));
  }

  /**
   * Returns all orders ordered by ascending textual identifier.
   *
   * @returns Detached order snapshots, including detached line objects.
   */
  public listOrders(): Order[] {
    return [...this.orders.values()]
      .map(copyOrder)
      .sort((left, right) => compareText(left.orderId, right.orderId));
  }

  /**
   * Confirms a draft order and decrements stock for each retained line.
   * Availability is intentionally checked per line without aggregating duplicate
   * products, preserving the defect used by experimental task T3.
   *
   * @param orderId Identifier of the draft order to confirm.
   * @returns A detached snapshot of the confirmed order.
   * @throws An error with code `ORDER_NOT_FOUND`, `INSUFFICIENT_STOCK`, or
   * `ORDER_ALREADY_CONFIRMED` when confirmation cannot be completed.
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
