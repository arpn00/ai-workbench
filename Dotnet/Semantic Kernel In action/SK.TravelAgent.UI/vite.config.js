import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// https://vitejs.dev/config/
// Vite automatically loads .env, .env.local, .env.[mode] files from the project root.
// Variables prefixed with VITE_ are exposed to the client via import.meta.env.
// The loadEnv() call here also reads them into the config so the proxy target
// can be driven by the same .env value (VITE_API_BASE_URL) without duplication.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react()],
    server: {
      port: 5173,
      // In development all /api/* requests are proxied to the .NET backend,
      // which avoids browser CORS restrictions entirely.
      proxy: {
        '/api': {
          target: env.VITE_API_BASE_URL || 'https://localhost:7001',
          changeOrigin: true,
          secure: false, // accept the dev self-signed TLS certificate
        },
      },
    },
  };
});
