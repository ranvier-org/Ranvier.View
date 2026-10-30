import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const directory = dirname(fileURLToPath(import.meta.url));
const root = resolve(directory, 'generated');
const dependencies = process.env.NATIVE_API_BROWSER_PACKAGE || resolve(directory, 'package.json');
const { chromium } = createRequire(dependencies)('@playwright/test');
const executableIndex = process.argv.indexOf('--executable');
const executablePath = executableIndex < 0 ? undefined : process.argv[executableIndex + 1];
if (executableIndex >= 0 && !executablePath) throw new Error('--executable requires a browser path');
const expectedCrash = process.argv.includes('--expect-crash');
const disableAroundFinal = process.argv.includes('--disable-around-final');
const server = createServer(async (request, response) => {
  try {
    const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    if (name === '/') {
      response.setHeader('Content-Type', 'text/html');
      response.end('<!doctype html><html><head><link rel="icon" href="data:,"></head><body></body></html>');
      return;
    }
    const file = resolve(root, '.' + name);
    if (!file.startsWith(root + sep)) throw new Error('Outside fixture output');
    response.setHeader('Content-Type', extname(file) === '.js' ? 'text/javascript' : 'text/plain');
    if (file.endsWith('CounterStyleRules.js')) {
      let source = (await readFile(file, 'utf8')).replaceAll('\r\n', '\n');
      const detachedMutation = String.raw`fixedRule.prefix = "\"Detached\"";`;
      if (!source.includes(detachedMutation)) throw new Error('Detached counter mutation diagnostic anchor changed');
      source = source.replace(detachedMutation, `console.log('RANVIER_C19_DETACHED_SETTER');\n${detachedMutation}`);
      if (disableAroundFinal) {
      const mutation = String.raw`fallback.symbols = "\"ChangedFallback\"";
        fixedRule.fallback = fallback.name;`;
      if (!source.includes(mutation)) throw new Error('Counter mutation diagnostic anchor changed');
      source = source.replace(mutation, `const c19Sheet = fallback.parentStyleSheet;
        const c19Disabled = c19Sheet.disabled;
        const c19Owner = c19Sheet.ownerNode;
        try {
          c19Sheet.disabled = true;
          c19Owner.ownerDocument.body.getBoundingClientRect();
          ${mutation}
        } finally { c19Sheet.disabled = c19Disabled; }
        if (fallback.parentStyleSheet !== c19Sheet || c19Sheet.ownerNode !== c19Owner) throw new Error('Lost counter identities');`);
      }
      response.end(source);
    } else {
      response.end(await readFile(file));
    }
  } catch {
    response.statusCode = 404;
    response.end();
  }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
let browser;
try {
  browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) });
  const version = browser.version();
  const page = await browser.newPage();
  let crashed = false;
  let reachedDetachedMutation = false;
  page.on('console', message => {
    if (message.text() === 'RANVIER_C19_DETACHED_SETTER') reachedDetachedMutation = true;
  });
  page.on('crash', () => { crashed = true; });
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  try {
    const result = await page.evaluate(async () => {
      const module = await import('/CounterStyleRules.js');
      return module.reproduceDetachedMutation(document, () => document.body.getBoundingClientRect());
    });
    console.log(JSON.stringify({ version, outcome: 'completed', result, disableAroundFinal }));
    if (expectedCrash) throw new Error('Previously crashing sequence completed; review this browser profile and remove its qualification');
  } catch (error) {
    if (!crashed) throw error;
    if (!reachedDetachedMutation) throw new Error('Renderer crashed before the detached mutation checkpoint');
    console.log(JSON.stringify({ version, outcome: 'renderer-crash', expectedCrash, disableAroundFinal, reachedDetachedMutation }));
    if (!expectedCrash) throw error;
  }
} finally {
  if (browser) await browser.close();
  await new Promise(resolve => server.close(resolve));
}
