/* THREE THINGS ADDED AT ONCE, AND EACH HAS ITS OWN WAY OF GOING WRONG.

   DISPLAY TRIM MUST BE FREE WHEN IT IS OFF. Both controls sit at 100 and at
   100 nothing may be applied — no filter property, no class, no per-frame
   full-screen pass. And `filter` makes an element a containing block for its
   positioned descendants, so turning it on must not move a single thing on
   screen. That is measured here rather than assumed.

   THE BOARDS SCREEN MUST SAY SOMETHING WITH NO HOST. Six rows that silently
   do nothing is worse than an honest sentence, and the records themselves
   are this device's own — they do not need Play Games to be true.

   THE COACH MUST NOT ADVANCE ON ITS OWN. A tutorial that walks itself
   forward is a slideshow; every step waits on the editor's real state, so
   the test does the wrong thing first and checks the step holds.          */
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

  /* ---- display trim ----------------------------------------------------- */
  const disp = await p.evaluate(() => {
    const de = document.documentElement;
    const filt = () => getComputedStyle(document.getElementById('stage')).filter;
    const off0 = {cls: de.classList.contains('disp'), filter: filt()};

    /* the geometry of everything that could care, before and after */
    const probe = () => ['hud','stage','scPause','turnPill','dpad'].map(id => {
      const e = document.getElementById(id);
      if(!e) return null;
      const r = e.getBoundingClientRect();
      return [id, Math.round(r.left), Math.round(r.top), Math.round(r.width), Math.round(r.height)];
    }).filter(Boolean);
    show(null);
    const before = probe();

    const r = document.getElementById('brightRange');
    r.value = 140; r.oninput();
    const on = {cls: de.classList.contains('disp'), filter: filt(), stored: store.bright,
                label: document.getElementById('brightVal').textContent};
    const after = probe();

    /* and back to the default must switch it off entirely, not to
       brightness(1) — an identity filter still costs a compositing pass */
    r.value = 100; r.oninput();
    const c = document.getElementById('contrastRange');
    c.value = 100; c.oninput();
    const off1 = {cls: de.classList.contains('disp'), filter: filt()};

    c.value = 130; c.oninput();
    const cOn = {cls: de.classList.contains('disp'), filter: filt(), stored: store.contrast};
    c.value = 100; c.oninput();

    return {off0, on, off1, cOn, moved: JSON.stringify(before) !== JSON.stringify(after), before, after};
  });
  ok('at the default nothing is filtered at all',
     disp.off0.cls === false && disp.off0.filter === 'none', `class ${disp.off0.cls}, filter ${disp.off0.filter}`);
  ok('brightness applies, is stored, and is shown as a number',
     disp.on.cls && /brightness\(1\.4\)/.test(disp.on.filter) && disp.on.stored === 140 && disp.on.label === '140%',
     `${disp.on.filter} ${disp.on.label}`);
  ok('...and turning a filter on moves nothing on screen',
     !disp.moved, disp.moved ? JSON.stringify(disp.before) + ' -> ' + JSON.stringify(disp.after) : 'geometry identical');
  ok('back at 100/100 the filter is removed, not set to identity',
     disp.off1.cls === false && disp.off1.filter === 'none', `class ${disp.off1.cls}, filter ${disp.off1.filter}`);
  ok('contrast works the same way',
     disp.cOn.cls && /contrast\(1\.3\)/.test(disp.cOn.filter) && disp.cOn.stored === 130, disp.cOn.filter);

  /* it has to survive a reload, which is the whole point of a setting */
  await p.evaluate(() => { store.bright = 120; store.contrast = 85; save(); });
  await p.waitForTimeout(320);
  await p.reload();
  await p.waitForTimeout(400);
  const kept = await p.evaluate(() => ({
    b: store.bright, c: store.contrast,
    cls: document.documentElement.classList.contains('disp'),
    filter: getComputedStyle(document.getElementById('stage')).filter,
    slider: +document.getElementById('brightRange').value,
    label: document.getElementById('contrastVal').textContent
  }));
  ok('the setting survives a reload and the slider agrees with it',
     kept.b === 120 && kept.c === 85 && kept.cls && kept.slider === 120 && kept.label === '85%',
     JSON.stringify(kept));
  await p.evaluate(() => { store.bright = 100; store.contrast = 100; save(); applyDisplay(); });

  /* ---- haptics were already here; check they still are ------------------ */
  const hap = await p.evaluate(() => {
    const r = document.getElementById('buzzRange');
    r.value = 60; r.oninput();
    return {stored: store.buzz, sw: !!document.getElementById('swHaptic'),
            range: [r.min, r.max]};
  });
  ok('the haptic switch and intensity slider are both present and live',
     hap.sw && hap.stored === 60 && hap.range[1] === '150', JSON.stringify(hap));

  /* ---- boards ----------------------------------------------------------- */
  const boards = await p.evaluate(() => {
    try{ delete window.AndroidGPG; }catch(e){ window.AndroidGPG = null; }
    store.dstreak = {cur:2, best:9, last:dayIndex()};
    store.dhist = {}; store.dhist[dayIndex()] = 14;
    store.daily = {day:dayIndex(), best:14, solved:1};
    levelNo = 12;
    openBoards('scPause');
    const rows = [...document.getElementById('boardList').children];
    const shown = !document.getElementById('scBoards').classList.contains('hide');
    const labels = rows.map(r => r.querySelector('.lbl').textContent);
    const vals = rows.map(r => r.querySelector('.bVal').textContent);
    const head = document.getElementById('boardHead').textContent;   /* read BEFORE a host exists */
    rows[0].click();
    const offMsg = document.getElementById('boardMsg').textContent;

    /* now stand a host up and check each row opens its own board */
    const sent = [];
    window.AndroidGPG = {submit:() => {}, show:(id) => sent.push(id)};
    buildBoards();
    const rows2 = [...document.getElementById('boardList').children];
    rows2.forEach(r => r.click());
    return {shown, labels, vals, offMsg, sent, head,
            offClass: rows[0].className, onClass: rows2[0].className};
  });
  ok('the boards screen opens with a row per board',
     boards.shown && boards.labels.length === 6, boards.labels.join(' · '));
  ok('...and every row shows this device’s own record',
     boards.vals[0] === '14 FOLDS' && boards.vals[4] === '9 DAYS' && /\d \/ 7 DAYS/.test(boards.vals[1]),
     boards.vals.join(' · '));
  ok('with no host it says so rather than presenting dead buttons',
     /LOCAL ONLY/.test(boards.head) && /NEED THE PLAY GAMES BUILD/.test(boards.offMsg) && / off/.test(boards.offClass),
     boards.offMsg);
  ok('with a host each row opens its own board',
     boards.sent.length === 6 && boards.sent[0] === 'daily' && boards.sent[4] === 'daily_streak' &&
     /^vault_\d\d$/.test(boards.sent[5]),
     boards.sent.join(' '));

  /* ---- the coach -------------------------------------------------------- */
  const coach = await p.evaluate(() => {
    try{ delete window.AndroidGPG; }catch(e){ window.AndroidGPG = null; }
    store.taughtForge = 0; store.made = {}; store.draft = null; ed = null;
    const txt = () => document.getElementById('edCoachT').textContent;
    const num = () => document.getElementById('edCoachN').textContent;
    const up  = () => !document.getElementById('edCoach').classList.contains('hide');

    openMade();
    document.getElementById('btnNewCube').click();
    const s1 = {up: up(), n: num(), t: txt()};

    /* the wrong thing first: laying too little must NOT advance it */
    ed.tool = '+';
    for(let i = 0; i < 4; i++) edApply(i, 0);
    const stuck = num();

    for(let i = 0; i < 5; i++) edApply(i, 1);           /* now nine cells */
    const s2 = num();
    document.getElementById('edUp').click();            /* step up a deck */
    const s3 = num();
    for(let i = 0; i < 5; i++){ edApply(i, 0); edApply(i, 1); }
    const s4 = num();
    ed.tool = 'S'; edApply(0, 0);
    const s5 = num();
    ed.tool = 'G'; edApply(4, 1);
    const s6 = num();

    /* a verify that fails must hold the step open and explain itself */
    document.getElementById('edCheck').click();
    const failed = {n: num(), t: txt(), msg: document.getElementById('edMsg').textContent};

    /* satisfy it the honest way: a real cube that solves */
    const src = BAKED[0];
    edFrom({n:src.n, vox:src.vox.replace('#','+'), start:src.start, goal:src.goal,
            keys:[], doors:[], name:'TAUGHT'}, null);
    document.getElementById('edName').value = 'TAUGHT';
    edDraw();
    document.getElementById('edCheck').click();
    const done = {n: num(), t: txt(), taught: store.taughtForge,
                  cls: document.getElementById('edCoach').className};

    /* it is offered once; a second visit is silent, and [ ? ] brings it back */
    ed = null; coach = -1;
    openEditor(null);
    const quiet = up();
    /* [ ? ] on a cube that is already built shows what is LEFT to do, not a
       lie about laying trace nobody still needs to lay */
    document.getElementById('edTeach').click();
    const onBuilt = {up: up(), n: num(), t: txt()};
    /* and on a fresh empty cube it opens at step one again */
    document.getElementById('btnNewCube').click();
    document.getElementById('edTeach').click();
    const onFresh = {up: up(), n: num(), t: txt()};
    return {s1, stuck, s2, s3, s4, s5, s6, failed, done, quiet, onBuilt, onFresh};
  });
  ok('the coach shows itself on the first cube anyone opens',
     coach.s1.up && coach.s1.n === '1' && /TRACE/.test(coach.s1.t), coach.s1.t);
  ok('...and does not advance until the step is actually done',
     coach.stuck === '1', `moved to ${coach.stuck} after four cells`);
  ok('laying enough trace advances it', coach.s2 === '2', coach.s2);
  ok('stepping up a deck advances it', coach.s3 === '3', coach.s3);
  ok('building on a second deck advances it', coach.s4 === '4', coach.s4);
  ok('placing a start advances it', coach.s5 === '5', coach.s5);
  ok('placing an exit advances it', coach.s6 === '6', coach.s6);
  ok('a failed verify holds the step and explains what is wrong',
     coach.failed.n === '6' && /BROKEN ONE/.test(coach.failed.t), coach.failed.t);
  ok('a cube that really solves finishes the lesson',
     /WHOLE JOB/.test(coach.done.t) && coach.done.taught === 1 && / done/.test(coach.done.cls), coach.done.t);
  ok('...and it is not shown again after that', coach.quiet === false);
  /* the restored cube already has its trace laid, so step one is skipped;
     it holds at "step up a deck", which is an action rather than a property
     of the cube and so is genuinely still undone in this session */
  ok('[ ? ] brings it back at what is still undone, skipping what is not',
     coach.onBuilt.up && Number(coach.onBuilt.n) > 1, `step ${coach.onBuilt.n}: ${coach.onBuilt.t}`);
  ok('...and on a fresh cube it opens at step one again',
     coach.onFresh.up && coach.onFresh.n === '1', `step ${coach.onFresh.n}`);

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
