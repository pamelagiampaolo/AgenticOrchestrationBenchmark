import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/public/t1/find-products-below-stock.test.ts"],
    exclude: ["tests/reserved/**"],
  },
});
