import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import { inflateSync } from 'node:zlib';

const root = resolve(dirname(fileURLToPath(import.meta.url)), 'generated');
const assets = { '/Ahem.ttf': resolve(dirname(fileURLToPath(import.meta.url)), 'Assets', 'Ahem.ttf') };
const customPropertiesOnly = process.argv.includes('--custom-properties-only');
const styleValuesOnly = process.argv.includes('--style-values-only');
const localesOnly = process.argv.includes('--locales-only');
const styleMapsOnly = process.argv.includes('--style-maps-only');
const geometryOnly = process.argv.includes('--geometry-only');
const matrixOnly = process.argv.includes('--matrix-only');
const transformsOnly = process.argv.includes('--transforms-only');
const svgOnly = process.argv.includes('--svg-only');
const svgSurfacesOnly = process.argv.includes('--svg-surfaces-only');
const resourcesOnly = process.argv.includes('--resources-only');
const svgPaintOnly = process.argv.includes('--svg-paint-only');
const inputStateOnly = process.argv.includes('--input-state-only');
const framesOnly = process.argv.includes('--frames-only');
const idleOnly = process.argv.includes('--idle-only');
const caretOnly = process.argv.includes('--caret-only');
const viewportOnly = process.argv.includes('--viewport-only');
const formsOnly = process.argv.includes('--forms-only');
const dialogsOnly = process.argv.includes('--dialogs-only');
const urlsOnly = process.argv.includes('--urls-only');
const segmentsOnly = process.argv.includes('--segments-only');
const binaryOnly = process.argv.includes('--binary-only');
const cryptoOnly = process.argv.includes('--crypto-only');
const highlightsOnly = process.argv.includes('--highlights-only');
const submissionsOnly = process.argv.includes('--submissions-only');
const domModelOnly = process.argv.includes('--dom-model-only');
const dependencies = process.env.NATIVE_API_BROWSER_PACKAGE || resolve(dirname(fileURLToPath(import.meta.url)), 'package.json');
const { chromium } = createRequire(dependencies)('@playwright/test');
const server = createServer(async (request, response) => {
  try {
    const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    if (name === '/fixtures/Ahem.ttf') {
      response.setHeader('Content-Type', 'font/ttf');
      response.end(await readFile(resolve(process.env.RANVIER_WEBKIT_SOURCE || 'C:/wk/src', 'Tools/TestWebKitAPI/Resources/cocoa/Ahem.ttf')));
      return;
    }
    if (name === '/') {
      response.setHeader('Content-Type', 'text/html');
      response.end('<!doctype html><html><body></body></html>');
      return;
    }
    const file = assets[name] ?? resolve(root, '.' + name);
    if (!assets[name] && !file.startsWith(root + sep)) throw new Error('Outside fixture output');
    response.setHeader('Content-Type', extname(file) === '.js' ? 'text/javascript' : extname(file) === '.ttf' ? 'font/ttf' : 'text/plain');
    response.end(await readFile(file));
  } catch {
    response.statusCode = 404;
    response.end();
  }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
// I12 needs page activation changes, which only new headless Chromium (channel 'chromium') delivers.
const runInputState = async () => {
  const inputBrowser = await chromium.launch({ headless: true, channel: 'chromium' });
  try {
    const context = await inputBrowser.newContext();
    const page = await context.newPage();
    const other = await context.newPage();
    await page.exposeFunction('__nativeApiSetActive', async active => {
      await (active ? page : other).bringToFront();
      await page.waitForFunction(value => document.hasFocus() === value, active, { polling: 20 });
    });
    await page.exposeFunction('__nativeApiMouse', async (action, x, y) => {
      if (action === 'move') await page.mouse.move(x, y);
      else if (action === 'down') await page.mouse.down();
      else await page.mouse.up();
    });
    await page.exposeFunction('__nativeApiTab', async () => { await page.keyboard.press('Tab'); });
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    // Playwright emulates focus for every page; navigation restores the emulation, so it is disabled afterwards.
    const cdp = await context.newCDPSession(page);
    await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: false });
    await page.bringToFront();
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runInputState(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.inputState !== 39) throw new Error(`Input state assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API I12 PASS assertions=${result.inputState} engine=chromium headless=new`);
  } finally {
    await inputBrowser.close();
  }
};
// G02/G12 drive the viewport with device-metrics emulation, read paint size from a device-scale screenshot and
// change activation by minimizing and restoring the window, which blurs and focuses it under emulated metrics.
const runViewport = async () => {
  const viewportBrowser = await chromium.launch({ headless: true });
  try {
    const context = await viewportBrowser.newContext();
    const page = await context.newPage();
    const cdp = await context.newCDPSession(page);
    const { windowId } = await cdp.send('Browser.getWindowForTarget');
    await cdp.send('Emulation.setDeviceMetricsOverride', { width: 320, height: 240, deviceScaleFactor: 1, mobile: false });
    await page.exposeFunction('__nativeApiSetActive', async active => {
      await cdp.send('Emulation.setFocusEmulationEnabled', { enabled: false });
      await cdp.send('Browser.setWindowBounds', { windowId, bounds: { windowState: active ? 'normal' : 'minimized' } });
      await page.waitForFunction(value => document.hasFocus() === value, active, { polling: 50 });
    });
    await page.exposeFunction('__nativeApiViewport', async (width, height, deviceScaleFactor) => {
      await cdp.send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor, mobile: false });
    });
    await page.exposeFunction('__nativeApiPaintSize', async () => {
      const png = Buffer.from((await cdp.send('Page.captureScreenshot', { format: 'png' })).data, 'base64');
      return [png.readUInt32BE(16), png.readUInt32BE(20)];
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    await page.bringToFront();
    const result = await page.evaluate(async () => (await import('/Browser.js')).runViewport());
    if (result.viewport !== VIEWPORT_ASSERTIONS) throw new Error(`Viewport assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API G02/G12 PASS assertions=${result.viewport} engine=chromium`);
    const scroll = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runViewportScroll(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (scroll.scroll !== SCROLL_ASSERTIONS) throw new Error(`Viewport scrolling assertions did not run: ${JSON.stringify(scroll)}`);
    console.log(`BROWSER_API G05 PASS assertions=${scroll.scroll} engine=chromium`);
    // G03 drives the page scale with Emulation.setPageScaleFactor and pans with a trusted wheel.
    await page.exposeFunction('__nativeApiPageScale', async pageScaleFactor => { await cdp.send('Emulation.setPageScaleFactor', { pageScaleFactor }); });
    await page.exposeFunction('__nativeApiWheel', async (x, y, deltaX, deltaY) => { await page.mouse.move(x, y); await page.mouse.wheel(deltaX, deltaY); });
    const visual = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runVisualViewport(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (visual.visualViewport !== VISUAL_VIEWPORT_ASSERTIONS) throw new Error(`Visual viewport assertions did not run: ${JSON.stringify(visual)}`);
    if (visual.navigation !== 3) throw new Error(`Visual viewport navigation assertions did not run: ${JSON.stringify(visual)}`);
    console.log(`BROWSER_API G03 PASS assertions=${visual.visualViewport} engine=chromium`);
    console.log(`BROWSER_API G03 NAVIGATION PASS assertions=${visual.navigation} engine=chromium`);
  } finally {
    await viewportBrowser.close();
  }
};
const VIEWPORT_ASSERTIONS = 44;
const SCROLL_ASSERTIONS = 278;
const VISUAL_VIEWPORT_ASSERTIONS = 66;
// D13 runs in its own page: the fixture forms submit into a sink frame of that page.
const runForms = async () => {
  const formsBrowser = await chromium.launch({ headless: true });
  try {
    const page = await formsBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return (await import('/Browser.js')).runForms(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.forms !== FORMS_ASSERTIONS) throw new Error(`Form assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API D13 PASS assertions=${result.forms} engine=chromium`);
  } finally {
    await formsBrowser.close();
  }
};
const FORMS_ASSERTIONS = 69;
// D16 runs the dialog fixture at 800x600 with trusted Playwright keyboard and mouse input.
const runDialogs = async () => {
  const dialogsBrowser = await chromium.launch({ headless: true });
  try {
    const page = await dialogsBrowser.newPage({ viewport: { width: 800, height: 600 } });
    await page.exposeFunction('__nativeApiKey', async key => { await page.keyboard.press(key); });
    await page.exposeFunction('__nativeApiClick', async (x, y) => { await page.mouse.click(x, y); });
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runDialogs(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.dialogs !== DIALOG_ASSERTIONS) throw new Error(`Dialog assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API D16 PASS assertions=${result.dialogs} engine=chromium`);
  } finally {
    await dialogsBrowser.close();
  }
};
const DIALOG_ASSERTIONS = 143;
// H13 runs the URL fixture in its own page.
const runUrls = async () => {
  const urlsBrowser = await chromium.launch({ headless: true });
  try {
    const page = await urlsBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return (await import('/Browser.js')).runUrls(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.urls !== URL_ASSERTIONS) throw new Error(`URL assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API H13 PASS assertions=${result.urls} engine=chromium`);
  } finally {
    await urlsBrowser.close();
  }
};
const URL_ASSERTIONS = 189;
// T06 runs the text segmentation fixture in its own page.
const runSegments = async () => {
  const segmentsBrowser = await chromium.launch({ headless: true });
  try {
    const page = await segmentsBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return (await import('/Browser.js')).runSegments(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.segments !== SEGMENT_ASSERTIONS) throw new Error(`Text segmentation assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API T06 PASS assertions=${result.segments} engine=chromium`);
  } finally {
    await segmentsBrowser.close();
  }
};
const SEGMENT_ASSERTIONS = 219;
// H11 runs the binary value fixture in its own page.
const runBinary = async () => {
  const binaryBrowser = await chromium.launch({ headless: true });
  try {
    const page = await binaryBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runBinary(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.binary !== BINARY_ASSERTIONS) throw new Error(`Binary value assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API H11 PASS assertions=${result.binary} engine=chromium`);
  } finally {
    await binaryBrowser.close();
  }
};
const BINARY_ASSERTIONS = 102;
// B22 runs the crypto fixture on http://127.0.0.1 (a secure context) and on http://insecure.test, which the browser
// resolves to the same server but treats as insecure.
const runCrypto = async () => {
  const cryptoBrowser = await chromium.launch({ headless: true, args: ['--host-resolver-rules=MAP insecure.test 127.0.0.1'] });
  try {
    const page = await cryptoBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runCrypto(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.crypto !== CRYPTO_ASSERTIONS) throw new Error(`Crypto assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API B22 PASS assertions=${result.crypto} engine=chromium`);
    const insecure = await cryptoBrowser.newPage();
    await insecure.goto(`http://insecure.test:${server.address().port}/`);
    const secure = await insecure.evaluate(() => isSecureContext);
    if (secure) throw new Error('http://insecure.test is a secure context');
    const plain = await insecure.evaluate(async () => { try { return await (await import('/Browser.js')).runCryptoInsecure(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (plain.crypto !== CRYPTO_INSECURE_ASSERTIONS) throw new Error(`Insecure crypto assertions did not run: ${JSON.stringify(plain)}`);
    console.log(`BROWSER_API B22 INSECURE PASS assertions=${plain.crypto} engine=chromium`);
  } finally {
    await cryptoBrowser.close();
  }
};
const CRYPTO_ASSERTIONS = 155;
const CRYPTO_INSECURE_ASSERTIONS = 4;
// T03 runs the custom highlight fixture in its own page.
const runHighlights = async () => {
  const highlightsBrowser = await chromium.launch({ headless: true });
  try {
    const page = await highlightsBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return (await import('/Browser.js')).runHighlights(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.highlights !== HIGHLIGHT_ASSERTIONS) throw new Error(`Highlight assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API T03 PASS assertions=${result.highlights} engine=chromium`);
  } finally {
    await highlightsBrowser.close();
  }
};
const HIGHLIGHT_ASSERTIONS = 35;
// D14 runs the form submission fixture in its own page; the provider keeps the page in place.
const runSubmissions = async () => {
  const submissionsBrowser = await chromium.launch({ headless: true });
  try {
    const page = await submissionsBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runSubmissions(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.submissions !== SUBMISSION_ASSERTIONS) throw new Error(`Form submission assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API D14 PASS assertions=${result.submissions} engine=chromium`);
  } finally {
    await submissionsBrowser.close();
  }
};
const SUBMISSION_ASSERTIONS = 26;
// A12 runs the frame fixture on the manual scheduler and on the Window's frames, timers and microtasks.
const runFrames = async () => {
  const framesBrowser = await chromium.launch({ headless: true });
  try {
    const page = await framesBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runFrames(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.frames !== 18 || result.manualFrames !== 18) throw new Error(`Frame assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API A12 PASS assertions=${result.frames} manual=${result.manualFrames} engine=chromium`);
  } finally {
    await framesBrowser.close();
  }
};
// G09 hit-tests the caret fixture under device-metrics emulation in new headless Chromium.
const runCaret = async () => {
  const caretBrowser = await chromium.launch({ headless: true, channel: 'chromium' });
  try {
    const context = await caretBrowser.newContext({ viewport: { width: 320, height: 240 } });
    const page = await context.newPage();
    const cdp = await context.newCDPSession(page);
    await page.exposeFunction('__nativeApiViewport', async (width, height, deviceScaleFactor) => {
      await cdp.send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor, mobile: false });
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runCaret(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.caret !== CARET_ASSERTIONS) throw new Error(`Caret assertions did not run: ${JSON.stringify(result)}`);
    if (!caretBrowser.version().startsWith(result.browser.split('.')[0] + '.')) throw new Error(`Oracle ${result.browser}, running ${caretBrowser.version()}`);
    console.log(`BROWSER_API G09 PASS assertions=${result.caret} engine=chromium headless=new version=${caretBrowser.version()}`);
  } finally {
    await caretBrowser.close();
  }
};
const CARET_ASSERTIONS = 1537;
// H01 runs the idle fixture on the manual scheduler and on the Window's idle callbacks and frames.
const runIdle = async () => {
  const idleBrowser = await chromium.launch({ headless: true });
  try {
    const page = await idleBrowser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    const result = await page.evaluate(async () => { try { return await (await import('/Browser.js')).runIdle(); } catch (error) { return { error: String(error?.message ?? error) }; } });
    if (result.idle !== IDLE_ASSERTIONS || result.manualIdle !== IDLE_ASSERTIONS || result.available !== true) throw new Error(`Idle assertions did not run: ${JSON.stringify(result)}`);
    console.log(`BROWSER_API H01 PASS assertions=${result.idle} manual=${result.manualIdle} capability=available engine=chromium`);
  } finally {
    await idleBrowser.close();
  }
};
const IDLE_ASSERTIONS = 14;
let browser;
try {
  if (inputStateOnly) { await runInputState(); process.exitCode = 0; }
  else if (viewportOnly) { await runViewport(); process.exitCode = 0; }
  else if (formsOnly) { await runForms(); process.exitCode = 0; }
  else if (dialogsOnly) { await runDialogs(); process.exitCode = 0; }
  else if (urlsOnly) { await runUrls(); process.exitCode = 0; }
  else if (segmentsOnly) { await runSegments(); process.exitCode = 0; }
  else if (binaryOnly) { await runBinary(); process.exitCode = 0; }
  else if (cryptoOnly) { await runCrypto(); process.exitCode = 0; }
  else if (highlightsOnly) { await runHighlights(); process.exitCode = 0; }
  else if (submissionsOnly) { await runSubmissions(); process.exitCode = 0; }
  else if (framesOnly) { await runFrames(); process.exitCode = 0; }
  else if (idleOnly) { await runIdle(); process.exitCode = 0; }
  else if (caretOnly) { await runCaret(); process.exitCode = 0; }
  else {
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const originalViewport = page.viewportSize();
  await page.exposeFunction('__nativeApiResizeViewport', async (width, height) => {
    await page.setViewportSize({ width, height });
  });
  const pixelAt = async (x, y) => {
    const png = await page.screenshot({ clip: { x: Math.floor(x), y: Math.floor(y), width: 1, height: 1 }, scale: 'css' });
    const chunks = [];
    let colorType = 0;
    for (let offset = 8; offset < png.length;) {
      const length = png.readUInt32BE(offset), kind = png.toString('latin1', offset + 4, offset + 8);
      if (kind === 'IHDR') colorType = png[offset + 17];
      if (kind === 'IDAT') chunks.push(png.subarray(offset + 8, offset + 8 + length));
      offset += 12 + length;
    }
    const pixel = inflateSync(Buffer.concat(chunks));
    if (colorType !== 2 && colorType !== 6) throw new Error(`Unexpected PNG color type ${colorType}`);
    return [pixel[1], pixel[2], pixel[3]];
  };
  await page.exposeFunction('__nativeApiPixelAt', pixelAt);
  await page.exposeFunction('__nativeApiPaintAt', async (x, y) => {
    const png = await page.screenshot({ clip: { x: Math.floor(x), y: Math.floor(y), width: 1, height: 1 }, scale: 'css' });
    const chunks = [];
    let colorType = 0;
    for (let offset = 8; offset < png.length;) {
      const length = png.readUInt32BE(offset), kind = png.toString('latin1', offset + 4, offset + 8);
      if (kind === 'IHDR') colorType = png[offset + 17];
      if (kind === 'IDAT') chunks.push(png.subarray(offset + 8, offset + 8 + length));
      offset += 12 + length;
    }
    const pixel = inflateSync(Buffer.concat(chunks));
    if (colorType !== 2 && colorType !== 6) throw new Error(`Unexpected PNG color type ${colorType}`);
    return pixel[1] < 64 && pixel[2] < 64 && pixel[3] < 64;
  });
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  const checks = await page.evaluate(async focused => {
    try {
      const fixture = await import('/Browser.js');
      return await (focused === 'dom-model' ? fixture.runDomModel() : focused === 'resources' ? fixture.runResources() : focused === 'locales' ? fixture.runLocales() : focused === 'svg' ? fixture.runSvg() : focused === 'svg-surfaces' ? fixture.runSvgSurfaces() : focused === 'svg-paint' ? fixture.runSvgPaint() : focused === 'transforms' ? fixture.runTransforms() : focused === 'matrix' ? fixture.runMatrix() : focused === 'geometry' ? fixture.runGeometry() : focused === 'style-maps' ? fixture.runStyleMaps() : focused === 'style-values' ? fixture.runStyleValues() : focused === 'custom-properties' ? fixture.runCustomProperties() : { ...await fixture.runSvgSurfaces(), ...await fixture.runSvgPaint(), ...await fixture.runResources(), ...await fixture.run() });
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
  }, domModelOnly ? 'dom-model' : resourcesOnly ? 'resources' : localesOnly ? 'locales' : svgOnly ? 'svg' : svgSurfacesOnly ? 'svg-surfaces' : svgPaintOnly ? 'svg-paint' : transformsOnly ? 'transforms' : matrixOnly ? 'matrix' : geometryOnly ? 'geometry' : styleMapsOnly ? 'style-maps' : styleValuesOnly ? 'style-values' : customPropertiesOnly ? 'custom-properties' : 'all');
  const checkDomModel = () => {
    if (checks.traversal !== 62 || checks.mathml !== 116) throw new Error(`DOM model assertions did not run: ${JSON.stringify(checks)}`);
    if (checks.traversalReentry !== 'InvalidStateError/InvalidStateError/unread') throw new Error(`Unexpected traversal re-entry: ${checks.traversalReentry}`);
    console.log(`BROWSER_API D01 PASS assertions=${checks.traversal} reentry=${checks.traversalReentry} engine=chromium`);
    console.log(`BROWSER_API S13 PASS assertions=${checks.mathml} math=MathMLElement engine=chromium`);
  };
  if (domModelOnly) {
    checkDomModel();
  } else if (resourcesOnly) {
    if (checks.resources !== 29) throw new Error(`Resource shared assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API H05 PASS assertions=${checks.resources} engine=chromium`);
  } else if (localesOnly) {
    if (checks.locales !== 35 || checks.localeDeterministic !== 14 || checks.localeContext !== 6) throw new Error(`Locale preference assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API H06 PASS assertions=${checks.locales} deterministic=${checks.localeDeterministic} engine=chromium`);
    console.log(`BROWSER_API H06_DETERMINISTIC PASS assertions=${checks.localeDeterministic} engine=chromium`);
    console.log(`BROWSER_API H06 CONTEXT PASS assertions=${checks.localeContext} engine=chromium`);
    console.log(`BROWSER_API H06 PROVIDER ${JSON.stringify(checks)}`);
  } else if (svgPaintOnly) {
    if (checks.svgPaint !== 49 || checks.svgFilters !== 118 || checks.svgClipping !== 38 || checks.svgResources !== 45) throw new Error(`SVG paint assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API S08 PASS assertions=${checks.svgPaint} engine=chromium`);
    console.log(`BROWSER_API S09 PASS assertions=${checks.svgFilters} engine=chromium`);
    console.log(`BROWSER_API S10 PASS assertions=${checks.svgClipping} engine=chromium`);
    console.log(`BROWSER_API S11 PASS assertions=${checks.svgResources} engine=chromium`);
  } else if (svgSurfacesOnly) {
    if (checks.svgShapes !== 194 || checks.svgText !== 210 || checks.svgViews !== 115 || checks.svgViewArguments !== 3 || checks.svgAnimations !== 31) throw new Error(`SVG surfaces assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API S03 PASS assertions=${checks.svgShapes} engine=chromium`);
    console.log(`BROWSER_API S05 PASS assertions=${checks.svgText} engine=chromium`);
    console.log(`BROWSER_API S06 PASS assertions=${checks.svgViews} arguments=${checks.svgViewArguments} engine=chromium`);
    console.log(`BROWSER_API S07 PASS assertions=${checks.svgAnimations} engine=chromium`);
  } else if (svgOnly) {
    if(checks.svgGeometry!==126 || checks.svgValues!==361 || checks.svgContext!==54) throw new Error(`SVG shared/realm assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API S01_S02 PASS assertions=${checks.svgGeometry} engine=chromium`);
    console.log(`BROWSER_API S04_REACHABLE PASS assertions=${checks.svgValues} engine=chromium`);
    console.log(`BROWSER_API SVG CONTEXT PASS assertions=${checks.svgContext} engine=chromium`);
  } else if (transformsOnly) {
    if(checks.transformValues!==471 || checks.transformContext!==3) throw new Error(`Typed transform shared/context assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API C16 PASS assertions=${checks.transformValues} engine=chromium`);
    console.log(`BROWSER_API C16 CONTEXT PASS assertions=${checks.transformContext} engine=chromium`);
  } else if (matrixOnly) {
    if(checks.matrices!==710 || checks.matrixContext!==3) throw new Error(`Matrix shared/context assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API G08 PASS assertions=${checks.matrices} engine=chromium`);
    console.log(`BROWSER_API G08 CONTEXT PASS assertions=${checks.matrixContext} engine=chromium`);
  } else if (geometryOnly) {
    if(checks.geometry!==177 || checks.geometryContext!==3) throw new Error(`Geometry shared/context assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API G07 PASS assertions=${checks.geometry} engine=chromium`);
    console.log(`BROWSER_API G07 CONTEXT PASS assertions=${checks.geometryContext} engine=chromium`);
  } else if (styleMapsOnly) {
    if (checks.styleMaps !== 127 || checks.styleMapContext !== 3) throw new Error(`Typed-map shared/context assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API C13 PASS assertions=${checks.styleMaps} engine=chromium`);
    console.log(`BROWSER_API C13 CONTEXT PASS assertions=${checks.styleMapContext} engine=chromium`);
  } else if (styleValuesOnly) {
    if (checks.styleValues !== 99 || checks.styleValueContext !== 3) throw new Error(`Typed-style shared/context assertions did not run: ${JSON.stringify(checks)}`);
    console.log(`BROWSER_API C14 PASS assertions=${checks.styleValues} engine=chromium`);
    console.log(`BROWSER_API C14 CONTEXT PASS assertions=${checks.styleValueContext} engine=chromium`);
  } else {
  if (checks.customProperties !== 50 || checks.customPropertyContext !== 3) {
    throw new Error(`Custom-property shared/context assertions did not run: ${JSON.stringify(checks)}`);
  }
  console.log(`BROWSER_API C12 PASS assertions=${checks.customProperties} engine=chromium`);
  console.log(`BROWSER_API C12 CONTEXT PASS assertions=${checks.customPropertyContext} engine=chromium`);
  if (!customPropertiesOnly) {
  if (checks.locales !== 35 || checks.localeDeterministic !== 14 || checks.localeContext !== 6) throw new Error(`Locale preference assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API H06 PASS assertions=${checks.locales} deterministic=${checks.localeDeterministic} context=${checks.localeContext} engine=chromium`);
  console.log(`BROWSER_API H06_DETERMINISTIC PASS assertions=${checks.localeDeterministic} engine=chromium`);
  console.log(`BROWSER_API H06 CONTEXT PASS assertions=${checks.localeContext} engine=chromium`);
  if(checks.svgGeometry!==126 || checks.svgValues!==361 || checks.svgContext!==54) throw new Error(`SVG shared/realm assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API S01_S02 PASS assertions=${checks.svgGeometry} engine=chromium`);
  console.log(`BROWSER_API S04_REACHABLE PASS assertions=${checks.svgValues} engine=chromium`);
  console.log(`BROWSER_API SVG CONTEXT PASS assertions=${checks.svgContext} engine=chromium`);
  if (checks.svgPaint !== 49 || checks.svgFilters !== 118 || checks.svgClipping !== 38 || checks.svgResources !== 45) throw new Error(`SVG paint assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API S08 PASS assertions=${checks.svgPaint} engine=chromium`);
  console.log(`BROWSER_API S09 PASS assertions=${checks.svgFilters} engine=chromium`);
  console.log(`BROWSER_API S10 PASS assertions=${checks.svgClipping} engine=chromium`);
  console.log(`BROWSER_API S11 PASS assertions=${checks.svgResources} engine=chromium`);
  if (checks.svgShapes !== 194 || checks.svgText !== 210 || checks.svgViews !== 115 || checks.svgViewArguments !== 3 || checks.svgAnimations !== 31) throw new Error(`SVG surfaces assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API S03 PASS assertions=${checks.svgShapes} engine=chromium`);
  console.log(`BROWSER_API S05 PASS assertions=${checks.svgText} engine=chromium`);
  console.log(`BROWSER_API S06 PASS assertions=${checks.svgViews} arguments=${checks.svgViewArguments} engine=chromium`);
  console.log(`BROWSER_API S07 PASS assertions=${checks.svgAnimations} engine=chromium`);
  if (checks.resources !== 29) throw new Error(`Resource shared assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API H05 PASS assertions=${checks.resources} engine=chromium`);
  if(checks.transformValues!==471 || checks.transformContext!==3) throw new Error(`Typed transform shared/context assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API C16 PASS assertions=${checks.transformValues} engine=chromium`);
  console.log(`BROWSER_API C16 CONTEXT PASS assertions=${checks.transformContext} engine=chromium`);
  if(checks.matrices!==710 || checks.matrixContext!==3) throw new Error(`Matrix shared/context assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API G08 PASS assertions=${checks.matrices} engine=chromium`);
  console.log(`BROWSER_API G08 CONTEXT PASS assertions=${checks.matrixContext} engine=chromium`);
  if(checks.geometry!==177 || checks.geometryContext!==3) throw new Error(`Geometry shared/context assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API G07 PASS assertions=${checks.geometry} engine=chromium`);
  console.log(`BROWSER_API G07 CONTEXT PASS assertions=${checks.geometryContext} engine=chromium`);
  if (checks.styleMaps !== 127 || checks.styleMapContext !== 3) throw new Error(`Typed-map shared/context assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API C13 PASS assertions=${checks.styleMaps} engine=chromium`);
  console.log(`BROWSER_API C13 CONTEXT PASS assertions=${checks.styleMapContext} engine=chromium`);
  if (checks.styleValues !== 99 || checks.styleValueContext !== 3) throw new Error(`Typed-style shared/context assertions did not run: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API C14 PASS assertions=${checks.styleValues} engine=chromium`);
  console.log(`BROWSER_API C14 CONTEXT PASS assertions=${checks.styleValueContext} engine=chromium`);
  if (checks.cssSupport !== 32 || checks.cssEscaping !== 45 || checks.cssOracle !== 196609 || checks.tables !== 30 || checks.viewTables < 29 || checks.reflection < 200 || checks.namespaces < 24 || checks.hyperlinks < 60 || checks.cssom < 58 || checks.svgCssom < 31 || checks.construction < 40) throw new Error(`Insufficient API assertions: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API D10 PASS assertions=${checks.tables} engine=chromium`);
  if (checks.timing !== 180 || checks.timingDeterministic !== 181) throw new Error('User timing assertions did not run');
  console.log(`BROWSER_API H02 PASS assertions=${checks.timing} deterministic=${checks.timingDeterministic} engine=chromium`);
  if (checks.capabilities !== 42) throw new Error(`Capability shared assertions did not run: ${checks.capabilities}`);
  console.log(`BROWSER_API H07 PASS assertions=${checks.capabilities} engine=chromium`);
  if (checks.cancellation !== 56 || checks.cancellationInterop !== 12) throw new Error(`Cancellation assertions did not run: ${checks.cancellation}/${checks.cancellationInterop}`);
  console.log(`BROWSER_API I01 PASS assertions=${checks.cancellation} interop=${checks.cancellationInterop} engine=chromium`);
  if (checks.customEvents !== 45) throw new Error(`Custom event assertions did not run: ${checks.customEvents}`);
  console.log(`BROWSER_API I02 PASS assertions=${checks.customEvents} engine=chromium`);
  checkDomModel();
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
  if (checks.positionTry !== 44 || !checks.positionTryAvailable) throw new Error(`Position fallback proof incomplete: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API C21 PASS assertions=${checks.positionTry} engine=chromium`);
  console.log(`BROWSER_API C21 CAPABILITY positionTry=${checks.positionTryAvailable} engine=chromium`);
  if (checks.counterStyles !== 64) throw new Error(`Counter style proof incomplete: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API C19 PASS assertions=${checks.counterStyles} engine=chromium`);
  console.log(`BROWSER_API C19 CAPABILITY speakAs=${checks.counterSpeakAs} engine=chromium`);
  if (checks.keyframeRules !== 82 || !checks.keyframeLength || checks.invalidKeyTextError !== 'SyntaxError') throw new Error(`Keyframe rule proof incomplete: ${JSON.stringify(checks)}`);
  console.log(`BROWSER_API C11 PASS assertions=${checks.keyframeRules} engine=chromium`);
  console.log(`BROWSER_API C11 CAPABILITY length=${checks.keyframeLength} invalidKeyTextError=${checks.invalidKeyTextError} engine=chromium`);
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
  if (checks.viewportOperations !== 30 || checks.semanticOperations !== 26 || checks.attachmentIdentifier !== 'undefined') {
    throw new Error('Selected D09/D11 operation proof did not run for the pinned Chromium profile');
  }
  console.log(`BROWSER_API D09_VIEWPORT PASS assertions=${checks.viewportOperations} engine=chromium`);
  console.log(`BROWSER_API D11_OPERATIONS PASS assertions=${checks.semanticOperations} engine=chromium`);
  console.log(`BROWSER_API D11 CAPABILITY attachmentIdentifier=${checks.attachmentIdentifier} engine=chromium`);
  console.log(`BROWSER_API CSSOM PASS assertions=${checks.cssom} engine=chromium`);
  if (checks.ruleMutations !== 131) throw new Error('Nested rule mutation assertions did not run');
  console.log(`BROWSER_API C08 PASS assertions=${checks.ruleMutations} engine=chromium`);
  console.log(`BROWSER_API SVG_CSSOM PASS assertions=${checks.svgCssom} engine=chromium`);
  console.log(`BROWSER_API HYPERLINKS PASS assertions=${checks.hyperlinks} engine=chromium`);
  console.log(`BROWSER_API C07 PASS assertions=${checks.construction} engine=chromium`);
  console.log(`BROWSER_API D12 PASS assertions=${checks.sheetElements} engine=chromium`);
  if (checks.sheetElements !== 109 || checks.failedLinkSheet !== 'replacement') throw new Error('Stylesheet/link element assertions did not run');
  console.log(`BROWSER_API D12 CAPABILITY failedLinkSheet=${checks.failedLinkSheet} engine=chromium`);
  if (checks.construction !== 78) throw new Error('Constructed stylesheet preservation assertions did not run');
  await page.locator('#native-api-svg').focus();
  await page.keyboard.press('Tab');
  if (!await page.locator('#native-api-math').evaluate(element => element === element.ownerDocument.activeElement)) {
    throw new Error('Tab did not move from SVG to MathML in document order');
  }
  console.log('BROWSER_API D09 TAB PASS engine=chromium');
  }
  }
  await runInputState();
  await runViewport();
  await runForms();
  await runDialogs();
  await runBinary();
  await runUrls();
  await runSegments();
  await runSubmissions();
  await runCrypto();
  await runHighlights();
  await runFrames();
  await runIdle();
  await runCaret();
  }
} finally {
  if (browser) await browser.close();
  await new Promise(resolve => server.close(resolve));
}
