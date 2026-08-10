/* ============================================================
   PERSISTENCE — one key, and it is allowed to fail.
   A WebView with storage disabled should still play; it just forgets.
   ============================================================ */
var store = {best:{}, reached:1, sound:1, haptic:1, depth:0, taught:0};
try{ var raw = localStorage.getItem('turnkey-v2'); if(raw) store = Object.assign(store, JSON.parse(raw)); }catch(e){}
var saveT = 0;
function save(){
  clearTimeout(saveT);
  saveT = setTimeout(function(){
    try{ localStorage.setItem('turnkey-v2', JSON.stringify(store)); }catch(e){}
  }, 220);
}

/* ============================================================
   AUDIO — a lock-and-stone bus, built from nothing.
   No files: a packaged app should not pay a download for six sounds it
   could synthesise, and an offline WebView should not go quiet.
   The context is created on the first touch because every mobile browser
   refuses one made any earlier.
   ============================================================ */
var AC = null;
function audio(){
  if(AC || !store.sound) return AC;
  try{ AC = new (window.AudioContext || window.webkitAudioContext)(); }catch(e){ AC = null; }
  return AC;
}
function tone(f, dur, type, gain, slideTo){
  var a = audio(); if(!a || !store.sound) return;
  var o = a.createOscillator(), g = a.createGain(), t = a.currentTime;
  o.type = type || 'sine'; o.frequency.setValueAtTime(f, t);
  if(slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(gain || 0.12, t + 0.008);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g); g.connect(a.destination); o.start(t); o.stop(t + dur + 0.02);
}
function noise(dur, cut, gain){
  var a = audio(); if(!a || !store.sound) return;
  var n = Math.floor(a.sampleRate * dur), b = a.createBuffer(1, n, a.sampleRate), d = b.getChannelData(0);
  for(var i = 0; i < n; i++) d[i] = (Math.random()*2 - 1) * (1 - i/n);
  var s = a.createBufferSource(); s.buffer = b;
  var f = a.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = cut || 1400;
  var g = a.createGain(); g.gain.value = gain || 0.16;
  s.connect(f); f.connect(g); g.connect(a.destination); s.start();
}
var sfx = {
  step:  function(){ noise(0.055, 900, 0.075); },
  /* the detent: a hard click and the mass behind it arriving a beat later */
  turn:  function(){ noise(0.035, 2600, 0.15); tone(96, 0.16, 'triangle', 0.1, 62); },
  deny:  function(){ tone(74, 0.13, 'square', 0.05, 58); },
  key:   function(){ tone(880, 0.1, 'triangle', 0.09); setTimeout(function(){ tone(1320, 0.16, 'triangle', 0.07); }, 70); },
  door:  function(){ noise(0.09, 700, 0.2); tone(150, 0.22, 'sawtooth', 0.06, 90); },
  win:   function(){ [523,659,784,1047].forEach(function(f,i){ setTimeout(function(){ tone(f, 0.42, 'triangle', 0.1); }, i*95); }); },
  stuck: function(){ tone(200, 0.3, 'sine', 0.06, 120); }
};
function buzz(ms){ if(store.haptic && navigator.vibrate) try{ navigator.vibrate(ms); }catch(e){} }

/* ============================================================
   BOARD STATE
   The integer basis M is the ONLY thing the rules ever read. The float
   basis the renderer uses during a drag is a picture of a turn that has
   not happened yet, and no rule is allowed to consult it.
   ============================================================ */
var cv = document.getElementById('stage'), ctx = cv.getContext('2d');
var W = 0, H = 0, DPR = 1;
var lv = null, N = 0, M = ORI_ID, pos = [0,0,0], kmask = 0, doors = 0, turns = 0;
var surf = null, reach = null, faces = [], undoStack = [], levelNo = 1, curBand = 0, viewBand = 0;
var walking = null, anim = null, drag = null, won = false, stuck = false;
var probe = document.createElement('div');
probe.style.cssText = 'position:fixed;top:0;left:0;width:0;height:0;visibility:hidden;pointer-events:none;'
  + 'padding:var(--sa-top) var(--sa-right) var(--sa-bottom) var(--sa-left);';
document.body.appendChild(probe);
function insets(){
  var s = getComputedStyle(probe);
  return {t:parseFloat(s.paddingTop)||0, r:parseFloat(s.paddingRight)||0,
          b:parseFloat(s.paddingBottom)||0, l:parseFloat(s.paddingLeft)||0};
}

function fit(){
  DPR = Math.min(window.devicePixelRatio || 1, 2.5);
  W = window.innerWidth; H = window.innerHeight;
  cv.width = Math.round(W*DPR); cv.height = Math.round(H*DPR);
  cv.style.width = W + 'px'; cv.style.height = H + 'px';
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  layout(); buildSky(); buildMotes(); shS = -1;
}
var S = 40, CX = 0, CY = 0;
function layout(){
  if(!N) return;
  var ins = insets();
  var top = ins.t + 68, bot = ins.b + 150;           /* the two HUD bars, never overlapped */
  var availW = W - ins.l - ins.r - 26, availH = H - top - bot;
  S = Math.max(18, Math.min(availW, availH) * 0.98 / N);
  CX = ins.l + (W - ins.l - ins.r)/2;
  CY = top + availH/2;
}

/* ---------- the render basis: M, bent by whatever turn is in progress ---- */
function yawBasis(m, th){
  var c = Math.cos(th), s = Math.sin(th);
  return {R:[c*m.R[0]+s*m.F[0], c*m.R[1]+s*m.F[1], c*m.R[2]+s*m.F[2]],
          U:m.U,
          F:[c*m.F[0]-s*m.R[0], c*m.F[1]-s*m.R[1], c*m.F[2]-s*m.R[2]]};
}
function pitchBasis(m, th){
  var c = Math.cos(th), s = Math.sin(th);
  return {R:m.R,
          U:[c*m.U[0]+s*m.F[0], c*m.U[1]+s*m.F[1], c*m.U[2]+s*m.F[2]],
          F:[c*m.F[0]-s*m.U[0], c*m.F[1]-s*m.U[1], c*m.F[2]-s*m.U[2]]};
}
function liveAngle(){
  if(anim) return {axis:anim.axis, ang:anim.ang};
  if(drag && drag.axis) return {axis:drag.axis, ang:drag.ang};
  return null;
}
function liveBasis(){
  var a = liveAngle();
  if(!a || Math.abs(a.ang) < 1e-4) return M;
  return a.axis === 'x' ? yawBasis(M, a.ang) : pitchBasis(M, a.ang);
}
/* A square rotating about a screen axis projects (cos+sin) times as wide, so
   pulling the camera back by exactly that keeps the cube inside the frame
   through the whole turn instead of clipping at 45 degrees. */
function liveZoom(){
  var a = liveAngle(); if(!a) return 1;
  var t = Math.abs(a.ang);
  return 1 / (Math.cos(t) + Math.sin(t));
}

/* ============================================================
   LEVEL LOAD
   ============================================================ */
