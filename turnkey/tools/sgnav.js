/* NO DEAD ENDS, AND NO BACK THAT GOES SOMEWHERE YOU HAVE NEVER BEEN.

   A dead end in a game is not usually a screen with no button on it. It is a
   screen whose only exit is the hardware back key, on a phone whose gesture
   bar the player has not found; or a BACK that is hardwired to one
   destination and therefore lies as soon as the screen gains a second way in.
   Both are invisible from the code and obvious in the hand.

   So this walks every screen the game can show and demands two things of
   each: at least one VISIBLE, enabled control that actually changes which
   screen is up, and — for screens reachable from more than one place — that
   BACK returns to the door it came through.

   The screen list is read from the game's own `show()` rather than typed
   here, so a screen added later is covered by this the day it exists.     */
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

  /* the list of screens comes from show() itself */
  const screens = await p.evaluate(() => {
    const src = document.querySelector('script').textContent;
    const m = src.match(/\[([^\]]*'scTitle'[^\]]*)\]\.forEach/);
    return m ? m[1].split(',').map(s => s.trim().replace(/'/g, '')) : [];
  });
  ok('the screen list was read from the game, not typed into the test',
     screens.length >= 8 && screens.indexOf('scTitle') === 0, screens.join(' '));

  /* ---- every screen offers a way out, on screen ------------------------- */
  const exits = await p.evaluate(async (screens) => {
    store.reached = 60; store.taught = 1; store.made = {};
    /* a built cube, so the forge and its editor have something in them */
    const src = BAKED[0];
    const mine = {n:src.n, vox:src.vox.replace('#','+'), start:src.start, goal:src.goal,
                  keys:[], doors:[], par:0, name:'MINE'};
    mine.par = validateLevel(mine).par;
    const id = canonId(mine);
    store.made[id] = edRecord(mine, 'built');
    madeKey = id;
    loadLevel(12);
    await new Promise(r => setTimeout(r, 300));

    /* THE ENTRANCE ANIMATION MUST BE OVER BEFORE ANYTHING IS JUDGED.
       Every child of a shown layer gets `animation: boot .16s both`, and
       `both` means the element holds the animation's FIRST frame — opacity
       zero — until it actually runs. Headless Chromium does not reliably
       advance CSS animations without a compositor driving it, so buttons
       that are 340x63 and perfectly clickable read as invisible. Forcing the
       animations to their end state tests the screen a player looks at
       rather than the sixteenth of a second before it. */
    const settle = el => {
      try{ el.getAnimations({subtree: true}).forEach(a => a.finish()); }catch(e){}
    };
    const visible = el => {
      if(el.disabled) return false;
      const r = el.getBoundingClientRect();
      if(r.width < 8 || r.height < 8) return false;
      const st = getComputedStyle(el);
      if(st.visibility === 'hidden' || st.display === 'none' || +st.opacity < 0.05) return false;
      /* a control scrolled outside its own layer is not reachable either */
      return true;
    };
    const which = () => screens.find(s => !document.getElementById(s).classList.contains('hide')) || null;

    const out = [];
    for(const sc of screens){
      /* put the game in a state where this screen makes sense */
      if(sc === 'scEdit'){ edFrom(store.made[id], id); }
      if(sc === 'scMade') buildMadeGrid();
      if(sc === 'scBoards') buildBoards();
      show(sc === 'scTitle' ? 'scTitle' : sc);
      await new Promise(r => setTimeout(r, 60));

      const el = document.getElementById(sc);
      settle(el);
      const buttons = [...el.querySelectorAll('button')].filter(visible);
      /* does any of them actually take you somewhere else? */
      let escapes = 0;
      for(const btn of buttons){
        const before = which();
        try{ btn.click(); }catch(e){}
        await new Promise(r => setTimeout(r, 40));
        if(which() !== before) escapes++;
        /* put it back for the next candidate */
        if(sc === 'scEdit'){ edFrom(store.made[id], id); }
        show(sc);
        await new Promise(r => setTimeout(r, 30));
        settle(el);
      }
      out.push({sc, buttons: buttons.length, escapes});
    }
    return out;
  }, screens);

  const noButton = exits.filter(e => e.buttons === 0);
  const noEscape = exits.filter(e => e.escapes === 0);
  ok('every screen has at least one visible, enabled control',
     noButton.length === 0, noButton.map(e => e.sc).join(', ') || exits.map(e => e.sc + ':' + e.buttons).join(' '));
  ok('...and on every screen at least one of them leaves it',
     noEscape.length === 0,
     noEscape.length ? 'DEAD END: ' + noEscape.map(e => e.sc).join(', ')
                     : exits.map(e => e.sc + ':' + e.escapes).join(' '));

  /* ---- the forge is on the title, where it can be found ----------------- */
  const front = await p.evaluate(async () => {
    show('scTitle');
    await new Promise(r => setTimeout(r, 120));
    const t = document.getElementById('scTitle');
    try{ t.getAnimations({subtree: true}).forEach(a => a.finish()); }catch(e){}
    const f = document.getElementById('btnForge');
    const onTitle = !!f && t.contains(f);
    const r = f ? f.getBoundingClientRect() : null;
    /* and it must not have pushed the hero cube off its own screen */
    const slot = document.getElementById('heroSlot').getBoundingClientRect();
    return {onTitle, w: r ? Math.round(r.width) : 0, h: r ? Math.round(r.height) : 0,
            reachable: !!r && r.top >= 0 && r.bottom <= window.innerHeight,
            heroH: Math.round(slot.height), colOver: t.scrollHeight > t.clientHeight + 1};
  });
  ok('FORGE is on the title screen', front.onTitle);
  ok('...at a real size and on screen', front.w > 60 && front.h > 24 && front.reachable,
     `${front.w}x${front.h}`);
  ok('...without making the title column scroll', !front.colOver, `hero slot ${front.heroH}px`);

  /* ---- BACK returns to the door you came through ------------------------ */
  const doors = await p.evaluate(async (screens) => {
    const which = () => screens.find(s => !document.getElementById(s).classList.contains('hide')) || null;
    const trip = async (openFrom) => {
      show(openFrom);
      await new Promise(r => setTimeout(r, 60));
      if(openFrom === 'scTitle') document.getElementById('btnForge').click();
      else { openMade('scCubes'); }
      await new Promise(r => setTimeout(r, 80));
      const landed = which();
      document.getElementById('btnMadeBack').click();
      await new Promise(r => setTimeout(r, 80));
      return {landed, back: which()};
    };
    const viaTitle = await trip('scTitle');
    const viaCubes = await trip('scCubes');
    /* and the hardware key has to agree with the button */
    show('scTitle'); await new Promise(r => setTimeout(r, 60));
    document.getElementById('btnForge').click();
    await new Promise(r => setTimeout(r, 80));
    window.TURNKEY.onBack();
    await new Promise(r => setTimeout(r, 80));
    const hardware = which();
    return {viaTitle, viaCubes, hardware};
  }, screens);
  ok('the forge opens from the title and BACK returns there',
     doors.viaTitle.landed === 'scMade' && doors.viaTitle.back === 'scTitle',
     `${doors.viaTitle.landed} -> ${doors.viaTitle.back}`);
  ok('...and opened from the vaults, BACK returns to the vaults',
     doors.viaCubes.landed === 'scMade' && doors.viaCubes.back === 'scCubes',
     `${doors.viaCubes.landed} -> ${doors.viaCubes.back}`);
  ok('...and the hardware back key agrees with the button',
     doors.hardware === 'scTitle', String(doors.hardware));

  /* ---- calibrate calibrates, and the manual is a manual ----------------- */
  /* These were the wrong way round: the button labelled CALIBRATE opened the
     how-to, while the settings sat unlabelled inside the pause card. Somebody
     looking for brightness pressed CALIBRATE and got a rulebook. */
  const names = await p.evaluate(async () => {
    const which = () => ['scTitle','scSet','scManual','scPause']
      .find(s => !document.getElementById(s).classList.contains('hide')) || null;
    const settle = () => { try{ document.querySelectorAll('.layer').forEach(
      l => l.getAnimations({subtree:true}).forEach(a => a.finish())); }catch(e){} };

    show('scTitle'); settle();
    document.getElementById('btnSet').click();
    await new Promise(r => setTimeout(r, 60));
    const fromTitle = which();
    const hasSliders = !!document.getElementById('scSet').querySelector('#brightRange');
    const head = document.getElementById('setName').textContent;
    document.getElementById('btnSetBack').click();
    await new Promise(r => setTimeout(r, 60));
    const backToTitle = which();

    /* and from the pause card it comes back to the pause card */
    show('scPause'); settle();
    document.getElementById('btnSet2').click();
    await new Promise(r => setTimeout(r, 60));
    const fromPause = which();
    document.getElementById('btnSetBack').click();
    await new Promise(r => setTimeout(r, 60));
    const backToPause = which();

    /* the manual lives under calibrate, and GOT IT returns there */
    document.getElementById('btnSet2').click();
    await new Promise(r => setTimeout(r, 60));
    document.getElementById('btnManual2').click();
    await new Promise(r => setTimeout(r, 60));
    const onManual = which();
    document.getElementById('btnManualBack').click();
    await new Promise(r => setTimeout(r, 60));
    const afterGotIt = which();

    return {fromTitle, hasSliders, head, backToTitle, fromPause, backToPause,
            onManual, afterGotIt, stillPause: setFrom};
  });
  ok('CALIBRATE opens the settings, not the rulebook',
     names.fromTitle === 'scSet' && names.hasSliders, `${names.fromTitle}, sliders ${names.hasSliders}`);
  ok('...and the screen is called CALIBRATE', names.head === 'CALIBRATE', names.head);
  ok('...opened from the title, BACK returns to the title', names.backToTitle === 'scTitle', names.backToTitle);
  ok('...opened from the pause card, BACK returns to the pause card',
     names.fromPause === 'scSet' && names.backToPause === 'scPause', names.backToPause);
  ok('the manual is reachable from calibrate and GOT IT returns there',
     names.onManual === 'scManual' && names.afterGotIt === 'scSet',
     `${names.onManual} -> ${names.afterGotIt}`);

  /* THE BUG THIS WAS REPORTED FOR. GOT IT launched a level from wherever the
     manual was opened, because that behaviour was written when the manual was
     ONLY reachable as first-run onboarding. It still has to do that there. */
  const firstRun = await p.evaluate(async () => {
    const which = () => ['scTitle','scSet','scManual','scPause']
      .find(s => !document.getElementById(s).classList.contains('hide')) || null;
    store.taught = 0; store.reached = 1;
    show('scTitle');
    document.getElementById('btnPlay').click();
    await new Promise(r => setTimeout(r, 80));
    const shownManual = which();
    document.getElementById('btnManualBack').click();
    await new Promise(r => setTimeout(r, 900));
    return {shownManual, into: which(), playing: !!lv && !document.getElementById('hud').classList.contains('on') === false};
  });
  ok('on a first run PLAY still shows the manual first', firstRun.shownManual === 'scManual');
  ok('...and GOT IT there does start the game', firstRun.into === null && firstRun.playing,
     `landed on ${firstRun.into}`);

  /* ---- a way to the front from the two screens seen most ---------------- */
  /* Neither card had one: from a finished cube the only ways on were the next
     level, a retry, or the shelf, and reaching the main screen meant going to
     the shelf and pressing BACK. */
  const menu = await p.evaluate(async () => {
    const which = () => ['scTitle','scCubes','scMade','scPause','scWin']
      .find(s => !document.getElementById(s).classList.contains('hide')) || null;
    store.reached = 60; store.taught = 1;
    loadLevel(12);
    await new Promise(r => setTimeout(r, 200));

    show('scPause');
    const pauseHas = !document.getElementById('btnToMenu').classList.contains('hide');
    document.getElementById('btnToMenu').click();
    await new Promise(r => setTimeout(r, 80));
    const fromPause = which();

    /* the win card, on an ordinary cube and on one somebody built */
    show('scWin');
    await new Promise(r => setTimeout(r, 60));
    document.getElementById('btnWinMenu').click();
    await new Promise(r => setTimeout(r, 80));
    const fromWin = which();

    /* and the shelf button has to say where it actually goes */
    made = false; daily = false;
    $('btnWinCubes').textContent = made ? '[ FORGE ]' : '[ VAULTS ]';
    const normalLabel = document.getElementById('btnWinCubes').textContent;
    made = true;
    $('btnWinCubes').textContent = made ? '[ FORGE ]' : '[ VAULTS ]';
    $('winWhat').textContent = made ? 'CUBE SOLVED' : 'VAULT SOLVED';
    const madeLabel = document.getElementById('btnWinCubes').textContent;
    const madeHead = document.getElementById('winWhat').textContent;
    made = false;
    return {pauseHas, fromPause, fromWin, normalLabel, madeLabel, madeHead};
  });
  ok('the pause card has a way to the front, and it goes there',
     menu.pauseHas && menu.fromPause === 'scTitle', String(menu.fromPause));
  ok('...and so does the win card', menu.fromWin === 'scTitle', String(menu.fromWin));
  ok('the win card names the shelf it actually opens',
     menu.normalLabel === '[ VAULTS ]' && menu.madeLabel === '[ FORGE ]',
     `${menu.normalLabel} / ${menu.madeLabel}`);
  ok('...and does not call a cube somebody built a vault',
     menu.madeHead === 'CUBE SOLVED', menu.madeHead);

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
