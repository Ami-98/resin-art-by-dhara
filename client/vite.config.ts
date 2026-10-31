import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// Builds directly into the ASP.NET Core wwwroot folder so a single server hosts both the SPA and /api
// (with server-side Open Graph meta injection via <!--RESINART_SEO_META-->).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: '../server/ResinArt.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5224',
      '/uploads': 'http://localhost:5224',
      '/sitemap.xml': 'http://localhost:5224',
      '/robots.txt': 'http://localhost:5224',
    },
  },
});
