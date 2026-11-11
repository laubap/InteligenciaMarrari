import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { monitorPlugin } from './server/proxy';

export default defineConfig(({ mode }) => ({
  plugins: [react(), monitorPlugin({ ...loadEnv(mode, process.cwd(), ''), ...process.env })],
  server: { host: '127.0.0.1', port: 5173, strictPort: true },
  preview: { host: '127.0.0.1', port: 5173, strictPort: true },
}));
