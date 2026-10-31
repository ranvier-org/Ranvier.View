import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const require = createRequire(process.env.NATIVE_API_BROWSER_PACKAGE || path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../NativeApi.Conformance/package.json'));
const { chromium } = require('@playwright/test');
const root = process.cwd();
const server = createServer(async (request,response) => {
  try {
    const url = new URL(request.url,'http://localhost');
    if (url.pathname === '/') { response.setHeader('Content-Type','text/html'); response.end('<!doctype html><body><script type="module" src="/tests/Capabilities/generated/Browser.js"></script>'); return; }
    const file = path.resolve(root,'.'+decodeURIComponent(url.pathname));
    if (!file.startsWith(root+path.sep)) { response.writeHead(403).end(); return; }
    response.setHeader('Content-Type','text/javascript'); response.end(await readFile(file));
  } catch (error) { response.writeHead(404).end(String(error)); }
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser = await chromium.launch({headless:true});
try {
  const page = await browser.newPage();
  page.on('pageerror',error=>console.error(error));
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  await page.waitForFunction(()=>window.__capabilityResult,{timeout:30000});
  const result=await page.evaluate(()=>window.__capabilityResult);
  await writeFile('artifacts/h07/browser-result.json',JSON.stringify(result,null,2));
  console.log(JSON.stringify(result));
  if(result.error || !result.passed) process.exitCode=1;
} finally { await browser.close(); await new Promise(resolve=>server.close(resolve)); }
