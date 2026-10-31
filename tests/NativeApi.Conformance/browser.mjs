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
const svgPaintOnly = process.argv.includes('--svg-paint-only');
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
let browser;
try {
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
      return await (focused === 'locales' ? fixture.runLocales() : focused === 'svg' ? fixture.runSvg() : focused === 'svg-surfaces' ? fixture.runSvgSurfaces() : focused === 'svg-paint' ? fixture.runSvgPaint() : focused === 'transforms' ? fixture.runTransforms() : focused === 'matrix' ? fixture.runMatrix() : focused === 'geometry' ? fixture.runGeometry() : focused === 'style-maps' ? fixture.runStyleMaps() : focused === 'style-values' ? fixture.runStyleValues() : focused === 'custom-properties' ? fixture.runCustomProperties() : { ...await fixture.runSvgSurfaces(), ...await fixture.runSvgPaint(), ...await fixture.run() });
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
  }, localesOnly ? 'locales' : svgOnly ? 'svg' : svgSurfacesOnly ? 'svg-surfaces' : svgPaintOnly ? 'svg-paint' : transformsOnly ? 'transforms' : matrixOnly ? 'matrix' : geometryOnly ? 'geometry' : styleMapsOnly ? 'style-maps' : styleValuesOnly ? 'style-values' : customPropertiesOnly ? 'custom-properties' : 'all');
  if (localesOnly) {
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
} finally {
  if (browser) await browser.close();
  await new Promise(resolve => server.close(resolve));
}
