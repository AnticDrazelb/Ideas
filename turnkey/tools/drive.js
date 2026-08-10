/* Drives the real game in a real Chromium at a real phone size, plays a
   level through the solver's own answer, and screenshots as it goes. */
const {chromium} = require('playwright');
const path = '/home/user/Ideas/turnkey/index.html';

(async () => {
  const browser = await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const page = await browser.newPage({
    viewport:{width:412, height:915}, deviceScaleFactor:2,
    isMobile:true, hasTouch:true
  });
  const errs = [];
  page.on('pageerror', e => errs.push('PAGEERROR: ' + e.message));
  page.on('console', m => { if(m.type()==='error') errs.push('CONSOLE: ' + m.text()); });

  await page.goto('file://' + path);
  await page.waitForTimeout(500);
  await page.screenshot({path:'shot-1-title.png'});

  // BEGIN -> manual -> game
  await page.click('#btnPlay'); await page.waitForTimeout(400);
  await page.screenshot({path:'shot-2-manual.png'});
  await page.click('#btnManualBack'); await page.waitForTimeout(600);
  await page.screenshot({path:'shot-3-level1.png'});

  const st = () => page.evaluate(() => ({
    lv: lvIndex, name: lv.name, n: N, turns, par: lv.par, won,
    pos: pos.slice(), ori: oriIndex(M), keys: keysHeld(),
    reach: Array.from(reach).reduce((a,b)=>a+b,0),
    hudOn: document.getElementById('hud').classList.contains('on')
  }));
  console.log('level 1 loaded:', JSON.stringify(await st()));

  // Ask the game's own solver for the answer, then perform it through the
  // real input path — drags and taps, not function calls.
  async function autoplay(maxActs=140){
    for(let i=0;i<maxActs;i++){
      const s = await page.evaluate(() => {
        if(won) return {done:true};
        const r = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors});
        if(!r.ok) return {fail:'unsolvable from current state'};
        if(!r.first) return {done:true};
        if(r.first.kind === 'turn') return {turn:r.first.dir};
        // walk: find where the first step lands, in screen coords
        const v = viewOf(N, M, pos), t = TURNS[r.first.dir];
        const u2 = v[0]+t.dx, v2 = v[1]+t.dy;
        const rc = tileRect(u2, v2);
        return {tap:[rc[0]+S/2, rc[1]+S/2]};
      });
      if(s.done) return true;
      if(s.fail) { console.log('  ' + s.fail); return false; }
      if(s.turn !== undefined){
        // a real drag, past the halfway detent
        const dirs = [[-1,0],[1,0],[0,-1],[0,1]]; // left,right,up,down (screen dy is negative for "up")
        const d = dirs[s.turn], cx=206, cy=470, L=200;
        await page.mouse.move(cx, cy);
        await page.mouse.down();
        for(let k=1;k<=6;k++) await page.mouse.move(cx + d[0]*L*k/6, cy + d[1]*L*k/6);
        await page.mouse.up();
        await page.waitForTimeout(430);
      } else {
        await page.mouse.click(s.tap[0], s.tap[1]);
        await page.waitForTimeout(170);
      }
    }
    return false;
  }

  const ok1 = await autoplay();
  await page.waitForTimeout(900);
  console.log('after autoplay level 1:', JSON.stringify(await st()));
  await page.screenshot({path:'shot-4-cleared.png'});

  // walk the whole campaign at par
  let cleared = 0, fails = [];
  for(let n=0;n<14;n++){
    await page.evaluate(i => { loadLevel(i); show(null); }, n);
    await page.waitForTimeout(220);
    const before = await st();
    const ok = await autoplay();
    await page.waitForTimeout(700);
    const after = await page.evaluate(() => ({won, turns, par:lv.par, name:lv.name}));
    if(ok && after.won && after.turns === after.par){ cleared++; }
    else fails.push(`${n+1} ${after.name}: ok=${ok} won=${after.won} turns=${after.turns} par=${after.par}`);
    // dismiss the win card
    await page.evaluate(() => show(null));
  }
  console.log(`\ncampaign: ${cleared}/14 cleared at exactly par through the real input path`);
  fails.forEach(f => console.log('  FAIL ' + f));

  // mid-turn screenshot: hold a drag at 45 degrees so the 3D path is on screen
  await page.evaluate(() => { loadLevel(9); show(null); });
  await page.waitForTimeout(300);
  await page.mouse.move(206, 470); await page.mouse.down();
  for(let k=1;k<=5;k++) await page.mouse.move(206 + 82*k/5, 470);
  await page.waitForTimeout(120);
  await page.screenshot({path:'shot-5-turning.png'});
  await page.mouse.up();
  await page.waitForTimeout(600);
  await page.screenshot({path:'shot-6-settled.png'});

  // level select + depth crutch
  await page.evaluate(() => { store.depth = 1; });
  await page.waitForTimeout(300);
  await page.screenshot({path:'shot-7-depth.png'});
  await page.evaluate(() => { store.depth = 0; buildCubeGrid(); show('scCubes'); });
  await page.waitForTimeout(400);
  await page.screenshot({path:'shot-8-cubes.png'});

  console.log('\nerrors: ' + (errs.length ? '\n  ' + errs.join('\n  ') : 'none'));
  await browser.close();
})();
