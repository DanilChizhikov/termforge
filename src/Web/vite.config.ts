import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const backendOrigin = 'http://127.0.0.1:5023';
const outDir = fileURLToPath(new URL('../Api/Entrypoint/wwwroot', import.meta.url));

export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: backendOrigin,
        ws: true,
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir,
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
