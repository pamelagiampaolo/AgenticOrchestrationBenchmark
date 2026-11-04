import { describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
  type Order,
  type Product,
} from "../../../src/index.js";

const fixture: InitialProduct[] = [
  { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
  { productId: "P-002", name: "Mouse", availableQuantity: 5 },
  { productId: "P-003", name: "Monitor", availableQuantity: 10 },
];

describe("T5 - repository separation", () => {
  test("T5-P02 keeps separately constructed systems independent", () => {
    const first = new InventoryManagement(fixture);
    const second = new InventoryManagement(fixture);

    const order = first.createOrder([{ productId: "P-002", quantity: 2 }]);
    const confirmed = first.confirmOrder(order.orderId);

    expect(confirmed).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines: [{ productId: "P-002", quantity: 2 }],
    });
    expect(first.getAvailableQuantity("P-002")).toBe(3);
    expect(second.getAvailableQuantity("P-002")).toBe(5);
    expect(second.listOrders()).toEqual([]);
  });

  test("T5-P03 returns independent product and order snapshots", () => {
    const system = new InventoryManagement(fixture);
    const created = system.createOrder([
      { productId: "P-002", quantity: 2 },
    ]);

    const products = system.listProducts();
    products[1]!.name = "Modificato";
    created.lines[0]!.quantity = 99;
    const read = system.getOrder(created.orderId);
    read.lines[0]!.quantity = 98;
    const listed = system.listOrders();
    listed[0]!.lines[0]!.quantity = 97;
    const confirmed = system.confirmOrder(created.orderId);
    confirmed.lines[0]!.quantity = 96;

    expect(system.listProducts()).toEqual([
      { productId: "P-001", name: "Tastiera" },
      { productId: "P-002", name: "Mouse" },
      { productId: "P-003", name: "Monitor" },
    ]);
    expect(system.getOrder(created.orderId)).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines: [{ productId: "P-002", quantity: 2 }],
    });
    expect(system.getAvailableQuantity("P-002")).toBe(3);
  });

  test("T5-P04 uses supplied repositories for order creation and confirmation", async () => {
    const productRepository = new ProductRepositoryDouble(fixture);
    const inventoryRepository = new InventoryRepositoryDouble(fixture);
    const orderRepository = new OrderRepositoryDouble();
    const module = await import("../../../src/index.js");
    const factory = Reflect.get(
      module,
      "createInventoryManagementWithRepositories",
    ) as unknown;

    expect(factory).toBeTypeOf("function");
    if (typeof factory !== "function") {
      return;
    }

    const system = factory(
      productRepository,
      inventoryRepository,
      orderRepository,
    ) as InventoryManagement;
    const created = system.createOrder([
      { productId: "P-002", quantity: 2 },
    ]);
    const confirmed = system.confirmOrder(created.orderId);

    expect(confirmed).toEqual({
      orderId: "O-001",
      status: "confirmed",
      lines: [{ productId: "P-002", quantity: 2 }],
    });
    expect(system.getAvailableQuantity("P-002")).toBe(3);
    expect(productRepository.wasUsed).toBe(true);
    expect(inventoryRepository.wasUsed).toBe(true);
    expect(orderRepository.wasUsed).toBe(true);
    expect(inventoryRepository.storedQuantity("P-002")).toBe(3);
    expect(orderRepository.storedOrder("O-001")).toEqual(confirmed);
  });
});

class ProductRepositoryDouble {
  public wasUsed = false;
  private readonly products: Product[];

  public constructor(initialProducts: readonly Product[]) {
    this.products = initialProducts.map(copyProduct);
  }

  public findById(productId: string): Product | undefined {
    this.wasUsed = true;
    const product = this.products.find(
      (candidate) => candidate.productId === productId,
    );
    return product === undefined ? undefined : copyProduct(product);
  }

  public list(): Product[] {
    this.wasUsed = true;
    return this.products.map(copyProduct);
  }
}

class InventoryRepositoryDouble {
  public wasUsed = false;
  private readonly quantities: Map<string, number>;

  public constructor(initialProducts: readonly InitialProduct[]) {
    this.quantities = new Map(
      initialProducts.map(({ productId, availableQuantity }) => [
        productId,
        availableQuantity,
      ]),
    );
  }

  public getAvailableQuantity(productId: string): number | undefined {
    this.wasUsed = true;
    return this.quantities.get(productId);
  }

  public setAvailableQuantity(
    productId: string,
    availableQuantity: number,
  ): void {
    this.wasUsed = true;
    this.quantities.set(productId, availableQuantity);
  }

  public storedQuantity(productId: string): number | undefined {
    return this.quantities.get(productId);
  }
}

class OrderRepositoryDouble {
  public wasUsed = false;
  private readonly orders = new Map<string, Order>();

  public findById(orderId: string): Order | undefined {
    this.wasUsed = true;
    const order = this.orders.get(orderId);
    return order === undefined ? undefined : copyOrder(order);
  }

  public list(): Order[] {
    this.wasUsed = true;
    return [...this.orders.values()].map(copyOrder);
  }

  public save(order: Order): void {
    this.wasUsed = true;
    this.orders.set(order.orderId, copyOrder(order));
  }

  public storedOrder(orderId: string): Order | undefined {
    const order = this.orders.get(orderId);
    return order === undefined ? undefined : copyOrder(order);
  }
}

function copyProduct(product: Product): Product {
  return { productId: product.productId, name: product.name };
}

function copyOrder(order: Order): Order {
  return {
    orderId: order.orderId,
    status: order.status,
    lines: order.lines.map(({ productId, quantity }) => ({
      productId,
      quantity,
    })),
  };
}
