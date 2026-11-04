import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/public/t2/receive-stock.test.ts"],
    exclude: ["tests/reserved/**"],
  },
});