function loadLevel(level){
  levelNo = Math.max(1, level|0);
  var src = levelData(levelNo);
  lv = {n:src.n, vox:src.vox.slice(), start:src.start, goal:src.goal,
        keys:src.keys, doors:src.doors, par:src.par, name:levelName(levelNo)};
  N = lv.n;
  M = ORI_ID; pos = lv.start.slice(); kmask = 0; doors = 0; turns = 0;
  var k0 = keyIndexAt(lv, pos); if(k0 >= 0) kmask |= (1<<k0);
  undoStack = []; walking = null; anim = null; drag = null; won = false; stuck = false;
  var band = vaultOf(levelNo);
  if(band !== curBand || !skyCv){ curBand = band; setStyle(band); }
  buildFaces();
  layout(); settle();
  if(levelNo > store.reached){ store.reached = levelNo; save(); }
  prebuild(levelNo + 1);            /* cut the next one while this one is played */
}

/* Jumping somewhere far from the cache costs a real mint, so say so rather
   than dropping a frame and hoping. One paint, then the work. */
function goLevel(level, thenShow){
  if(level > BAKED.length && !mintCache[level]){
    toast('cutting cube ' + level + '…');
    show(null);
    setTimeout(function(){ loadLevel(level); if(thenShow) thenShow(); }, 30);
  } else { loadLevel(level); show(null); if(thenShow) thenShow(); }
}

/* Only faces with nothing next to them are ever drawn — the inside of the
   cube is not visible from anywhere, and meshing it away is the difference
   between 1300 quads a frame and 300 on the levels that need it. */
var FACE_D = [[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]];
var FACE_Q = FACE_D.map(function(d){
  var a = Math.abs(d[0]) === 1 ? [0,1,0] : [1,0,0];
  var b = [d[1]*a[2]-d[2]*a[1], d[2]*a[0]-d[0]*a[2], d[0]*a[1]-d[1]*a[0]];
  a = [b[1]*d[2]-b[2]*d[1], b[2]*d[0]-b[0]*d[2], b[0]*d[1]-b[1]*d[0]];
  var q = [], sg = [[1,1],[-1,1],[-1,-1],[1,-1]];
  for(var i = 0; i < 4; i++)
    q.push([d[0]*0.5 + (a[0]*sg[i][0] + b[0]*sg[i][1])*0.5,
            d[1]*0.5 + (a[1]*sg[i][0] + b[1]*sg[i][1])*0.5,
            d[2]*0.5 + (a[2]*sg[i][0] + b[2]*sg[i][1])*0.5]);
  return q;
});
function buildFaces(){
  faces = [];
  for(var y = 0; y < N; y++) for(var z = 0; z < N; z++) for(var x = 0; x < N; x++){
    var t = lv.vox[vidx(N,x,y,z)];
    if(t === '.') continue;
    for(var f = 0; f < 6; f++){
      var d = FACE_D[f], nx = x+d[0], ny = y+d[1], nz = z+d[2];
      var out = nx < 0 || ny < 0 || nz < 0 || nx >= N || ny >= N || nz >= N;
      if(out || lv.vox[vidx(N,nx,ny,nz)] === '.') faces.push({x:x, y:y, z:z, f:f, t:t});
    }
  }
}

/* recompute everything the projection decides, after any settled change */
function settle(){
  surf = project(N, lv.vox, M);
  computeReach();
  refreshHud();
}
function computeReach(){
  reach = new Uint8Array(N*N);
  var v = viewOf(N, M, pos), q = [v[0]*N + v[1]];
  reach[q[0]] = 1;
  for(var i = 0; i < q.length; i++){
    var u = (q[i]/N)|0, w = q[i] % N;
    for(var t = 0; t < 4; t++){
      var u2 = u + TURNS[t].dx, w2 = w + TURNS[t].dy;
      if(u2 < 0 || w2 < 0 || u2 >= N || w2 >= N) continue;
      var k = u2*N + w2;
      if(reach[k]) continue;
      if(!walkable(lv, surf, u2, w2, doors)) continue;
      reach[k] = 1; q.push(k);
    }
  }
}
function keysHeld(){ return popcnt(kmask) - popcnt(doors); }
function popcnt(x){ var c = 0; while(x){ x &= x-1; c++; } return c; }

/* ============================================================
   RENDER
   Two paths that agree exactly at rest.

   SETTLED — the cube is square to the camera, so every side face has zero
   projected area and the image IS a flat grid. Drawn as rects: crisper,
   cheaper, and the state the player actually reads a puzzle in.

   TURNING — the honest 3D solid, so a turn shows you what you are turning.
   The moment it lands, the first path takes over mid-frame and nothing
   moves, because both draw the same front faces from the same numbers.
   ============================================================ */
/* TWO RAMPS THAT MUST NEVER MEET.

   Depth is drawn as brightness, and material is drawn as brightness too, and
   the first build let those two jobs share a range: a near lump of bedrock
   came out the same value as a far deck, so the board read as one grey mass
   with the play surface hiding inside it. That is fatal here, because the
   one question the player asks every frame is "may I stand there".

   So each material gets its own band and the bands do not overlap. The
   DARKEST deck is still brighter than the BRIGHTEST bedrock, and the two
   ramps run in opposite temperatures. Depth then modulates freely inside a
   band without ever being able to change what a tile IS. Every vault
   repaints all four endpoints and this invariant is what they are chosen
   against.                                                                */
var ST = VAULTS[0];
function rgbs(c){ return 'rgb(' + (c[0]|0) + ',' + (c[1]|0) + ',' + (c[2]|0) + ')'; }
function mixc(a, b, t){
  return 'rgb(' + Math.round(a[0]+(b[0]-a[0])*t) + ',' + Math.round(a[1]+(b[1]-a[1])*t) + ',' + Math.round(a[2]+(b[2]-a[2])*t) + ')';
}
function tileFill(t, d, lightMul){
  var near = Math.max(0, Math.min(1, d / Math.max(1, N-1)));
  var lm = lightMul === undefined ? 1 : lightMul;
  var a = t === '+' ? ST.dF : ST.rF, b = t === '+' ? ST.dN : ST.rN;
  return mixc([a[0]*lm, a[1]*lm, a[2]*lm], [b[0]*lm, b[1]*lm, b[2]*lm], near);
}
function setStyle(band){
  ST = vaultStyle(band);
  var r = document.documentElement.style;
  r.setProperty('--void', rgbs(ST.vd));
  r.setProperty('--void-2', rgbs([ST.vd[0]+8, ST.vd[1]+7, ST.vd[2]+10]));
  r.setProperty('--void-3', rgbs([ST.vd[0]+17, ST.vd[1]+15, ST.vd[2]+22]));
  buildSky();
}

/* ---------- THE SKY -------------------------------------------------------
   The void columns are not decoration and they are not black by accident:
   they are the one place you are looking THROUGH the cube, and they should
   read as distance rather than as a hole in the picture. So there is a real
   starfield behind everything, painted once per vault and parallaxed off the
   view basis — turn the cube and the stars slide, which is the cheapest
   possible way of saying "the thing that moved was the world".              */
