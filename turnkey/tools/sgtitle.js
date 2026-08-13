/* THE FRONT DOOR IS FIVE CONTROLS IN A COLUMN, AND THEY HAVE TO LOOK LIKE IT.

   Two faults reported off a real phone, both of them the kind that make a
   finished screen read as an unfinished one:

   CALIBRATE had no box. It kept the .ghost it wore when it was a full-width
   line under the column — transparent ground, no edge, dim ink. That is a
   deliberate "also available" treatment for a rule, and it is exactly wrong
   for the fourth of four controls sitting in a two-up row: it read as a
   label somebody forgot to finish.

   AND THE DAILY'S LABEL WAS OFF ITS OWN CENTRE. The button carries a badge
   saying whether today's cube is done, and as an inline child it was a
   second item in a centring flex row — so the button's own gap plus the
   badge's margin pushed "[ DAILY ]" eleven pixels left of where "[ VAULTS ]"
   and "[ FORGE ]" sat, WHETHER THE BADGE HAD ANYTHING IN IT OR NOT. One
   label out of four off its centre is visible from across a room.

   Both are pure geometry, so both are measurable. This checks the labels are
   centred on their buttons at every phone size in both badge states, that
   all four controls carry the same chrome, that the badge stays inside the
   button it belongs to and off the label, and that CALIBRATE — the longest
   word on the screen — still fits its box at 320px.

   The last check is the one that guards the fix rather than the fault: the
   slim row exists so the hero cube keeps its height, and giving CALIBRATE a
   box must not have cost the cube anything.                                */
const {chromium} = require('playwright');
let bad = 0;
const ok = (n, c, x = '') => { console.log((c ? '  ok  ' : 'FAIL  ') + n + (x ? '   ' + x : '')); if(!c) bad = 1; };

/* the hero heights this screen had BEFORE calibrate was given a box, per
   viewport — the row was built to cost nothing and has to keep costing it */
const HERO_WAS = {'320x568':222, '360x640':225, '390x844':417, '430x932':480, '375x553':198};

(async () => {
  const b = await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const errs = [];
  const rows = [];

  for(const [w, h] of [[320,568],[360,640],[390,844],[430,932],[375,553]]){
    const p = await b.newPage({viewport:{width:w, height:h}, deviceScaleFactor:2, isMobile:true, hasTouch:true});
    p.on('pageerror', e => errs.push(`${w}x${h}: ${e.message}`));
    await p.goto('file:///home/user/Ideas/singularity/index.html');
    await p.waitForTimeout(600);

    const m = await p.evaluate(() => {
      /* the boot animation holds these at opacity 0 with fill:both, and a
         measurement taken mid-animation is a measurement of nothing */
      document.getElementById('scTitle').getAnimations({subtree:true}).forEach(a => a.finish());
      const g = id => document.getElementById(id);
      const bx = id => g(id).getBoundingClientRect();
      /* the LABEL, not the button: the fault was entirely inside the box */
      const label = id => {
        const n = [...g(id).childNodes].find(c => c.nodeType === 3 && c.textContent.trim());
        const r = document.createRange(); r.selectNode(n); return r.getBoundingClientRect();
      };
      const off = id => +((label(id).left + label(id).right) / 2
                        - (bx(id).left + bx(id).right) / 2).toFixed(1);
      const ids = ['btnDaily','btnCubes','btnForge','btnSet'];
      const chrome = id => { const c = getComputedStyle(g(id));
        return c.backgroundColor + '|' + (c.boxShadow === 'none' ? 'flat' : 'edged') + '|' + c.color; };

      const snap = () => ({ offs: ids.map(off) });
      const out = snap();
      out.chrome  = ids.map(chrome);
      out.hero    = Math.round(bx('heroSlot').height);
      /* CALIBRATE is the longest word here; slack is label-to-padding-edge */
      out.calSlack = +(label('btnSet').left - (bx('btnSet').left
                     + parseFloat(getComputedStyle(g('btnSet')).paddingLeft))).toFixed(1);
      /* the two left-hand buttons are the pair the eye compares */
      out.column = Math.abs(bx('btnDaily').left - bx('btnForge').left) < 0.5 &&
                   Math.abs(bx('btnDaily').width - bx('btnForge').width) < 0.5;

      /* AND AGAIN ON THE DAY IT HAS SOMETHING TO SAY. The empty badge was
         the reported case, but a badge that only misaligns the label once
         you have played is a worse bug, not a fixed one. */
      g('dailyMark').textContent = '12 FOLDS';
      out.solvedOffs = snap().offs;
      const lab = label('btnDaily'), mk = g('dailyMark').getBoundingClientRect(), bb = bx('btnDaily');
      out.badgeClearsLabel = +(mk.top - lab.bottom).toFixed(1);
      out.badgeInside      = +(bb.bottom - mk.bottom).toFixed(1);
      g('dailyMark').textContent = '';
      return out;
    });

    const key = `${w}x${h}`;
    rows.push({key, ...m});
    await p.close();
  }

  const worst = f => rows.reduce((a, r) => Math.max(a, f(r)), -Infinity);
  const show  = f => rows.map(r => r.key + ':' + f(r)).join('  ');

  ok('every label on the title is centred on its own button',
     worst(r => Math.max(...r.offs.map(Math.abs))) < 0.6,
     show(r => r.offs.join('/')));
  ok('...and still is on the day the daily badge has something in it',
     worst(r => Math.max(...r.solvedOffs.map(Math.abs))) < 0.6,
     show(r => r.solvedOffs.join('/')));
  ok('all four controls carry the same ground, edge and ink',
     rows.every(r => new Set(r.chrome).size === 1),
     rows[0].chrome.join('  '));
  ok('the badge sits below the label rather than through it',
     worst(r => -r.badgeClearsLabel) < 0, show(r => r.badgeClearsLabel + 'px'));
  ok('...and inside the button rather than out of the bottom of it',
     rows.every(r => r.badgeInside >= 0), show(r => r.badgeInside + 'px'));
  ok('CALIBRATE fits its box on the smallest phone',
     rows.every(r => r.calSlack > 0), show(r => r.calSlack + 'px'));
  ok('DAILY and FORGE are the same box in the same column',
     rows.every(r => r.column));
  ok('giving CALIBRATE a box cost the hero cube nothing',
     rows.every(r => r.hero === HERO_WAS[r.key]),
     show(r => r.hero + (r.hero === HERO_WAS[r.key] ? '' : ` WAS ${HERO_WAS[r.key]}`)));
  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
