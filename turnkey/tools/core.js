/* ============================================================
   TURNKEY — CORE
   The projection model, the solver, and the generator. Written as plain
   functions with no module syntax so this exact text is inlined into the
   game; the shim at the bottom is the only Node-only line.
   ============================================================ */

/* ---------- rng ----------
   NEVER SHUFFLE WITH sort(). `[0,1,2,3].sort(function(){ return rng()-0.5; })`
   is an INCONSISTENT comparator, so how many times the comparator is called —
   and therefore how many numbers it pulls off the stream — is a property of
   the engine's sort implementation, not of the seed. Node's V8 and
   Chromium's V8 disagree, which meant the same level number generated a
   different cube in the browser than in the test suite, and would have
   differed between two Android WebView versions on two phones.
   Fisher-Yates consumes exactly one number per element, everywhere, forever. */
function shuffle4(rng, a){
  for(var i = a.length - 1; i > 0; i--){
    var j = Math.floor(rng() * (i + 1)), t = a[i];
    a[i] = a[j]; a[j] = t;
  }
  return a;
}
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

/* ============================================================
   THE PLATES — a second transform, and the reason par can climb again

   Turn-par is bounded by the diameter of the cube's orientation graph: any
   face is a handful of quarter-turns from any other, so no amount of size or
   locks pushes it past four or five. Rotation was the only verb, and the
   ceiling was rotation's ceiling.

   A PLATE is the second verb. Step on one and the cube's MATERIAL inverts —
   every deck becomes bedrock and every wall becomes floor, or every wall
   becomes air and every gap fills with stone. The geometry is untouched.
   What changes is which of it you may stand on, which means the projection
   you spent four turns learning is now its own negative and the route you
   were walking is a wall.

   Two plates, two bits, FOUR material states — and because the second is
   applied after the first, the pair composes into a third layout neither
   makes alone:

     world 0   deck +      bedrock #     void .        as carved
     world 1   deck #      bedrock +     void .        INVERT: the negative
     world 2   deck +      bedrock .     void #        DRAIN: stone becomes
                                                       air, air becomes stone
     world 3   deck .      bedrock +     void #        both: a third world

   The state space is now position x orientation x keys x doors x WORLD, and
   that is what lets a cube ask for eight turns and get them.

   A PLATE IS ALWAYS THE SURFACE OF ITS COLUMN. It is carved clean through
   the rock, so it is legible from every face in every world — you can always
   see where the plates are, which is what makes planning with them possible
   at all. It has a second consequence worth stating plainly, because it is
   the best thing about them: since a plate always gives footing, EVERY TURN
   IS LEGAL WHILE YOU STAND ON ONE. Plates are pivots. In a game where where
   you stand decides which turns you have, a square that gives you all four
   is worth walking a long way for.

   THE FIVE-SECOND LIMIT IS NOT IN HERE, AND THAT IS ON PURPOSE.

   In the game a flipped world springs back after five seconds and puts you
   back on the nearest plate. This model has no clock in it — a turn costs
   one, a step costs nothing, and neither costs any time — so the solver plans
   as if a flip lasted forever.

   That makes par a LOWER BOUND rather than a promise, and it is the right
   bound to publish. Everything the guarantee is for still holds: the route
   the generator carved is still in the cube, the keys still open the doors in
   some order, the opening cell is still not buried, and the number on the HUD
   is still the fewest turns the geometry can be beaten in. What the clock
   adds is a demand on the player's hands, not a change to the cube.

   AND THE SPRING-BACK IS WHY IT CANNOT COST THEM THE LEVEL. On 39 of the 41
   plate cubes measured, THE EXIT DOES NOT EXIST IN WORLD 0 — it is carved
   into cells that are rock until the material inverts, which is the whole
   point of the second verb. So running out of time must never leave the
   player in world 0 away from the plate: that is the state in which the cube
   genuinely cannot be finished, and it is where an earlier version of this
   dropped them. Putting them back ON the plate makes the clock cost the walk
   and nothing else. Press it again and go.

   Putting the clock in the search would mean carrying real time in the state
   key, which turns a 0-1 BFS over a few thousand states into a search over a
   continuum, to answer a question the player is better placed to answer than
   the solver: whether they can move that fast.
   ============================================================ */
var GLYPH_BIT = {A:1, B:2};                 /* A inverts, B drains */
function isGlyph(c){ return c === 'A' || c === 'B'; }