var skyCv = null, PAD = 26;
function buildSky(){
  if(!W || !H) return;
  skyCv = document.createElement('canvas');
  skyCv.width = Math.round((W + PAD*2) * DPR); skyCv.height = Math.round((H + PAD*2) * DPR);
  var c = skyCv.getContext('2d');
  c.setTransform(DPR, 0, 0, DPR, 0, 0);
  var rng = mulberry32(0x5EED ^ (curBand * 2654435761));
  var g = c.createRadialGradient(W/2+PAD, H*0.42+PAD, 0, W/2+PAD, H*0.42+PAD, Math.max(W,H)*0.72);
  g.addColorStop(0, 'rgba(' + (ST.at[0]|0) + ',' + (ST.at[1]|0) + ',' + (ST.at[2]|0) + ',0.13)');
  g.addColorStop(1, 'rgba(' + (ST.at[0]|0) + ',' + (ST.at[1]|0) + ',' + (ST.at[2]|0) + ',0)');
  c.fillStyle = g; c.fillRect(0, 0, W+PAD*2, H+PAD*2);
  for(var i = 0; i < 190; i++){
    var x = rng()*(W+PAD*2), y = rng()*(H+PAD*2);
    var r = rng()*rng()*1.7 + 0.32, a = 0.12 + rng()*rng()*0.7;
    c.globalAlpha = a;
    c.fillStyle = rgbs(ST.st);
    c.beginPath(); c.arc(x, y, r, 0, 6.284); c.fill();
    if(r > 1.3){ c.globalAlpha = a*0.28; c.beginPath(); c.arc(x, y, r*3.2, 0, 6.284); c.fill(); }
  }
}

/* ---------- GRAIN ---------------------------------------------------------
   One 64px noise tile, overlaid on the tiles only. Flat fills read as
   plastic; the same fills with a whisper of grain read as stone, and it
   costs one extra rect per tile.                                           */
var grainPat = null;
function buildGrain(){
  var c = document.createElement('canvas'); c.width = c.height = 64;
  var g = c.getContext('2d'), id = g.createImageData(64, 64), rng = mulberry32(11);
  for(var i = 0; i < 64*64; i++){
    var v = 128 + (rng()-0.5)*78;
    id.data[i*4] = id.data[i*4+1] = id.data[i*4+2] = v; id.data[i*4+3] = 255;
  }
  g.putImageData(id, 0, 0);
  grainPat = ctx.createPattern(c, 'repeat');
}

/* ---------- THE DROPS -----------------------------------------------------
   Where two neighbouring columns sit at different depths, the nearer one
   casts onto the farther one. This is the single most useful thing added to
   the resting board: it is honest (the light is at the camera, so that
   shadow is real), it turns a flat grid into a relief you can read at a
   glance, and it puts a visible price on exactly the adjacencies that are
   lying to you about distance.                                              */
var shGrad = null, shS = -1;
function buildDrops(){
  if(shS === S) return;
  shS = S; shGrad = [];
  var reach = S*0.42, defs = [[0,0,reach,0],[S,0,S-reach,0],[0,0,0,reach],[0,S,0,S-reach]];
  for(var i = 0; i < 4; i++){
    var d = defs[i], g = ctx.createLinearGradient(d[0], d[1], d[2], d[3]);
    /* Reaches 42% of the tile and no further, so the CENTRE of every tile
       always shows its true material undarkened. That is not a nicety: the
       whole board is read off "is this brighter than bedrock can be", and a
       shadow deep enough to push a deck under that line would be the
       renderer lying about the rules. */
    g.addColorStop(0, 'rgba(0,0,0,0.45)'); g.addColorStop(0.55, 'rgba(0,0,0,0.13)');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    shGrad.push(g);
  }
}

/* ---------- DUST + IMPACT -------------------------------------------------
   Forty motes so the void is not a still image, and one white flash on the
   cage when a turn lands so the detent you can hear has something to look
   at. Both are pure feel and both are nearly free.                          */
var motes = [], impact = 0;
function buildMotes(){
  motes = [];
  var rng = mulberry32(4242);
  for(var i = 0; i < 40; i++)
    motes.push({x:rng()*W, y:rng()*H, r:rng()*1.5+0.4, a:0.05+rng()*0.16,
                vx:(rng()-0.5)*5, vy:-2 - rng()*7});
}
function stepMotes(dt){
  var k = dt/1000;
  for(var i = 0; i < motes.length; i++){
    var m = motes[i];
    m.x += m.vx*k; m.y += m.vy*k;
    if(m.y < -8){ m.y = H + 6; m.x = Math.random()*W; }
    if(m.x < -8) m.x = W + 6; else if(m.x > W + 8) m.x = -6;
  }
  if(impact > 0) impact = Math.max(0, impact - dt/340);
}
function drawMotes(){
  ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.fillStyle = rgbs(ST.st);
  for(var i = 0; i < motes.length; i++){
    var m = motes[i];
    ctx.globalAlpha = m.a;
    ctx.beginPath(); ctx.arc(m.x, m.y, m.r, 0, 6.284); ctx.fill();
  }
  ctx.restore();
}
/* an additive halo, so the three things that mean something glow rather
   than merely being coloured differently */
function halo(x, y, r, col, a){
  var g = ctx.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, 'rgba(' + col + ',' + a + ')');
  g.addColorStop(0.5, 'rgba(' + col + ',' + (a*0.32).toFixed(3) + ')');
  g.addColorStop(1, 'rgba(' + col + ',0)');
  ctx.save(); ctx.globalCompositeOperation = 'lighter';
  ctx.fillStyle = g; ctx.fillRect(x-r, y-r, r*2, r*2); ctx.restore();
}

/* A FIXED LIGHT, NOT A FACING TERM.

   Shading purely by how squarely a face meets the camera looks correct and
   is useless: halfway through a yaw the front faces and the side faces are
   tilted by exactly the same 45 degrees, so they come out the same value and
   the turning solid reads as a flat smear of rectangles. (Which it
   geometrically IS — an orthographic yaw of axis-aligned boxes produces no
   diagonals at all. Every scrap of three-dimensionality here has to come
   from light.)

   So the light is a fixed direction in VIEW space, over the player's left
   shoulder. The one constraint is the seam: a face square to the camera must
   come out at exactly 1.0, because that is the case the flat renderer draws
   at rest, and any other value would make the picture jump the moment a drag
   begins. 0.30 + 0.70 is that seam, written so it cannot drift.            */
function LIT(nx, ny, nz){
  return Math.max(0, Math.min(1, 0.30 + 0.70*nz + 0.24*nx - 0.14*ny));
}

/* PERSPECTIVE, BUT ONLY WHILE IT IS TURNING.

   The renderer is orthographic because the resting board must be a true
   square grid — that is the surface the puzzle is read on and it cannot
   keystone. But an orthographic turn of a box that is axis-aligned produces
   nothing but rectangles: no diagonals, no convergence, no cube. Lighting
   alone could not sell it.

   So the projection grows a little perspective as the turn opens and gives
   it all back as the turn lands, scaled by |sin| of the angle. At rest the
   term is exactly zero and this is precisely the orthographic projection the
   flat renderer draws, which is the whole reason the hand-off between the
   two is invisible. In between, the near face is genuinely larger than the
   far one and the eye is finally looking at a solid.                       */
