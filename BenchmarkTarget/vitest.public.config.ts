import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["tests/public/**/*.test.ts"],
    exclude: ["tests/reserved/**"],
  },
});
