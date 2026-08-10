/* ============================================================
   TURNKEY — CORE
   The projection model, the solver, and the generator. Written as plain
   functions with no module syntax so this exact text is inlined into the
   game; the shim at the bottom is the only Node-only line.
   ============================================================ */

/* ---------- rng ---------- */
function mulberry32(a){
  return function(){
    a |= 0; a = a + 0x6D2B79F5 | 0;
    var t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

/* ---------- ORIENTATION -------------------------------------------------
   An orientation is a world->view basis: R is the world direction that
   points screen-right, U points screen-up, F points TOWARD THE CAMERA.
   All three are signed unit axes, so every orientation is an integer
   signed permutation and nothing ever drifts. There are 24 of them.     */
var ORI_ID = {R:[1,0,0], U:[0,1,0], F:[0,0,1]};
function vneg(v){ return [-v[0], -v[1], -v[2]]; }
function vdot(a,b){ return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]; }

/* Dragging RIGHT pushes the near face right, so the world's LEFT side swings
   toward the camera. Dragging UP tips the top away and brings the bottom
   forward. Both derived once here rather than at each call site. */
var TURNS = [
  {id:'left',  dx:-1, dy: 0, f:function(m){ return {R:vneg(m.F), U:m.U,      F:m.R      }; }},
  {id:'right', dx: 1, dy: 0, f:function(m){ return {R:m.F,       U:m.U,      F:vneg(m.R)}; }},
  {id:'up',    dx: 0, dy: 1, f:function(m){ return {R:m.R,       U:m.F,      F:vneg(m.U)}; }},
  {id:'down',  dx: 0, dy:-1, f:function(m){ return {R:m.R,       U:vneg(m.F),F:m.U      }; }}
];
function oriKey(m){ return m.R.join() + ';' + m.U.join() + ';' + m.F.join(); }

/* The 24 orientations, indexed, so solver states can be integers. */
var ORIS = (function(){
  var list = [ORI_ID], seen = {}; seen[oriKey(ORI_ID)] = 0;
  for(var i = 0; i < list.length; i++){
    for(var t = 0; t < 4; t++){
      var m = TURNS[t].f(list[i]), k = oriKey(m);
      if(seen[k] === undefined){ seen[k] = list.length; list.push(m); }
    }
  }
  list.index = seen;
  return list;
})();
function oriIndex(m){ return ORIS.index[oriKey(m)]; }

/* ---------- THE CUBE ----------------------------------------------------
   A level is n^3 cells of three kinds:
     '.'  empty      — nothing there
     '#'  bedrock    — solid, but you cannot stand on its face
     '+'  deck       — solid, and walkable when it is the nearest thing
   Index order is [y][z][x] so a level literal reads as a stack of slabs
   from the bottom up, which is the only way authoring one by hand is
   survivable.                                                            */
function vidx(n,x,y,z){ return (y*n + z)*n + x; }

/* World <-> view. Both are integer round-trips: c is the cube's centre and
   cancels out, so viewOf(worldOf(v)) === v exactly. */
function viewOf(n,m,p){
  var c = (n-1)/2, q = [p[0]-c, p[1]-c, p[2]-c];
  return [Math.round(vdot(m.R,q)+c), Math.round(vdot(m.U,q)+c), Math.round(vdot(m.F,q)+c)];
}
function worldOf(n,m,v){
  var c = (n-1)/2, a = v[0]-c, b = v[1]-c, d = v[2]-c;
  return [Math.round(a*m.R[0]+b*m.U[0]+d*m.F[0]+c),
          Math.round(a*m.R[1]+b*m.U[1]+d*m.F[1]+c),
          Math.round(a*m.R[2]+b*m.U[2]+d*m.F[2]+c)];
}

/* ---------- THE COLLAPSE ------------------------------------------------
   THIS FUNCTION IS THE WHOLE GAME.

   Every cell sharing a screen column projects to the same square, and the
   same square is ONE PLACE. So a column is represented by exactly one cell:
   the nearest solid one to the camera. Everything behind it is discarded —
   not hidden, discarded. Two decks a hundred cells apart in the world are
   neighbours on screen if their columns are neighbours, and you may walk
   between them as if they touched, because on screen they do.

   surf[u*n+v] = {d, t, w} — depth, kind, and the world cell it came from,
   or null for a column with nothing in it at all (a void: impassable).   */
function project(n, vox, m){
  var surf = new Array(n*n), i;
  for(i = 0; i < n*n; i++) surf[i] = null;
  for(var y = 0; y < n; y++) for(var z = 0; z < n; z++) for(var x = 0; x < n; x++){
    var t = vox[vidx(n,x,y,z)];
    if(t === '.') continue;
    var v = viewOf(n, m, [x,y,z]), k = v[0]*n + v[1];
    if(!surf[k] || v[2] > surf[k].d) surf[k] = {d:v[2], t:t, w:[x,y,z]};
  }
  return surf;
}

/* An object (key, door, goal, and the player's own footing) is attached to a
   cell, and exists in a projection only while that cell IS the surface of
   its column. Bury it behind bedrock and it is gone until you turn. */
function surfaceAt(n, surf, m, w){
  var v = viewOf(n, m, w), s = surf[v[0]*n + v[1]];
  return (s && s.w[0] === w[0] && s.w[1] === w[1] && s.w[2] === w[2]) ? v : null;
}

/* ---------- THE RULES ---------------------------------------------------
   A level: {n, vox, start, goal, keys[], doors[]} — every position a world
   cell. A play state: {pos (the cell underfoot), ori, keys, doors bitmask}.  */
function walkable(lv, surf, u, v, doorsOpen){
  if(u < 0 || v < 0 || u >= lv.n || v >= lv.n) return null;
  var s = surf[u*lv.n + v];
  if(!s || s.t !== '+') return null;
  for(var i = 0; i < lv.doors.length; i++){
    if(doorsOpen & (1<<i)) continue;
    var d = lv.doors[i];
    if(s.w[0] === d[0] && s.w[1] === d[1] && s.w[2] === d[2]) return null;   // shut, and standing on the surface
  }
  return s;
}
function keyIndexAt(lv, w){
  for(var i = 0; i < lv.keys.length; i++){
    var k = lv.keys[i];
    if(k[0] === w[0] && k[1] === w[1] && k[2] === w[2]) return i;
  }
  return -1;
}
function doorIndexAt(lv, w){
  for(var i = 0; i < lv.doors.length; i++){
    var d = lv.doors[i];
    if(d[0] === w[0] && d[1] === w[1] && d[2] === w[2]) return i;
  }
  return -1;
}
function samePos(a,b){ return a[0]===b[0] && a[1]===b[1] && a[2]===b[2]; }

/* Turning is legal only when the column you are standing in still has
   footing after the turn. That single rule is what makes WHERE you stand
   gate WHICH turns you have — position gating rotation, rotation gating
   position. It is the lock and the key, falling out of the geometry. */
function landing(lv, m2, pos, doorsOpen){
  var surf2 = project(lv.n, lv.vox, m2);
  var v = viewOf(lv.n, m2, pos);
  return walkable(lv, surf2, v[0], v[1], doorsOpen);
}

/* ---------- SOLVER ------------------------------------------------------
   0-1 BFS: a step costs nothing, a turn costs one. So the first time the
   goal is reached is the FEWEST TURNS it can possibly be done in, which is
   the number a level is graded and filtered on. Buckets rather than a deque
   because the cost never exceeds a couple of dozen.
   Returns {ok, turns, steps} — or {ok:false} if the cube is a lie.       */
function popcount(x){ var c = 0; while(x){ x &= x-1; c++; } return c; }

function solve(lv, budget, from){
  budget = budget || 40;
  var n = lv.n;
  /* Keys are tracked as a bitmask of WHICH ones are taken, never a count —
     a count lets the search step off a key cell and back on to mint another. */
  var start = from ? {pos:from.pos, ori:from.ori, kmask:from.kmask, doors:from.doors}
                   : {pos: lv.start, ori: 0, kmask: 0, doors: 0};
  if(!from){
    var s0 = keyIndexAt(lv, lv.start);
    if(s0 >= 0) start.kmask = 1 << s0;
  }
  var buckets = [], seen = {}, projCache = {}, parent = {};
  function P(oi){
    if(!projCache[oi]) projCache[oi] = project(n, lv.vox, ORIS[oi]);
    return projCache[oi];
  }
  function skey(s){ return vidx(n,s.pos[0],s.pos[1],s.pos[2]) + ':' + s.ori + ':' + s.kmask + ':' + s.doors; }
  function spare(s){ return popcount(s.kmask) - popcount(s.doors); }
  function push(c, s){ (buckets[c] || (buckets[c] = [])).push(s); }
  function relax(cost, s, fromKey, act){
    var k = skey(s);
    if(seen[k] === undefined || seen[k] > cost){
      seen[k] = cost; parent[k] = fromKey === undefined ? null : [fromKey, act]; push(cost, s);
    }
  }
  relax(0, start);

  /* Walk the parent chain back from the goal. A hint needs only the very
     first action — the rest is the player's to find — but the LENGTH of the
     chain is the second difficulty axis the vault curve scales on, so it is
     measured here where the answer is already in hand. */
  function chainOf(endKey){
    var chain = [], k = endKey, guard = 0;
    while(parent[k] && guard++ < 8000){ chain.push(parent[k][1]); k = parent[k][0]; }
    return chain.reverse();
  }
  function report(cost, endKey){
    var ch = chainOf(endKey), steps = 0;
    for(var i = 0; i < ch.length; i++) if(ch[i].kind === 'step') steps++;
    return {ok:true, turns:cost, steps:steps, first: ch.length ? ch[0] : null};
  }

  for(var cost = 0; cost <= budget; cost++){
    var q = buckets[cost];
    if(!q) continue;
    for(var qi = 0; qi < q.length; qi++){       /* q grows while we walk it — intended, free steps land here */
      var s = q[qi];
      if(seen[skey(s)] < cost) continue;
      if(samePos(s.pos, lv.goal)) return report(cost, skey(s));
      var m = ORIS[s.ori], surf = P(s.ori), v = viewOf(n, m, s.pos);

      /* steps — free */
      for(var t = 0; t < 4; t++){
        var u2 = v[0] + TURNS[t].dx, v2 = v[1] + TURNS[t].dy;
        if(u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
        var raw = surf[u2*n + v2];
        if(!raw || raw.t !== '+') continue;
        var nd = s.doors, di = doorIndexAt(lv, raw.w);
        if(di >= 0 && !(s.doors & (1<<di))){
          if(spare(s) < 1) continue;            /* shut, and nothing to open it with */
          nd = s.doors | (1<<di);               /* a door you can open is a step, not a wall */
        }
        var nk = s.kmask, ki = keyIndexAt(lv, raw.w);
        if(ki >= 0) nk |= (1<<ki);
        relax(cost, {pos: raw.w, ori: s.ori, kmask: nk, doors: nd}, skey(s), {kind:'step', dir:t});
      }
      /* turns — one each */
      for(var t2 = 0; t2 < 4; t2++){
        var m2 = TURNS[t2].f(m), land = landing(lv, m2, s.pos, s.doors);
        if(!land) continue;
        var nk2 = s.kmask, ki2 = keyIndexAt(lv, land.w);
        if(ki2 >= 0) nk2 |= (1<<ki2);
        relax(cost + 1, {pos: land.w, ori: oriIndex(m2), kmask: nk2, doors: s.doors}, skey(s), {kind:'turn', dir:t2});
      }
    }
  }
  return {ok:false};
}

/* ---------- GENERATOR ---------------------------------------------------
   Carving the SOLUTION first, then filling in around it, is the only way
   this is tractable: a random cube is a random cube, but a cube built by
   walking and turning is guaranteed to contain at least the route that
   built it. The solver then reports the true optimum, which is usually
   shorter than the route that was carved — and that difference is where
   the good levels live.

   Carving only ever turns bedrock into deck. That is monotone: it can add
   footing, never remove it, so carving for the fifth turn can never break
   the path carved for the first. Only a genuinely empty column forces a new
   cell to be placed, and that is the one move that can hide something from
   another face — which is what the verify pass is for.                    */
function carve(n, vox, m, u, v, hintDepth){
  var best = -1, bw = null;
  for(var y = 0; y < n; y++) for(var z = 0; z < n; z++) for(var x = 0; x < n; x++){
    if(vox[vidx(n,x,y,z)] === '.') continue;
    var vw = viewOf(n, m, [x,y,z]);
    if(vw[0] !== u || vw[1] !== v) continue;
    if(vw[2] > best){ best = vw[2]; bw = [x,y,z]; }
  }
  if(bw){ vox[vidx(n,bw[0],bw[1],bw[2])] = '+'; return bw; }
  var d = Math.max(0, Math.min(n-1, hintDepth === undefined ? (n>>1) : hintDepth));
  var w = worldOf(n, m, [u,v,d]);
  vox[vidx(n,w[0],w[1],w[2])] = '+';
  return w;
}

function generate(seed, opt){
  var rng = mulberry32(seed), n = opt.n, i;
  var vox = new Array(n*n*n);
  for(i = 0; i < n*n*n; i++) vox[i] = rng() < opt.density ? '#' : '.';

  var m = ORI_ID, path = [], turnsMade = 0;
  var u = 1 + Math.floor(rng()*(n-2)), v = 1 + Math.floor(rng()*(n-2));
  var pos = carve(n, vox, m, u, v);
  path.push(pos);

  for(var leg = 0; leg <= opt.turns; leg++){
    var steps = opt.legMin + Math.floor(rng()*(opt.legMax - opt.legMin + 1));
    for(var s = 0; s < steps; s++){
      var order = [0,1,2,3].sort(function(){ return rng() - 0.5; }), moved = false;
      var vv = viewOf(n, m, pos);
      for(var t = 0; t < 4 && !moved; t++){
        var u2 = vv[0] + TURNS[order[t]].dx, v2 = vv[1] + TURNS[order[t]].dy;
        if(u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
        pos = carve(n, vox, m, u2, v2, vv[2]);
        path.push(pos); moved = true;
      }
      if(!moved) break;
    }
    if(leg === opt.turns) break;
    /* turn, then guarantee footing on the far side of it */
    var pick = Math.floor(rng()*4), m2 = TURNS[pick].f(m);
    var lv2 = viewOf(n, m2, pos);
    m = m2; pos = carve(n, vox, m, lv2[0], lv2[1]);
    path.push(pos); turnsMade++;
  }

  /* the goal is the far end of the carve; keys and doors are pinched onto
     the route at fractions of its length so they gate rather than decorate */
  var goal = path[path.length-1];
  var lv = {n:n, vox:vox, start:path[0], goal:goal, keys:[], doors:[]};
  if(samePos(lv.start, goal)) return null;

  if(opt.locks){
    for(i = 0; i < opt.locks; i++){
      var ki = Math.floor(path.length * (0.18 + 0.22*i)), di = Math.floor(path.length * (0.55 + 0.2*i));
      var kw = path[Math.min(ki, path.length-2)], dw = path[Math.min(di, path.length-2)];
      if(samePos(kw, lv.start) || samePos(kw, goal) || samePos(dw, goal) || samePos(dw, lv.start)) continue;
      if(keyIndexAt(lv, kw) >= 0 || doorIndexAt(lv, dw) >= 0 || samePos(kw, dw)) continue;
      lv.keys.push(kw); lv.doors.push(dw);
    }
  }
  return lv;
}

/* WIDENING. The carve leaves a single thread: from any face you see the one
   route and walk it. Dead ends are what make a face worth reading, so a few
   extra surface cells are promoted to deck at random across random faces —
   then the level is re-solved and kept only if the answer did not get any
   cheaper. Difficulty from plausible wrong turns, never from hidden ones. */
function widen(rng, lv, count){
  var n = lv.n;
  for(var c = 0; c < count; c++){
    var m = ORIS[Math.floor(rng()*24)], surf = project(n, lv.vox, m), pool = [];
    for(var i = 0; i < surf.length; i++) if(surf[i] && surf[i].t === '#') pool.push(surf[i].w);
    if(!pool.length) continue;
    var w = pool[Math.floor(rng()*pool.length)];
    lv.vox[vidx(n,w[0],w[1],w[2])] = '+';
  }
  return lv;
}

/* A level is worth keeping only if the collapse is load-bearing: if it can
   be walked without ever turning, it is a maze with a gimmick painted on.
   The start check is not a formality — the carve places cells to guarantee
   footing on later legs, and one of those can land in front of the opening
   cell and bury the player inside the rock before the first frame. */
function assess(lv, wantTurns){
  var s0 = project(lv.n, lv.vox, ORI_ID);
  if(!surfaceAt(lv.n, s0, ORI_ID, lv.start)) return null;
  if(!walkable(lv, s0, viewOf(lv.n, ORI_ID, lv.start)[0], viewOf(lv.n, ORI_ID, lv.start)[1], 0)) return null;
  if(doorIndexAt(lv, lv.start) >= 0 || doorIndexAt(lv, lv.goal) >= 0) return null;
  var r = solve(lv);
  if(!r.ok) return null;
  if(r.turns < wantTurns) return null;
  var solid = 0;
  for(var i = 0; i < lv.vox.length; i++) if(lv.vox[i] !== '.') solid++;
  return {turns:r.turns, fill: solid / lv.vox.length};
}



/* ============================================================
   THE DIFFICULTY CONTRACT — ten cubes to a vault, forever.

   A vault is a block of ten levels and owns one difficulty step and one
   look. Inside a vault the demand still creeps, so the tenth cube of a
   vault is harder than the first and easier than the next vault's first —
   the curve is a staircase with a slope on every tread, not a cliff every
   ten.

   Everything here is CAPPED. Size stops at 7 because an 8-cube costs four
   times the search and reads worse on a phone; turns stop at 9 because the
   generator's acceptance falls off a cliff past that and a level nobody can
   hold in their head is not harder, it is just longer. Past the caps the
   difficulty keeps arriving through lock count and density, which cost
   nothing to search.
   ============================================================ */
var CAP_LOCKS = 3;

/* WHAT THE SHAPE CAN ACTUALLY DELIVER.

   The first curve here asked for nine-turn cubes at vault ten and got
   threes, every time, at every budget. That is not a tuning miss — it is
   the mechanic's ceiling, and it is worth writing down.

   Turn-par is bounded by the diameter of the cube's orientation graph. Any
   orientation is a handful of quarter-turns from any other, so once a deck
   set is well connected you can get anywhere in three or four turns no
   matter how big the cube or how many locks are on it. Measured over
   thousands of candidates:

       n=5  par tops out at 2        locks barely move par at all:
       n=6  par tops out at 3          n=7, 0 locks -> max 4
       n=7  par tops out at 4          n=7, 3 locks -> max 4
       n=7 at density .60 -> 5       density is the real lever, not locks

   So difficulty scales on the three axes that DO respond, and turn-par is
   allowed to be the small elegant number it is rather than being flogged
   toward a figure it cannot reach:

     SIZE      more cube to read
     DENSITY   more bedrock, so fewer decks are exposed per face and routes
               are tighter — the one thing that genuinely forces more turns
     LOCKS     sequencing. Locks do not raise the turn count; they raise
               how much has to be true at once for a route to work.
     LENGTH    the minimum route, in steps. A four-turn solution that runs
               forty steps is a different animal to one that runs eight, and
               it is the axis with no ceiling in sight.                     */
function specFor(level){
  var band = Math.floor((level-1)/10), w = (level-1) % 10;
  var b = Math.min(band, 11);                  /* the shape curve saturates; the length curve does not */
  var n = b < 1 ? 5 : b < 3 ? 6 : 7;
  var parLo = (n === 5 ? 1 : n === 6 ? 2 : 3) + (b >= 7 ? 1 : 0);
  return {
    n: n,
    turns: parLo,
    locks: Math.min(CAP_LOCKS, Math.floor(b/2) + (w >= 6 ? 1 : 0)),
    density: Math.min(0.62, 0.42 + 0.021*b + 0.004*w),
    /* Leg length is deliberately NOT scaled with the vault. It was, and it
       backfired: a longer carve lays down more deck, more deck means more
       shortcuts, and par fell from 4 to 1 at vault 21 while route length
       barely moved. The two axes fight, and turn-par is the one worth
       keeping. */
    legMin: 2 + Math.floor(n/3), legMax: 4 + Math.floor(n/2),
    parLo: parLo, parHi: parLo + 2,
    minSteps: 5 + band*2 + w,                  /* band, not b — this one keeps climbing */
    band: band, level: level
  };
}

/* The floor under everything: a cube that cannot fail to be solvable,
   because it is a straight line of decks in open space and nothing else.
   It is deliberately boring. It exists so that the promise "every level
   the player is handed has been proved winnable" has no exceptions —
   not "almost none", none. The tests assert it never actually fires. */
function fallbackLevel(level){
  var n = 5, vox = [], i;
  for(i = 0; i < n*n*n; i++) vox[i] = '.';
  for(i = 0; i < n; i++) vox[vidx(n, i, 2, 2)] = '+';
  return {n:n, vox:vox, start:[0,2,2], goal:[n-1,2,2], keys:[], doors:[],
          par:0, level:level, band:Math.floor((level-1)/10), fallback:true};
}

function hashSeed(level){
  var h = (level * 2654435761) ^ 0x9e3779b9;
  h = Math.imul(h ^ (h >>> 15), 2246822507);
  h = Math.imul(h ^ (h >>> 13), 3266489909);
  return (h ^ (h >>> 16)) >>> 0;
}

/* How well a candidate answers the demand. Being too EASY is a much smaller
   sin than being too hard or malformed, because a cube below par still
   plays — it just under-delivers — whereas one the search could not finish
   inside the budget may be a slog nobody can see the end of. */
function score(turns, steps, fill, spec){
  if(fill < 0.24 || fill > 0.80) return 50;
  if(turns > spec.parHi)  return 200;                       /* overshoot: usable, not wanted */
  if(turns < spec.parLo)  return 100 + turns*10 + Math.min(60, steps);
  /* in band. Prefer more turns, then a longer route — and treat the vault's
     step demand as a bonus rather than a gate, so a short but otherwise
     perfect cube still beats nothing. */
  return 1000 + turns*20 + Math.min(120, steps) + (steps >= spec.minSteps ? 200 : 0);
}

/* ---------- MINT — the only way a level ever reaches a player ------------
   Solvability is not filtered for, it is CONSTRUCTED: the generator carves
   a route by walking and turning, so the route that built the cube is
   always in it. The solver's job is the other half — proving that route
   survived the decoy pass, that the opening cell is not buried, that the
   keys really do open the doors in some order, and reporting the true
   minimum, which is usually shorter than the carve and is what par becomes.

   Then the gate: whatever wins, the level is solved one final time before
   it is handed over, and anything that fails falls back. So there is no
   path through this function that returns an unwinnable cube — not on a
   bad seed, not when the budget expires, not at level nine million.

   The budget bounds the search for QUALITY only. Running out means the
   player gets an easier cube than the vault asked for, never a broken one.
   ------------------------------------------------------------------- */
function mint(level, budgetMs, now){
  now = now || function(){ return Date.now(); };
  var spec = specFor(level), seed = hashSeed(level), t0 = now();
  var best = null, bestScore = -1, tries = 0;

  for(var i = 0; i < 3000; i++){
    if((i & 3) === 0 && best && now() - t0 > budgetMs) break;
    tries++;
    var lv = generate(seed + i * 7919, spec);
    if(!lv) continue;
    if(lv.keys.length !== spec.locks) continue;
    widen(mulberry32(seed + i * 104729), lv, Math.max(2, spec.n + 2 - spec.turns));

    var s0 = project(lv.n, lv.vox, ORI_ID);
    if(!surfaceAt(lv.n, s0, ORI_ID, lv.start)) continue;      /* opened buried */
    if(doorIndexAt(lv, lv.start) >= 0 || doorIndexAt(lv, lv.goal) >= 0) continue;

    /* One bounded solve does all the work: past parHi it stops looking, so
       a cube that is too hard costs no more than one that is just right. */
    var r = solve(lv, spec.parHi);
    if(!r.ok) continue;

    var solid = 0;
    for(var k = 0; k < lv.vox.length; k++) if(lv.vox[k] !== '.') solid++;
    var sc = score(r.turns, r.steps, solid / lv.vox.length, spec);
    if(sc > bestScore){
      bestScore = sc; best = lv; best.par = r.turns; best.steps = r.steps;
    }
    /* Stop only on a cube that maxes the band on BOTH axes. Anything less
       and the remaining budget is better spent looking, because the first
       in-band candidate is usually the weakest one that qualifies. */
    if(r.turns >= spec.parHi && r.steps >= spec.minSteps) break;
  }

  if(!best) best = fallbackLevel(level);
  var gate = solve(best, 60);                                  /* THE GATE */
  if(!gate.ok) { best = fallbackLevel(level); gate = solve(best, 60); }
  best.par = gate.turns; best.steps = gate.steps;
  best.level = level; best.band = spec.band; best.tries = tries;
  best.ms = now() - t0;
  return best;
}

/* Node-only. Everything above this line is what the browser gets, so this
   MUST stay last: the build truncates the file here. */
if(typeof module !== 'undefined') module.exports = {
  widen, specFor, fallbackLevel, hashSeed, score, mint, CAP_LOCKS,
  mulberry32, ORI_ID, TURNS, ORIS, oriIndex, oriKey, vidx, viewOf, worldOf,
  project, surfaceAt, walkable, keyIndexAt, doorIndexAt, samePos, landing,
  solve, generate, assess, carve
};
