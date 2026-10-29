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
  const checks = await page.evaluate(async () => {
    try {
      return await (await import('/Browser.js')).run();
    } catch (error) {
      throw new Error(JSON.stringify({
        type: error?.constructor?.name,
        message: error?.message,
        detail: error?.Message,
        fields: Object.fromEntries(Object.getOwnPropertyNames(error).filter(key => key !== 'stack').map(key => [key, error[key]])),
      }));
    }
  });
  if (checks.tables !== 30 || checks.reflection < 200 || checks.namespaces < 24 || checks.cssom < 58 || checks.svgCssom < 31 || checks.construction < 40) throw new Error(`Insufficient API assertions: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API D10 PASS assertions=${checks.tables} engine=chromium`);
  console.log(`BROWSER_API D11 PASS assertions=${checks.reflection} engine=chromium`);
  console.log(`BROWSER_API D09 PASS assertions=${checks.namespaces} engine=chromium`);
  console.log(`BROWSER_API CSSOM PASS assertions=${checks.cssom} engine=chromium`);
  console.log(`BROWSER_API SVG_CSSOM PASS assertions=${checks.svgCssom} engine=chromium`);
  console.log(`BROWSER_API C07 PASS assertions=${checks.construction} engine=chromium`);
  await page.locator('#native-api-svg').focus();
  await page.keyboard.press('Tab');
  if (!await page.locator('#native-api-math').evaluate(element => element === element.ownerDocument.activeElement)) {
    throw new Error('Tab did not move from SVG to MathML in document order');
  }
  console.log('BROWSER_API D09 TAB PASS engine=chromium');
} finally {
  if (browser) await browser.close();
  await new Promise(resolve => server.close(resolve));
}
