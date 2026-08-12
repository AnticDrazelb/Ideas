/* VAULTS ARE NOW LEADERBOARD UNITS, AND THE ARITHMETIC HAS TO HOLD.

   Three claims, and they are the ones that break silently.

   THE PARTITION IS A PARTITION. vaultOf / vaultStart / vaultSize describe
   the same division of the level line or they describe two, and two would
   mean a cube whose palette comes from one vault and whose leaderboard
   comes from another. Every level up to the end of the ranked region is
   walked and checked against all three.

   IT IS INTEGERS ALL THE WAY DOWN. The tail's cumulative sum is quadratic
   and the tempting inverse is a square root. A boundary landing on 99.9999
   on one device and 100.0000 on another is the `sort()` bug again: same
   level number, different vault, different cube. Nothing here may be
   fractional.

   THE CLOCK MEASURES THE SOLVE AND SURVIVES A BACKGROUNDED APP. It counts
   thinking time, it stops when the Activity goes away, and a vault posts
   only once every cube in it has a time.                                  */
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

  /* ---- the partition ---------------------------------------------------- */
  const part = await p.evaluate(() => {
    const last = vaultStart(RANKED_VAULTS - 1) + vaultSize(RANKED_VAULTS - 1) - 1;
    let mismatch = null, gap = null, frac = null;

    /* starts are contiguous and every one of them is a whole number */
    for(let band = 0; band < RANKED_VAULTS + 6; band++){
      const s = vaultStart(band), n = vaultSize(band);
      if(s % 1 !== 0 || n % 1 !== 0) frac = frac || [band, s, n];
      if(band > 0){
        const prev = vaultStart(band - 1) + vaultSize(band - 1);
        if(prev !== s) gap = gap || [band, prev, s];
      }
    }
    /* and every level agrees with the band it claims to be in */
    for(let L = 1; L <= last && !mismatch; L++){
      const band = vaultOf(L);
      const s = vaultStart(band), n = vaultSize(band);
      if(L < s || L >= s + n) mismatch = [L, band, s, n];
    }
    const sizes = [];
    for(let band = 0; band < RANKED_VAULTS; band++) sizes.push(vaultSize(band));
    return {last, mismatch, gap, frac, sizes,
            authored: sizes.slice(0, 6),
            first: vaultStart(0), tailStart: vaultStart(6),
            biggest: Math.max.apply(null, sizes)};
  });

  ok('every level lands in the vault that claims it', !part.mismatch,
     part.mismatch ? `level ${part.mismatch[0]} says band ${part.mismatch[1]} = [${part.mismatch[2]}..${part.mismatch[2]+part.mismatch[3]-1}]` : `${part.last} levels walked`);
  ok('vault starts are contiguous, no gap and no overlap', !part.gap,
     part.gap ? `band ${part.gap[0]}: previous ends ${part.gap[1]}, this starts ${part.gap[2]}` : '');
  ok('no vault boundary is a fraction', !part.frac,
     part.frac ? `band ${part.frac[0]} start ${part.frac[1]} size ${part.frac[2]}` : 'integers throughout');
  ok('the authored six keep their own lengths',
     String(part.authored) === '10,15,20,20,15,10', String(part.authored));
  ok('the tail grows by five from twenty-five',
     String(part.sizes.slice(6, 11)) === '25,30,35,40,45', String(part.sizes.slice(6, 11)));
  ok('thirty ranked vaults hold 2070 cubes', part.last === 2070, `${part.last} cubes`);
  ok('...and the longest of them is 140', part.biggest === 140, `${part.biggest} cubes`);

  /* ---- the clock -------------------------------------------------------- */
  const clock = await p.evaluate(async () => {
    store.reached = 900; store.taught = 1;
    loadLevel(4); show(null);
    await new Promise(r => setTimeout(r, 700));
    const early = clockMs();

    /* backgrounding must not be banked */
    window.TURNKEY.pause();
    const atPause = clockMs();
    await new Promise(r => setTimeout(r, 600));
    const afterHidden = clockMs();
    window.TURNKEY.resume();
    await new Promise(r => setTimeout(r, 300));
    const afterResume = clockMs();

    return {early, atPause, afterHidden, afterResume};
  });

  ok('the clock is running before the player has touched anything',
     clock.early > 500, `${clock.early}ms after 700ms on the cube`);
  ok('a backgrounded app banks no time',
     clock.afterHidden - clock.atPause === 0,
     `${clock.atPause}ms -> ${clock.afterHidden}ms across 600ms hidden`);
  ok('...and it picks up again on resume',
     clock.afterResume - clock.afterHidden > 200, `+${clock.afterResume - clock.afterHidden}ms`);

  /* ---- clearing a cube actually records a time --------------------------- */
  const rec = await p.evaluate(async () => {
    store.tbest = {}; store.vbest = {}; store.best = {};
    loadLevel(2); show(null);
    await new Promise(r => setTimeout(r, 800));
    const live = clockMs();
    finish();                              /* the win path, not a re-implementation */
    await new Promise(r => setTimeout(r, 60));
    const first = store.tbest[2];

    /* a slower second run must not overwrite a faster first */
    loadLevel(2); show(null);
    await new Promise(r => setTimeout(r, 1400));
    finish();
    await new Promise(r => setTimeout(r, 60));
    return {live, first, kept: store.tbest[2], stopped: solveLive};
  });
  ok('clearing a cube writes its time',
     rec.first !== undefined && Math.abs(rec.first - rec.live) < 120,
     `clock ${rec.live}ms, stored ${rec.first}ms`);
  ok('...and the clock stops when the cube is done', rec.stopped === false);
  ok('a slower run does not replace a faster one',
     rec.kept === rec.first, `kept ${rec.kept}ms`);

  /* ---- a vault posts once, and only when it is finished ------------------ */
  const post = await p.evaluate(() => {
    /* stand a host up rather than stubbing GPG, so the real adapter — and
       its stringification on the way across the bridge — is what runs */
    const sent = [];
    window.AndroidGPG = {submit:(board, score) => sent.push([board, score]), show:() => {}};
    store.tbest = {}; store.vbest = {};

    const band = 0, v0 = vaultStart(band), n = vaultSize(band);
    for(let i = 0; i < n - 1; i++) store.tbest[v0 + i] = 1000;
    postVault(band);
    const partial = sent.length;

    store.tbest[v0 + n - 1] = 1000;         /* the last cube arrives */
    postVault(band);
    const whole = sent.slice();

    postVault(band);                        /* nothing improved */
    const again = sent.length;

    store.tbest[v0] = 400;                  /* a faster run at cube one */
    postVault(band);

    /* a vault past the ranked region has no board to post to */
    const far = RANKED_VAULTS, f0 = vaultStart(far), fn = vaultSize(far);
    for(let i = 0; i < fn; i++) store.tbest[f0 + i] = 1000;
    postVault(far);

    return {partial, whole, again, all: sent, vbest0: store.vbest[0],
            unranked: sent.filter(s => s[0] === 'vault_' + (far + 1)).length};
  });

  ok('an unfinished vault posts nothing', post.partial === 0, `${post.partial} sent`);
  ok('the last cube in a vault posts its total',
     post.whole.length === 1 && post.whole[0][0] === 'vault_01' && post.whole[0][1] === '10000',
     JSON.stringify(post.whole));
  ok('a clear that improves nothing posts nothing', post.again === 1, `${post.again} sent`);
  ok('a faster cube reposts the better total',
     post.all.length === 2 && post.all[1][1] === '9400', JSON.stringify(post.all[1]));
  ok('...and the stored total matches what was sent', post.vbest0 === 9400, String(post.vbest0));
  ok('a vault past the ranked region has no board', post.unranked === 0, `${post.unranked} sent`);

  /* ---- the shelf still renders a vault five times the length it was ----- */
  const grid = await p.evaluate(() => {
    store.reached = 2500;
    const out = [];
    for(const band of [0, 5, 6, 15, 29, 30, 45]){
      viewBand = band; buildCubeGrid();
      const g = document.getElementById('cubeGrid');
      out.push({band, tiles: g.children.length, size: vaultSize(band),
                name: document.getElementById('vaultName').textContent});
    }
    return out;
  });
  const gridBad = grid.filter(r => r.tiles !== r.size);
  ok('the cube grid draws one tile per cube at every vault length',
     gridBad.length === 0,
     gridBad.length ? JSON.stringify(gridBad[0]) : grid.map(r => r.band + ':' + r.tiles).join(' '));
  ok('...including a vault of 140 and one past the ranked region',
     grid.some(r => r.tiles === 140) && grid[grid.length-1].tiles > 0,
     grid[grid.length-1].name + ' = ' + grid[grid.length-1].tiles + ' cubes');

  /* ---- the offline page must not care ----------------------------------- */
  const off = await p.evaluate(() => {
    try{ delete window.AndroidGPG; }catch(e){ window.AndroidGPG = null; }
    return {on: GPG.available(), threw: (function(){
      try{ GPG.submit('vault_01', 1); GPG.show('vault_01'); return false; }catch(e){ return String(e); }
    })()};
  });
  ok('with no host the bridge reports itself absent', off.on === false);
  ok('...and calling it anyway is a no-op, not a throw', off.threw === false, String(off.threw));

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
