/* FIVE SLOTS, AND THEY HAVE TO LAST FOREVER.

   The daily boards rest on three things that are easy to get wrong and
   impossible to notice being wrong from inside the game.

   THE DAY BOUNDARY IS MIDNIGHT UTC-7 AND DOES NOT MOVE. It has to match the
   instant Play Games buckets a daily board on, or a ranking spans two
   different puzzles. It also must not shift with daylight saving, which is
   why it is an offset and not a timezone.

   THE PERIODS ARE REAL PERIODS. A week opens on a Sunday because that is
   where the service turns its own week; a month is a calendar month read on
   the same clock the day index was cut on. Every day belongs to exactly one
   of each.

   AND MISSING A DAY MUST COST. Folds are lower-is-better, so a plain sum
   would make not playing the winning move. This walks a month of play with
   holes in it and checks the ordering never inverts.

   Time is stubbed rather than waited for: streaks, rollovers and a finished
   month are not things a test can sit through.                            */
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
  await p.waitForTimeout(400);

  /* the page owns these numbers; the test must not carry its own copy that
     could drift away from them silently */
  const K = await p.evaluate(() => ({DAY_MISS, SEASON_DAYS, PACK}));
  const packed = (period, total) => period*K.PACK + (K.PACK - 1 - total);

  /* ---- the boundary ----------------------------------------------------- */
  const bound = await p.evaluate(() => {
    const real = Date.now;
    const at = ms => { Date.now = () => ms; return dayIndex(); };
    /* January and July: a fixed offset must give the same answer in both,
       where a timezone would have moved by an hour */
    const jan = Date.UTC(2026, 0, 15), jul = Date.UTC(2026, 6, 15);
    const out = {};
    for(const [name, base] of [['jan', jan], ['jul', jul]]){
      const before = at(base + 6*3600000 + 59*60000 + 59000);
      const after  = at(base + 7*3600000);
      out[name] = {rolls: after - before, before, after};
    }
    /* and the label must be read on the same clock as the index */
    Date.now = () => Date.UTC(2026, 7, 12, 3, 0, 0);   /* 03:00 UTC = still 11 Aug in PT */
    const early = {idx: dayIndex(), label: dayLabel()};
    Date.now = () => Date.UTC(2026, 7, 12, 9, 0, 0);   /* 09:00 UTC = 12 Aug in PT */
    const late = {idx: dayIndex(), label: dayLabel()};
    Date.now = real;
    return {out, early, late};
  });
  ok('the day turns at exactly 07:00 UTC in January',
     bound.out.jan.rolls === 1, `${bound.out.jan.before} -> ${bound.out.jan.after}`);
  ok('...and at exactly 07:00 UTC in July, unmoved by daylight saving',
     bound.out.jul.rolls === 1, `${bound.out.jul.before} -> ${bound.out.jul.after}`);
  ok('03:00 UTC still belongs to the previous Pacific day',
     bound.early.idx === bound.late.idx - 1 && bound.early.label === '2026.08.11',
     `${bound.early.label} then ${bound.late.label}`);
  ok('the label agrees with the index it was cut from',
     bound.late.label === '2026.08.12', bound.late.label);

  /* ---- the periods partition the day line ------------------------------- */
  const per = await p.evaluate(() => {
    let weekBad = null, monBad = null, seaBad = null, dowBad = null;
    for(let d = 20000; d < 20800; d++){          /* ~2 years either side of now */
      const w = weekSpan(weekOf(d)), m = monthSpan(monthOf(d)), s = seasonSpan(seasonOf(d));
      if(d < w[0] || d > w[1]) weekBad = weekBad || [d, w];
      if(d < m[0] || d > m[1]) monBad  = monBad  || [d, m];
      if(d < s[0] || d > s[1]) seaBad  = seaBad  || [d, s];
      /* a week opens on a Sunday and closes on a Saturday */
      if(dayDate(w[0]).getUTCDay() !== 0 || dayDate(w[1]).getUTCDay() !== 6) dowBad = dowBad || [d, w];
    }
    /* month spans start on the 1st and end on the last of the month */
    const m0 = monthSpan(2026*12 + 1);           /* February 2026, 28 days */
    const m1 = monthSpan(2024*12 + 1);           /* February 2024, a leap year */
    return {weekBad, monBad, seaBad, dowBad,
            feb26: m0[1] - m0[0] + 1, feb24: m1[1] - m1[0] + 1,
            firstOf: dayDate(m0[0]).getUTCDate(), lastOf: dayDate(m0[1]).getUTCDate()};
  });
  ok('every day sits inside its own week', !per.weekBad, JSON.stringify(per.weekBad || ''));
  ok('every day sits inside its own month', !per.monBad, JSON.stringify(per.monBad || ''));
  ok('every day sits inside its own season', !per.seaBad, JSON.stringify(per.seaBad || ''));
  ok('weeks run Sunday to Saturday, as the service does', !per.dowBad, JSON.stringify(per.dowBad || ''));
  ok('a month span is the real month, leap years included',
     per.feb26 === 28 && per.feb24 === 29 && per.firstOf === 1 && per.lastOf === 28,
     `Feb26 ${per.feb26}d, Feb24 ${per.feb24}d, ${per.firstOf}..${per.lastOf}`);

  /* ---- packing ---------------------------------------------------------- */
  const pack = await p.evaluate(() => {
    const worst = SEASON_DAYS * DAY_MISS;
    const far = monthOf(20000 + 365*100);        /* a century out */
    return {
      newerWins: packed(500, worst) > packed(499, 0),
      betterWins: packed(500, 10) > packed(500, 11),
      exact: Number.isSafeInteger(packed(far, worst)),
      headroom: worst, biggest: packed(far, worst)
    };
  });
  ok('a newer period outranks every older one, however badly played',
     pack.newerWins, `worst case total ${pack.headroom}`);
  ok('...and inside one period, fewer folds still wins', pack.betterWins);
  ok('a score a century out is still an exact integer',
     pack.exact, `${pack.biggest} vs 2^53 = ${Number.MAX_SAFE_INTEGER}`);

  /* ---- playing, with holes in it ---------------------------------------- */
  const run = await p.evaluate(() => {
    const real = Date.now;
    const sent = [];
    window.AndroidGPG = {submit:(board, score) => sent.push([board, score]), show:() => {}};
    store.dhist = {}; store.dstreak = {cur:0, best:0, last:-1}; store.dpost = {w:-1, m:-1, s:-1};

    /* start on a Sunday so the week boundaries are easy to reason about */
    let d0 = 20800; while(dayDate(d0).getUTCDay() !== 0) d0++;
    const play = (day, folds) => { Date.now = () => day*86400000 + DAY_ANCHOR + 3600000; recordDaily(folds); };

    play(d0,     20);
    play(d0 + 1, 22);
    play(d0 + 2, 18);
    const streak3 = store.dstreak.cur;
    play(d0 + 4, 25);                            /* a day skipped: streak breaks */
    const afterGap = {cur: store.dstreak.cur, best: store.dstreak.best};
    play(d0 + 5, 30);
    play(d0 + 6, 21);

    const weekSent = sent.filter(s => s[0] === 'daily_week').length;
    play(d0 + 7, 19);                            /* into the next week: it posts */
    const w = weekOf(d0), wk = sent.filter(s => s[0] === 'daily_week');

    /* what that week should be worth: six played days plus one penalty */
    const want = 20 + 22 + 18 + 25 + 30 + 21 + DAY_MISS;

    /* a wild solve can never be worse than not turning up */
    play(d0 + 8, 400);
    const clamped = store.dhist[d0 + 8];

    /* an improved retry of the same day reposts, a worse one does not */
    const beforeRetry = sent.filter(s => s[0] === 'daily').length;
    play(d0 + 8, 500);
    const afterWorse = sent.filter(s => s[0] === 'daily').length;
    play(d0 + 8, 12);
    const afterBetter = sent.filter(s => s[0] === 'daily').length;

    /* and it does not repost a week it has already sent */
    play(d0 + 9, 17);
    const weekTotalSent = sent.filter(s => s[0] === 'daily_week').length;

    Date.now = real;
    return {streak3, afterGap, weekSent, wk, want, w, clamped, streakBest: store.dstreak.best,
            beforeRetry, afterWorse, afterBetter, weekTotalSent,
            daySent: sent.filter(s => s[0] === 'daily').map(s => s[1]),
            streakSent: sent.filter(s => s[0] === 'daily_streak').map(s => s[1])};
  });

  ok('three days running is a streak of three', run.streak3 === 3, String(run.streak3));
  ok('a missed day resets the streak but keeps the best',
     run.afterGap.cur === 1 && run.afterGap.best === 3, JSON.stringify(run.afterGap));
  /* the run rebuilds past the gap and goes on to beat three, so what matters
     is not which numbers were sent but that every one of them was a record:
     strictly increasing, never a repeat, ending on the best actually held */
  const streaks = run.streakSent.map(Number);   /* scores cross as strings */
  const rising = streaks.every((v, i) => i === 0 || v > streaks[i-1]);
  ok('the streak board only ever hears about a new best',
     rising && streaks.length > 0 && streaks[streaks.length-1] === run.streakBest,
     String(run.streakSent) + ' with best ' + run.streakBest);
  ok('an unfinished week posts nothing', run.weekSent === 0, `${run.weekSent} sent`);
  ok('the week posts once it is over, charged for the day that was missed',
     run.wk.length === 1 && run.wk[0][1] === String(packed(run.w, run.want)),
     `total should be ${run.want}` + (run.wk[0] ? `, sent ${run.wk[0][1]}` : ''));
  ok('...and is not sent again the next day', run.weekTotalSent === 1, `${run.weekTotalSent} sent`);
  ok('a catastrophic solve still beats not playing',
     run.clamped === K.DAY_MISS - 1, String(run.clamped));
  ok('a worse retry posts nothing, a better one posts',
     run.afterWorse === run.beforeRetry && run.afterBetter === run.beforeRetry + 1,
     `${run.beforeRetry} -> ${run.afterWorse} -> ${run.afterBetter}`);

  /* ---- a fresh install must not post a month it was not here for -------- */
  const fresh = await p.evaluate(() => {
    const real = Date.now;
    const sent = [];
    window.AndroidGPG = {submit:(board, score) => sent.push([board, score]), show:() => {}};
    store.dhist = {}; store.dstreak = {cur:0, best:0, last:-1}; store.dpost = {w:-1, m:-1, s:-1};
    const d = 21000;
    Date.now = () => d*86400000 + DAY_ANCHOR + 3600000;
    recordDaily(20);
    const agg = sent.filter(s => s[0] !== 'daily' && s[0] !== 'daily_streak');
    /* and the history does not grow without bound */
    for(let i = d - 400; i < d; i++) store.dhist[i] = 20;
    pruneDaily(d);
    const kept = Object.keys(store.dhist).length;
    Date.now = real;
    return {agg: agg.length, kept};
  });
  ok('a first-ever day posts no aggregate for periods it missed',
     fresh.agg === 0, `${fresh.agg} sent`);
  ok('history is pruned to the longest window that reads it',
     fresh.kept <= K.SEASON_DAYS + 33, `${fresh.kept} days kept`);

  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
