/* THE FORGE — an editor is only as good as its two refusals.

   IT MUST NOT SHIP A CUBE THAT CANNOT BE FINISHED. Par is the solver's
   proven minimum and every scored thing in the game reads it, so a level
   with no solution has no par and nothing can score it. That is a refusal,
   not a warning.

   AND IT MUST NOT SHIP A CUBE THAT ALREADY EXISTS. Which means knowing what
   "already" means: a cube rotated is the same cube, so identity is taken
   over all twenty-four orientations, and keys stay married to their doors
   through the transform — sorting those two lists apart would let a cube
   with its pairings swapped hash as a duplicate of a completely different
   puzzle.

   The share code is the third thing, and it is the one that has to survive
   a stranger: it round-trips exactly, it rejects a mistyped character, and
   it cannot smuggle a name that is markup.                                */
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

  /* ---- identity --------------------------------------------------------- */
  const ident = await p.evaluate(() => {
    const base = BAKED[4];                       /* has a key and a door */
    const id0 = canonId(base);
    /* the same cube written down from every one of the twenty-four
       orientations must come back with the same identity */
    const ids = ORIS.map(m => {
      const n = base.n, vox = new Array(n*n*n);
      for(let y = 0; y < n; y++) for(let z = 0; z < n; z++) for(let x = 0; x < n; x++){
        const v = viewOf(n, m, [x,y,z]);
        vox[vidx(n, v[0], v[1], v[2])] = base.vox[vidx(n,x,y,z)];
      }
      const pt = w => viewOf(n, m, w);
      return canonId({n:n, vox:vox.join(''), start:pt(base.start), goal:pt(base.goal),
                      keys:base.keys.map(pt), doors:base.doors.map(pt)});
    });
    /* and a cube with its key/door pairing swapped must NOT */
    const two = {n:base.n, vox:base.vox, start:base.start, goal:base.goal,
                 keys:[base.keys[0], base.goal], doors:[base.doors[0], base.start]};
    const swapped = {n:base.n, vox:base.vox, start:base.start, goal:base.goal,
                     keys:[base.goal, base.keys[0]], doors:[base.doors[0], base.start]};
    /* one changed voxel is a different cube */
    const flipAt = base.vox.indexOf('#');
    const tweaked = {n:base.n, vox:base.vox.slice(0, flipAt) + '+' + base.vox.slice(flipAt+1),
                     start:base.start, goal:base.goal, keys:base.keys, doors:base.doors};
    return {id0, uniform: new Set(ids).size, count: ids.length,
            pairing: canonId(two) !== canonId(swapped),
            tweak: canonId(tweaked) !== id0, len: id0.length};
  });
  ok('a cube rotated is the same cube, in all twenty-four orientations',
     ident.uniform === 1, `${ident.count} orientations gave ${ident.uniform} distinct id(s)`);
  ok('an identity is eight characters', ident.len === 8, ident.id0);
  ok('swapping which node opens which lock is a different cube', ident.pairing);
  ok('changing one voxel is a different cube', ident.tweak);

  /* ---- the two refusals ------------------------------------------------- */
  const deny = await p.evaluate(() => {
    const base = BAKED[0];
    const asIs = collisionOf(base, null);
    /* a solid block with a start and an exit and no way between them */
    /* two exposed traces with nothing between them: the start is on the
       surface, so this gets past the geometry checks and has to be refused
       by the solver rather than before it */
    const n = 5, air = new Array(n*n*n).fill('.');
    air[vidx(n,0,0,0)] = '+'; air[vidx(n,4,4,4)] = '+';
    const dead = {n:n, vox:air.join(''), start:[0,0,0], goal:[4,4,4], keys:[], doors:[], par:0};
    const empty = {n:n, vox:new Array(n*n*n).fill('.').join(''),
                   start:[0,0,0], goal:[4,4,4], keys:[], doors:[], par:0};
    const lop = {n:base.n, vox:base.vox, start:base.start, goal:base.goal,
                 keys:[base.start], doors:[], par:0};
    const same = {n:base.n, vox:base.vox, start:base.start, goal:base.start, keys:[], doors:[], par:0};
    return {
      asIs: asIs && asIs.kind, asIsLevel: asIs && asIs.level, asIsName: asIs && asIs.name,
      dead: validateLevel(dead).why,
      empty: validateLevel(empty).why,
      lop: validateLevel(lop).why,
      same: validateLevel(same).why,
      good: validateLevel(base)
    };
  });
  ok('an authored cube offered back is recognised as already existing',
     deny.asIs === 'baked' && deny.asIsLevel === 1, `${deny.asIs} ${deny.asIsLevel} ${deny.asIsName}`);
  ok('a cube with no route is refused', deny.dead === 'NO SOLUTION EXISTS', String(deny.dead));
  ok('a cube of pure void is refused', !!deny.empty, String(deny.empty));
  ok('a node with no lock is refused', deny.lop === 'EVERY NODE NEEDS A LOCK', String(deny.lop));
  ok('a start that is also the exit is refused', !!deny.same, String(deny.same));
  ok('...and a real cube passes, at the par it was authored with',
     deny.good.ok && deny.good.par === 1, JSON.stringify(deny.good));

  /* ---- the baked catalogue ---------------------------------------------- */
  /* the whole point of sgbake.js: a cube from deep in the generated game is
     recognised without the phone ever minting it */
  const cat = await p.evaluate(() => {
    const idx = mintedIndex(), n = Object.keys(idx).length;
    const out = [];
    for(const L of [1, 91, 500, 1400, 2070]){
      const hit = collisionOf(levelData(L), null);
      out.push({L, kind: hit && hit.kind, at: hit && hit.level});
    }
    /* and one past the ranked region is NOT in the index, by construction */
    const beyond = collisionOf(levelData(2400), null);
    return {n, out, beyond: beyond && beyond.kind, ids: MINTED_IDS.length};
  });
  ok('the catalogue index covers the whole ranked region',
     cat.n === 2070 && cat.ids === 2070*8, `${cat.n} identities, ${cat.ids} chars`);
  const misses = cat.out.filter(r => !(r.at === r.L && (r.kind === 'minted' || r.kind === 'baked')));
  ok('a generated cube offered as your own is recognised, by level number',
     misses.length === 0, misses.length ? JSON.stringify(misses) : cat.out.map(r => r.L + ':' + r.kind).join(' '));
  ok('...and a cube from past the ranked region is not claimed',
     cat.beyond === null || cat.beyond === undefined, String(cat.beyond));

  /* ---- share codes ------------------------------------------------------ */
  const share = await p.evaluate(() => {
    const out = [];
    for(const i of [0, 4, 9]){                   /* n = 5, 6 and 7 */
      const src = BAKED[i], code = shareEncode(src), back = shareDecode(code);
      const same = back && back.n === src.n && back.vox === src.vox &&
        String(back.start) === String(src.start) && String(back.goal) === String(src.goal) &&
        String(back.keys) === String(src.keys) && String(back.doors) === String(src.doors);
      out.push({n:src.n, len:code.length, same:!!same, id: canonId(src) === (back ? canonId(back) : '')});
    }
    const code = shareEncode(BAKED[4]);
    /* one character changed anywhere must fail the checksum, not decode into
       a different cube */
    let slipped = 0, tried = 0;
    for(let i = 0; i < code.length; i += 7){
      const c = code.charAt(i) === 'A' ? 'B' : 'A';
      const bent = code.slice(0, i) + c + code.slice(i+1);
      if(bent === code) continue;
      tried++;
      const r = shareDecode(bent);
      if(r && canonId(r) === canonId(BAKED[4])) continue;   /* same cube, fine */
      if(r) slipped++;
    }
    return {out, tried, slipped,
            junk: shareDecode('not a code at all'), empty: shareDecode(''),
            spaced: !!shareDecode(code.replace(/(.{8})/g, '$1 '))};
  });
  share.out.forEach(r => ok(`a ${r.n}-cube survives the round trip (${r.len} chars)`, r.same && r.id));
  ok('a single bent character is caught, never silently decoded',
     share.slipped === 0, `${share.tried} mutations, ${share.slipped} slipped through`);
  ok('nonsense is rejected', share.junk === null && share.empty === null);
  ok('...but whitespace a share sheet added is forgiven', share.spaced);

  /* ---- the editor, driven the way a player drives it --------------------- */
  const edit = await p.evaluate(async () => {
    store.made = {};
    show('scCubes');
    document.getElementById('btnForge').click();
    const onMade = !document.getElementById('scMade').classList.contains('hide');
    document.getElementById('btnNewCube').click();
    const onEdit = !document.getElementById('scEdit').classList.contains('hide');
    const cells = document.getElementById('edSlice').children.length;

    /* an empty cube cannot be verified */
    document.getElementById('edCheck').click();
    const emptyWhy = document.getElementById('edMsg').textContent;

    /* load an authored cube in and it must be refused as a duplicate */
    edFrom(BAKED[0], null); edDraw();
    document.getElementById('edCheck').click();
    const dupWhy = document.getElementById('edMsg').textContent;

    /* change one cell of it and it becomes new — find a lattice cell on the
       current deck and turn it into a trace */
    let painted = false;
    for(let z = 0; z < ed.n && !painted; z++) for(let x = 0; x < ed.n && !painted; x++){
      if(ed.vox[vidx(ed.n, x, ed.layer, z)] === '#'){
        ed.tool = '+'; edApply(x, z); painted = true;
      }
    }
    document.getElementById('edName').value = 'my <b>cube</b>';
    document.getElementById('edCheck').click();
    const newWhy = document.getElementById('edMsg').textContent;

    document.getElementById('edSave').click();
    const saved = Object.keys(store.made);
    const rec = store.made[saved[0]];
    document.getElementById('edBack').click();
    const tiles = document.getElementById('madeGrid').children.length;
    const tileText = document.getElementById('madeGrid').innerHTML;

    return {onMade, onEdit, cells, emptyWhy, dupWhy, newWhy, painted,
            count: saved.length, name: rec && rec.name, par: rec && rec.par,
            id: saved[0], tiles, injected: /<b>cube<\/b>/.test(tileText)};
  });
  ok('the forge opens from the vault shelf', edit.onMade);
  ok('a new cube opens the editor on a 5x5 deck', edit.onEdit && edit.cells === 25, `${edit.cells} cells`);
  ok('an empty cube will not verify', /START|EXIT/.test(edit.emptyWhy), edit.emptyWhy);
  ok('an authored cube offered as your own is denied by name',
     /ALREADY EXISTS/.test(edit.dupWhy) && /FOOTING/.test(edit.dupWhy), edit.dupWhy);
  ok('one changed cell makes it a new cube', edit.painted && /VERIFIED/.test(edit.newWhy), edit.newWhy);
  ok('saving keeps it under its own identity', edit.count === 1 && edit.id.length === 8, edit.id);
  ok('a name is folded to the interface alphabet, so it can never be markup',
     edit.name === 'MY BCUBEB' && !edit.injected, JSON.stringify(edit.name));
  ok('...and it appears on the shelf', edit.tiles === 1, `${edit.tiles} tiles`);

  /* ---- importing -------------------------------------------------------- */
  const imp = await p.evaluate(() => {
    store.made = {}; buildMadeGrid();
    const src = BAKED[6];
    const mine = {n:src.n, vox:src.vox.replace('#', '+'), start:src.start, goal:src.goal,
                  keys:src.keys, doors:src.doors, par:0, name:'GIFT'};
    const code = shareEncode(mine);
    const set = c => { document.getElementById('impCode').value = c;
                       document.getElementById('btnImport').click();
                       return document.getElementById('madeMsg').textContent; };
    const first = set(code), n1 = Object.keys(store.made).length;
    const again = set(code), n2 = Object.keys(store.made).length;
    const junk  = set('AAAA');
    const known = set(shareEncode(BAKED[0]));
    return {first, n1, again, n2, junk, known};
  });
  ok('a shared code imports and lands on the shelf',
     /IMPORTED/.test(imp.first) && imp.n1 === 1, imp.first);
  ok('the same code twice is refused, not duplicated',
     /ALREADY HERE/.test(imp.again) && imp.n2 === 1, imp.again);
  ok('a broken code is refused', /NOT A CUBE CODE/.test(imp.junk), imp.junk);
  ok('a code carrying a cube the game already ships is refused by name',
     /ALREADY HERE/.test(imp.known) && /FOOTING/.test(imp.known), imp.known);

  /* ---- playing one ------------------------------------------------------ */
  const play = await p.evaluate(async () => {
    store.made = {};
    const src = BAKED[0];
    const mine = {n:src.n, vox:src.vox.replace('#', '+'), start:src.start, goal:src.goal,
                  keys:[], doors:[], par:0, name:'MINE'};
    const v = validateLevel(mine); mine.par = v.par;
    const id = canonId(mine);
    store.made[id] = edRecord(mine);
    madeKey = id;
    const reached0 = store.reached;
    /* finish() is called directly, so the fold count has to be set the way
       playing would have set it — recording zero proves nothing */
    loadLevel(MADE); show(null);
    await new Promise(r => setTimeout(r, 500));
    const nm = lv.name, isMade = made, par = lv.par;
    turns = 4;
    finish();
    await new Promise(r => setTimeout(r, 80));
    return {nm, isMade, par, reached0, reached1: store.reached,
            best: store.made[id].best, tbest: store.made[id].tbest > 0,
            leakedBest: store.best[DAILY_SPEC_LEVEL], vaultPosted: store.vbest[3]};
  });
  ok('a built cube loads and plays under its own name',
     play.isMade && play.nm === 'MINE' && play.par > 0, `${play.nm} par ${play.par}`);
  ok('clearing it records its own best and time',
     play.best > 0 && play.tbest, `best ${play.best}`);
  ok('...and does not touch the progression',
     play.reached1 === play.reached0, `${play.reached0} -> ${play.reached1}`);
  ok('...and posts to no leaderboard and no vault',
     play.leakedBest === undefined && play.vaultPosted === undefined,
     `best ${play.leakedBest}, vault ${play.vaultPosted}`);

  /* ---- an unsaved cube survives the app dying, and back goes back ------- */
  const persist = await p.evaluate(() => {
    store.made = {}; store.draft = null; ed = null;
    edNew(6); openEditor(null);
    ed.tool = '+'; edApply(2, 3);
    document.getElementById('edName').value = 'HALF DONE';
    edLevel();                                   /* folds the name in, drafts */
    const d = store.draft;

    /* the editor forgets everything, the way a killed Activity does */
    ed = null;
    openEditor(null);
    const got = {n: ed.n, cell: ed.vox[vidx(ed.n, 2, 0, 3)], name: ed.name};

    /* and the hardware back button knows both new screens */
    const b1 = window.TURNKEY.onBack();
    const onMade = !document.getElementById('scMade').classList.contains('hide');
    const b2 = window.TURNKEY.onBack();
    const onCubes = !document.getElementById('scCubes').classList.contains('hide');
    return {drafted: !!d && d.n === 6 && d.name === 'HALF DONE', got, b1, onMade, b2, onCubes};
  });
  ok('an in-progress cube is drafted as it is built', persist.drafted, JSON.stringify(persist.got));
  ok('...and comes back after the app is killed',
     persist.got.n === 6 && persist.got.cell === '+' && persist.got.name === 'HALF DONE',
     JSON.stringify(persist.got));
  ok('back leaves the editor for the shelf, not the app',
     persist.b1 === true && persist.onMade);
  ok('...and back again returns to the vaults', persist.b2 === true && persist.onCubes);

  /* ---- and it fits the narrowest phone ---------------------------------- */
  /* the deck grid was sized in vw, which is the VIEWPORT — but it sits inside
     the layer's own 20px padding, so at 320 it came out 1.6px wider than the
     box holding it and the whole screen scrolled sideways */
  const fits = [];
  for(const [w, h] of [[320,568],[360,640],[390,844],[844,390]]){
    const q = await b.newPage({viewport:{width:w,height:h}, deviceScaleFactor:2, isMobile:true, hasTouch:true});
    await q.goto('file:///home/user/Ideas/singularity/index.html');
    await q.waitForTimeout(350);
    fits.push(await q.evaluate(([w2, h2]) => {
      edNew(7); openEditor(null);                /* the biggest cube, worst case */
      const L = document.getElementById('scEdit');
      const sl = document.getElementById('edSlice').getBoundingClientRect();
      return {at:w2 + 'x' + h2, over: L.scrollWidth > L.clientWidth + 1, cell: Math.round(sl.width/7)};
    }, [w, h]));
    await q.close();
  }
  const spill = fits.filter(f => f.over);
  ok('the editor never scrolls sideways, at any size',
     spill.length === 0, spill.length ? spill.map(f => f.at).join(' ') : fits.map(f => f.at + ':' + f.cell + 'px').join('  '));
  ok('...and a cell stays big enough to hit on the smallest phone',
     fits.every(f => f.cell >= 36), fits.map(f => f.cell).join(','));

  /* ---- no tile may ever draw on top of another --------------------------- */
  /* This is what `aspect-ratio` on a content-sized grid row cost: the row
     wanted the item's height and the item wanted its own width, and Safari
     broke that cycle by letting the item exceed its row. Every tile in the
     vault shelf drew over the one below it. The row height is a measured
     number now, so the cycle is gone — this is the assertion that keeps
     anyone from putting `aspect-ratio` back. */
  const laps = [];
  for(const [w, h] of [[320,568],[360,640],[390,844],[414,736],[430,932]]){
    const q = await b.newPage({viewport:{width:w,height:h}, deviceScaleFactor:2, isMobile:true, hasTouch:true});
    await q.goto('file:///home/user/Ideas/singularity/index.html');
    await q.waitForTimeout(350);
    laps.push(await q.evaluate(([w2, h2]) => {
      const over = g => {
        const r = [...g.children].map(c => c.getBoundingClientRect());
        let n = 0;
        for(let i = 0; i < r.length; i++) for(let j = i+1; j < r.length; j++){
          const a = r[i], c = r[j];
          if(Math.min(a.right,c.right) - Math.max(a.left,c.left) > 1 &&
             Math.min(a.bottom,c.bottom) - Math.max(a.top,c.top) > 1) n++;
        }
        return {n, sq: r.length ? Math.abs(r[0].width - r[0].height) : 0};
      };
      store.reached = 30; buildCubeGrid(); show('scCubes');
      const vault = over(document.getElementById('cubeGrid'));
      store.made = {};
      for(let i = 0; i < 5; i++){
        const src = BAKED[i];
        store.made['x'+i] = {id:'x'+i, name:'C'+i, n:src.n, vox:src.vox, start:src.start,
                             goal:src.goal, keys:src.keys, doors:src.doors, par:1, made:i};
      }
      buildMadeGrid(); show('scMade');
      const shelf = over(document.getElementById('madeGrid'));
      edNew(7); openEditor(null);
      const slice = over(document.getElementById('edSlice'));
      return {at:w2+'x'+h2, vault, shelf, slice};
    }, [w, h]));
    await q.close();
  }
  const bad3 = laps.filter(l => l.vault.n || l.shelf.n || l.slice.n);
  ok('no tile overlaps another, on any grid, at any portrait size',
     bad3.length === 0,
     bad3.length ? bad3.map(l => `${l.at} vault:${l.vault.n} shelf:${l.shelf.n} slice:${l.slice.n}`).join('  ')
                 : laps.map(l => l.at).join('  '));
  ok('...and the tiles are still square',
     laps.every(l => l.vault.sq <= 1 && l.slice.sq <= 1),
     laps.map(l => l.at + ':' + l.vault.sq.toFixed(1)).join(' '));

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