var PERSP = 0.062;
function persp(pv, vz){ return pv ? 1 / (1 - pv*vz) : 1; }

/* THE CAGE — the cube's twelve edges.
   At rest it is simply the board's border, because eleven of the twelve
   project onto the outline of the square. The instant a turn starts it
   opens out into a wireframe box, and that is what tells the eye that the
   flat grid it has been reading was a solid all along. Without it a turn
   reads as a smear of quads sliding over each other. */
var CAGE_E = [[0,1],[1,3],[3,2],[2,0],[4,5],[5,7],[7,6],[6,4],[0,4],[1,5],[2,6],[3,7]];
function drawCage(m, z, alpha, pv){
  var h = N/2, pt = [];
  for(var i = 0; i < 8; i++){
    var wx = (i&1?h:-h), wy = (i&2?h:-h), wz = (i&4?h:-h);
    var k = persp(pv, m.F[0]*wx + m.F[1]*wy + m.F[2]*wz);
    pt.push([CX + (m.R[0]*wx + m.R[1]*wy + m.R[2]*wz)*S*z*k,
             CY - (m.U[0]*wx + m.U[1]*wy + m.U[2]*wz)*S*z*k]);
  }
  ctx.save();
  ctx.strokeStyle = 'rgba(' + (ST.dN[0]|0) + ',' + (ST.dN[1]|0) + ',' + (ST.dN[2]|0) + ',' + (alpha + impact*0.5) + ')';
  ctx.lineWidth = 1.25 + impact*1.6; ctx.lineJoin = 'round';
  ctx.beginPath();
  for(var e = 0; e < 12; e++){
    ctx.moveTo(pt[CAGE_E[e][0]][0], pt[CAGE_E[e][0]][1]);
    ctx.lineTo(pt[CAGE_E[e][1]][0], pt[CAGE_E[e][1]][1]);
  }
  ctx.stroke(); ctx.restore();
}
function tileRect(u, v, z){
  z = z || 1;
  return [CX + (u - N/2)*S*z, CY + (N/2 - 1 - v)*S*z, S*z, S*z];
}

function draw(){
  ctx.clearRect(0, 0, W, H);
  if(!lv) return;
  /* the sky first, parallaxed off the view basis: turn the cube and the
     stars slide, which is the cheapest way to say the WORLD moved */
  if(skyCv){
    var m0 = liveBasis();
    ctx.drawImage(skyCv, -PAD - m0.F[0]*11 - m0.R[0]*4, -PAD + m0.U[1]*8 - m0.F[1]*9, W + PAD*2, H + PAD*2);
  }
  var a = liveAngle();
  if(a && Math.abs(a.ang) > 1e-4) draw3d();
  else drawFlat();
  drawMotes();
  if(impact > 0){
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    var r = Math.max(W, H)*0.62, g = ctx.createRadialGradient(CX, CY, N*S*0.30, CX, CY, r);
    g.addColorStop(0, 'rgba(' + (ST.dN[0]|0) + ',' + (ST.dN[1]|0) + ',' + (ST.dN[2]|0) + ',' + (impact*0.10).toFixed(3) + ')');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, W, H); ctx.restore();
  }
}

function drawFlat(){
  var u, v, s, r, i;
  buildDrops();
  drawCage(M, 1, 0.16, 0);
  /* 1. the tiles */
  for(u = 0; u < N; u++) for(v = 0; v < N; v++){
    s = surf[u*N + v]; if(!s) continue;
    r = tileRect(u, v);
    ctx.fillStyle = tileFill(s.t, s.d);
    ctx.fillRect(r[0], r[1], r[2] + 0.6, r[3] + 0.6);
    if(s.t === '+'){
      ctx.fillStyle = 'rgba(255,255,255,' + (0.05 + 0.09*(s.d/Math.max(1,N-1))).toFixed(3) + ')';
      ctx.fillRect(r[0] + S*0.10, r[1] + S*0.10, S*0.80, S*0.80);
    }
  }
  /* 1b. grain, on the tiles only, so stone reads as stone */
  if(grainPat){
    ctx.save(); ctx.globalCompositeOperation = 'overlay'; ctx.globalAlpha = 0.13;
    ctx.fillStyle = grainPat;
    for(u = 0; u < N; u++) for(v = 0; v < N; v++){
      if(!surf[u*N + v]) continue;
      r = tileRect(u, v);
      ctx.fillRect(r[0], r[1], r[2] + 0.6, r[3] + 0.6);
    }
    ctx.restore();
  }
  /* 2. THE DROPS. Where two neighbouring columns sit at different depths the
        nearer one casts onto the farther one, and a hairline marks the seam.
        The light is at the camera, so this shadow is not a flourish — it is
        the true one, and it is the game admitting in the open exactly which
        of its adjacencies are lying to you about distance. */
  ctx.lineCap = 'butt';
  var SD = [[1,0,0,1],[-1,0,1,0],[0,1,2,3],[0,-1,3,2]];   /* du,dv, gradIntoMe, gradIntoThem */
  for(u = 0; u < N; u++) for(v = 0; v < N; v++){
    s = surf[u*N + v]; if(!s) continue;
    r = tileRect(u, v);
    for(i = 0; i < 4; i++){
      var u2 = u + SD[i][0], v2 = v + SD[i][1];
      if(u2 < 0 || v2 < 0 || u2 >= N || v2 >= N) continue;
      var s2 = surf[u2*N + v2]; if(!s2) continue;
      var dd = s2.d - s.d;
      if(dd <= 0) continue;                                /* only a NEARER neighbour casts */
      ctx.save();
      ctx.translate(r[0], r[1]);
      ctx.globalAlpha = Math.min(MAX_SHADOW/0.45, 0.14 + dd*0.08);
      ctx.fillStyle = shGrad[SD[i][2]];
      ctx.fillRect(0, 0, S, S);
      ctx.restore();
    }
    var nb = [[1,0],[0,1]];
    for(i = 0; i < 2; i++){
      var u3 = u + nb[i][0], v3 = v + nb[i][1];
      if(u3 >= N || v3 >= N) continue;
      var s3 = surf[u3*N + v3]; if(!s3) continue;
      var ad = Math.abs(s3.d - s.d); if(!ad) continue;
      ctx.strokeStyle = 'rgba(0,0,0,' + Math.min(0.85, 0.26 + ad*0.15).toFixed(2) + ')';
      ctx.lineWidth = Math.min(3.4, 1 + ad*0.5);
      ctx.beginPath();
      if(i === 0){ ctx.moveTo(r[0]+S, r[1]); ctx.lineTo(r[0]+S, r[1]+S); }
      else       { ctx.moveTo(r[0], r[1]);   ctx.lineTo(r[0]+S, r[1]); }
      ctx.stroke();
    }
  }
  /* 3. THE REACHABLE SET. The connectivity graph, drawn. This is the single
        most useful thing on screen: turn the cube and watch the lit region
        change, and the mechanic has explained itself with no words. */
  ctx.strokeStyle = 'rgba(255,106,61,.42)'; ctx.lineWidth = Math.max(1.2, S*0.035);
  for(u = 0; u < N; u++) for(v = 0; v < N; v++){
    if(!reach[u*N + v]) continue;
    r = tileRect(u, v);
    ctx.strokeRect(r[0] + S*0.07, r[1] + S*0.07, S*0.86, S*0.86);
  }
  /* 4. depth numerals, for as long as someone wants the crutch */
  if(store.depth){
    ctx.font = '700 ' + Math.round(S*0.24) + 'px ui-monospace, monospace';
    ctx.textAlign = 'left'; ctx.textBaseline = 'top';
    for(u = 0; u < N; u++) for(v = 0; v < N; v++){
      s = surf[u*N + v]; if(!s) continue;
      r = tileRect(u, v);
      ctx.fillStyle = s.t === '+' ? 'rgba(10,9,16,.55)' : 'rgba(233,225,209,.42)';
      ctx.fillText(String(s.d), r[0] + S*0.10, r[1] + S*0.09);
    }
  }
  /* 5. things standing on the deck */
  for(i = 0; i < lv.doors.length; i++){
    var dv = surfaceAt(N, surf, M, lv.doors[i]); if(!dv) continue;
    drawDoor(tileRect(dv[0], dv[1]), !!(doors & (1<<i)));
  }
  for(i = 0; i < lv.keys.length; i++){
    if(kmask & (1<<i)) continue;
    var kv = surfaceAt(N, surf, M, lv.keys[i]); if(!kv) continue;
    drawKey(tileRect(kv[0], kv[1]));
  }
  var gv = surfaceAt(N, surf, M, lv.goal);
  if(gv) drawGoal(tileRect(gv[0], gv[1]));
  /* 6. you */
  var p = playerScreen();
  drawPlayer(p[0], p[1], S);
}

