import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
} from "../../src/index.js";

describe("InventoryManagement", () => {
  let initialProducts: InitialProduct[];

  beforeEach(() => {
    initialProducts = [
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
      { productId: "P-003", name: "Monitor", availableQuantity: 10 },
    ];
  });

  test("lists only product data ordered by product identifier", () => {
    const inventory = new InventoryManagement([
      initialProducts[2]!,
      initialProducts[0]!,
      initialProducts[1]!,
    ]);

    expect(inventory.listProducts()).toEqual([
      { productId: "P-001", name: "Tastiera" },
      { productId: "P-002", name: "Mouse" },
      { productId: "P-003", name: "Monitor" },
    ]);
  });

  test.each([
    ["P-001", 0],
    ["P-002", 5],
    ["P-003", 10],
  ])("returns the available quantity for %s", (productId, expectedQuantity) => {
    const inventory = new InventoryManagement(initialProducts);

    expect(inventory.getAvailableQuantity(productId)).toBe(expectedQuantity);
  });

  test("exposes a stable error code for an unknown product", () => {
    const inventory = new InventoryManagement(initialProducts);

    expect(() => inventory.getAvailableQuantity("P-999")).toThrowError(
      expect.objectContaining({ code: "PRODUCT_NOT_FOUND" }),
    );
  });

  test("copies initial and returned product data", () => {
    const inventory = new InventoryManagement(initialProducts);

    initialProducts[0]!.name = "Nome modificato";
    initialProducts[0]!.availableQuantity = 99;
    const listedProducts = inventory.listProducts();
    listedProducts[0]!.name = "Altra modifica";

    expect(inventory.listProducts()[0]).toEqual({
      productId: "P-001",
      name: "Tastiera",
    });
    expect(inventory.getAvailableQuantity("P-001")).toBe(0);
  });

  test("keeps data independent between instances", () => {
    const firstInventory = new InventoryManagement(initialProducts);
    const secondInventory = new InventoryManagement(initialProducts);

    const firstResult = firstInventory.listProducts();
    firstResult[1]!.name = "Nome modificato";

    expect(firstInventory.listProducts()[1]!.name).toBe("Mouse");
    expect(secondInventory.listProducts()[1]!.name).toBe("Mouse");
  });
});
