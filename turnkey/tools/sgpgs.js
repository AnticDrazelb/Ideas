/* EVERY PLAY GAMES HOOK, PROVEN BY USE RATHER THAN BY GREP.

   A leaderboard integration fails quietly. Nothing throws when a score is
   never posted, nothing looks wrong when a board has a viewer and no
   submitter, and the only way to find out is to ship it. So this stands a
   fake host up, PLAYS the game — a daily, a week rolling over, a vault
   finished — and records every id that crosses the bridge.

   The load-bearing assertion is the last one: the set of boards the game can
   SHOW and the set it can SUBMIT to must be the same set. A board you can
   open but never post to ranks nobody; a board you post to but cannot open
   is a score nobody sees.                                                  */
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

  /* ---- the bridge itself ------------------------------------------------ */
  const bridge = await p.evaluate(() => {
    try{ delete window.AndroidGPG; }catch(e){ window.AndroidGPG = null; }
    const before = GPG.available();
    let threw = false;
    try{ GPG.submit('daily', 1); GPG.show('daily'); }catch(e){ threw = String(e); }
    window.AndroidGPG = {submit:() => {}, show:() => {}};
    const after = GPG.available();          /* resolved per call, not at load */
    try{ delete window.AndroidGPG; }catch(e){ window.AndroidGPG = null; }
    return {before, after, threw, back: GPG.available(),
            api: ['available','submit','show'].filter(k => typeof GPG[k] === 'function')};
  });
  ok('the bridge exposes available, submit and show',
     bridge.api.length === 3, bridge.api.join(','));
  ok('...reports no host when there is none, and never throws',
     bridge.before === false && bridge.threw === false && bridge.back === false, String(bridge.threw));
  ok('...and notices a host installed after the page loaded', bridge.after === true);

  /* ---- play, with a host listening -------------------------------------- */
  const run = await p.evaluate(async () => {
    const sub = [], shown = [];
    window.AndroidGPG = {submit:(id, v) => sub.push([id, v]), show:(id) => shown.push(id)};
    const real = Date.now;

    /* a vault, finished by actually clearing its last cube */
    store.tbest = {}; store.vbest = {}; store.best = {}; store.made = {};
    const v0 = vaultStart(0), vn = vaultSize(0);
    for(let i = 0; i < vn - 1; i++) store.tbest[v0 + i] = 2000;
    loadLevel(v0 + vn - 1); show(null);
    await new Promise(r => setTimeout(r, 350));
    turns = 4; finish();
    await new Promise(r => setTimeout(r, 80));
    const afterVault = sub.slice();

    /* Dailies. A period only posts once it has FINISHED, and the three
       periods are different lengths — a season is ninety-one days, so a
       week's play crosses a week boundary and never a season one. Each
       window is therefore positioned on a real boundary rather than hoping
       one turns up. Every window is eight days, so each also contains a
       Sunday and posts the week. */
    const play = async (day, folds) => {
      Date.now = () => day*86400000 + DAY_ANCHOR + 3600000;
      loadLevel(DAILY); show(null);
      await new Promise(r => setTimeout(r, 120));
      turns = folds; finish();
      await new Promise(r => setTimeout(r, 40));
    };
    const boundary = of => { let d = 20900; while(of(d+1) === of(d)) d++; return d; };
    for(const edge of [boundary(seasonOf), boundary(monthOf)]){
      store.dhist = {}; store.dstreak = {cur:0, best:0, last:-1}; store.dpost = {w:-1, m:-1, s:-1};
      for(let i = 0; i < 8; i++) await play(edge - 3 + i, 20 + i);
    }
    Date.now = real;

    /* and the viewer: every row, opened */
    levelNo = 5;
    openBoards('scPause');
    [...document.getElementById('boardList').children].forEach(r => r.click());

    const ids = s => [...new Set(s.map(x => Array.isArray(x) ? x[0] : x))].sort();
    return {subs: sub, shownIds: ids(shown), subIds: ids(sub),
            vaultPosted: afterVault.filter(s => /^vault_/.test(s[0])),
            allStrings: sub.every(s => typeof s[1] === 'string')};
  });

  ok('clearing a vault posts that vault board',
     run.vaultPosted.length === 1 && run.vaultPosted[0][0] === 'vault_01',
     JSON.stringify(run.vaultPosted));
  ok('a daily clear posts today and the streak',
     run.subIds.indexOf('daily') >= 0 && run.subIds.indexOf('daily_streak') >= 0, run.subIds.join(' '));
  ok('a week rolling over posts the week, month and season',
     ['daily_week','daily_month','daily_season'].every(k => run.subIds.indexOf(k) >= 0),
     run.subIds.join(' '));
  ok('every score crosses the bridge as a string, so no digit is lost',
     run.allStrings, JSON.stringify(run.subs.slice(0, 2)));
  ok('the boards screen opens all six', run.shownIds.length === 6, run.shownIds.join(' '));

  /* THE ONE THAT MATTERS: a board you can open but never post to ranks
     nobody, and a board you post to but cannot open is a score nobody sees. */
  const onlyShown = run.shownIds.filter(i => run.subIds.indexOf(i) < 0);
  const onlySub   = run.subIds.filter(i => run.shownIds.indexOf(i) < 0);
  ok('every board the game shows is a board the game posts to, and vice versa',
     onlyShown.length === 0 && onlySub.length === 0,
     onlyShown.length || onlySub.length
       ? `show-only: ${onlyShown.join(',') || 'none'}  submit-only: ${onlySub.join(',') || 'none'}`
       : run.shownIds.join(' '));

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
