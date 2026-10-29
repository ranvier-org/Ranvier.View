import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const root = resolve(dirname(fileURLToPath(import.meta.url)), 'generated');
const dependencies = process.env.NATIVE_API_BROWSER_PACKAGE || resolve(dirname(fileURLToPath(import.meta.url)), 'package.json');
const { chromium } = createRequire(dependencies)('@playwright/test');
const server = createServer(async (request, response) => {
  try {
    const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    if (name === '/') {
      response.setHeader('Content-Type', 'text/html');
      response.end('<!doctype html><html><body></body></html>');
      return;
    }
    const file = resolve(root, '.' + name);
    if (!file.startsWith(root + sep)) throw new Error('Outside fixture output');
    response.setHeader('Content-Type', extname(file) === '.js' ? 'text/javascript' : 'text/plain');
    response.end(await readFile(file));
  } catch {
    response.statusCode = 404;
    response.end();
  }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
let browser;
try {
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  const checks = await page.evaluate(async () => (await import('/Browser.js')).run());
  if (!Number.isInteger(checks) || checks < 30) throw new Error(`Insufficient table assertions: ${checks}`);
  console.log(`BROWSER_API D10 PASS assertions=${checks} engine=chromium`);
} finally {
  if (browser) await browser.close();
  await new Promise(resolve => server.close(resolve));
}