function playerScreen(){
  var v = viewOf(N, M, pos), r = tileRect(v[0], v[1]);
  var x = r[0] + S/2, y = r[1] + S/2;
  if(walking){
    var pv = viewOf(N, M, walking.from), pr = tileRect(pv[0], pv[1]);
    var t = walking.t;
    x = (pr[0] + S/2) + (x - pr[0] - S/2)*t;
    y = (pr[1] + S/2) + (y - pr[1] - S/2)*t;
  }
  return [x, y];
}

function drawPlayer(x, y, sz){
  var rr = sz*0.27;
  halo(x, y, sz*1.05, '255,106,61', 0.34);
  ctx.save();
  ctx.shadowColor = 'rgba(255,106,61,.85)'; ctx.shadowBlur = sz*0.5;
  ctx.fillStyle = '#ff6a3d';
  ctx.beginPath();
  ctx.moveTo(x, y - rr); ctx.lineTo(x + rr, y); ctx.lineTo(x, y + rr); ctx.lineTo(x - rr, y);
  ctx.closePath(); ctx.fill();
  ctx.restore();
  ctx.fillStyle = '#ffd9c8';
  ctx.beginPath(); ctx.arc(x, y, rr*0.32, 0, 6.284); ctx.fill();
}
function drawGoal(r){
  var x = r[0] + S/2, y = r[1] + S/2, t = (Date.now() % 2400) / 2400;
  halo(x, y, S*1.15, '53,215,161', 0.30);
  ctx.save();
  ctx.strokeStyle = '#35d7a1'; ctx.lineWidth = Math.max(1.6, S*0.06);
  ctx.shadowColor = 'rgba(53,215,161,.8)'; ctx.shadowBlur = S*0.45;
  ctx.beginPath(); ctx.arc(x, y, S*0.30, 0, 6.284); ctx.stroke();
  ctx.globalAlpha = 0.55 * (1 - t);
  ctx.beginPath(); ctx.arc(x, y, S*(0.16 + 0.24*t), 0, 6.284); ctx.stroke();
  ctx.restore();
}
function drawKey(r){
  var x = r[0] + S/2, y = r[1] + S/2;
  halo(x, y, S*0.85, '255,196,77', 0.26);
  ctx.save();
  ctx.fillStyle = '#ffc44d'; ctx.shadowColor = 'rgba(255,196,77,.8)'; ctx.shadowBlur = S*0.4;
  ctx.beginPath(); ctx.arc(x, y - S*0.09, S*0.115, 0, 6.284); ctx.fill();
  ctx.fillRect(x - S*0.035, y - S*0.02, S*0.07, S*0.24);
  ctx.fillRect(x - S*0.035, y + S*0.12, S*0.13, S*0.05);
  ctx.restore();
}
function drawDoor(r, open){
  ctx.save();
  ctx.globalAlpha = open ? 0.22 : 1;
  ctx.strokeStyle = '#35d7a1'; ctx.lineWidth = Math.max(1.4, S*0.055);
  for(var i = 0; i < 3; i++){
    var x = r[0] + S*(0.28 + i*0.22);
    ctx.beginPath(); ctx.moveTo(x, r[1] + S*0.17); ctx.lineTo(x, r[1] + S*0.83); ctx.stroke();
  }
  if(!open){
    ctx.lineWidth = Math.max(1.2, S*0.04);
    ctx.beginPath(); ctx.moveTo(r[0]+S*0.20, r[1]+S*0.5); ctx.lineTo(r[0]+S*0.80, r[1]+S*0.5); ctx.stroke();
  }
  ctx.restore();
}

/* ---------- the turning cube ---------- */
function draw3d(){
  var m = liveBasis(), z = liveZoom(), c = (N-1)/2, list = [], i;
  var pv = PERSP * Math.abs(Math.sin(liveAngle().ang));
  function px(wx, wy, wz, out){
    var qx = wx - c, qy = wy - c, qz = wz - c;
    out[2] = m.F[0]*qx + m.F[1]*qy + m.F[2]*qz;
    var k = persp(pv, out[2]);
    out[0] = CX + (m.R[0]*qx + m.R[1]*qy + m.R[2]*qz) * S * z * k;
    out[1] = CY - (m.U[0]*qx + m.U[1]*qy + m.U[2]*qz) * S * z * k;
  }
  var tmp = [0,0,0];
  for(i = 0; i < faces.length; i++){
    var fc = faces[i], d = FACE_D[fc.f];
    var nz = m.F[0]*d[0] + m.F[1]*d[1] + m.F[2]*d[2];
    if(nz <= 0.02) continue;                                  /* pointing away — never drawn */
    var nx = m.R[0]*d[0] + m.R[1]*d[1] + m.R[2]*d[2];
    var ny = m.U[0]*d[0] + m.U[1]*d[1] + m.U[2]*d[2];
    var q = FACE_Q[fc.f], pts = [], dep = 0;
    for(var k = 0; k < 4; k++){
      px(fc.x + q[k][0], fc.y + q[k][1], fc.z + q[k][2], tmp);
      pts.push(tmp[0], tmp[1]); dep += tmp[2];
    }
    px(fc.x, fc.y, fc.z, tmp);
    list.push({d:dep/4, k:0, pts:pts, t:fc.t, dep:tmp[2] + c, lit:LIT(nx, ny, nz)});
  }
  /* markers ride half a cell proud of their own face so they stay visible
     as the solid turns under them */
  function marker(w, kind, extra){
    px(w[0] + m.F[0]*0.62, w[1] + m.F[1]*0.62, w[2] + m.F[2]*0.62, tmp);
    list.push({d:tmp[2] + 0.62, k:kind, x:tmp[0], y:tmp[1], extra:extra});
  }
  for(i = 0; i < lv.doors.length; i++) marker(lv.doors[i], 3, !!(doors & (1<<i)));
  for(i = 0; i < lv.keys.length; i++) if(!(kmask & (1<<i))) marker(lv.keys[i], 2, 0);
  marker(lv.goal, 4, 0);
  marker(pos, 1, 0);

  list.sort(function(a, b){ return a.d - b.d; });
  var sz = S*z;
  drawCage(m, z, 0.46, pv);
  for(i = 0; i < list.length; i++){
    var e = list[i];
    if(e.k === 0){
      ctx.fillStyle = tileFill(e.t, e.dep, e.lit);
      ctx.beginPath();
      ctx.moveTo(e.pts[0], e.pts[1]); ctx.lineTo(e.pts[2], e.pts[3]);
      ctx.lineTo(e.pts[4], e.pts[5]); ctx.lineTo(e.pts[6], e.pts[7]);
      ctx.closePath(); ctx.fill();
      ctx.strokeStyle = 'rgba(10,9,16,.5)'; ctx.lineWidth = 0.7; ctx.stroke();
    } else if(e.k === 1) drawPlayer(e.x, e.y, sz);
    else if(e.k === 2) drawKey([e.x - sz/2, e.y - sz/2]);
    else if(e.k === 3) drawDoor([e.x - sz/2, e.y - sz/2], e.extra);
    else if(e.k === 4) drawGoal([e.x - sz/2, e.y - sz/2]);
  }
}

