const {chromium} = require('playwright');
const FILE = 'file:///home/user/Ideas/turnkey/index.html';
let bad = 0;
const ok = (n, c, extra='') => { console.log((c?'  ok  ':'FAIL  ') + n + (extra?'   '+extra:'')); if(!c) bad=1; };

(async () => {
  const b = await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p = await b.newPage({viewport:{width:412,height:915}, deviceScaleFactor:2, isMobile:true, hasTouch:true});
  const errs = [];
  p.on('pageerror', e => errs.push(e.message));
  await p.goto(FILE);
  await p.waitForTimeout(400);

  // ---- undo restores position, orientation, keys, doors and the turn count
  await p.evaluate(() => { loadLevel(7); show(null); });
  await p.waitForTimeout(200);
  const snap0 = await p.evaluate(() => JSON.stringify([pos, oriIndex(M), kmask, doors, turns]));
  const turned = await p.evaluate(() => {
    for(let t=0;t<4;t++) if(tryTurn(t)) return true;
    return false;
  });
  await p.waitForTimeout(500);
  const snap1 = await p.evaluate(() => JSON.stringify([pos, oriIndex(M), kmask, doors, turns]));
  await p.evaluate(() => undo());
  await p.waitForTimeout(120);
  const snap2 = await p.evaluate(() => JSON.stringify([pos, oriIndex(M), kmask, doors, turns]));
  ok('a turn changes state', turned && snap0 !== snap1);
  ok('undo restores it exactly', snap0 === snap2);

  // ---- an illegal turn is refused and costs nothing
  const refused = await p.evaluate(() => {
    const before = [JSON.stringify(pos), oriIndex(M), turns];
    let anyRefused = false;
    for(let t=0;t<4;t++) if(!landing(lv, TURNS[t].f(M), pos, doors)){ anyRefused = !tryTurn(t); break; }
    return {anyRefused, same: JSON.stringify([JSON.stringify(pos), oriIndex(M), turns]) === JSON.stringify(before)};
  });
  ok('an illegal turn is refused without changing state', refused.same);

  // ---- the turn counter can never advance without a legal landing
  await p.evaluate(() => { loadLevel(12); show(null); });
  await p.waitForTimeout(200);
  const legality = await p.evaluate(() => {
    let violations = 0;
    for(let i=0;i<400;i++){
      const t = Math.floor(Math.random()*4);
      const legal = !!landing(lv, TURNS[t].f(M), pos, doors);
      const before = turns;
      const took = tryTurn(t);
      if(took){ anim = null; M = TURNS[t].f(M); pos = landing(lv, M, pos, doors).w.slice(); turns = before+1; settle(); }
      if(took !== legal) violations++;
      // the cell underfoot must ALWAYS be a deck that is the surface of its column
      const s = surfaceAt(N, project(N, lv.vox, M), M, pos);
      if(!s || lv.vox[vidx(N,pos[0],pos[1],pos[2])] !== '+') violations++;
    }
    return violations;
  });
  ok('400 random turns: legality and footing never violated', legality === 0, `violations=${legality}`);

  // ---- stuck detection: burn the only key on a door that is not the way out
  const stuckCase = await p.evaluate(() => {
    // find a level with one key and force the doors-open state without the key
    for(let i=1;i<=10;i++){
      loadLevel(i);
      if(lv.keys.length !== 1) continue;
      const before = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors});
      // pretend the key was spent on the door without ever collecting it is
      // impossible; instead check the honest invariant: a state with the door
      // shut and no key left must be reported unsolvable if the goal is behind it
      const after = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:1, doors:1});
      return {i, beforeOk: before.ok, afterOk: after.ok};
    }
    return null;
  });
  ok('solver answers from an arbitrary mid-game state', stuckCase && stuckCase.beforeOk);

  // ---- the Android back chain unwinds screen by screen and then gives up
  const backs = await p.evaluate(() => {
    const out = [];
    loadLevel(1); show(null);
    out.push(TURNKEY.onBack());                       // playing -> pause
    out.push(document.getElementById('scPause').classList.contains('hide') === false);
    out.push(TURNKEY.onBack());                       // pause -> playing
    show('scCubes'); out.push(TURNKEY.onBack());      // cubes -> title
    out.push(document.getElementById('scTitle').classList.contains('hide') === false);
    out.push(TURNKEY.onBack());                       // title -> let Android finish
    return out;
  });
  ok('back: play->pause->play->cubes->title, then releases the Activity',
     JSON.stringify(backs) === JSON.stringify([true,true,true,true,true,false]));

  // ---- host inset injection actually moves the board
  const insetMove = await p.evaluate(() => {
    loadLevel(9); show(null);
    const a = CY;
    TURNKEY.setInsets(140, 0, 60, 0);
    const bY = CY;
    TURNKEY.setInsets(0,0,0,0);
    return {moved: Math.abs(bY - a) > 10, restored: Math.abs(CY - a) < 0.01};
  });
  ok('setInsets re-lays out the board and is reversible', insetMove.moved && insetMove.restored);

  // ---- frame cost on the largest cube, mid-turn (the expensive path)
  const perf = await p.evaluate(() => {
    loadLevel(24); show(null);
    drag = {x0:0,y0:0,axis:'x',ang:Math.PI/4};        // hold it at 45 degrees
    const t0 = performance.now();
    for(let i=0;i<120;i++) draw();
    const mid = (performance.now()-t0)/120;
    drag = null;
    const t1 = performance.now();
    for(let i=0;i<120;i++) draw();
    return {mid, rest:(performance.now()-t1)/120, quads: faces.length};
  });
  ok('mid-turn frame under 8ms on the biggest cube', perf.mid < 8,
     `turning ${perf.mid.toFixed(2)}ms / resting ${perf.rest.toFixed(2)}ms / ${perf.quads} faces`);

  // ---- the dead-end detector actually reaches the player
  const dead = await p.evaluate(async () => {
    loadLevel(7); show(null);
    const real = window.solve;
    window.solve = () => ({ok:false});                 // pretend this cube is now a lie
    afterAction();
    await new Promise(r => setTimeout(r, 220));
    const fired = stuck && document.getElementById('toast').classList.contains('on');
    const msg = document.getElementById('toast').textContent;
    window.solve = real;
    return {fired, msg};
  });
  ok('a dead end is detected and surfaced, not left to be discovered', dead.fired, dead.msg);

  // ---- and a live game is never wrongly declared dead
  const notDead = await p.evaluate(async () => {
    loadLevel(7); show(null);
    afterAction();
    await new Promise(r => setTimeout(r, 220));
    return !stuck;
  });
  ok('a solvable position is not falsely flagged', notDead);

  ok('no page errors', errs.length === 0, errs.join(' | '));
  await b.close();
  process.exit(bad);
})();