/* The effective kind of a cell in a given world. Glyphs are exempt: a plate
   is a plate in every world, which is what makes it safe to stand on while
   everything around it changes. */
function effType(c, world){
  if(c === 'A' || c === 'B') return c;
  var t = c;
  if(world & 1){ if(t === '+') t = '#'; else if(t === '#') t = '+'; }
  if(world & 2){ if(t === '#') t = '.'; else if(t === '.') t = '#'; }
  return t;
}
/* Which base char yields footing in this world — the inverse of the above,
   and the only thing the carve needs to know about worlds. */
function baseForWalk(world){ return (world & 1) ? '#' : '+'; }
function isWalkType(t){ return t === '+' || t === 'A' || t === 'B'; }

/* Effective arrays are derived, cached per world, and never mutated. */
function effVox(lv, world){
  if(!lv._eff) lv._eff = {};
  if(lv._eff[world]) return lv._eff[world];
  var out = new Array(lv.vox.length);
  for(var i = 0; i < out.length; i++) out[i] = effType(lv.vox[i], world);
  return (lv._eff[world] = out);
}
function clearEff(lv){ lv._eff = null; }
function glyphAt(lv, w){
  var c = lv.vox[vidx(lv.n, w[0], w[1], w[2])];
  return GLYPH_BIT[c] || 0;
}

/* ---------- THE CUBE ----------------------------------------------------
   A level is n^3 cells of five kinds:
     '.'  empty      — nothing there
     '#'  bedrock    — solid, but you cannot stand on its face
     '+'  deck       — solid, and walkable when it is the nearest thing
     'A'  invert plate, 'B' drain plate — always walkable, always the
          surface of their column, and they change the world when stepped on
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
/* An orientation is a signed permutation, so where a cell lands on screen is
   fixed the moment the orientation is — it does not depend on the cube's
   contents at all. Recomputing it per cell per projection was most of the
   solver's cost: a search over 24 orientations x 4 worlds fills 96
   projections, each of which was running viewOf on every cell. The mapping
   is built once per (size, orientation) and reused for the life of the tab. */
