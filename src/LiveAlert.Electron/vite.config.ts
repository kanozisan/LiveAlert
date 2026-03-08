import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  base: './',
  root: '.',
  build: {
    outDir: 'dist/renderer',
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: path.resolve(__dirname, 'src/renderer/index.html'),
        overlay: path.resolve(__dirname, 'src/overlay/index.html'),
      },
    },
  },
  resolve: {
    alias: {
      '@core': path.resolve(__dirname, 'src/core'),
    },
  },
});
