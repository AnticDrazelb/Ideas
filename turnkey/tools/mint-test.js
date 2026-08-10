var C = require('./core.js');
var bad = 0;
function ok(n, c, x){ console.log((c?'  ok  ':'FAIL  ') + n + (x?'   '+x:'')); if(!c) bad = 1; }

/* Every level a player could ever reach must be provably winnable. The only
   way to have any confidence in that is to actually mint a lot of them and
   re-solve every one from scratch. */
/* plates quadruple the search space, so a mint costs more than it did; the
   ceiling here is what the BACKGROUND prebuild has to fit inside, and the
   only time a player waits for one is a jump to an uncached level, which
   says "cutting cube N" while it works. */
var LEVELS_TO_TEST = [];
for(var i = 11; i <= 130; i++) LEVELS_TO_TEST.push(i);                 // vaults 2-13, every level
[200, 401, 777, 1000, 2500, 9999, 100000, 1000001].forEach(function(l){ LEVELS_TO_TEST.push(l); });

var slow = [], fallbacks = [], unsolvable = [], offBand = [], parByBand = {}, tot = 0, maxMs = 0;

LEVELS_TO_TEST.forEach(function(level){
  var t0 = Date.now();
  var lv = C.mint(level, 240);
  var ms = Date.now() - t0;
  tot += ms; if(ms > maxMs) maxMs = ms;
  if(ms > 1400) slow.push(level + ':' + ms + 'ms');
  if(lv.fallback) fallbacks.push(level);

  /* re-solve from scratch, ignoring anything mint claimed */
  var fresh = {n:lv.n, vox:lv.vox.slice(), start:lv.start, goal:lv.goal, keys:lv.keys, doors:lv.doors};
  var r = C.solve(fresh, 60);
  if(!r.ok) unsolvable.push(level);
  else if(r.turns !== lv.par) unsolvable.push(level + ' (par mismatch ' + r.turns + '!=' + lv.par + ')');

  /* the player must not open the game standing inside the rock */
  var s0 = C.project(lv.n, lv.vox, C.ORI_ID);
  if(!C.surfaceAt(lv.n, s0, C.ORI_ID, lv.start)) unsolvable.push(level + ' (start buried)');

  var spec = C.specFor(level);
  if(lv.par < spec.parLo || lv.par > spec.parHi) offBand.push(level + ' par=' + lv.par + ' want ' + spec.parLo + '-' + spec.parHi);
  (parByBand[spec.band] = parByBand[spec.band] || []).push(lv.par);
});

ok('every minted level re-solves, with par exactly as claimed', unsolvable.length === 0, unsolvable.slice(0,5).join(', '));
ok('the fallback never fires', fallbacks.length === 0, fallbacks.slice(0,8).join(', '));
ok('no mint exceeds 1400ms of counted work', slow.length === 0, slow.slice(0,6).join(', '));
ok('mints land inside their vault\'s par band', offBand.length <= LEVELS_TO_TEST.length * 0.06,
   offBand.length + '/' + LEVELS_TO_TEST.length + ' outside: ' + offBand.slice(0,4).join(', '));

/* determinism: the same level number is the same cube, always and everywhere.
   Re-running with the SAME budget proves nothing about a different machine —
   the search used to be time-boxed, so a faster device got through more
   candidates and chose a different winner, and this test happily passed while
   cube 41 was a five-turn puzzle in one browser and a three-turn puzzle in
   another. Handing it wildly different budgets is the check that catches it:
   if the clock can reach the result at all, these will not match. */
var a = C.mint(57, 240), b = C.mint(57, 240);
ok('level 57 is the same cube twice', a.vox.join('') === b.vox.join('') && a.par === b.par && JSON.stringify(a.start) === JSON.stringify(b.start));
var fastBudget = C.mint(57, 1), slowBudget = C.mint(57, 60000);
ok('...and the same cube however much time it is given',
   fastBudget.vox.join('') === slowBudget.vox.join('') && fastBudget.par === slowBudget.par,
   'par ' + fastBudget.par + ' vs ' + slowBudget.par);
var c1 = C.mint(58, 240);
ok('...and level 58 is a different one', c1.vox.join('') !== a.vox.join(''));

/* the fallback itself must be sound, since it is the last line of defence */
var fb = C.fallbackLevel(4242);
ok('the fallback cube is itself solvable', C.solve(fb, 60).ok);
ok('the fallback opens on a valid surface', !!C.surfaceAt(fb.n, C.project(fb.n, fb.vox, C.ORI_ID), C.ORI_ID, fb.start));

console.log('\n  average mint ' + (tot/LEVELS_TO_TEST.length).toFixed(0) + 'ms, worst ' + maxMs + 'ms, ' + LEVELS_TO_TEST.length + ' levels');
console.log('  par by vault:');
Object.keys(parByBand).sort(function(x,y){return x-y;}).slice(0,14).forEach(function(b){
  var v = parByBand[b], sp = C.specFor(b*10 + 1);
  console.log('    vault ' + (+b+1) + '  n=' + sp.n + ' locks=' + sp.locks +
              '  par ' + Math.min.apply(null,v) + '-' + Math.max.apply(null,v) +
              '  (asked ' + sp.parLo + '-' + sp.parHi + ')');
});
process.exit(bad);
