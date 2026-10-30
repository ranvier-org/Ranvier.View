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
  const originalViewport = page.viewportSize();
  await page.exposeFunction('__nativeApiResizeViewport', async (width, height) => {
    await page.setViewportSize({ width, height });
  });
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  const checks = await page.evaluate(async () => {
    try {
      return await (await import('/Browser.js')).run();
    } catch (error) {
      throw new Error(JSON.stringify({
        type: error?.constructor?.name,
        name: error?.name,
        message: error?.message,
        stack: error?.stack,
        detail: error?.Message,
        fields: Object.fromEntries(Object.getOwnPropertyNames(error).filter(key => key !== 'stack').map(key => [key, error[key]])),
      }));
    }
  });
  if (checks.cssSupport !== 32 || checks.cssEscaping !== 45 || checks.cssOracle !== 196609 || checks.tables !== 30 || checks.viewTables < 29 || checks.reflection < 200 || checks.namespaces < 24 || checks.hyperlinks < 60 || checks.cssom < 58 || checks.svgCssom < 31 || checks.construction < 40) throw new Error(`Insufficient API assertions: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API D10 PASS assertions=${checks.tables} engine=chromium`);
  if (checks.timing !== 178 || checks.timingDeterministic !== 179) throw new Error('User timing assertions did not run');
  console.log(`BROWSER_API H02 PASS assertions=${checks.timing} deterministic=${checks.timingDeterministic} engine=chromium`);
  console.log(`BROWSER_API D10_VIEW PASS assertions=${checks.viewTables} engine=chromium`);
  if (checks.viewHyperlinks !== 50) throw new Error('View hyperlink authoring assertions did not run');
  console.log(`BROWSER_API D11_VIEW_HYPERLINKS PASS assertions=${checks.viewHyperlinks} engine=chromium`);
  console.log(`BROWSER_API C05 PASS assertions=${checks.cssEscaping} oracle=${checks.cssOracle} engine=chromium`);
  console.log(`BROWSER_API C03 PASS assertions=${checks.cssSupport} engine=chromium`);
  if (checks.queryContextChecks !== 2) throw new Error('CSS query provider context checks did not run');
  console.log(`BROWSER_API C03 CONTEXT PASS assertions=${checks.queryContextChecks} engine=chromium`);
  if (checks.computedStyles !== 42) throw new Error('Computed-style assertions did not run');
  console.log(`BROWSER_API C01_C02 PASS assertions=${checks.computedStyles} engine=chromium`);
  if (checks.typedStyles !== 39) throw new Error('Typed CSS declaration assertions did not run');
  if (checks.fragmentRectangles < 60) throw new Error('Fragment rectangle assertions did not run');
  console.log(`BROWSER_API G06 PASS assertions=${checks.fragmentRectangles} engine=chromium`);
  if (checks.fontRules < 35 || checks.transitionRules < 21) throw new Error('Font/transition rule assertions did not run');
  console.log(`BROWSER_API C18 PASS assertions=${checks.fontRules} engine=chromium`);
  console.log(`BROWSER_API C18 CAPABILITY fontWidthAlias=${checks.fontWidthAlias} engine=chromium`);
  console.log(`BROWSER_API C22 PASS assertions=${checks.transitionRules} engine=chromium`);
  if (checks.conditionalRules !== 44) throw new Error('Conditional rule assertions did not run');
  console.log(`BROWSER_API C09 PASS assertions=${checks.conditionalRules} engine=chromium`);
  if (checks.cascadeRules !== 93) throw new Error('Cascade rule assertions did not run for the pinned Chromium profile');
  console.log(`BROWSER_API C10 PASS assertions=${checks.cascadeRules} engine=chromium`);
  console.log(`BROWSER_API C10 CAPABILITY scopeRules=${checks.scopeRules} nestedDeclarations=${checks.nestedDeclarations} importContentsReadable=${checks.importContentsReadable} engine=chromium`);
  const restoredViewport = page.viewportSize();
  if (restoredViewport.width !== originalViewport.width || restoredViewport.height !== originalViewport.height) {
    throw new Error('Conditional rule fixture did not restore the original viewport');
  }
  console.log(`BROWSER_API C04 PASS assertions=${checks.typedStyles} engine=chromium`);
  if (checks.computedContextChecks !== 2) throw new Error('Computed-style context checks did not run');
  console.log(`BROWSER_API C01_C02 CONTEXT PASS assertions=${checks.computedContextChecks} engine=chromium`);
  console.log(`BROWSER_API D11 PASS assertions=${checks.reflection} engine=chromium`);
  console.log(`BROWSER_API D09 PASS assertions=${checks.namespaces} engine=chromium`);
  console.log(`BROWSER_API CSSOM PASS assertions=${checks.cssom} engine=chromium`);
  if (checks.ruleMutations !== 131) throw new Error('Nested rule mutation assertions did not run');
  console.log(`BROWSER_API C08 PASS assertions=${checks.ruleMutations} engine=chromium`);
  console.log(`BROWSER_API SVG_CSSOM PASS assertions=${checks.svgCssom} engine=chromium`);
  console.log(`BROWSER_API HYPERLINKS PASS assertions=${checks.hyperlinks} engine=chromium`);
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
