import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

const backend = 'http://localhost:8420'

export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: '../src/InformadoRSS/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': backend,
      '/health': backend,
    },
  },
})
