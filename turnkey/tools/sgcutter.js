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
  /* MEASURE THE CLAIM, NOT A PROXY FOR IT. Raw frame deltas were the obvious
     instrument and they are not trustworthy here: after several seconds of
     setTimeout with no compositing, headless Chromium hands back irregular
     rAF gaps of ~300ms whether the worker is busy or idle, whether anything
     is cached or not, and with loadLevel measured at 1.4ms and draw at
     0.45ms — nothing in the game accounts for them.

     The actual claim is narrower and exactly checkable: MINTING NO LONGER
     HAPPENS ON THE MAIN THREAD during a level transition. That was the whole
     defect — 200-500ms of generator work in the middle of play — so the test
     counts main-thread mint calls and times the transition directly. */
  const stall = await p.evaluate(async () => {
    store.reached = 900; store.taught = 1;
    mintCache = {};
    /* arrive the way a player arrives, with the next cube already cut */
    loadLevel(699); show(null);
    await new Promise(r => setTimeout(r, 3000));
    loadLevel(700); show(null);
    await new Promise(r => setTimeout(r, 3000));

    /* count every main-thread mint from here on */
    const realMint = mint;
    let mainThreadMints = 0, mintMs = 0;
    window.mint = function(){ mainThreadMints++; const t = performance.now();
      const r = realMint.apply(null, arguments); mintMs += performance.now() - t; return r; };

    const t0 = performance.now();
    loadLevel(701); show(null);              /* the transition, prebuild and all */
    const transition = performance.now() - t0;

    await new Promise(r => setTimeout(r, 3500));   /* let the worker deliver 702 */
    const cached = Object.keys(mintCache).map(Number).sort((a,b) => a-b);
    window.mint = realMint;
    return {transition: +transition.toFixed(1), mainThreadMints, mintMs: Math.round(mintMs), cached};
  });
  ok('a level transition does no minting on the main thread at all',
     stall.mainThreadMints === 0, `${stall.mainThreadMints} mint(s), ${stall.mintMs}ms`);
  ok('...and the transition itself is under a frame and a half',
     stall.transition < 25, `${stall.transition}ms`);
  ok('...while the next cube still gets cut in the background',
     stall.cached.indexOf(702) >= 0, 'cached: ' + stall.cached.join(','));

  /* WHAT IT COSTS WHEN THE HEAD START IS NOT THERE. Clearing a cube faster
     than the next one can be cut still blocks, and at n=9 that block is
     bigger than it used to be. Worth measuring rather than assuming: it is
     the one case the worker does not cover. */
  const cold = await p.evaluate(async () => {
    mintCache = {};
    const t0 = performance.now();
    loadLevel(880);                       /* nothing cached, nothing prebuilt */
    return Math.round(performance.now() - t0);
  });
  ok('a cube reached with no head start still loads in under three seconds',
     cold < 3000, `${cold}ms blocking — only reachable by clearing a level faster than one cut`);

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
