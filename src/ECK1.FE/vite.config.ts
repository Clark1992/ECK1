import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  css: {
    devSourcemap: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/testplatform/hubs/scenarios': {
        target: 'http://testplatform.localhost:30200',
        changeOrigin: true,
        ws: true,
        rewrite: (path) => path.replace(/^\/testplatform/, ''),
      },
      '/testplatform': {
        target: 'http://testplatform.localhost:30200',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/testplatform/, ''),
      },
      '/api/hubs/realtime': {
        target: 'http://api.localhost:30200',
        changeOrigin: true,
        ws: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
      '/api': {
        target: 'http://api.localhost:30200',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
});
