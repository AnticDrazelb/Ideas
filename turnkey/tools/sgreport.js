/* REPORTING, AND THE BLACKLIST IT FEEDS.

   Two things have to be true or this feature is theatre.

   A REPORT MUST NOT SILENTLY VANISH. One offline HTML file cannot deliver
   mail; it can only hand a finished message to whatever app the phone has.
   In a WebView with no mail client that hand-off goes nowhere and the player
   walks away believing they reported something. So the message is composed,
   handed over AND copied, and the test checks the composed body actually
   carries the identity and a working share code — a report that does not
   say which cube it is about is a report nobody can action.

   AND A BLOCKED CUBE MUST NOT COME BACK. The blacklist is keyed on the
   canonical identity, which is taken over all twenty-four orientations, so
   the obvious evasion — reshare it lying on its side — has to fail. That is
   the assertion this file exists for.                                     */
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

  /* ---- the button appears in exactly one situation ---------------------- */
  const vis = await p.evaluate(async () => {
    store.reached = 60; store.made = {}; store.hidden = {};
    const src = BAKED[0];
    const mine = {n:src.n, vox:src.vox.replace('#','+'), start:src.start, goal:src.goal,
                  keys:[], doors:[], par:0, name:'THEIRS'};
    mine.par = validateLevel(mine).par;
    const id = canonId(mine);
    store.made[id] = edRecord(mine, 'import');
    /* ASK THE BROWSER, NOT THE CLASS LIST. This used to read
       classList.contains('hide') and passed for months while the button was
       on screen on every ranked cube in the game: the class was going on and
       off exactly as intended and no rule in the stylesheet answered it.
       A control is hidden when it is not drawn, and that is a different
       question. checkVisibility walks the ancestors, so it also covers the
       panel fade that hides the whole card during play. */
    const drawn = () => {
      const el = document.getElementById('btnReport');
      document.querySelectorAll('.layer').forEach(l =>
        l.getAnimations({subtree:true}).forEach(a => a.finish()));
      return el.checkVisibility({opacityProperty:true, visibilityProperty:true}) &&
             el.getBoundingClientRect().height > 0;
    };

    loadLevel(12); show('scPause');
    const onNormal = drawn();
    loadLevel(DAILY); show('scPause');
    const onDaily = drawn();
    madeKey = id; loadLevel(MADE);
    await new Promise(r => setTimeout(r, 300));
    show('scPause');
    const onMade = drawn();
    show(null);
    const whenPlaying = drawn();
    /* the mechanism itself: .hide on a plain element has to mean something */
    const probe = (() => {
      const d = document.createElement('button');
      d.className = 'btn ghost hide'; document.body.appendChild(d);
      const v = getComputedStyle(d).display; d.remove(); return v;
    })();
    return {id, onNormal, onDaily, onMade, whenPlaying, probe};
  });
  ok('the hide class actually hides a control, not just labels it',
     vis.probe === 'none', `.btn.ghost.hide computes display:${vis.probe}`);
  ok('no report button on a cube the game shipped', vis.onNormal === false);
  ok('...nor on the daily', vis.onDaily === false);
  ok('...but there is one on a cube somebody built', vis.onMade === true);
  ok('...and it is not on screen while playing', vis.whenPlaying === false);

  /* ---- it takes two taps, and says what it is going to do --------------- */
  const flow = await p.evaluate(async () => {
    let mailed = null;
    window.AndroidHost = {mail:(to, subject, body) => { mailed = {to, subject, body}; }};
    show('scPause');
    const btn = document.getElementById('btnReport'), msg = document.getElementById('reportMsg');

    btn.click();
    const armed = {t: msg.textContent, mailed: !!mailed, hidden: !!store.hidden[madeKey]};
    /* leaving the screen must disarm it */
    show(null); show('scPause');
    const disarmed = reportArmed;
    btn.click();                                  /* arms again */
    btn.click();                                  /* and sends */
    const sent = {t: msg.textContent, to: mailed && mailed.to,
                  subject: mailed && mailed.subject, body: mailed && mailed.body,
                  hidden: !!store.hidden[madeKey]};
    buildMadeGrid();
    const tiles = document.getElementById('madeGrid').children.length;
    const head = document.getElementById('madeCount').textContent;
    return {armed, disarmed, sent, tiles, head};
  });
  ok('one tap arms rather than sends', flow.armed.mailed === false && /TAP AGAIN/.test(flow.armed.t), flow.armed.t);
  ok('...and is nothing yet, so the cube is still there', flow.armed.hidden === false);
  ok('leaving the pause screen disarms it', flow.disarmed === '', `armed "${flow.disarmed}"`);
  ok('the second tap hands a message to the mail app',
     flow.sent.to === 'jeffrey_blezard@icloud.com' && /cube report/.test(flow.sent.subject || ''),
     `${flow.sent.to} · ${flow.sent.subject}`);
  ok('...and the reported cube leaves this device’s shelf',
     flow.sent.hidden === true && flow.tiles === 0 && /1 REPORTED/.test(flow.head), flow.head);

  /* the body has to be actionable on its own */
  const body = flow.sent.body || '';
  const codeLine = body.split(/\r?\n/).filter(l => /^[A-Za-z0-9_-]{40,}$/.test(l))[0] || '';
  const round = await p.evaluate(c => {
    const lv = shareDecode(c);
    return lv ? {ok: validateLevel(lv).ok, id: canonId(lv)} : null;
  }, codeLine);
  ok('the report names the cube by its identity', body.indexOf(vis.id) >= 0, vis.id);
  ok('...and carries a code that really loads that cube',
     !!round && round.ok && round.id === vis.id, JSON.stringify(round));
  ok('...and says where the cube came from', /imported from a share code/.test(body));

  /* ---- the blacklist ---------------------------------------------------- */
  const block = await p.evaluate(async () => {
    store.made = {}; store.hidden = {};
    const src = BAKED[4];
    const nasty = {n:src.n, vox:src.vox.replace('#','+'), start:src.start, goal:src.goal,
                   keys:src.keys, doors:src.doors, par:0, name:'NASTY'};
    nasty.par = validateLevel(nasty).par;
    const id = canonId(nasty);
    const code = shareEncode(nasty);

    /* block it the way sgblock.js would */
    BLOCKED[id] = '2026-08-12 reported';

    document.getElementById('impCode').value = code;
    document.getElementById('btnImport').click();
    const refused = {msg: document.getElementById('madeMsg').textContent,
                     stored: Object.keys(store.made).length};

    /* THE EVASION: the same cube, rotated, reshared. The identity is taken
       over all twenty-four orientations, so this must be refused too. */
    const spin = m => {
      const n = nasty.n, vox = new Array(n*n*n);
      for(let y = 0; y < n; y++) for(let z = 0; z < n; z++) for(let x = 0; x < n; x++){
        const v = viewOf(n, m, [x,y,z]);
        vox[vidx(n, v[0], v[1], v[2])] = nasty.vox[vidx(n,x,y,z)];
      }
      const pt = w => viewOf(n, m, w);
      return {n:n, vox:vox.join(''), start:pt(nasty.start), goal:pt(nasty.goal),
              keys:nasty.keys.map(pt), doors:nasty.doors.map(pt), par:nasty.par, name:'TURNED'};
    };
    /* not every orientation leaves a playable cube — the start can end up
       buried under the projection — so take one that genuinely would import
       if it were not blocked, or the test proves nothing */
    let turned = null;
    for(let i = 1; i < ORIS.length && !turned; i++){
      const c = spin(ORIS[i]);
      if(c.vox !== nasty.vox && validateLevel(c).ok) turned = c;
    }
    const foundTurn = !!turned;
    document.getElementById('impCode').value = turned ? shareEncode(turned) : '';
    document.getElementById('btnImport').click();
    const rotated = {msg: document.getElementById('madeMsg').textContent,
                     stored: Object.keys(store.made).length,
                     found: foundTurn,
                     sameId: foundTurn && canonId(turned) === id};

    /* and one already sitting in the shelf when the block ships */
    delete BLOCKED[id];
    store.made[id] = edRecord(nasty, 'import');
    const before = Object.keys(store.made).length;
    BLOCKED[id] = 'blocked later';
    const swept = purgeBlocked();

    /* a cube that vanished under a player must not take the loader down */
    madeKey = id;
    let threw = false;
    try{ loadLevel(MADE); }catch(e){ threw = String(e); }
    delete BLOCKED[id];
    return {refused, rotated, before, swept, after: Object.keys(store.made).length, threw};
  });
  ok('a blocked cube will not import', /BLOCKED/.test(block.refused.msg) && block.refused.stored === 0, block.refused.msg);
  ok('...and neither will the same cube shared rotated — the obvious evasion',
     block.rotated.found && block.rotated.sameId && /BLOCKED/.test(block.rotated.msg) &&
     block.rotated.stored === 0,
     `a playable rotation exists: ${block.rotated.found} · same identity: ${block.rotated.sameId} · ${block.rotated.msg}`);
  ok('a block that ships later sweeps the shelf at boot',
     block.before === 1 && block.swept === 1 && block.after === 0,
     `${block.before} -> ${block.after}`);
  ok('...and loading a cube that is gone does not throw', block.threw === false, String(block.threw));

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
