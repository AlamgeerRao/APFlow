// Static-file host for the built SPA on Azure App Service (NODE|24-lts).
// Azure sets PORT; App Service's own reverse proxy/HTTPS termination handles TLS.
import express from 'express';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const distDir = path.join(__dirname, 'dist');
const port = process.env.PORT || 8080;

const app = express();
app.use(express.static(distDir));

// SPA fallback: any request express.static didn't already resolve (client-side
// routing via react-router-dom) resolves to index.html rather than a 404.
app.use((req, res) => {
  res.sendFile(path.join(distDir, 'index.html'));
});

app.listen(port, () => {
  console.log(`APFlow.Web listening on port ${port}`);
});
