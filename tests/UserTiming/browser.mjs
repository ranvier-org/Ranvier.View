import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const root = resolve(dirname(fileURLToPath(import.meta.url)), 'generated');
const dependencies = process.env.NATIVE_API_BROWSER_PACKAGE || resolve(dirname(fileURLToPath(import.meta.url)), '../NativeApi.Conformance/package.json');
const { chromium } = createRequire(dependencies)('@playwright/test');
const server = createServer(async (request,response) => {
  try {
    const name = decodeURIComponent(new URL(request.url,'http://localhost').pathname);
    if (name === '/') {
      response.setHeader('Content-Type','text/html');
      response.end('<!doctype html><html><body></body></html>');
      return;
    }
    const file = resolve(root,'.'+name);
    if (!file.startsWith(root+sep)) throw new Error('Outside fixture output');
    response.setHeader('Content-Type',extname(file)==='.js'?'text/javascript':'text/plain');
    response.end(await readFile(file));
  } catch {
    response.statusCode=404;
    response.end();
  }
});
await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
let browser;
try {
  browser=await chromium.launch({headless:true});
  const page=await browser.newPage();
  await page.goto(`http://127.0.0.1:${server.address().port}/`);
  const result=await page.evaluate(async () => {
    const module=await import('/Browser.js');
    let result;
    try { result=module.run(); }
    catch (error) { throw new Error(`${error.message || error.Message || String(error)} ${error.stack || ''}`); }
    let contexts=0;
    function check(condition,message) { contexts++; if (!condition) throw new Error(message); }
    const provider=module.provider(document);
    performance.mark('h02-external',{startTime:37,detail:{text:'external'}});
    const external=provider.entries().find(entry=>entry.Name==='h02-external');
    check(external?.StartTime===37,'Facade did not read real browser marks');
    provider.mark('h02-from-facade');
    check(performance.getEntriesByName('h02-from-facade','mark')[0]?.detail==='facade','Real browser buffer did not receive facade mark/detail');
    check(provider.origin()===performance.timeOrigin,'Facade clock has a different origin');
    const cyclic={}; cyclic.self=cyclic;
    performance.mark('h02-external-cycle',{detail:cyclic});
    let cycleRejected=false;
    try { provider.entries(); } catch(error) { cycleRejected=String(error).includes('DataCloneError'); }
    check(cycleRejected,'Unsupported cyclic external detail was not explicitly rejected');
    performance.clearMarks('h02-external-cycle');
    performance.mark('h02-external-date',{detail:new Date()});
    let dateRejected=false;
    try { provider.entries(); } catch(error) { dateRejected=String(error).includes('DataCloneError'); }
    check(dateRejected,'Unsupported external Date detail was silently changed');
    performance.clearMarks('h02-external-date');
    const frame=document.createElement('iframe');
    frame.srcdoc='<!doctype html><html><body>first</body></html>';
    const loaded=new Promise(resolve=>frame.onload=resolve);
    document.body.append(frame);
    await loaded;
    try {
      const previous=frame.contentDocument;
      const child=module.provider(previous);
      check(child.origin()===frame.contentWindow.performance.timeOrigin,'Iframe clock did not use its own realm');
      child.mark('h02-old-document');
      check(frame.contentWindow.performance.getEntriesByName('h02-old-document').length===1,'Iframe mark reached another realm');
      const nextLoaded=new Promise(resolve=>frame.onload=resolve);
      frame.srcdoc='<!doctype html><html><body>second</body></html>';
      await nextLoaded;
      check(frame.contentDocument!==previous,'Iframe navigation did not replace the document');
      let rejected=false;
      try { child.now(); } catch { rejected=true; }
      check(rejected,'Old document timing provider survived navigation');
      const next=module.provider(frame.contentDocument);
      next.mark('h02-new-document');
      check(frame.contentWindow.performance.getEntriesByName('h02-new-document').length===1,'Replacement timing provider failed');
      check(frame.contentWindow.performance.getEntriesByName('h02-old-document').length===0,'Old timing marks leaked across navigation');
      next.dispose();child.dispose();
    } finally { frame.remove(); }
    provider.dispose();
    performance.clearMarks();performance.clearMeasures();
    return {...result,contexts};
  });
  if (result.shared!==180 || result.deterministic!==181 || result.contexts!==11) throw new Error(`Timing guards failed: ${JSON.stringify(result)}`);
  console.log(`BROWSER_API H02 PASS assertions=${result.shared} deterministic=${result.deterministic} contexts=${result.contexts} engine=${browser.version()}`);
} finally {
  if (browser) await browser.close();
  await new Promise(resolve=>server.close(resolve));
}
