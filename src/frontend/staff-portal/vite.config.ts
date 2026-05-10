import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

const staffPort = Number(process.env.STAFF_PORT ?? process.env.PORT ?? 3000);

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': resolve(__dirname, './src'),
    },
  },
  server: {
    host: '0.0.0.0',
    port: staffPort,
    proxy: {
      // Route each API area to its local microservice in development.
      '/api/sfa': {
        target: 'http://localhost:5010',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api\/sfa/, ''),
      },
      '/api/css': {
        target: 'http://localhost:5020',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api\/css/, ''),
      },
      '/api/marketing': {
        target: 'http://localhost:5030',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api\/marketing/, ''),
      },
      '/api/analytics': {
        target: 'http://localhost:5040',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api/ai': {
        target: 'http://localhost:5050',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api/notifications': {
        target: 'http://localhost:5070',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api/notification-templates': {
        target: 'http://localhost:5070',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api/notification-preferences': {
        target: 'http://localhost:5070',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
  build: {
    sourcemap: true,
    outDir: 'dist',
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      thresholds: {
        lines: 80,
        branches: 80,
        functions: 80,
        statements: 80,
      },
    },
  },
});
