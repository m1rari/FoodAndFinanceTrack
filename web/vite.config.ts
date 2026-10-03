import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
//
// Демо-режим для локального просмотра интерфейса без backend:
//   VITE_MOCK_API=1 npm run dev
// Он подменяет `../api/client` мок-реализацией (src/api/mock.ts).
const mock = process.env.VITE_MOCK_API === '1'

export default defineConfig({
  plugins: [react()],
  resolve: mock
    ? {
        alias: [{ find: /^\.\.\/api\/client$/, replacement: new URL('./src/api/mock.ts', import.meta.url).pathname }],
      }
    : {},
  build: {
    target: 'es2020',
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules')) {
            return 'vendor'
          }
        },
      },
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5177',
        changeOrigin: true,
      },
    },
  },
})