/* ============================================================
   MOVING
   ============================================================ */
function snapshot(){ return {pos:pos.slice(), ori:oriIndex(M), kmask:kmask, doors:doors, turns:turns}; }
function pushUndo(){ undoStack.push(snapshot()); if(undoStack.length > 200) undoStack.shift(); }
function restore(s){
  pos = s.pos.slice(); M = ORIS[s.ori]; kmask = s.kmask; doors = s.doors; turns = s.turns;
  walking = null; anim = null; drag = null; won = false; stuck = false;
  settle();
}

/* Pathing treats a shut door as a wall, EXCEPT as the final square: you open
   a door by walking into it deliberately, one at a time, never by having a
   route swallow two of them and your last key on the way past. */
function pathTo(tu, tv){
  var start = viewOf(N, M, pos), from = new Int32Array(N*N).fill(-1);
  var q = [start[0]*N + start[1]], seen = new Uint8Array(N*N);
  seen[q[0]] = 1;
  for(var i = 0; i < q.length; i++){
    var u = (q[i]/N)|0, v = q[i] % N;
    if(u === tu && v === tv) break;
    for(var t = 0; t < 4; t++){
      var u2 = u + TURNS[t].dx, v2 = v + TURNS[t].dy, k = u2*N + v2;
      if(u2 < 0 || v2 < 0 || u2 >= N || v2 >= N || seen[k]) continue;
      var cell = walkable(lv, surf, u2, v2, doors);
      if(!cell){
        if(!(u2 === tu && v2 === tv)) continue;
        var raw = surf[k];
        if(!raw || raw.t !== '+') continue;
        var di = doorIndexAt(lv, raw.w);
        if(di < 0 || (doors & (1<<di)) || keysHeld() < 1) continue;
      }
      seen[k] = 1; from[k] = q[i]; q.push(k);
    }
  }
  var end = tu*N + tv;
  if(!seen[end]) return null;
  var out = [], cur = end;
  while(cur !== start[0]*N + start[1]){ out.unshift(cur); cur = from[cur]; if(cur < 0) return null; }
  return out;
}

function tapCell(u, v){
  if(walking || anim || won) return;
  var s = surf[u*N + v];
  if(!s){ toast('nothing there'); sfx.deny(); return; }
  if(s.t !== '+'){ toast('bedrock — no footing on this face'); sfx.deny(); return; }
  var di = doorIndexAt(lv, s.w), shut = di >= 0 && !(doors & (1<<di));
  if(shut && keysHeld() < 1){ toast('locked — find a key'); sfx.deny(); buzz(14); return; }
  var path = pathTo(u, v);
  if(!path){ toast(shut ? 'no way to that door' : 'not connected — turn the cube'); sfx.deny(); return; }
  pushUndo();
  walking = {queue:path, from:pos.slice(), t:0, step:0};
}

function advanceWalk(dt){
  if(!walking) return;
  walking.t += dt / 105;
  if(walking.t < 1) return;
  var k = walking.queue.shift();
  var u = (k/N)|0, v = k % N, cell = surf[k];
  walking.from = pos.slice();
  pos = cell.w.slice();
  var di = doorIndexAt(lv, pos);
  if(di >= 0 && !(doors & (1<<di))){ doors |= (1<<di); sfx.door(); buzz(18); toast('unlocked'); }
  else sfx.step();
  var ki = keyIndexAt(lv, pos);
  if(ki >= 0 && !(kmask & (1<<ki))){ kmask |= (1<<ki); sfx.key(); buzz(12); toast('key'); }
  walking.t = 0;
  if(!walking.queue.length){ walking = null; settle(); afterAction(); }
  else { computeReach(); refreshHud(); }
}

function tryTurn(t){
  if(walking || anim || won) return false;
  var m2 = TURNS[t].f(M);
  if(!landing(lv, m2, pos, doors)) return false;
  var axis = (t === 0 || t === 1) ? 'x' : 'y';
  var to = (t === 1 || t === 2) ? Math.PI/2 : -Math.PI/2;
  var from = drag && drag.axis === axis ? drag.ang : 0;
  drag = null;
  pushUndo();
  anim = {axis:axis, ang:from, from:from, to:to, t:0,
          dur:180 + 150*(Math.abs(to - from)/(Math.PI/2)), turn:t};
  return true;
}
function springBack(){
  if(!drag || !drag.axis) { drag = null; return; }
  anim = {axis:drag.axis, ang:drag.ang, from:drag.ang, to:0, t:0, dur:190, turn:-1};
  drag = null;
}
function advanceAnim(dt){
  if(!anim) return;
  anim.t += dt;
  var p = Math.min(1, anim.t / anim.dur), e = 1 - Math.pow(1 - p, 3);
  anim.ang = anim.from + (anim.to - anim.from) * e;
  if(p < 1) return;
  var t = anim.turn; anim = null;
  if(t < 0) return;
  M = TURNS[t].f(M);
  var land = landing(lv, M, pos, doors);
  pos = land.w.slice();
  var ki = keyIndexAt(lv, pos);
  if(ki >= 0 && !(kmask & (1<<ki))){ kmask |= (1<<ki); sfx.key(); toast('key'); }
  turns++;
  impact = 1;
  sfx.turn(); buzz(11);
  settle(); afterAction();
}

/* ============================================================
   AFTER EVERY ACTION — win, and the thing a puzzle game owes you
   The solver runs from the CURRENT state, not the level's, so a cube you
   have made unwinnable says so at once instead of thirty moves later. It
   is deferred past the frame because on a slow phone the worst level costs
   about a tenth of a second, and that is a stutter if it lands mid-turn.
   ============================================================ */
