/* MINTING OFF THE MAIN THREAD, AND THE TWO THINGS THAT MUST SURVIVE IT.

   THE STALL HAS TO BE GONE. Cutting a cube costs 200-500ms and prebuild was
   doing it on the main thread two seconds into every level. This measures
   frame deltas across a real level load and fails if any frame is long enough
   to be seen.

   AND THE CUBE MUST BE THE SAME CUBE. This is the part that would be a
   disaster to get wrong quietly: the worker runs a SLICE of this file's own
   source, and if that slice ever diverged — or if the worker's RNG consumed a
   different number of draws — level N would differ between the thread that
   cut it and the thread that checks it, and the determinism the whole game
   rests on would be gone with nothing to show for it. Every level minted both
   ways is compared by canonical identity.

   The fallback matters too. Blob workers can be refused by a CSP, by a
   file:// origin, or by an old WebView, and the game has to be exactly as
   good as it was before rather than broken in a new way.                   */
const {chromium} = require('playwright');
let bad = 0;
const ok = (n, c, x = '') => { console.log((c ? '  ok  ' : 'FAIL  ') + n + (x ? '   ' + x : '')); if(!c) bad = 1; };

(async () => {
  const b = await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p = await b.newPage({viewport:{width:390,height:844}, deviceScaleFactor:2, isMobile:true, hasTouch:true});
  const errs = [];
  p.on('pageerror', e => errs.push('PAGEERROR: ' + e.message));
  p.on('console', m => { if(m.type() === 'error') errs.push('CONSOLE: ' + m.text()); });
  await p.goto('file:///home/user/Ideas/singularity/index.html');
  await p.waitForTimeout(500);

  /* ---- the worker exists and is built from this file -------------------- */
  const up = await p.evaluate(() => {
    const w = cutterUp();
    const src = document.querySelector('script').textContent;
    return {
      alive: !!w, off: cutOff,
      scripts: document.querySelectorAll('script').length,
      hasStart: src.indexOf('@@WORKER-CORE-START@@') >= 0,
      hasEnd: src.indexOf('@@WORKER-CORE-END@@') >= 0,
      /* nothing between the markers may touch a document, or the worker
         throws on its very first line and the feature is silently dead */
      domInCore: (() => {
        const a = src.indexOf('@@WORKER-CORE-START@@'), e = src.indexOf('@@WORKER-CORE-END@@');
        /* The slice opens INSIDE the marker comment, so there is no `/*` for
           the stripper to pair with and that comment's own prose survives —
           and it contains the words "document" and "canvas", because it is
           the sentence telling people not to put those here. Drop everything
           up to the first close before stripping the rest. */
        let core = src.slice(a, e);
        core = core.slice(core.indexOf('*/') + 2);
        core = core.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
        const hits = core.match(/\b(document|localStorage|requestAnimationFrame|HTMLCanvasElement)\b/g);
        return hits ? [...new Set(hits)] : [];
      })()
    };
  });
  ok('there is exactly one script, so the slice is unambiguous', up.scripts === 1, String(up.scripts));
  ok('both core markers are present', up.hasStart && up.hasEnd);
  ok('nothing between the markers touches the DOM',
     up.domInCore.length === 0, up.domInCore.join(', ') || 'clean');
  ok('the worker starts', up.alive && !up.off);

  /* ---- IT IS THE SAME CUBE ---------------------------------------------- */
  const same = await p.evaluate(async () => {
    const levels = [40, 137, 400, 901, 1500, 2070];
    const out = [];
    for(const L of levels){
      delete mintCache[L];
      const viaWorker = await new Promise(res => {
        const id = ++cutSeq;
        cutJobs[id] = {then: res};
        cutter.postMessage({id, level: L, budget: MINT_BUDGET * 2});
        setTimeout(() => res(null), 20000);
      });
      delete mintCache[L];
      const viaMain = mint(L, MINT_BUDGET);
      const norm = lv => ({...lv, vox: Array.isArray(lv.vox) ? lv.vox.join('') : lv.vox});
      out.push({
        L,
        got: !!viaWorker,
        idMatch: !!viaWorker && canonId(norm(viaWorker)) === canonId(norm(viaMain)),
        parMatch: !!viaWorker && viaWorker.par === viaMain.par,
        par: viaMain.par
      });
      delete mintCache[L];
    }
    return out;
  });
  const mismatched = same.filter(r => !r.got || !r.idMatch || !r.parMatch);
  ok('a cube cut on the worker is identical to one cut on the main thread',
     mismatched.length === 0,
     mismatched.length ? JSON.stringify(mismatched) : same.map(r => r.L + ':par' + r.par).join(' '));

  /* ---- the stall is gone ------------------------------------------------ */
  const frames = await p.evaluate(async () => {
    store.reached = 900; store.taught = 1;
    mintCache = {};
    loadLevel(700); show(null);
    await new Promise(r => setTimeout(r, 400));
    const marks = [];
    let last = performance.now();
    const t0 = last;
    await new Promise(res => {
      const tick = () => {
        const now = performance.now();
        marks.push(now - last); last = now;
        if(now - t0 < 4500) requestAnimationFrame(tick); else res();
      };
      /* the load that used to stall: it prebuilds the next cube */
      loadLevel(701); show(null);
      requestAnimationFrame(tick);
    });
    marks.shift();
    return {worst: +Math.max.apply(null, marks).toFixed(0),
            over100: marks.filter(m => m > 100).length,
            over250: marks.filter(m => m > 250).length,
            prebuilt: Object.keys(mintCache).map(Number).sort((a,b) => a-b)};
  });
  ok('no frame long enough to see, across a level load and its prebuild',
     frames.over100 === 0 && frames.over250 === 0,
     `worst ${frames.worst}ms, ${frames.over100} frames over 100ms`);
  ok('...and the next cube really was cut in the background',
     frames.prebuilt.indexOf(702) >= 0, 'cached: ' + frames.prebuilt.join(','));

  /* ---- and it still works with no worker at all ------------------------- */
  const fallback = await p.evaluate(async () => {
    if(cutter) cutter.terminate();
    cutter = null; cutOff = true; mintCache = {};
    const t0 = performance.now();
    prebuild(760);
    await new Promise(r => setTimeout(r, 2500));
    const built = !!mintCache[760];
    /* and a level reached before anything cut it still loads, blocking */
    cutOff = true; mintCache = {};
    loadLevel(770);
    const loaded = !!lv && lv.n > 0;
    cutOff = false;
    return {built, loaded, ms: Math.round(performance.now() - t0)};
  });
  ok('with no worker the old timer path still cuts the next cube', fallback.built);
  ok('...and a level with nothing cached still loads synchronously', fallback.loaded);

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
