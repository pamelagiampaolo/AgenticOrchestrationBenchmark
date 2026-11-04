import { beforeEach, describe, expect, test } from "vitest";

import {
  InventoryManagement,
  type InitialProduct,
} from "../../../src/index.js";

describe("T1 - findProductsBelowStock", () => {
  let initialProducts: InitialProduct[];
  let inventory: InventoryManagement;

  beforeEach(() => {
    initialProducts = [
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
      { productId: "P-003", name: "Monitor", availableQuantity: 10 },
    ];
    inventory = new InventoryManagement(initialProducts);
  });

  test("T1-P01 returns products strictly below threshold in product identifier order", () => {
    expect(inventory.findProductsBelowStock(6)).toEqual([
      { productId: "P-001", name: "Tastiera", availableQuantity: 0 },
      { productId: "P-002", name: "Mouse", availableQuantity: 5 },
    ]);
  });

  test("T1-P02 returns an empty collection for threshold zero", () => {
    expect(inventory.findProductsBelowStock(0)).toEqual([]);
  });

  test("T1-P03 rejects a negative threshold without changing stock", () => {
    const quantitiesBefore = readQuantities(inventory);

    expect(() => inventory.findProductsBelowStock(-1)).toThrowError(
      expect.objectContaining({ code: "INVALID_THRESHOLD" }),
    );
    expect(readQuantities(inventory)).toEqual(quantitiesBefore);
  });

  test("T1-P04 rejects a decimal threshold without changing stock", () => {
    const quantitiesBefore = readQuantities(inventory);

    expect(() => inventory.findProductsBelowStock(2.5)).toThrowError(
      expect.objectContaining({ code: "INVALID_THRESHOLD" }),
    );
    expect(readQuantities(inventory)).toEqual(quantitiesBefore);
  });
});

function readQuantities(inventory: InventoryManagement): number[] {
  return ["P-001", "P-002", "P-003"].map((productId) =>
    inventory.getAvailableQuantity(productId),
  );
}
