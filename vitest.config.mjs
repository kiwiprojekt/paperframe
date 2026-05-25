import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests/js/**/*.test.js'],
    coverage: {
      provider: 'v8',
      include: ['paperframe-server/StaticAssets/app.js'],
      reporter: ['text', 'lcov']
    }
  }
});
