import { createHash } from 'node:crypto';
import { mkdir, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { inflateSync } from 'node:zlib';

// Mirrors the 96x64 A11 page in Ranvier.WebCore/tests/gpu/linux_view_transition_pixels.py.
const pageHtml = `<!doctype html><style>
html,body{margin:0;background:rgb(17,34,51)}
:root{view-transition-name:root}
#box{position:absolute;left:8px;top:8px;width:40px;height:32px;background:rgb(255,0,0);transform:translateZ(0)}
::view-transition-group(root){animation:none}
::view-transition-old(root){animation:none;opacity:1}
::view-transition-new(root){animation:reveal 100s linear both;mix-blend-mode:normal}
@keyframes reveal{from{opacity:0}to{opacity:1}}
</style><div id="box"></div>`;

const colors = {
  red: [255, 0, 0, 255],
  purple: [128, 0, 128, 255],
  blue: [0, 0, 255, 255],
  green: [0, 255, 0, 255],
  background: [17, 34, 51, 255],
};

function decodeOnePixel(png) {
  if (png.toString('hex', 0, 8) !== '89504e470d0a1a0a') throw new Error('Screenshot is not a PNG');
  let width, height, bitDepth, colorType;
  const chunks = [];
  for (let offset = 8; offset < png.length;) {
    const length = png.readUInt32BE(offset);
    const kind = png.toString('latin1', offset + 4, offset + 8);
    if (kind === 'IHDR') {
      width = png.readUInt32BE(offset + 8);
      height = png.readUInt32BE(offset + 12);
      bitDepth = png[offset + 16];
      colorType = png[offset + 17];
    }
    if (kind === 'IDAT') chunks.push(png.subarray(offset + 8, offset + 8 + length));
    offset += length + 12;
  }
  if (width !== 1 || height !== 1 || bitDepth !== 8 || (colorType !== 2 && colorType !== 6)) {
    throw new Error(`Unexpected one-pixel PNG format ${width}x${height}, depth=${bitDepth}, type=${colorType}`);
  }
  const bytes = inflateSync(Buffer.concat(chunks));
  const channels = colorType === 6 ? 4 : 3;
  // Every PNG filter predicts zero for the first pixel, so its encoded samples are the RGBA/RGB values.
  const rgba = [...bytes.subarray(1, 1 + channels)];
  if (channels === 3) rgba.push(255);
  return rgba;
}

function assertColor(actual, expected, label) {
  if (!actual.every((value, index) => Math.abs(value - expected[index]) <= 2)) {
    throw new Error(`${label}: expected ${expected}, got ${actual}`);
  }
}

async function nextPaint(page) {
  await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}

async function activeNewAnimation(page) {
  await page.waitForFunction(() => [...document.getAnimations({ subtree: true })].some(animation =>
    animation.effect?.pseudoElement === '::view-transition-new(root)'), null, { timeout: 5000 });
  return page.evaluateHandle(() => [...document.getAnimations({ subtree: true })].find(animation =>
    animation.effect?.pseudoElement === '::view-transition-new(root)'));
}

export async function runViewTransitionPixels(chromium, requestedOutputDirectory = process.env.NATIVE_API_PIXEL_OUTPUT) {
  const temporaryOutput = !requestedOutputDirectory;
  const outputDirectory = requestedOutputDirectory
    ? requestedOutputDirectory
    : await mkdtemp(join(tmpdir(), 'ranvier-view-transition-pixels-'));
  await mkdir(outputDirectory, { recursive: true });
  let browser;
  try {
    browser = await chromium.launch({ headless: true, channel: 'chromium' });
    const page = await browser.newPage({ viewport: { width: 96, height: 64 }, deviceScaleFactor: 1 });
    await page.setContent(pageHtml);
    const samples = [];
    const capture = async (label, expected) => {
      await nextPaint(page);
      const screenshot = Buffer.from(await page.screenshot({ path: join(outputDirectory, `${String(samples.length + 1).padStart(2, '0')}-${label}.png`) }));
      const boxPng = Buffer.from(await page.screenshot({ clip: { x: 20, y: 20, width: 1, height: 1 }, scale: 'css' }));
      const backgroundPng = Buffer.from(await page.screenshot({ clip: { x: 80, y: 50, width: 1, height: 1 }, scale: 'css' }));
      const box = decodeOnePixel(boxPng);
      const background = decodeOnePixel(backgroundPng);
      assertColor(box, expected, `${label} box pixel`);
      assertColor(background, colors.background, `${label} background pixel`);
      const sample = {
        label,
        box,
        background,
        screenshotBytes: screenshot.length,
        screenshotSha256: createHash('sha256').update(screenshot).digest('hex'),
      };
      samples.push(sample);
      console.log(`BROWSER_PIXELS ${JSON.stringify(sample)}`);
    };
    const transitionAndPauseAt = async targetColor => {
      await page.evaluate(color => {
        window.__pixelTransition = document.startViewTransition(() => {
          document.querySelector('#box').style.background = color;
        });
        return window.__pixelTransition.ready;
      }, targetColor);
      const animation = await activeNewAnimation(page);
      await animation.evaluate(value => value.pause());
      return animation;
    };
    const setTime = async (animation, milliseconds) => {
      await animation.evaluate((value, time) => { value.currentTime = time; }, milliseconds);
      await nextPaint(page);
    };

    await capture('old-red', colors.red);
    const first = await transitionAndPauseAt('rgb(0,0,255)');
    const liveColor = await page.locator('#box').evaluate(element => getComputedStyle(element).backgroundColor);
    if (liveColor !== 'rgb(0, 0, 255)') throw new Error(`Live DOM did not become blue: ${liveColor}`);
    await setTime(first, 0);
    await capture('live-blue-at-zero-red-snapshot', colors.red);
    await setTime(first, 50000);
    await capture('midpoint-purple', colors.purple);
    await setTime(first, 100000);
    await capture('at-100-seconds-blue', colors.blue);
    await first.evaluate(animation => animation.finish());
    await page.evaluate(async () => { await window.__pixelTransition.finished; });
    await capture('finished-blue', colors.blue);
    await first.dispose();

    const second = await transitionAndPauseAt('rgb(255,0,0)');
    await setTime(second, 50000);
    await capture('reverse-before-skip-purple', colors.purple);
    await page.evaluate(async () => {
      window.__pixelTransition.skipTransition();
      await window.__pixelTransition.finished;
    });
    await capture('skip-cleans-pseudo-tree-red', colors.red);
    await second.dispose();

    const third = await transitionAndPauseAt('rgb(0,0,255)');
    await setTime(third, 50000);
    await capture('replacement-before-navigation-purple', colors.purple);
    await third.dispose();
    const replacement = pageHtml.replace('rgb(255,0,0)', 'rgb(0,255,0)');
    await page.goto(`data:text/html,${encodeURIComponent(replacement)}`);
    await capture('replacement-document-green', colors.green);

    if (samples.length !== 9) throw new Error(`Expected 9 screenshots, captured ${samples.length}`);
    console.log(`BROWSER_PIXELS A11 PASS screenshots=${samples.length} browser=${browser.version()} route=standalone-html output=${outputDirectory}`);
    return { browserVersion: browser.version(), samples, outputDirectory };
  } finally {
    if (browser) await browser.close();
    if (temporaryOutput) await rm(outputDirectory, { recursive: true, force: true });
  }
}