var stuckT = 0;
function afterAction(){
  if(samePos(pos, lv.goal)){ finish(); return; }
  clearTimeout(stuckT);
  stuckT = setTimeout(function(){
    if(won || walking || anim) return;
    var r = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors});
    stuck = !r.ok;
    if(stuck){ toast('no way on from here — undo'); sfx.stuck(); buzz(30); }
  }, 60);
}
function finish(){
  won = true;
  var best = store.best[levelNo];
  if(best === undefined || turns < best){ store.best[levelNo] = turns; save(); }
  if(levelNo + 1 > store.reached){ store.reached = levelNo + 1; save(); }
  prebuild(levelNo + 1);
  sfx.win(); buzz(40);
  setTimeout(function(){
    $('winTurns').textContent = turns;
    $('winPar').textContent = lv.par;
    $('winBest').textContent = store.best[levelNo];
    var vd = $('winVerdict');
    vd.className = 'verdict ' + (turns <= lv.par ? 'par' : 'over');
    vd.textContent = turns <= lv.par ? 'FEWEST POSSIBLE' : (turns - lv.par) + ' OVER PAR';
    /* a vault boundary is worth marking — it is the only structure an
       endless game has, and the next ten cubes look different because of it */
    var crossing = vaultOf(levelNo + 1) !== vaultOf(levelNo);
    $('winVault').textContent = crossing ? 'VAULT ' + roman(vaultOf(levelNo+1)) + ' — ' + vaultName(vaultOf(levelNo+1)) : '';
    $('winVault').style.display = crossing ? '' : 'none';
    $('btnRetry').style.display = turns <= lv.par ? 'none' : '';
    show('scWin');
  }, 520);
}

/* ============================================================
   INPUT — one finger, two verbs, and the clash taken out.
   A gesture is a TAP until it has travelled far enough to be a drag, and
   once it is a drag it is locked to one axis for the rest of its life. No
   gesture ever changes its mind halfway, because a control that
   reinterprets itself under the thumb feels broken even when it is right.
   ============================================================ */
var TAP_SLOP = 14;
function pxPerQuarter(){ return Math.max(90, Math.min(W, H) * 0.40); }
function pointer(e){
  var t = e.touches ? e.touches[0] : e;
  return {x:t.clientX, y:t.clientY};
}
function onDown(e){
  if(won || anim || walking || !lv) return;
  if(!$('hud').classList.contains('on')) return;
  audio();
  var p = pointer(e);
  drag = {x0:p.x, y0:p.y, axis:null, ang:0};
}
function onMove(e){
  if(!drag) return;
  var p = pointer(e), dx = p.x - drag.x0, dy = p.y - drag.y0;
  if(!drag.axis){
    if(Math.abs(dx) < TAP_SLOP && Math.abs(dy) < TAP_SLOP) return;
    drag.axis = Math.abs(dx) > Math.abs(dy) ? 'x' : 'y';
  }
  if(e.cancelable) e.preventDefault();
  var amt = drag.axis === 'x' ? dx : -dy;
  var q = Math.max(-1, Math.min(1, amt / pxPerQuarter()));
  drag.ang = q * Math.PI/2;
  refreshTicks();
}
function onUp(e){
  if(!drag) return;
  if(!drag.axis){
    var p = {x:drag.x0, y:drag.y0};
    drag = null;
    var u = Math.round((p.x - CX)/S + N/2 - 0.5), v = Math.round((CY - p.y)/S + N/2 - 0.5);
    if(u >= 0 && v >= 0 && u < N && v < N) tapCell(u, v);
    return;
  }
  var ang = drag.ang, axis = drag.axis;
  if(Math.abs(ang) > Math.PI/4){
    var t = axis === 'x' ? (ang > 0 ? 1 : 0) : (ang > 0 ? 2 : 3);
    if(!tryTurn(t)){ toast('no footing that way'); sfx.deny(); buzz(16); springBack(); }
  } else springBack();
  refreshTicks();
}
cv.addEventListener('touchstart', onDown, {passive:true});
cv.addEventListener('touchmove', onMove, {passive:false});
cv.addEventListener('touchend', onUp);
cv.addEventListener('touchcancel', function(){ springBack(); });
cv.addEventListener('mousedown', onDown);
window.addEventListener('mousemove', onMove);
window.addEventListener('mouseup', onUp);
/* desktop, for anyone who opens this on one */
window.addEventListener('keydown', function(e){
  if(!lv || !$('hud').classList.contains('on')) return;
  var map = {ArrowLeft:0, ArrowRight:1, ArrowUp:2, ArrowDown:3};
  if(map[e.key] !== undefined){
    e.preventDefault();
    if(!tryTurn(map[e.key])){ toast('no footing that way'); sfx.deny(); }
  }
  else if(e.key === 'z') undo();
  else if(e.key === 'Escape') show('scPause');
});

/* ============================================================
   UI
   ============================================================ */
function $(id){ return document.getElementById(id); }
var toastT = 0;
function toast(msg){
  var el = $('toast');
  el.textContent = msg; el.classList.add('on');
  clearTimeout(toastT); toastT = setTimeout(function(){ el.classList.remove('on'); }, 1500);
}
function show(id){
  ['scTitle','scCubes','scManual','scPause','scWin'].forEach(function(s){
    $(s).classList.toggle('hide', s !== id);
  });
  $('hud').classList.toggle('on', !id);
}
var ROMAN = ['I','II','III','IV','V','VI','VII','VIII','IX','X','XI','XII'];
function roman(b){ return b < ROMAN.length ? ROMAN[b] : String(b+1); }
function refreshHud(){
  if(!lv) return;
  $('lvName').textContent = levelNo + ' · ' + lv.name;
  $('turnN').textContent = turns;
  $('parN').textContent = '/' + lv.par;
  $('turnPill').classList.toggle('over', turns > lv.par);
  var kp = $('keyPill'), held = keysHeld();
  kp.style.display = lv.keys.length ? '' : 'none';
  $('keyN').textContent = held;
  $('btnUndo').disabled = !undoStack.length;
  refreshTicks();
}
/* The ticks are the only answer to "may I turn?", so they are recomputed on
   every drag frame rather than only when something settles. */
function refreshTicks(){
  var ids = ['tkL','tkR','tkU','tkD'], order = [0,1,2,3];
  var live = liveAngle();
  for(var i = 0; i < 4; i++){
    var t = order[i], el = $(ids[i]);
    var can = lv && !won && !!landing(lv, TURNS[t].f(M), pos, doors);
    el.className = 'tick ' + (can ? 'ok' : 'no');
    if(live && Math.abs(live.ang) > 0.1){
      var act = live.axis === 'x' ? (live.ang > 0 ? 1 : 0) : (live.ang > 0 ? 2 : 3);
      if(act === t) el.style.opacity = '1';
      else el.style.opacity = '.3';
    } else el.style.opacity = '';
  }
}
function undo(){
  if(!undoStack.length || walking || anim) return;
  restore(undoStack.pop());
  sfx.step();
}
function hint(){
  if(!lv || won) return;
  var r = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors});
  if(!r.ok){ toast('no way on from here — undo'); sfx.stuck(); return; }
  if(!r.first){ toast('you are standing on it'); return; }
  if(r.first.kind === 'turn'){
    var names = ['drag LEFT', 'drag RIGHT', 'drag UP', 'drag DOWN'];
    toast(names[r.first.dir]);
  } else {
    toast(r.turns ? 'walk first — ' + r.turns + ' turn' + (r.turns>1?'s':'') + ' to go' : 'walk — no turns left to make');
  }
}

