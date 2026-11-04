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

class ProductNotFoundError extends Error {
  public readonly code = "PRODUCT_NOT_FOUND";

  public constructor(productId: string) {
    super(`Product '${productId}' was not found.`);
    this.name = "ProductNotFoundError";
  }
}

/**
 * Provides in-memory access to the product catalog and current stock.
 * Each instance owns independent copies of all supplied data.
 */
export class InventoryManagement {
  private readonly products: Product[];
  private readonly availableQuantities: Map<string, number>;

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
      throw new ProductNotFoundError(productId);
    }

    return availableQuantity;
  }
}

function compareProductIds(left: string, right: string): number {
  if (left < right) {
    return -1;
  }

  if (left > right) {
    return 1;
  }

  return 0;
}