var VIEWMAP = {};
function viewMap(n, m){
  var key = n + '|' + oriIndex(m), cached = VIEWMAP[key];
  if(cached) return cached;
  var map = new Int32Array(n*n*n);
  for(var y = 0; y < n; y++) for(var z = 0; z < n; z++) for(var x = 0; x < n; x++){
    var v = viewOf(n, m, [x,y,z]);
    map[vidx(n,x,y,z)] = (v[0]*n + v[1])*n + v[2];
  }
  return (VIEWMAP[key] = map);
}
function project(n, vox, m){
  var surf = new Array(n*n), i, nn = n*n, map = viewMap(n, m);
  for(i = 0; i < nn; i++) surf[i] = null;
  for(i = 0; i < vox.length; i++){
    var t = vox[i];
    if(t === '.') continue;
    var e = map[i], d = e % n, k = (e - d) / n, cur = surf[k];
    var g = isGlyph(t);
    /* a plate is carved clean through, so it wins its column outright; among
       equals the nearer one wins as always */
    if(!cur || (g && !isGlyph(cur.t)) || ((g === isGlyph(cur.t)) && d > cur.d)){
      var yy = (i / nn) | 0, rr = i - yy*nn, zz = (rr / n) | 0;
      surf[k] = {d:d, t:t, w:[rr - zz*n, yy, zz]};
    }
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
  if(!s || !isWalkType(s.t)) return null;
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

/* THE OTHER SIDE OF THE CUBE, exactly.

   The cell diametrically opposite the one you are standing in — through the
   centre, not around the outside. If something is sitting there it can be
   seen through the horizon, faintly, and that is ALL it does: no reach, no
   route, no bearing on any turn. It is a look through a hole at the far
   side of the world, and the only reward is noticing.

   Which is worth having precisely because it costs nothing. A player who
   never sees it has lost nothing; a player who does has found out what the
   thing they are moving actually is. */
function antipode(){ return [N-1-pos[0], N-1-pos[1], N-1-pos[2]]; }
function throughLook(){
  if(!lv) return null;
  var a = antipode(), i;
  /* a thing that is rock in this world is not in this world, and the far
     side of a hole is no exception — same rule the flat board draws by */
  if(!isWalkType(effType(lv.vox[vidx(lv.n, a[0], a[1], a[2])], world))) return null;
  if(samePos(a, lv.goal)) return {kind:4};
  for(i = 0; i < lv.keys.length; i++)
    if(!(kmask & (1<<i)) && samePos(a, lv.keys[i])) return {kind:2};
  for(i = 0; i < lv.doors.length; i++)
    if(samePos(a, lv.doors[i])) return {kind:3, i:i};
  return null;
}

/* ONE STEP AWAY, NOT THROUGH THE CENTRE.
   throughLook() is a peek at the antipode and has no bearing on movement.
   This is the opposite: real, walkable neighbours — the four cells a single
   step from where you stand right now — checked for whether one of them is
   the goal, an uncollected key, or a door. Nothing here changes what you may
   do; it only tells the eye where the next useful step is before the hand
   has to work it out. */
function nearSpecials(){
  if(!lv) return [];
  var v = viewOf(N, M, pos), out = [];
  for(var t = 0; t < 4; t++){
    var u2 = v[0] + TURNS[t].dx, v2 = v[1] + TURNS[t].dy;
    var cell = walkable(lv, surf, u2, v2, doors);
    if(!cell) continue;
    var w = cell.w;
    if(samePos(w, lv.goal)){ out.push({u2:u2, v2:v2, kind:4, w:w}); continue; }
    var ki = keyIndexAt(lv, w);
    if(ki >= 0 && !(kmask & (1<<ki))){ out.push({u2:u2, v2:v2, kind:2, w:w}); continue; }
    var di = doorIndexAt(lv, w);
    if(di >= 0){ out.push({u2:u2, v2:v2, kind:3, i:di, w:w}); continue; }
  }
  return out;
}

/* Turning is legal only when the column you are standing in still has
   footing after the turn. That single rule is what makes WHERE you stand
   gate WHICH turns you have — position gating rotation, rotation gating
   position. It is the lock and the key, falling out of the geometry. */
function landing(lv, m2, pos, doorsOpen, world){
  var surf2 = project(lv.n, effVox(lv, world || 0), m2);
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
  var start = from ? {pos:from.pos, ori:from.ori, kmask:from.kmask, doors:from.doors, world:from.world|0}
                   : {pos: lv.start, ori: 0, kmask: 0, doors: 0, world: 0};
  if(!from){
    var s0 = keyIndexAt(lv, lv.start);
    if(s0 >= 0) start.kmask = 1 << s0;
    /* opening on a plate would fire it before the first frame; generation
       never does that, but a hand-written cube might */
    start.world = glyphAt(lv, lv.start);
  }
  var buckets = [], seen = {}, projCache = {}, parent = {};
  function P(oi, wd){
    var k = oi + '|' + wd;
    if(!projCache[k]) projCache[k] = project(n, effVox(lv, wd), ORIS[oi]);
    return projCache[k];
  }
  function skey(s){ return vidx(n,s.pos[0],s.pos[1],s.pos[2]) + ':' + s.ori + ':' + s.kmask + ':' + s.doors + ':' + s.world; }
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
      var m = ORIS[s.ori], surf = P(s.ori, s.world), v = viewOf(n, m, s.pos);

      /* steps — free */
      for(var t = 0; t < 4; t++){
        var u2 = v[0] + TURNS[t].dx, v2 = v[1] + TURNS[t].dy;
        if(u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
        var raw = surf[u2*n + v2];
        if(!raw || !isWalkType(raw.t)) continue;
        var nd = s.doors, di = doorIndexAt(lv, raw.w);
        if(di >= 0 && !(s.doors & (1<<di))){
          if(spare(s) < 1) continue;            /* shut, and nothing to open it with */
          nd = s.doors | (1<<di);               /* a door you can open is a step, not a wall */
        }
        var nk = s.kmask, ki = keyIndexAt(lv, raw.w);
        if(ki >= 0) nk |= (1<<ki);
        /* a plate fires under the foot that lands on it */
        var nw = s.world ^ (GLYPH_BIT[raw.t] || 0);
        relax(cost, {pos: raw.w, ori: s.ori, kmask: nk, doors: nd, world: nw}, skey(s), {kind:'step', dir:t});
      }
      /* turns — one each. A plate you turn onto does NOT fire: you press it
         with a foot, and rotating the world is not pressing it. That also
         keeps the landing you just checked from being invalidated by the
         world changing underneath it. */
      for(var t2 = 0; t2 < 4; t2++){
        var m2 = TURNS[t2].f(m), land = landing(lv, m2, s.pos, s.doors, s.world);
        if(!land) continue;
        var nk2 = s.kmask, ki2 = keyIndexAt(lv, land.w);
        if(ki2 >= 0) nk2 |= (1<<ki2);
        relax(cost + 1, {pos: land.w, ori: oriIndex(m2), kmask: nk2, doors: s.doors, world: s.world}, skey(s), {kind:'turn', dir:t2});
      }
    }
  }
  /* THE TWO WAYS TO FAIL ARE NOT THE SAME ANSWER. Exhausting every reachable
     state without finding the goal means the cube is dead from here; running
     out of budget with work still queued means only that the search stopped
     looking. The live readout says "no way on" to the first and "40+" to the
     second, and a caller that does not care can keep reading .ok. */
  return {ok:false, over: !!buckets[budget+1]};
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
/* Carving in world W means writing the BASE char that comes out walkable
   under W — which is the whole of what the plates cost the generator.
   A locked cell is one an earlier leg is relying on in a different world;
   it is never overwritten, so a route carved for world 0 cannot be
   demolished by a later leg carving for world 1. */
function carve(n, vox, m, u, v, hintDepth, world, lock){
  world = world | 0;
  var ev = new Array(vox.length), i;
  for(i = 0; i < vox.length; i++) ev[i] = effType(vox[i], world);
  var best = -1, bw = null;
  for(var y = 0; y < n; y++) for(var z = 0; z < n; z++) for(var x = 0; x < n; x++){
    var id = vidx(n,x,y,z);
    if(ev[id] === '.') continue;
    var vw = viewOf(n, m, [x,y,z]);
    if(vw[0] !== u || vw[1] !== v) continue;
    if(isGlyph(ev[id])){ bw = [x,y,z]; best = 1e9; }          /* a plate is already footing */
    else if(vw[2] > best){ best = vw[2]; bw = [x,y,z]; }
  }
  var want = baseForWalk(world);
  if(bw){
    var bi = vidx(n,bw[0],bw[1],bw[2]);
    if(isGlyph(vox[bi])) return bw;
    if(lock && lock[bi] !== undefined && lock[bi] !== want) return null;
    vox[bi] = want; if(lock) lock[bi] = want;
    return bw;
  }
  var d = Math.max(0, Math.min(n-1, hintDepth === undefined ? (n>>1) : hintDepth));
  var w = worldOf(n, m, [u,v,d]), wi = vidx(n,w[0],w[1],w[2]);
  if(lock && lock[wi] !== undefined && lock[wi] !== want) return null;
  vox[wi] = want; if(lock) lock[wi] = want;
  return w;
}

function generate(seed, opt){
  var rng = mulberry32(seed), n = opt.n, i;
  var vox = new Array(n*n*n);
  for(i = 0; i < n*n*n; i++) vox[i] = rng() < opt.density ? '#' : '.';

  var m = ORI_ID, path = [], world = 0, lock = {};
  var nGlyph = opt.glyphs || 0, kinds = opt.glyphKinds || 1, placed = [];
  /* the legs a plate is dropped on, spread through the route so the world
     changes in the MIDDLE of solving rather than at one end of it */
  var legCount = opt.turns + 1, glyphLegs = {};
  for(i = 0; i < nGlyph; i++) glyphLegs[Math.max(1, Math.round(legCount * (i+1)/(nGlyph+1)))] = i;

  var u = 1 + Math.floor(rng()*(n-2)), v = 1 + Math.floor(rng()*(n-2));
  var pos = carve(n, vox, m, u, v, undefined, world, lock);
  if(!pos) return null;
  path.push(pos);

  for(var leg = 0; leg <= opt.turns; leg++){
    var steps = opt.legMin + Math.floor(rng()*(opt.legMax - opt.legMin + 1));
    for(var st = 0; st < steps; st++){
      var order = shuffle4(rng, [0,1,2,3]), moved = false;
      var vv = viewOf(n, m, pos);
      for(var t = 0; t < 4 && !moved; t++){
        var u2 = vv[0] + TURNS[order[t]].dx, v2 = vv[1] + TURNS[order[t]].dy;
        if(u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
        var nx = carve(n, vox, m, u2, v2, vv[2], world, lock);
        if(!nx) continue;                       /* that cell belongs to another world */
        pos = nx; path.push(pos); moved = true;
      }
      if(!moved) break;
    }
    /* drop a plate under the foot that is standing here, and carry on
       carving in the world it opens */
    if(glyphLegs[leg] !== undefined && placed.length < nGlyph && path.length > 2){
      var gi = vidx(n, pos[0], pos[1], pos[2]);
      var kind = (kinds > 1 && (rng() < 0.45)) ? 'B' : 'A';
      vox[gi] = kind; lock[gi] = kind;
      placed.push({w:pos.slice(), kind:kind});
      world ^= GLYPH_BIT[kind];
      /* the cell you stand on after the flip is the plate itself, so nothing
         needs re-seating — that is exactly why plates are exempt.

         THEN KEEP WALKING, in the world the plate just opened. Without this
         the plate lands on the final leg and the goal ends up ON it, which
         is rejected — so every candidate died and vault II fell through to
         the fallback cube. The route has to go somewhere after the flip;
         that is the entire point of the flip. */
      var after = 2 + Math.floor(rng()*3);
      for(var ea = 0; ea < after; ea++){
        var ord2 = shuffle4(rng, [0,1,2,3]), mv2 = false;
        var vw2 = viewOf(n, m, pos);
        for(var t2 = 0; t2 < 4 && !mv2; t2++){
          var a2 = vw2[0] + TURNS[ord2[t2]].dx, b2 = vw2[1] + TURNS[ord2[t2]].dy;
          if(a2 < 0 || b2 < 0 || a2 >= n || b2 >= n) continue;
          var nx2 = carve(n, vox, m, a2, b2, vw2[2], world, lock);
          if(!nx2) continue;
          pos = nx2; path.push(pos); mv2 = true;
        }
        if(!mv2) break;
      }
    }
    if(leg === opt.turns) break;
    /* turn, then guarantee footing on the far side of it */
    var pick = Math.floor(rng()*4), m2 = TURNS[pick].f(m);
    var lv2 = viewOf(n, m2, pos);
    var lnd = carve(n, vox, m2, lv2[0], lv2[1], undefined, world, lock);
    if(!lnd) continue;
    m = m2; pos = lnd; path.push(pos);
  }

  /* the goal is the far end of the carve; keys and doors are pinched onto
     the route at fractions of its length so they gate rather than decorate */
  var goal = path[path.length-1];
  var lv = {n:n, vox:vox, start:path[0], goal:goal, keys:[], doors:[], glyphs:placed, _lock:lock};
  if(samePos(lv.start, goal)) return null;
  /* neither end may be a plate: one would fire before the first frame, the
     other would change the world at the moment the level ends */
  if(isGlyph(vox[vidx(n, lv.start[0], lv.start[1], lv.start[2])])) return null;
  if(isGlyph(vox[vidx(n, goal[0], goal[1], goal[2])])) return null;
  if(placed.length !== nGlyph) return null;

  /* Keys and doors go on the route, but NEVER on a plate. A shut door
     sitting on a plate blocks the plate's own column, which quietly destroys
     the one property plates are built around — that you can always turn from
     one — and reads as nonsense besides. Walk outward from the intended spot
     until a cell turns up that is nobody else's. */
  function slotNear(idx){
    for(var off = 0; off < path.length; off++){
      for(var sgn = -1; sgn <= 1; sgn += 2){
        var j = idx + off*sgn;
        if(j < 1 || j > path.length - 2) continue;
        var c = path[j];
        if(isGlyph(vox[vidx(n, c[0], c[1], c[2])])) continue;
        if(samePos(c, lv.start) || samePos(c, goal)) continue;
        if(keyIndexAt(lv, c) >= 0 || doorIndexAt(lv, c) >= 0) continue;
        return c;
      }
      if(off === 0) continue;
    }
    return null;
  }
  if(opt.locks){
    for(i = 0; i < opt.locks; i++){
      var kw = slotNear(Math.floor(path.length * (0.18 + 0.22*i)));
      var dw = slotNear(Math.floor(path.length * (0.55 + 0.2*i)));
      if(!kw || !dw || samePos(kw, dw)) continue;
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
function widen(rng, lv, count, lock){
  var n = lv.n;
  for(var c = 0; c < count; c++){
    var m = ORIS[Math.floor(rng()*24)], surf = project(n, effVox(lv, 0), m), pool = [];
    for(var i = 0; i < surf.length; i++) if(surf[i] && surf[i].t === '#') pool.push(surf[i].w);
    if(!pool.length) continue;
    var w = pool[Math.floor(rng()*pool.length)], wi = vidx(n,w[0],w[1],w[2]);
    if(isGlyph(lv.vox[wi])) continue;
    if(lock && lock[wi] !== undefined) continue;      /* a route in some world needs this */
    lv.vox[wi] = '+'; clearEff(lv);
  }
  return lv;
}

/* A level is worth keeping only if the collapse is load-bearing: if it can
   be walked without ever turning, it is a maze with a gimmick painted on.
   The start check is not a formality — the carve places cells to guarantee
   footing on later legs, and one of those can land in front of the opening
   cell and bury the player inside the rock before the first frame. */
function assess(lv, wantTurns){
  clearEff(lv);
  var s0 = project(lv.n, effVox(lv, 0), ORI_ID);
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
/* WHEN THE PLATES ARRIVE.

   Level 20 is the last cube of vault II and the first with a plate on it —
   deliberately at the END of a vault, so the mechanic lands as a door into
   the next one rather than as a footnote in the middle of a set.

   INVERT alone for two vaults, because one transform is enough to relearn.
   DRAIN joins at vault V, and from there the two compose into worlds neither
   makes alone. A second plate appears at vault VII, which is roughly where
   the old curve had run out of things to ask for. */
var GLYPH_FROM = 20;
function glyphPlan(level){
  if(level < GLYPH_FROM) return {glyphs:0, glyphKinds:0};
  var band = Math.floor((level-1)/10);
  /* ONE plate, always. A second lowers par (more worlds to shortcut through)
     and costs a chunk of search time for the privilege. Variety past vault V
     comes from WHICH plate it is, not how many. */
  return {glyphs: 1, glyphKinds: band >= 4 ? 2 : 1};
}

function specFor(level){
  var band = Math.floor((level-1)/10), w = (level-1) % 10;
  var b = Math.min(band, 11);                  /* the shape curve saturates; the length curve does not */
  var n = b < 1 ? 5 : b < 3 ? 6 : 7;
  var gp = glyphPlan(level);
  /* A plate is a second transform, so a cube carrying one can be asked for
     more than the rotation ceiling alone would ever give up. Measured over
     thousands of candidates, one plate moves the ceiling by two:

         n=6  max par 3 -> 5        n=7  max par 4 -> 6
         and roughly 2.5x as many cubes land at par 3 or better

     TWO plates is WORSE, which is not what you would guess: max par falls
     back to 5. It is the same trap as the decoy pass and the leg length —
     more freedom to reach any world is more ways to shortcut, and the search
     finds them. So the second plate is carried for VARIETY, not difficulty.

     The DEMAND is deliberately not raised for carrying a plate either. Asking
     for the new ceiling put two thirds of mints outside their own band,
     because the tail of the distribution is thin and a 240ms search cannot
     be relied on to find it. The band stays where it is reliably met and the
     scorer reaches for the top of it — so plates show up as fours where the
     old curve gave threes, rather than as a promise the generator misses. */
  /* The band is deliberately wide and set to what is RELIABLY delivered, not
     to what the ceiling allows. Its job is to catch the curve collapsing, not
     to express ambition — the scorer expresses that by reaching for the top
     of the band, and "par rises across vaults" is asserted separately against
     the running game. */
  var parLo = (n === 5 ? 1 : 2);
  /* TWO DIFFERENT NUMBERS THAT WERE ONE NUMBER, AND THAT WAS THE BUG.

     `turns` is the CARVE's ambition — how many times the generator's own
     route turns while it is cutting the cube. `parLo/parHi` is the
     ACCEPTANCE band the solver's answer has to land in. They started life as
     the same field, and when the band was widened down to 2 the carve went
     with it: the generator spent three vaults cutting two-turn routes, and a
     four-turn optimum cannot exist in a cube whose route never needed one.
     The candidate histogram made it obvious the moment it was printed —
     {0:7, 1:11, 2:16} where the probe had shown a fifth of cubes at par 3+.

     The carve reaches HIGH and the band stays WIDE. The carve costs nothing
     to raise; the band is what has to be honest. */
  var carveTurns = Math.min(9, 3 + b + (gp.glyphs ? 1 : 0));
  return {
    n: n,
    glyphs: gp.glyphs, glyphKinds: gp.glyphKinds,
    turns: carveTurns,
    /* doors multiply the search by 2^locks and worlds already multiply it by
       four; a cube carrying a plate keeps its lock count down so a mint stays
       inside a couple of hundred milliseconds on a phone */
    locks: Math.min(gp.glyphs ? 2 : CAP_LOCKS, Math.floor(b/2) + (w >= 6 ? 1 : 0)),
    density: Math.min(0.62, 0.42 + 0.021*b + 0.004*w),
    /* Leg length is deliberately NOT scaled with the vault. It was, and it
       backfired: a longer carve lays down more deck, more deck means more
       shortcuts, and par fell from 4 to 1 at vault 21 while route length
       barely moved. The two axes fight, and turn-par is the one worth
       keeping. */
    legMin: 2 + Math.floor(n/3), legMax: 4 + Math.floor(n/2),
    parLo: parLo, parHi: parLo + 4,
    minSteps: 5 + band*2 + w,                  /* band, not b — this one keeps climbing */
    /* HOW MANY CANDIDATES TO LOOK AT, AS A COUNT AND NEVER AS A CLOCK.
       This used to be a millisecond budget, and that quietly broke the one
       promise the whole no-server design rests on: a faster machine got
       through more candidates before the budget expired and therefore chose
       a DIFFERENT winner, so cube 41 was a five-turn puzzle in one browser
       and a three-turn puzzle in another. The determinism test could not see
       it because it only ever re-ran in the same environment.
       Work is now counted, not timed. Every device does exactly this many
       passes and lands on exactly the same cube — a slow phone takes longer,
       which is what the background prebuild is for.

       It counts candidates that reach the SOLVER, not loop passes. Most
       passes bail long before that — the carve fails, the lock count comes
       out wrong, the opening cell ends up buried — and counting those made
       the effective sample size swing by a factor of three between specs,
       which showed up as the difficulty curve going flat. */
    tries: gp.glyphs ? 24 : 40,
    decoys: gp.glyphs ? 3 : 4,
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
  /* In band. TURNS DOMINATE, and by a wide margin — this used to read
     `turns*20 + min(120,steps) + 200 if long enough`, which meant a rambling
     two-turn cube outscored a tight four-turn one and the whole difficulty
     curve sat flat at par 2.7 no matter how many candidates were sampled.
     Par is the number on the HUD and the number the player is playing
     against; route length only breaks ties between cubes of equal par. */
  return 1000 + turns*300 + Math.min(90, steps) + (steps >= spec.minSteps ? 40 : 0);
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

  /* budgetMs is accepted for call-site compatibility and deliberately does
     NOT bound the search — see the note on spec.tries. The outer cap only
     stops a pathological spec spinning; the real bound is `tries`, which
     counts candidates actually put to the solver. */
  for(var i = 0; i < spec.tries * 14 && tries < spec.tries; i++){
    var lv = generate(seed + i * 7919, spec);
    if(!lv) continue;
    if(lv.keys.length !== spec.locks) continue;
    /* DECOYS. This used to read `spec.n + 2 - spec.turns`, which was tuned
       when `turns` meant the ambition of the vault. It now means the bottom
       of a deliberately wide band — always 2 — so the formula silently
       pinned itself at seven decoys and spent three vaults quietly handing
       the solver shortcuts. A formula whose input changed meaning is the
       hardest kind of regression to see, because nothing about it looks
       wrong. It is a small constant now, and it is measured against par. */
    widen(mulberry32(seed + i * 104729), lv, spec.decoys, lv._lock);

    clearEff(lv);
    var s0 = project(lv.n, effVox(lv, 0), ORI_ID);
    if(!surfaceAt(lv.n, s0, ORI_ID, lv.start)) continue;      /* opened buried */
    if(doorIndexAt(lv, lv.start) >= 0 || doorIndexAt(lv, lv.goal) >= 0) continue;

    /* One bounded solve does all the work: past parHi it stops looking, so
       a cube that is too hard costs no more than one that is just right. */
    tries++;
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
  widen, specFor, glyphPlan, effType, effVox, isGlyph, glyphAt, GLYPH_BIT, isWalkType, GLYPH_FROM, fallbackLevel, hashSeed, score, mint, CAP_LOCKS,
  mulberry32, ORI_ID, TURNS, ORIS, oriIndex, oriKey, vidx, viewOf, worldOf,
  project, surfaceAt, walkable, keyIndexAt, doorIndexAt, samePos, landing,
  solve, generate, assess, carve
};