/* The cube list is a window on an endless shelf, so it pages by vault
   rather than trying to be a list of everything. Par is not shown for a
   cube you have not opened — it is a fact about a level that does not
   exist yet, and minting ten of them to fill in a grid would cost seconds. */
function buildCubeGrid(){
  var g = $('cubeGrid'); g.innerHTML = '';
  var st = vaultStyle(viewBand);
  $('vaultName').textContent = 'VAULT ' + roman(viewBand) + ' — ' + st.name;
  $('vaultSwatch').style.background = 'linear-gradient(135deg,' + rgbs(st.dN) + ' 0 50%,' + rgbs(st.rN) + ' 50% 100%)';
  $('vaultPrev').disabled = viewBand === 0;
  $('vaultNext').disabled = (viewBand+1)*10 + 1 > store.reached;
  for(var k = 0; k < 10; k++){
    var level = viewBand*10 + k + 1;
    var best = store.best[level], done = best !== undefined;
    var locked = level > store.reached;
    var par = level <= BAKED.length ? BAKED[level-1].par : (mintCache[level] ? mintCache[level].par : null);
    var marks = !done ? 0 : (par === null ? 1 : best <= par ? 3 : best <= par + 2 ? 2 : 1);
    var el = document.createElement('button');
    el.className = 'cube' + (done ? ' done' : '') + (marks === 3 ? ' perfect' : '') + (locked ? ' locked' : '');
    el.innerHTML = '<div class="idx">' + level + '</div><div class="nm">' + (locked ? '—' : levelName(level)) + '</div>' +
      '<div class="marks">' + [0,1,2].map(function(j){ return '<i class="' + (j < marks ? 'on' : '') + '"></i>'; }).join('') + '</div>';
    (function(L, lk){ el.onclick = function(){
      if(lk){ toast('clear the cubes before it'); return; }
      goLevel(L);
    }; })(level, locked);
    g.appendChild(el);
  }
}
function openCubes(){ viewBand = vaultOf(levelNo); buildCubeGrid(); show('scCubes'); }

/* ---------- bindings, each exactly once ---------- */
$('btnPlay').onclick = function(){
  audio();
  if(!store.taught){ store.taught = 1; save(); show('scManual'); return; }
  goLevel(firstUncleared());
};
function firstUncleared(){
  for(var i = 1; i <= store.reached; i++) if(store.best[i] === undefined) return i;
  return store.reached;
}
$('btnCubes').onclick = openCubes;
$('btnCubesBack').onclick = function(){ show('scTitle'); };
$('vaultPrev').onclick = function(){ if(viewBand > 0){ viewBand--; buildCubeGrid(); } };
$('vaultNext').onclick = function(){ if((viewBand+1)*10 + 1 <= store.reached){ viewBand++; buildCubeGrid(); } };
$('btnManual').onclick = function(){ show('scManual'); };
$('btnManual2').onclick = function(){ show('scManual'); };
$('btnManualBack').onclick = function(){
  if(!lv){ goLevel(firstUncleared()); } else show(null);
};
$('btnMenu').onclick = function(){
  $('pauseName').textContent = lv ? lv.name : 'PAUSED';
  $('pauseSub').textContent = lv ? ('Vault ' + roman(vaultOf(levelNo)) + ' · ' + vaultName(vaultOf(levelNo)) +
      ' — fewest possible: ' + lv.par + ' turn' + (lv.par===1?'':'s')) : '';
  show('scPause');
};
$('btnResume').onclick = function(){ show(null); };
$('btnRestart').onclick = function(){ loadLevel(levelNo); show(null); };
$('btnToCubes').onclick = openCubes;
$('btnUndo').onclick = undo;
$('btnHint').onclick = hint;
$('btnNext').onclick = function(){ goLevel(levelNo + 1); };
$('btnRetry').onclick = function(){ loadLevel(levelNo); show(null); };
$('btnWinCubes').onclick = openCubes;
function bindSwitch(id, key){
  var el = $(id);
  el.classList.toggle('on', !!store[key]);
  el.parentElement.onclick = function(){
    store[key] = store[key] ? 0 : 1;
    el.classList.toggle('on', !!store[key]);
    save();
    if(key === 'sound' && store.sound) audio();
  };
}
bindSwitch('swSound', 'sound');
bindSwitch('swHaptic', 'haptic');
bindSwitch('swDepth', 'depth');

/* ============================================================
   THE THREE HOOKS A NATIVE HOST NEEDS.
   A packaged WebView has no address bar, so the app owns the back gesture,
   the lifecycle, and — see the safe-area note in the stylesheet — the
   insets. All three are published on one object rather than scattered
   across globals the host has to guess at.
   ============================================================ */
window.TURNKEY = {
  /* true = handled, keep the Activity alive. false = let Android finish it. */
  onBack: function(){
    if(!$('scWin').classList.contains('hide')){ buildCubeGrid(); show('scCubes'); return true; }
    if(!$('scManual').classList.contains('hide')){ show(lv ? 'scPause' : 'scTitle'); return true; }
    if(!$('scPause').classList.contains('hide')){ show(null); return true; }
    if(!$('scCubes').classList.contains('hide')){
      if(viewBand > 0){ viewBand--; buildCubeGrid(); return true; }
      show('scTitle'); return true;
    }
    if($('hud').classList.contains('on')){ show('scPause'); return true; }
    return false;
  },
  pause: function(){ running = false; },
  resume: function(){ if(!running){ running = true; last = 0; requestAnimationFrame(loop); } },
  /* the host measures the real display cutout and hands it over */
  setInsets: function(t, r, b, l){
    var s = document.documentElement.style;
    s.setProperty('--sa-top', t + 'px'); s.setProperty('--sa-right', r + 'px');
    s.setProperty('--sa-bottom', b + 'px'); s.setProperty('--sa-left', l + 'px');
    fit();
  },
  version: '1.0'
};
document.addEventListener('visibilitychange', function(){
  if(document.hidden) window.TURNKEY.pause(); else window.TURNKEY.resume();
});
window.addEventListener('resize', fit);
window.addEventListener('orientationchange', function(){ setTimeout(fit, 120); });

/* ============================================================
   LOOP
   ============================================================ */
var running = true, last = 0;
function loop(ts){
  if(!running) return;
  var dt = last ? Math.min(64, ts - last) : 16;
  last = ts;
  advanceAnim(dt);
  advanceWalk(dt);
  stepMotes(dt);
  draw();
  requestAnimationFrame(loop);
}
buildGrain(); buildMotes(); setStyle(0);
fit();
show('scTitle');
$('btnPlay').textContent = store.reached ? 'CONTINUE' : 'BEGIN';
requestAnimationFrame(loop);
