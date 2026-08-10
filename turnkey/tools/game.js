/* ============================================================
   PERSISTENCE — one key, and it is allowed to fail.
   A WebView with storage disabled should still play; it just forgets.
   ============================================================ */
var store = {best:{}, reached:1, sound:1, haptic:1, depth:0, taught:0, sawPlate:0};
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
/* A real bus, not six loose oscillators.

   Everything lands on master -> compressor -> out, with a send to a cheap
   feedback-delay reverb, because the single biggest difference between "a
   game with sounds in it" and "a game that feels like a place" is that the
   sounds share a room. A dry click in silence reads as a UI beep; the same
   click with 180ms of dark tail reads as stone.                            */
var AC = null, BUS = null, WET = null, AMB = null, ambBand = -1;
function audio(){
  if(AC || !store.sound) return AC;
  try{ AC = new (window.AudioContext || window.webkitAudioContext)(); }catch(e){ return (AC = null); }
  BUS = AC.createGain(); BUS.gain.value = 0.9;
  var comp = AC.createDynamicsCompressor();
  comp.threshold.value = -14; comp.knee.value = 22; comp.ratio.value = 5;
  comp.attack.value = 0.004; comp.release.value = 0.16;
  BUS.connect(comp); comp.connect(AC.destination);
  /* the room: two delays at prime-ish spacings, damped, fed back a little */
  WET = AC.createGain(); WET.gain.value = 0.30;
  var d1 = AC.createDelay(1), d2 = AC.createDelay(1), fb = AC.createGain(), lp = AC.createBiquadFilter();
  d1.delayTime.value = 0.083; d2.delayTime.value = 0.127;
  fb.gain.value = 0.34; lp.type = 'lowpass'; lp.frequency.value = 1900;
  WET.connect(d1); d1.connect(lp); lp.connect(d2); d2.connect(fb); fb.connect(d1);
  d2.connect(BUS); lp.connect(BUS);
  return AC;
}
function out(node, send){
  node.connect(BUS);
  if(send && WET){ var g = AC.createGain(); g.gain.value = send; node.connect(g); g.connect(WET); }
}
function tone(f, dur, type, gain, slideTo, send){
  var a = audio(); if(!a || !store.sound) return;
  var o = a.createOscillator(), g = a.createGain(), t = a.currentTime;
  o.type = type || 'sine'; o.frequency.setValueAtTime(f, t);
  if(slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(gain || 0.12, t + 0.008);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g); out(g, send === undefined ? 0.18 : send);
  o.start(t); o.stop(t + dur + 0.02);
}
function noise(dur, cut, gain, send, sweepTo){
  var a = audio(); if(!a || !store.sound) return;
  var n = Math.max(1, Math.floor(a.sampleRate * dur)), b = a.createBuffer(1, n, a.sampleRate), d = b.getChannelData(0);
  for(var i = 0; i < n; i++) d[i] = (Math.random()*2 - 1) * (1 - i/n);
  var s = a.createBufferSource(); s.buffer = b;
  var f = a.createBiquadFilter(); f.type = 'lowpass';
  f.frequency.setValueAtTime(cut || 1400, a.currentTime);
  if(sweepTo) f.frequency.exponentialRampToValueAtTime(sweepTo, a.currentTime + dur);
  var g = a.createGain(); g.gain.value = gain || 0.16;
  s.connect(f); f.connect(g); out(g, send === undefined ? 0.22 : send);
  s.start();
}

/* THE AMBIENT BED. Two detuned saws through a heavy lowpass, pitched off the
   vault number, at a level you notice only when it stops. It is the cheapest
   atmosphere in the building and the reason a vault feels like somewhere
   rather than a recoloured grid. */
var VAULT_ROOT = [55, 49, 61.7, 46.2, 58.3, 65.4, 51.9, 43.7];
function ambience(band){
  var a = audio(); if(!a || !store.sound || band === ambBand) return;
  ambBand = band;
  if(AMB){ try{ AMB.g.gain.setTargetAtTime(0.0001, a.currentTime, 0.4); var old = AMB;
    setTimeout(function(){ try{ old.o1.stop(); old.o2.stop(); }catch(e){} }, 2200); }catch(e){} }
  var root = VAULT_ROOT[band % VAULT_ROOT.length] * (band > 15 ? 1.5 : 1);
  var o1 = a.createOscillator(), o2 = a.createOscillator(), g = a.createGain(), f = a.createBiquadFilter();
  o1.type = o2.type = 'sawtooth';
  o1.frequency.value = root; o2.frequency.value = root * 1.005;
  f.type = 'lowpass'; f.frequency.value = 210; f.Q.value = 0.7;
  g.gain.setValueAtTime(0.0001, a.currentTime);
  g.gain.setTargetAtTime(0.055, a.currentTime, 1.2);
  o1.connect(f); o2.connect(f); f.connect(g); out(g, 0.5);
  o1.start(); o2.start();
  AMB = {o1:o1, o2:o2, g:g};
}

/* Steps drift in pitch so a run of them sounds like walking rather than a
   stuck record — the cheapest possible fix for the most repeated sound. */
var stepN = 0;
var sfx = {
  step:  function(){
    stepN++;
    noise(0.05, 700 + (stepN % 3) * 190, 0.07, 0.10);
    tone(150 + (stepN % 4) * 11, 0.06, 'sine', 0.045, 0, 0.08);
  },
  land:  function(){ noise(0.09, 520, 0.10, 0.3); tone(78, 0.14, 'sine', 0.07, 58, 0.25); },
  /* the detent, in three layers: the latch, the stone it is set in, and the
     mass arriving underneath a beat late */
  turn:  function(){
    noise(0.035, 5200, 0.16, 0.12);
    noise(0.30, 2400, 0.10, 0.42, 380);
    tone(104, 0.30, 'triangle', 0.13, 52, 0.34);
    tone(41, 0.42, 'sine', 0.16, 33, 0.10);
  },
  creak: function(v){ noise(0.10, 320 + v*900, 0.020 + v*0.03, 0.3); },
  deny:  function(){ noise(0.08, 420, 0.16, 0.2); tone(68, 0.16, 'square', 0.07, 50, 0.1); },
  key:   function(){
    tone(1046, 0.10, 'triangle', 0.10, 0, 0.4);
    setTimeout(function(){ tone(1568, 0.20, 'triangle', 0.085, 0, 0.5); }, 62);
  },
  keyIn: function(){ tone(2093, 0.13, 'sine', 0.07, 0, 0.4); },
  door:  function(){
    noise(0.24, 900, 0.20, 0.4, 260); tone(126, 0.30, 'sawtooth', 0.07, 84, 0.3);
    setTimeout(function(){ noise(0.05, 3800, 0.11, 0.3); }, 190);
  },
  win:   function(){
    [523,659,784,1047,1319].forEach(function(f,i){
      setTimeout(function(){ tone(f, 0.65, 'triangle', 0.10, 0, 0.55); }, i*88);
    });
    tone(65, 0.9, 'sine', 0.15, 44, 0.3);
    noise(0.5, 2600, 0.09, 0.6, 300);
  },
  vault: function(){
    [196,262,330,392].forEach(function(f,i){ setTimeout(function(){ tone(f,1.5,'sine',0.09,0,0.7); }, i*130); });
    noise(1.1, 1500, 0.07, 0.8, 200);
  },
  /* the world turning over: a long filtered sweep in the direction of the
     change, a bell for the plate, and a sub under both */
  plate: function(bit){
    noise(0.85, 240, 0.20, 0.75, bit === 1 ? 3200 : 160);
    tone(bit === 1 ? 174 : 330, 0.8, 'sawtooth', 0.10, bit === 1 ? 520 : 116, 0.6);
    tone(55, 1.0, 'sine', 0.17, 40, 0.25);
    setTimeout(function(){ tone(bit === 1 ? 1046 : 784, 0.5, 'triangle', 0.09, 0, 0.7); }, 120);
  },
  undo:  function(){ tone(420, 0.16, 'sine', 0.06, 700, 0.3); noise(0.07, 1800, 0.05, 0.2); },
  stuck: function(){ tone(200, 0.34, 'sine', 0.07, 118, 0.4); }
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
var lv = null, N = 0, M = ORI_ID, pos = [0,0,0], kmask = 0, doors = 0, turns = 0, world = 0;
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

/* visualViewport is the honest number on a phone. window.innerHeight on iOS
   Safari includes the strip the URL bar is sitting on, so the board was being
   sized against space the player cannot actually see or touch. */
function vpSize(){
  var vv = window.visualViewport;
  if(vv && vv.width > 0) return {w:Math.round(vv.width), h:Math.round(vv.height)};
  return {w:window.innerWidth, h:window.innerHeight};
}
function fit(){
  DPR = Math.min(window.devicePixelRatio || 1, 2.5);
  var vp = vpSize(); W = vp.w; H = vp.h;
  cv.width = Math.round(W*DPR); cv.height = Math.round(H*DPR);
  cv.style.width = W + 'px'; cv.style.height = H + 'px';
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  layout(); buildSky(); buildMotes(); shS = -1;
}
var S = 40, CX = 0, CY = 0;
/* LAYOUT — MOBILE FIRST, AND THE BOARD ALWAYS FITS.

   The board is square. A phone is not. So the width always binds and the
   height always has slack, and the job of this function is to spend the
   width completely and put the slack where a thumb wants it.

   The previous version sized the attract cube at 1.12x of the smaller side,
   which put 23-26 PIXELS OF BOARD OFF BOTH SCREEN EDGES on every phone
   tested — and a tile you cannot see is a tile you cannot tap. Whatever else
   changes here, `side` is clamped to both axes and that cannot regress
   without the viewport suite failing.

   The slack is then biased upward, so the gap under the board is larger than
   the gap over it. That is not symmetry for its own sake: the bottom of a
   phone is where the thumb lives and where the controls are, and a board
   sitting dead-centre crowds them. */
function layout(){
  if(!N) return;
  var ins = insets();
  /* On the title screen the chrome owns a band at each end and the cube owns
     the middle, so it is sized and centred against THAT band rather than the
     whole plate — which is what stops the hero sitting under the buttons. */
  var topBar = ins.t + (demo ? Math.round(H*0.26) : 54);
  var botBar = ins.b + (demo ? Math.round(H*0.34) : 112);
  var availW = W - ins.l - ins.r - (demo ? 8 : 12);
  var availH = H - topBar - botBar;
  var side = Math.max(60, Math.min(availW, demo ? availH*1.45 : availH));
  S = side / N;
  CX = ins.l + (W - ins.l - ins.r)/2;
  CY = topBar + availH/2 - (demo ? 0 : Math.min(30, (availH - side)*0.14));
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
  if(demo && Math.abs(demoAng) > 1e-4) return {axis:demoAxis, ang:demoAng};
  return null;
}
/* THE HERO POSE.

   In play the camera is square to a face, because that is the surface the
   puzzle is read on and it must not keystone. The attract screen has no such
   duty: it should show the thing as an OBJECT, so it gets a real
   three-quarter view — yaw turning forever, pitch parked where the top face
   reads — and a fitted zoom rather than the play view's fixed one, because a
   cube rotated about two axes at once sweeps a much bigger silhouette than
   one rotated about a single axis.                                          */
var HERO_PITCH = 0.50;
function heroBasis(){
  return pitchBasis(yawBasis(demoM, demoAng), HERO_PITCH);
}
/* Fit by measuring, not by trigonometry: project the eight corners and scale
   to whatever box they actually came out as. Works for any pose, and cannot
   drift out of step with the projection the way a closed form would. */
function fitScale(m, boxW, boxH){
  var h = N/2, mx = 0, my = 0;
  for(var i = 0; i < 8; i++){
    var x = (i&1?h:-h), y = (i&2?h:-h), z = (i&4?h:-h);
    mx = Math.max(mx, Math.abs(m.R[0]*x + m.R[1]*y + m.R[2]*z));
    my = Math.max(my, Math.abs(m.U[0]*x + m.U[1]*y + m.U[2]*z));
  }
  return Math.min(boxW / (2*mx*S), boxH / (2*my*S));
}
function baseBasis(){ return demo ? demoM : M; }
function curSurf(){ return demo ? (demoSurf || surf) : surf; }
function liveBasis(){
  if(demo) return heroBasis();
  var a = liveAngle(), B = baseBasis();
  if(!a || Math.abs(a.ang) < 1e-4) return B;
  return a.axis === 'x' ? yawBasis(B, a.ang) : pitchBasis(B, a.ang);
}
/* A square rotating about a screen axis projects (cos+sin) times as wide, so
   pulling the camera back by exactly that keeps the cube inside the frame
   through the whole turn instead of clipping at 45 degrees. */
function liveZoom(){
  /* the plinth sticks out past the cube and perspective enlarges whatever is
     nearest, so the fit box is the cube's footprint less a margin for both */
  if(demo) return fitScale(heroBasis(), N*S*0.74, N*S*0.74);
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
  M = ORI_ID; pos = lv.start.slice(); kmask = 0; doors = 0; turns = 0; world = 0;
  clearEff(lv); flip = null;
  var k0 = keyIndexAt(lv, pos); if(k0 >= 0) kmask |= (1<<k0);
  undoStack = []; walking = null; anim = null; drag = null; won = false; stuck = false;
  var band = vaultOf(levelNo);
  if(band !== curBand || !skyCv){ curBand = band; setStyle(band); }
  buildFaces();
  layout(); settle();
  parts = []; flyers = []; exitFlash = 0; exitPull = -1; kickV.t = 1;
  var sv = viewOf(N, M, pos);
  startWave('in', sv[0], sv[1], 380);
  revealT = -120;
  ambience(band);
  if(levelNo > store.reached){ store.reached = levelNo; save(); }
  /* TEACH IT WHEN IT SHOWS UP, ONCE. Not in the manual four screens earlier,
     not as a toast that has gone before it is read — on the cube that has one,
     with the thing itself waiting behind the card. */
  if(!store.sawPlate && (lv.vox.indexOf('A') >= 0 || lv.vox.indexOf('B') >= 0)){
    store.sawPlate = 1; save();
    setTimeout(function(){ if(!won && !walking && !anim) show('scPlate'); }, 260);
  }
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
  surf = project(N, effVox(lv, world), M);
  computeReach();
  refreshHud();
}
function computeReach(){
  reach = new Uint8Array(N*N);
  reachDist = new Uint8Array(N*N);
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
      reach[k] = 1; reachDist[k] = Math.min(255, reachDist[q[i]] + 1); q.push(k);
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
/* Quarried stone, not slate — and lighter than any vault plays in, because
   the hero is lit from behind and every side face is already losing most of
   its value to the shading term before the depth fade touches it. */
var HERO_STYLE = enforceBands({name:'HERO',
  dF:[150,126,96], dN:[250,241,222], rF:[46,39,38], rN:[116,104,100],
  vd:[10,8,13], st:[255,198,150], at:[255,150,60]});
function setStyle(band){
  ST = demo ? HERO_STYLE : vaultStyle(band);
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

/* ============================================================
   JUICE — what each moment is supposed to feel like

   Every beat in this game is one of five things, and each gets built toward
   a specific sensation rather than "add particles":

     a STEP    purposeful, weighted. Not a cursor moving.
     a DRAG    heavy. You are turning a stone vault, and it resists.
     a LANDING a mechanism seating. The single most important feel in the
               game, because it is the verb — so it overshoots, kicks the
               camera along its own axis, shakes dust off the whole cube, and
               sweeps the newly-reachable set alight.
     a REFUSAL immediate and physical. The cube tries and slams back.
     the EXIT  earned. The board comes apart toward you.

   Nothing here is allowed to make the player wait. Every transition is
   under 420ms and none of them gate input that could have been taken.
   ============================================================ */

/* ============================================================
   THE WORDMARK — built out of the same thing the game is built out of

   The title was type in a system font with letter-spacing on it, which is a
   fine way to label a screen and a poor way to open a game about stone
   cubes. It is now cut from blocks: a 5x7 bitmap per letter, every lit cell
   extruded toward the viewer with a lit top face, a shaded right face and a
   bevel, so the word is made of the same material as the thing behind it.

   Drawn once into an offscreen canvas at boot and handed to an <img>, so it
   costs one canvas and zero frames — and it lives in the DOM, which means it
   scales with the layout and sits above the glass instead of behind it.
   ============================================================ */
var GLYPH6 = {
  T:['111111','111111','001100','001100','001100','001100','001100'],
  U:['110011','110011','110011','110011','110011','111111','011110'],
  R:['111110','110011','110011','111110','110110','110011','110011'],
  N:['110011','111011','111011','110111','110111','110011','110011'],
  K:['110011','110110','111100','111000','111100','110110','110011'],
  E:['111111','110000','110000','111110','110000','110000','111111'],
  Y:['110011','110011','011110','001100','001100','001100','001100']
};
function buildWordmark(word, cell){
  var cols = [], i, j;
  for(i = 0; i < word.length; i++){
    var g = GLYPH6[word[i]];
    for(j = 0; j < 6; j++){
      var col = [];
      for(var r = 0; r < 7; r++) col.push(g[r][j] === '1');
      cols.push(col);
    }
    if(i < word.length-1) cols.push(null);
  }
  var W0 = cols.length, H0 = 7;
  var dep = Math.round(cell*0.55), pad = Math.round(cell*1.2);
  var cv = document.createElement('canvas');
  cv.width  = W0*cell + dep + pad*2;
  cv.height = H0*cell + dep + pad*2;
  var c = cv.getContext('2d');
  function on(x,y){ return x>=0 && y>=0 && x<W0 && y<H0 && cols[x] && cols[x][y]; }
  function eachCell(fn){
    for(var y=0;y<H0;y++) for(var x=0;x<W0;x++) if(on(x,y)) fn(x,y, pad + x*cell, pad + y*cell);
  }
  /* THE EXTRUSION, the blunt way and therefore the right way: stamp the whole
     silhouette once per pixel of depth, back to front. Building it face by
     face needs the neighbour logic to be perfect on every edge and diagonal,
     and when it is not the letters come out looking smeared rather than
     solid. Nine stamps of a flat shape cannot have a seam. */
  for(var d = dep; d >= 1; d--){
    var k = d/dep;
    c.fillStyle = 'rgb(' + Math.round(52+34*(1-k)) + ',' + Math.round(46+30*(1-k)) + ',' + Math.round(38+26*(1-k)) + ')';
    eachCell(function(x,y,px,py){ c.fillRect(px+d, py+d, cell, cell); });
  }
  /* the front plate, one block at a time, each with its own bevel so the
     wordmark is made of the same masonry as the cube */
  eachCell(function(x,y,px,py){
    var g2 = c.createLinearGradient(px, py, px, py+cell);
    g2.addColorStop(0, '#fdf9ef'); g2.addColorStop(1, '#d9cfb8');
    c.fillStyle = g2; c.fillRect(px, py, cell, cell);
    var lw = Math.max(1.5, cell*0.09);
    c.lineWidth = lw;
    c.strokeStyle = 'rgba(255,255,255,.75)';
    c.beginPath(); c.moveTo(px+lw/2, py+cell); c.lineTo(px+lw/2, py+lw/2); c.lineTo(px+cell, py+lw/2); c.stroke();
    c.strokeStyle = 'rgba(70,56,38,.34)';
    c.beginPath(); c.moveTo(px+cell-lw/2, py); c.lineTo(px+cell-lw/2, py+cell-lw/2); c.lineTo(px, py+cell-lw/2); c.stroke();
    /* mortar: a hairline around every block, so 5000 of them still read as
       individual stones at thumbnail size */
    c.strokeStyle = 'rgba(40,32,22,.22)'; c.lineWidth = 1;
    c.strokeRect(px+.5, py+.5, cell-1, cell-1);
  });
  /* one lit stone, in the U, because a wordmark cut from rock should have
     something alive in it — the same amber as the light inside the vault */
  var lx = pad + 7*cell + cell*0.5, ly = pad + 1*cell + cell*0.5;
  var lg = c.createRadialGradient(lx, ly, 0, lx, ly, cell*1.25);
  lg.addColorStop(0, 'rgba(255,178,80,.95)'); lg.addColorStop(1, 'rgba(255,150,60,0)');
  c.fillStyle = lg; c.fillRect(lx-cell*1.25, ly-cell*1.25, cell*2.5, cell*2.5);
  var ig = c.createLinearGradient(lx-cell*0.5, ly-cell*0.5, lx+cell*0.5, ly+cell*0.5);
  ig.addColorStop(0, '#ffd79a'); ig.addColorStop(1, '#ff9a2e');
  c.fillStyle = ig; c.fillRect(lx-cell*0.5, ly-cell*0.5, cell, cell);
  return cv;
}

/* ---------- THE ATTRACT CUBE ----------------------------------------------
   The title screen used to be type on a gradient. It is now type over the
   actual game engine, tumbling a real cube slowly and forever behind the
   glass. It costs nothing — the renderer already exists and the menus were
   drawing nothing at all — and it means the first thing anyone sees is the
   one idea the game has. */
/* IT GETS ITS OWN BASIS, AND THAT IS NOT A STYLE CHOICE.

   The attract cube used to tumble by rotating M — the real game basis — while
   `pos` stayed put at the level's opening cell. Leaving the title screen then
   dropped you into a live level standing INSIDE THE ROCK: your cell was no
   longer the surface of its column, so the lit reachable set was stale, turn
   legality was computed from a position that did not exist, and the cube was
   unwinnable. Every first-time player hit it, because BEGIN -> GOT IT was the
   only way in.

   So the demo now owns demoM and demoSurf and cannot reach the rules at all.
   The renderer reads whichever is live; nothing else ever sees demoM.        */
var demo = false, demoM = ORI_ID, demoSurf = null;
var demoAxis = 'x', demoAng = 0, demoDir = 1, demoWait = 0, demoT = 0;
function demoStart(){
  demoM = ORI_ID; demoAng = 0; demoWait = 260; demoT = 0;
  demoAxis = 'x'; demoDir = 1;
  if(lv) demoSurf = project(N, lv.vox, demoM);
}
function stepDemo(dt){
  if(!demo || !lv) return;
  demoT += dt;
  /* it never rests square to the camera. A cube parked flat is a grid, and
     the one thing the title screen has to say is "this is a solid". */
  /* one direction, forever, slowly — an object on a turntable rather than a
     puzzle being solved */
  demoAng += dt / 9000 * Math.PI/2;
  if(Math.abs(demoAng) >= Math.PI/2){
    demoM = TURNS[demoAxis === 'x' ? (demoDir > 0 ? 1 : 0) : (demoDir > 0 ? 2 : 3)].f(demoM);
    demoAng = 0; demoWait = 420;
    demoAxis = Math.random() < 0.62 ? 'x' : 'y';
    demoDir = Math.random() < 0.5 ? 1 : -1;
    demoSurf = project(N, lv.vox, demoM);
  }
}

/* ---------- CAMERA KICK ---------------------------------------------------
   Directional, not random: a turn about the screen-up axis kicks sideways,
   a turn about screen-right kicks vertically, so the shake tells you which
   way the mass went. Decays as a damped sine, which reads as a mechanism
   settling rather than an earthquake. Only the BOARD moves — the HUD is DOM
   and stays nailed down, which is what keeps it legible mid-impact. */
var kickV = {x:0, y:0, t:1, dur:1, mag:0};
function kick(mag, dx, dy){
  kickV.mag = mag; kickV.x = dx; kickV.y = dy; kickV.t = 0; kickV.dur = 380;
}
function kickOffset(){
  if(kickV.t >= 1) return null;
  var p = kickV.t, damp = Math.pow(1 - p, 2.2), w = Math.sin(p * Math.PI * 5.5) * damp;
  return [kickV.x * kickV.mag * w, kickV.y * kickV.mag * w];
}

/* ---------- PARTICLES -----------------------------------------------------
   One pool, additive, capped. Dust off a footfall, grit shaken loose by a
   turn, sparks off a latch. They exist to make the board feel like it has
   mass and dirt on it, so they are always small, always short, and never
   drawn over the play surface densely enough to hide a tile. */
var parts = [], PART_CAP = 220;
function burst(x, y, n, o){
  o = o || {};
  for(var i = 0; i < n && parts.length < PART_CAP; i++){
    var a = o.ang === undefined ? Math.random()*6.284 : o.ang + (Math.random()-0.5)*(o.spread||6.284);
    var sp = (o.spd || 40) * (0.35 + Math.random()*0.9);
    parts.push({x:x, y:y, vx:Math.cos(a)*sp, vy:Math.sin(a)*sp - (o.lift||0),
                life:0, max:(o.life||500)*(0.6+Math.random()*0.7),
                r:(o.r||2)*(0.5+Math.random()), col:o.col || '233,225,209',
                g:(o.g === undefined ? 42 : o.g), fade:o.fade||1});
  }
}
function stepParts(dt){
  var k = dt/1000;
  for(var i = parts.length-1; i >= 0; i--){
    var p = parts[i];
    p.life += dt;
    if(p.life >= p.max){ parts.splice(i,1); continue; }
    p.x += p.vx*k; p.y += p.vy*k; p.vy += p.g*k; p.vx *= 0.985; p.vy *= 0.985;
  }
}
function drawParts(){
  if(!parts.length) return;
  ctx.save(); ctx.globalCompositeOperation = 'lighter';
  for(var i = 0; i < parts.length; i++){
    var p = parts[i], t = 1 - p.life/p.max;
    ctx.globalAlpha = Math.max(0, t*t*p.fade);
    ctx.fillStyle = 'rgb(' + p.col + ')';
    ctx.beginPath(); ctx.arc(p.x, p.y, p.r*(0.4+t*0.6), 0, 6.284); ctx.fill();
  }
  ctx.restore();
}

/* ---------- THE TILE WAVE -------------------------------------------------
   One mechanism serving both ends of a level. Entering, the board assembles
   outward from where you are standing; clearing it, the board comes apart
   outward from the door you just walked through. Same code, sign flipped —
   which is why arriving and leaving feel like the same place doing the same
   thing in opposite directions. */
var wave = null;
function startWave(mode, fu, fv, dur){
  wave = {mode:mode, fu:fu, fv:fv, t:0, dur:dur || 430, stagger:34, rise:170};
}
function tilePhase(u, v){
  if(!wave) return 1;
  var d = Math.sqrt((u-wave.fu)*(u-wave.fu) + (v-wave.fv)*(v-wave.fv));
  var t = (wave.t - d*wave.stagger) / wave.rise;
  t = Math.max(0, Math.min(1, t));
  t = t*t*(3-2*t);
  return wave.mode === 'in' ? t : 1 - t;
}
function stepWave(dt){
  if(!wave) return;
  wave.t += dt;
  if(wave.t > wave.dur + N*wave.stagger + wave.rise) wave = null;
}

/* ---------- THE KEY GOES SOMEWHERE ----------------------------------------
   A key that vanishes on pickup is a number changing. A key that flies to
   the counter and makes it flinch is an object being put in a pocket, and
   the player never has to look for where the count lives again. */
var flyers = [];
function flyKey(){
  var pv = viewOf(N, M, pos), pr = tileRect(pv[0], pv[1]);
  var pill = $('keyPill'), r = pill.getBoundingClientRect();
  flyers.push({x0:pr[0]+S/2, y0:pr[1]+S/2, x1:r.left+r.width*0.32, y1:r.top+r.height/2, t:0, dur:380});
}
function stepFlyers(dt){
  for(var i = flyers.length-1; i >= 0; i--){
    var f = flyers[i];
    f.t += dt;
    if(f.t >= f.dur){
      flyers.splice(i,1);
      var p = $('keyPill'); p.classList.remove('pop'); void p.offsetWidth; p.classList.add('pop');
      sfx.keyIn();
      var r = p.getBoundingClientRect();
      burst(r.left+r.width*0.32, r.top+r.height/2, 10, {spd:70, life:380, r:1.8, col:'255,196,77', g:0});
    }
  }
}
function drawFlyers(){
  for(var i = 0; i < flyers.length; i++){
    var f = flyers[i], p = f.t/f.dur, e = p*p*(3-2*p);
    var x = f.x0 + (f.x1-f.x0)*e, y = f.y0 + (f.y1-f.y0)*e - Math.sin(p*Math.PI)*70;
    halo(x, y, 24, '255,196,77', 0.5*(1-p*0.5));
    ctx.save(); ctx.fillStyle = '#ffc44d';
    ctx.beginPath(); ctx.arc(x, y, 5*(1-p*0.35), 0, 6.284); ctx.fill(); ctx.restore();
  }
}

/* ---------- THE FLIP ------------------------------------------------------
   The plate is the only thing in the game that changes the BOARD rather than
   your view of it, so it cannot be a state swap and a toast. The old
   material has to be seen leaving.

   So the change propagates outward from the plate at a fixed speed, and each
   tile crosses over as the front reaches it: the old material shrinking out,
   the new one growing in behind. For a second and a half you are looking at
   two worlds at once with a lit seam running between them, and the seam
   starts under your own feet. It reuses the level-entry wave's geometry
   wholesale — same distance-staggered phase, different payload.            */
var PLATE_COL = {1:'168,107,255', 2:'111,214,255'};
var PLATE_NAME = {1:'INVERT', 2:'DRAIN'};
var flip = null;
function firePlate(bit, cx, cy){
  var v = viewOf(N, M, pos);
  var old = surf;
  world ^= bit;
  clearEff(lv);
  surf = project(N, effVox(lv, world), M);
  flip = {t:0, fu:v[0], fv:v[1], old:old, col:PLATE_COL[bit], dur:520, stagger:30, rise:150};
  computeReach(); refreshHud();
  revealT = -260;                       /* the reachable sweep follows the front */
  kick(13, 0, 1);
  impact = 1;
  sfx.plate(bit); buzz(34);
  burst(cx, cy, 26, {spd:150, life:760, r:2.6, col:PLATE_COL[bit], g:0});
  burst(cx, cy, 12, {spd:52, life:1000, r:3.6, col:'240,240,255', g:-16});
  toast(world ? (PLATE_NAME[bit] + ' — the cube is a different place') : 'restored');
}
function stepFlip(dt){
  if(!flip) return;
  flip.t += dt;
  if(flip.t > flip.dur + N*flip.stagger + flip.rise) flip = null;
}
function flipPhase(u, v){
  if(!flip) return 1;
  var d = Math.sqrt((u-flip.fu)*(u-flip.fu) + (v-flip.fv)*(v-flip.fv));
  var t = (flip.t - d*flip.stagger) / flip.rise;
  t = Math.max(0, Math.min(1, t));
  return t*t*(3-2*t);
}

/* ---------- THE REVEAL ----------------------------------------------------
   After every turn the lit reachable set sweeps outward from the player in
   BFS order rather than snapping on. It is the prettiest thing on screen and
   it is also the most useful: the sweep IS the connectivity graph being
   traced, so the player watches the answer to "what did that turn buy me"
   get drawn one tile at a time. Juice and teaching, same effect. */
var revealT = 999, reachDist = null;

/* ---------- DUST + IMPACT -------------------------------------------------
   Forty motes so the void is not a still image, and one white flash on the
   cage when a turn lands so the detent you can hear has something to look
   at. Both are pure feel and both are nearly free.                          */
var motes = [], impact = 0, exitFlash = 0, exitPull = -1;
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
/* The drifting specks are little CUBES now. In a game where everything is
   made of blocks, round motes were the one thing on screen that was not. */
function drawMotes(){
  ctx.save();
  for(var i = 0; i < motes.length; i++){
    var m = motes[i], r = m.r*2.6, hx = r, hy = r*0.56;
    ctx.globalAlpha = m.a*1.5;
    ctx.fillStyle = mixc(ST.vd, ST.st, 0.55);                    /* top */
    ctx.beginPath();
    ctx.moveTo(m.x, m.y-hy); ctx.lineTo(m.x+hx, m.y); ctx.lineTo(m.x, m.y+hy); ctx.lineTo(m.x-hx, m.y);
    ctx.closePath(); ctx.fill();
    ctx.globalAlpha = m.a*1.1;
    ctx.fillStyle = mixc(ST.vd, ST.st, 0.30);                    /* left */
    ctx.beginPath();
    ctx.moveTo(m.x-hx, m.y); ctx.lineTo(m.x, m.y+hy); ctx.lineTo(m.x, m.y+hy+r*0.8); ctx.lineTo(m.x-hx, m.y+r*0.8);
    ctx.closePath(); ctx.fill();
    ctx.globalAlpha = m.a*0.8;
    ctx.fillStyle = mixc(ST.vd, ST.st, 0.16);                    /* right */
    ctx.beginPath();
    ctx.moveTo(m.x+hx, m.y); ctx.lineTo(m.x, m.y+hy); ctx.lineTo(m.x, m.y+hy+r*0.8); ctx.lineTo(m.x+hx, m.y+r*0.8);
    ctx.closePath(); ctx.fill();
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
function LIT(nx, ny, nz, ambient){
  var a = ambient === undefined ? 0.30 : ambient;
  return Math.max(0, Math.min(1, a + (1-a)*nz + 0.24*nx - 0.14*ny));
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
  /* the kick moves the BOARD, never the HUD — a shaken readout is an
     unreadable one, and the DOM chrome staying nailed down is what lets the
     impact be as big as it is */
  var ko = kickOffset();
  ctx.save();
  if(ko) ctx.translate(ko[0], ko[1]);
  /* THE LIGHT INSIDE — and ONLY on the attract screen.
     Drawn before the cube and never masked, so the only places it survives
     are the void columns: the gaps you are looking straight through. The cube
     appears lit from within and it costs one gradient, because the geometry
     does the masking for free.

     It was briefly on the play board too, and that was a mistake worth
     recording. An additive layer under the tiles lifts BEDROCK as much as it
     lifts deck, which eats the luminance separation the whole board is read
     off — and the palette test could not see it, because it measures
     tileFill and this was painted underneath. Atmosphere does not get to
     cost legibility on the surface the puzzle is solved on. */
  if(demo){
    var vg = ctx.createRadialGradient(CX, CY, 0, CX, CY, N*S*0.44);
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    vg.addColorStop(0, 'rgba(255,150,60,0.40)');
    vg.addColorStop(0.55, 'rgba(255,150,60,0.15)');
    vg.addColorStop(1, 'rgba(255,150,60,0)');
    ctx.fillStyle = vg; ctx.fillRect(CX - N*S*0.8, CY - N*S*0.8, N*S*1.6, N*S*1.6);
    ctx.restore();
  }
  var a = liveAngle();
  if(demo || (a && Math.abs(a.ang) > 1e-4)) draw3d();
  else drawFlat();
  drawParts();
  ctx.restore();
  drawFlyers();
  drawMotes();
  if(exitFlash > 0){
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    ctx.globalAlpha = exitFlash * 0.5;
    var eg = ctx.createRadialGradient(CX, CY, 0, CX, CY, Math.max(W,H)*0.7);
    eg.addColorStop(0, 'rgba(53,215,161,0.5)'); eg.addColorStop(1, 'rgba(53,215,161,0)');
    ctx.fillStyle = eg; ctx.fillRect(0,0,W,H); ctx.restore();
  }
  if(impact > 0){
    ctx.save(); ctx.globalCompositeOperation = 'lighter';
    var r = Math.max(W, H)*0.62, g = ctx.createRadialGradient(CX, CY, N*S*0.30, CX, CY, r);
    g.addColorStop(0, 'rgba(' + (ST.dN[0]|0) + ',' + (ST.dN[1]|0) + ',' + (ST.dN[2]|0) + ',' + (impact*0.10).toFixed(3) + ')');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = g; ctx.fillRect(0, 0, W, H); ctx.restore();
  }
}

function drawFlat(){
  var u, v, s, r, i, SF = curSurf(), BM = baseBasis();
  if(!SF) return;
  buildDrops();
  drawCage(BM, 1, 0.16, 0);
  /* 1. the tiles, each riding whatever wave is passing through the board */
  var anyWave = !!wave;
  if(flip && !demo){
    /* the old material is still on screen behind the front — draw it first,
       shrinking out, so the crossover is a thing you watch rather than a
       frame you miss */
    for(u = 0; u < N; u++) for(v = 0; v < N; v++){
      var os = flip.old[u*N + v]; if(!os) continue;
      var fp = flipPhase(u, v); if(fp >= 0.999) continue;
      var orr = tileRect(u, v), osc = 1 - fp;
      ctx.globalAlpha = 1 - fp;
      ctx.fillStyle = tileFill(os.t === 'A' || os.t === 'B' ? '+' : os.t, os.d);
      ctx.fillRect(orr[0] + S*(1-osc)/2, orr[1] + S*(1-osc)/2, S*osc + 0.6, S*osc + 0.6);
    }
    ctx.globalAlpha = 1;
  }
  for(u = 0; u < N; u++) for(v = 0; v < N; v++){
    s = SF[u*N + v]; if(!s) continue;
    r = tileRect(u, v);
    var ph = anyWave ? tilePhase(u, v) : 1;
    if(flip && !demo) ph = Math.min(ph, flipPhase(u, v));
    if(ph <= 0.002) continue;
    if(ph < 0.999){
      var sc = 0.55 + 0.45*ph, ix = r[0] + S*(1-sc)/2, iy = r[1] + S*(1-sc)/2;
      ctx.globalAlpha = ph;
      ctx.fillStyle = tileFill(s.t === 'A' || s.t === 'B' ? '+' : s.t, s.d);
      ctx.fillRect(ix, iy, S*sc + 0.6, S*sc + 0.6);
      ctx.globalAlpha = 1;
      continue;
    }
    ctx.fillStyle = tileFill(s.t === 'A' || s.t === 'B' ? '+' : s.t, s.d);
    ctx.fillRect(r[0], r[1], r[2] + 0.6, r[3] + 0.6);
    if(isWalkType(s.t)){
      ctx.fillStyle = 'rgba(255,255,255,' + (0.05 + 0.09*(s.d/Math.max(1,N-1))).toFixed(3) + ')';
      ctx.fillRect(r[0] + S*0.10, r[1] + S*0.10, S*0.80, S*0.80);
    }
  }
  if(anyWave){ drawWaveExtras(); return; }
  /* 1b. grain, on the tiles only, so stone reads as stone */
  if(grainPat){
    ctx.save(); ctx.globalCompositeOperation = 'overlay'; ctx.globalAlpha = 0.13;
    ctx.fillStyle = grainPat;
    for(u = 0; u < N; u++) for(v = 0; v < N; v++){
      if(!SF[u*N + v]) continue;
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
    s = SF[u*N + v]; if(!s) continue;
    r = tileRect(u, v);
    for(i = 0; i < 4; i++){
      var u2 = u + SD[i][0], v2 = v + SD[i][1];
      if(u2 < 0 || v2 < 0 || u2 >= N || v2 >= N) continue;
      var s2 = SF[u2*N + v2]; if(!s2) continue;
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
      var s3 = SF[u3*N + v3]; if(!s3) continue;
      var ad = Math.abs(s3.d - s.d); if(!ad) continue;
      ctx.strokeStyle = 'rgba(0,0,0,' + Math.min(0.85, 0.26 + ad*0.15).toFixed(2) + ')';
      ctx.lineWidth = Math.min(3.4, 1 + ad*0.5);
      ctx.beginPath();
      if(i === 0){ ctx.moveTo(r[0]+S, r[1]); ctx.lineTo(r[0]+S, r[1]+S); }
      else       { ctx.moveTo(r[0], r[1]);   ctx.lineTo(r[0]+S, r[1]); }
      ctx.stroke();
    }
  }
  if(demo) return;
  /* 3. THE REACHABLE SET. The connectivity graph, drawn. This is the single
        most useful thing on screen: turn the cube and watch the lit region
        change, and the mechanic has explained itself with no words. */
  ctx.lineWidth = Math.max(1.2, S*0.035);
  for(u = 0; u < N; u++) for(v = 0; v < N; v++){
    var rk = u*N + v;
    if(!reach[rk]) continue;
    /* the sweep: each tile lights when the wavefront reaches its BFS depth,
       so what you are watching is the connectivity graph being traced */
    var lit = Math.max(0, Math.min(1, (revealT - reachDist[rk]*46) / 150));
    if(lit <= 0) continue;
    r = tileRect(u, v);
    ctx.strokeStyle = 'rgba(255,106,61,' + (0.42*lit).toFixed(3) + ')';
    ctx.strokeRect(r[0] + S*0.07, r[1] + S*0.07, S*0.86, S*0.86);
    if(lit < 1){
      ctx.save(); ctx.globalCompositeOperation = 'lighter';
      ctx.globalAlpha = (1-lit)*0.5;
      ctx.fillStyle = 'rgba(255,106,61,1)';
      ctx.fillRect(r[0] + S*0.07, r[1] + S*0.07, S*0.86, S*0.86);
      ctx.restore();
    }
  }
  /* 4. depth numerals, for as long as someone wants the crutch */
  if(store.depth){
    ctx.font = '700 ' + Math.round(S*0.24) + 'px ui-monospace, monospace';
    ctx.textAlign = 'left'; ctx.textBaseline = 'top';
    for(u = 0; u < N; u++) for(v = 0; v < N; v++){
      s = SF[u*N + v]; if(!s) continue;
      r = tileRect(u, v);
      ctx.fillStyle = s.t === '+' ? 'rgba(10,9,16,.55)' : 'rgba(233,225,209,.42)';
      ctx.fillText(String(s.d), r[0] + S*0.10, r[1] + S*0.09);
    }
  }
  /* 4b. the seam — a lit ring at the wavefront, so the change has a leading
         edge instead of just happening */
  if(flip){
    var rad = (flip.t / flip.rise) * S * 0.92;
    if(rad > 0 && rad < N*S*1.9){
      var fr = tileRect(flip.fu, flip.fv);
      ctx.save(); ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = 'rgba(' + flip.col + ',' + Math.max(0, 0.85 - rad/(N*S*1.5)).toFixed(3) + ')';
      ctx.lineWidth = Math.max(2, S*0.11);
      ctx.beginPath(); ctx.arc(fr[0]+S/2, fr[1]+S/2, rad, 0, 6.284); ctx.stroke();
      ctx.restore();
    }
  }
  /* 5. things standing on the deck — none of which the attract cube needs */
  if(demo) return;
  for(u = 0; u < N; u++) for(v = 0; v < N; v++){
    var gs = SF[u*N + v];
    if(gs && isGlyph(gs.t)) drawPlate(tileRect(u, v), GLYPH_BIT[gs.t], gs.d);
  }
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

/* While the board is assembling or coming apart, only the two things that
   are people-shaped stay on screen. Everything else is scenery and scenery
   does not need to be there for the transition. */
function drawWaveExtras(){
  var gv = surfaceAt(N, surf, M, lv.goal);
  if(gv && wave.mode === 'out') drawGoal(tileRect(gv[0], gv[1]));
  if(wave.mode === 'in'){ var p = playerScreen(); drawPlayer(p[0], p[1], S); }
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

function drawPlayer(x, y, sz, k){
  /* on the way out you do not blink off the board, you are drawn into the
     door — down to nothing, spinning, over a quarter of a second */
  if(exitPull >= 0){
    k = 1 - exitPull;
    if(k <= 0.02) return;
  }
  k = k === undefined ? 1 : k;
  var rr = sz*0.27*k;
  ctx.save(); ctx.translate(x, y); ctx.rotate((1-k)*3.4); ctx.translate(-x, -y);
  halo(x, y, sz*1.05*k, '255,106,61', 0.34*k);
  ctx.save();
  ctx.shadowColor = 'rgba(255,106,61,.85)'; ctx.shadowBlur = sz*0.5;
  ctx.fillStyle = '#ff6a3d';
  ctx.beginPath();
  ctx.moveTo(x, y - rr); ctx.lineTo(x + rr, y); ctx.lineTo(x, y + rr); ctx.lineTo(x - rr, y);
  ctx.closePath(); ctx.fill();
  ctx.restore();
  ctx.fillStyle = '#ffd9c8';
  ctx.beginPath(); ctx.arc(x, y, rr*0.32, 0, 6.284); ctx.fill();
  ctx.restore();
}
/* A plate reads as a thing cut INTO the deck rather than set on top of it:
   a recessed ring, a bar across it for INVERT (the axis that flips) or a
   level line for DRAIN, and a slow breath so it is visibly live. It is drawn
   at its own depth like everything else, so a plate far down the cube is dim
   — but it is never occluded, which is the one thing that makes planning
   with them possible. */
function drawPlate(r, bit, depth){
  var x = r[0] + S/2, y = r[1] + S/2, col = PLATE_COL[bit];
  var t = (Date.now() % 2600) / 2600, br = 0.55 + 0.45*Math.sin(t*6.284);
  var lit = 0.35 + 0.65 * (depth / Math.max(1, N-1));
  halo(x, y, S*0.95, col, 0.30*br*lit);
  ctx.save();
  ctx.strokeStyle = 'rgba(' + col + ',' + (0.55 + 0.4*br*lit).toFixed(3) + ')';
  ctx.lineWidth = Math.max(1.6, S*0.065);
  ctx.beginPath(); ctx.arc(x, y, S*0.29, 0, 6.284); ctx.stroke();
  ctx.lineWidth = Math.max(1.3, S*0.05);
  ctx.beginPath();
  if(bit === 1){ ctx.moveTo(x - S*0.17, y - S*0.17); ctx.lineTo(x + S*0.17, y + S*0.17); }
  else { ctx.moveTo(x - S*0.19, y); ctx.lineTo(x + S*0.19, y); }
  ctx.stroke();
  ctx.restore();
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
  var la = liveAngle();
  var pv = demo ? PERSP * 0.85 : PERSP * Math.abs(Math.sin(la ? la.ang : 0));
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
    var mo = ((fc.x*73856093 ^ fc.y*19349663 ^ fc.z*83492791) >>> 8) % 100;
    list.push({d:dep/4, k:0, pts:pts, t:fc.t, dep:tmp[2] + c, lit:LIT(nx, ny, nz),
               up: ny > 0.55, moss: mo < 34 ? (mo/34) : 0});
  }
  /* markers ride half a cell proud of their own face so they stay visible
     as the solid turns under them */
  function marker(w, kind, extra){
    px(w[0] + m.F[0]*0.62, w[1] + m.F[1]*0.62, w[2] + m.F[2]*0.62, tmp);
    list.push({d:tmp[2] + 0.62, k:kind, x:tmp[0], y:tmp[1], extra:extra});
  }
  if(!demo){
    for(i = 0; i < lv.doors.length; i++) marker(lv.doors[i], 3, !!(doors & (1<<i)));
    for(i = 0; i < lv.keys.length; i++) if(!(kmask & (1<<i))) marker(lv.keys[i], 2, 0);
    marker(lv.goal, 4, 0);
    marker(pos, 1, 0);
  }

  /* THE PLINTH. A object needs something to stand on or it is a diagram of an
     object. Same projection, same lighting, drawn under everything. */
  if(demo){
    var ph = N/2, pt = ph + 0.75, pb = ph + 0.10, py0 = ph, py1 = ph + 0.62;
    var corner = [[-pt,-pt],[pt,-pt],[pt,pt],[-pt,pt]], top = [], bot = [];
    for(i = 0; i < 4; i++){
      px(c + corner[i][0], c - py0, c + corner[i][1], tmp); top.push([tmp[0], tmp[1], tmp[2]]);
      px(c + corner[i][0], c - py1, c + corner[i][1], tmp); bot.push([tmp[0], tmp[1], tmp[2]]);
    }
    ctx.save();
    for(i = 0; i < 4; i++){
      var j = (i+1) % 4;
      var midz = (top[i][2] + top[j][2]) / 2;
      if(midz > 0) continue;                       /* the far sides only */
      ctx.fillStyle = 'rgba(28,26,36,.95)';
      ctx.beginPath(); ctx.moveTo(top[i][0],top[i][1]); ctx.lineTo(top[j][0],top[j][1]);
      ctx.lineTo(bot[j][0],bot[j][1]); ctx.lineTo(bot[i][0],bot[i][1]); ctx.closePath(); ctx.fill();
    }
    ctx.fillStyle = tileFill('#', N-1, 0.85);
    ctx.beginPath(); ctx.moveTo(top[0][0],top[0][1]);
    for(i = 1; i < 4; i++) ctx.lineTo(top[i][0],top[i][1]);
    ctx.closePath(); ctx.fill();
    for(i = 0; i < 4; i++){
      var j2 = (i+1) % 4, midz2 = (top[i][2] + top[j2][2]) / 2;
      if(midz2 <= 0) continue;                     /* then the near ones, over the top */
      ctx.fillStyle = 'rgba(22,20,30,.98)';
      ctx.beginPath(); ctx.moveTo(top[i][0],top[i][1]); ctx.lineTo(top[j2][0],top[j2][1]);
      ctx.lineTo(bot[j2][0],bot[j2][1]); ctx.lineTo(bot[i][0],bot[i][1]); ctx.closePath(); ctx.fill();
    }
    ctx.restore();
  }
  list.sort(function(a, b){ return a.d - b.d; });
  var sz = S*z;
  drawCage(m, z, demo ? 0.10 : 0.46, pv);
  for(i = 0; i < list.length; i++){
    var e = list[i];
    if(e.k === 0){
      ctx.fillStyle = tileFill(e.t, e.dep, e.lit);
      ctx.beginPath();
      ctx.moveTo(e.pts[0], e.pts[1]); ctx.lineTo(e.pts[2], e.pts[3]);
      ctx.lineTo(e.pts[4], e.pts[5]); ctx.lineTo(e.pts[6], e.pts[7]);
      ctx.closePath(); ctx.fill();
      ctx.strokeStyle = 'rgba(10,9,16,.5)'; ctx.lineWidth = 0.7; ctx.stroke();
      /* weathering, and only where weather would reach: the faces that point
         up. Seeded off the cell so a given block is always mossy. */
      if(e.up && e.moss){
        ctx.save(); ctx.globalAlpha = 0.16 + 0.2*e.moss;
        ctx.fillStyle = 'rgb(126,150,86)';
        ctx.beginPath();
        ctx.moveTo(e.pts[0], e.pts[1]); ctx.lineTo(e.pts[2], e.pts[3]);
        ctx.lineTo(e.pts[4], e.pts[5]); ctx.lineTo(e.pts[6], e.pts[7]);
        ctx.closePath(); ctx.fill(); ctx.restore();
      }
    } else if(e.k === 1) drawPlayer(e.x, e.y, sz);
    else if(e.k === 2) drawKey([e.x - sz/2, e.y - sz/2]);
    else if(e.k === 3) drawDoor([e.x - sz/2, e.y - sz/2], e.extra);
    else if(e.k === 4) drawGoal([e.x - sz/2, e.y - sz/2]);
  }
}

/* ============================================================
   MOVING
   ============================================================ */
function snapshot(){ return {pos:pos.slice(), ori:oriIndex(M), kmask:kmask, doors:doors, turns:turns, world:world}; }
function pushUndo(){ undoStack.push(snapshot()); if(undoStack.length > 200) undoStack.shift(); }
function restore(s){
  pos = s.pos.slice(); M = ORIS[s.ori]; kmask = s.kmask; doors = s.doors; turns = s.turns;
  world = s.world | 0;
  walking = null; anim = null; drag = null; won = false; stuck = false; flip = null;
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
      /* A PLATE MAY ONLY BE THE DESTINATION. Stepping on one rewrites the
         board, so every step planned after it was planned against a world
         that no longer exists. Routing stops there and you plan again — which
         also makes standing on a plate a decision rather than something that
         happens to you on the way past. */
      if(cell && isGlyph(cell.t) && !(u2 === tu && v2 === tv)) continue;
      if(!cell){
        if(!(u2 === tu && v2 === tv)) continue;
        var raw = surf[k];
        if(!raw || !isWalkType(raw.t)) continue;
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
  if(!isWalkType(s.t)){ toast('bedrock — no footing on this face'); sfx.deny(); return; }
  var di = doorIndexAt(lv, s.w), shut = di >= 0 && !(doors & (1<<di));
  if(shut && keysHeld() < 1){ toast('locked — find a key'); sfx.deny(); buzz(14); return; }
  var path = pathTo(u, v);
  if(!path){ toast(shut ? 'no way to that door' : 'not connected — turn the cube'); sfx.deny(); return; }
  /* Tapping the cell you already stand on yields an EMPTY path, which is
     truthy — it used to start a walk with nothing in it, and the next frame
     read surf[undefined].w and took the render loop down with it. */
  if(!path.length) return;
  pushUndo();
  walking = {queue:path, from:pos.slice(), t:0, step:0};
}

function advanceWalk(dt){
  if(!walking) return;
  walking.t += dt / 105;
  if(walking.t < 1) return;
  if(!walking.queue.length){ walking = null; settle(); return; }
  var cell = surf[walking.queue.shift()];
  if(!cell){ walking = null; settle(); return; }   /* the board moved under a queued step */
  walking.from = pos.slice();
  pos = cell.w.slice();
  var pv2 = viewOf(N, M, pos), pr2 = tileRect(pv2[0], pv2[1]);
  var cx2 = pr2[0] + S/2, cy2 = pr2[1] + S/2;
  var di = doorIndexAt(lv, pos);
  if(di >= 0 && !(doors & (1<<di))){
    doors |= (1<<di); sfx.door(); buzz(24); toast('unlocked');
    kick(5, 0, 1);
    burst(cx2, cy2, 22, {spd:120, life:620, r:2.4, col:'53,215,161', g:90});
  } else {
    sfx.step();
    burst(cx2, cy2 + S*0.22, 3, {spd:20, life:340, r:1.5, col:'200,190,170', g:34, fade:0.6});
  }
  var ki = keyIndexAt(lv, pos);
  if(ki >= 0 && !(kmask & (1<<ki))){
    kmask |= (1<<ki); sfx.key(); buzz(12); toast('key');
    burst(cx2, cy2, 16, {spd:95, life:520, r:2.2, col:'255,196,77', g:20});
    flyKey();
  }
  var gb = glyphAt(lv, pos);
  if(gb){ firePlate(gb, cx2, cy2); }
  walking.t = 0;
  if(!walking.queue.length){
    walking = null; sfx.land(); kick(3, 0, 1);
    burst(cx2, cy2 + S*0.24, 6, {spd:38, life:420, r:1.8, col:'210,200,180', g:56, fade:0.7});
    settle(); afterAction();
  }
  else { computeReach(); refreshHud(); }
}

function tryTurn(t){
  if(walking || anim || won) return false;
  var m2 = TURNS[t].f(M);
  if(!landing(lv, m2, pos, doors, world)) return false;
  var axis = (t === 0 || t === 1) ? 'x' : 'y';
  var to = (t === 1 || t === 2) ? Math.PI/2 : -Math.PI/2;
  var from = drag && drag.axis === axis ? drag.ang : 0;
  drag = null;
  pushUndo();
  anim = {axis:axis, ang:from, from:from, to:to, t:0,
          dur:180 + 150*(Math.abs(to - from)/(Math.PI/2)), turn:t, back:1};
  return true;
}

/* A REFUSED TURN IS A PHYSICAL EVENT, not an error message.
   The cube commits a few degrees, hits the stop, and slams back — so the
   answer arrives in the hand before the toast arrives in the eye. */
function refuseTurn(t){
  var axis = (t === 0 || t === 1) ? 'x' : 'y';
  var sgn = (t === 1 || t === 2) ? 1 : -1;
  var from = drag && drag.axis === axis ? drag.ang : sgn * 0.14;
  drag = null;
  anim = {axis:axis, ang:from, from:from, to:0, t:0, dur:260, turn:-1, back:0, hard:1};
  kick(5, t < 2 ? -sgn : 0, t < 2 ? 0 : sgn);
  sfx.deny(); buzz(18); toast('no footing that way');
}
function springBack(){
  if(!drag || !drag.axis) { drag = null; return; }
  anim = {axis:drag.axis, ang:drag.ang, from:drag.ang, to:0, t:0, dur:190, turn:-1};
  drag = null;
}
/* The detent, as a curve. A plain ease-out arrives at 90 degrees and stops,
   which is what a slideshow does. A real latch goes slightly past its seat
   and is pulled back into it, so this overshoots by a few degrees and
   settles — and that half-frame of coming BACK is most of what makes the
   turn feel like a mechanism instead of a transition. A refusal uses the
   hard curve instead: no overshoot, arrive early, stop dead. */
function easeSeat(p){
  var c = 1.9;
  return 1 + (c + 1) * Math.pow(p - 1, 3) + c * Math.pow(p - 1, 2);
}
function easeSlam(p){ return 1 - Math.pow(1 - p, 5); }

function advanceAnim(dt){
  if(!anim) return;
  anim.t += dt;
  var p = Math.min(1, anim.t / anim.dur);
  var e = anim.hard ? easeSlam(p) : (anim.back ? easeSeat(p) : 1 - Math.pow(1 - p, 3));
  anim.ang = anim.from + (anim.to - anim.from) * e;
  if(p < 1) return;
  var t = anim.turn; anim = null;
  if(t < 0) return;
  M = TURNS[t].f(M);
  var land = landing(lv, M, pos, doors, world);
  pos = land.w.slice();
  var ki = keyIndexAt(lv, pos);
  if(ki >= 0 && !(kmask & (1<<ki))){ kmask |= (1<<ki); sfx.key(); flyKey(); toast('key'); }
  turns++;
  impact = 1;
  /* the mass arrives: kick the camera along the turn's own axis, shake grit
     off the whole board, and start the reachable set sweeping outward */
  var sgn = (t === 1 || t === 2) ? 1 : -1;
  kick(9, t < 2 ? -sgn : 0, t < 2 ? 0 : sgn);
  var pv = viewOf(N, M, pos), pr = tileRect(pv[0], pv[1]);
  for(var g = 0; g < 5; g++){
    var gx = CX + (Math.random()-0.5)*N*S*0.9, gy = CY + (Math.random()-0.5)*N*S*0.9;
    burst(gx, gy, 3, {spd:26, life:620, r:1.5, col:'190,180,160', g:70, fade:0.5});
  }
  burst(pr[0]+S/2, pr[1]+S/2, 8, {spd:64, life:420, r:2.1, col:'255,140,90', g:60});
  revealT = 0;
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
    var r = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors, world:world});
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
  /* THE EXIT. The player is pulled into the door, a ring goes out across the
     board, and then the board itself comes apart outward from that same
     square. The card does not appear over a still image of a solved puzzle —
     it appears after the puzzle has left. */
  var gv = viewOf(N, M, pos), gr = tileRect(gv[0], gv[1]);
  var gx = gr[0] + S/2, gy = gr[1] + S/2;
  exitPull = 0;
  burst(gx, gy, 30, {spd:190, life:800, r:2.6, col:'53,215,161', g:0});
  burst(gx, gy, 16, {spd:70, life:1000, r:3.4, col:'233,245,255', g:-14});
  kick(11, 0, 1);
  exitFlash = 1;
  setTimeout(function(){ startWave('out', gv[0], gv[1], 420); }, 190);
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
    if(crossing) sfx.vault();
    show('scWin');
  }, 880);
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
  var was = Math.abs(drag.ang);
  drag.ang = q * Math.PI/2;
  /* the vault complains as it moves, and complains louder near the detent —
     so the commit threshold is something you can hear coming */
  if(Math.abs(drag.ang) - was > 0.12){ sfx.creak(Math.abs(q)); }
  if(was < Math.PI/4 && Math.abs(drag.ang) >= Math.PI/4) buzz(8);
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
    if(!tryTurn(t)) refuseTurn(t);
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
    if(!tryTurn(map[e.key])) refuseTurn(map[e.key]);
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
  ['scTitle','scCubes','scManual','scPause','scWin','scPlate'].forEach(function(s){
    $(s).classList.toggle('hide', s !== id);
  });
  $('hud').classList.toggle('on', !id);
  /* the attract cube tumbles behind the title and the manual; behind the
     pause and win cards the real board stays put, because those are about
     the level you are in the middle of */
  var wasDemo = demo;
  /* the manual opened mid-level is NOT an attract screen — the board behind
     it is a puzzle somebody is in the middle of */
  demo = (id === 'scTitle') || (id === 'scManual' && manualFrom === 'scTitle');
  if(demo && !lv){ loadLevel(1); wave = null; }
  if(!demo) demoAng = 0;
  if(demo !== wasDemo){ if(demo) demoStart(); setStyle(curBand); layout(); }
}
var ROMAN = ['I','II','III','IV','V','VI','VII','VIII','IX','X','XI','XII'];
function roman(b){ return b < ROMAN.length ? ROMAN[b] : String(b+1); }
function refreshHud(){
  if(!lv) return;
  $('lvName').textContent = levelNo + ' · ' + lv.name;
  var tn = $('turnN');
  if(tn.textContent !== String(turns)){
    tn.textContent = turns;
    var tp = $('turnPill'); tp.classList.remove('bump'); void tp.offsetWidth; tp.classList.add('bump');
  }
  $('parN').textContent = '/' + lv.par;
  $('turnPill').classList.toggle('over', turns > lv.par);
  var wt = $('worldTag'), names = {0:'', 1:'INVERTED', 2:'DRAINED', 3:'INVERTED · DRAINED'};
  wt.textContent = names[world] || '';
  wt.classList.toggle('on', world !== 0);
  wt.style.color = 'rgb(' + (world === 2 ? PLATE_COL[2] : PLATE_COL[1]) + ')';
  wt.style.boxShadow = 'inset 0 0 0 1px rgba(' + (world === 2 ? PLATE_COL[2] : PLATE_COL[1]) + ',.34)';
  wt.style.background = 'rgba(' + (world === 2 ? PLATE_COL[2] : PLATE_COL[1]) + ',.10)';
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
    var can = lv && !won && !!landing(lv, TURNS[t].f(M), pos, doors, world);
    el.className = 'tick ' + (can ? 'ok' : 'no');
    if(live && Math.abs(live.ang) > 0.1){
      var act = live.axis === 'x' ? (live.ang > 0 ? 1 : 0) : (live.ang > 0 ? 2 : 3);
      el.classList.toggle('armed', act === t && Math.abs(live.ang) > Math.PI/4 && can);
      el.style.opacity = act === t ? '1' : '.3';
    } else { el.style.opacity = ''; el.classList.remove('armed'); }
  }
}
function undo(){
  if(!undoStack.length || walking || anim) return;
  restore(undoStack.pop());
  sfx.step();
}
function hint(){
  if(!lv || won) return;
  var r = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors, world:world});
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
var manualFrom = 'scTitle';
$('btnManual').onclick = function(){ manualFrom = 'scTitle'; show('scManual'); };
$('btnManual2').onclick = function(){ manualFrom = 'scPause'; show('scManual'); };
$('btnManualBack').onclick = function(){
  /* Opened from the pause menu this is just a reference card — go back to the
     level. Opened from the title it is the way IN, and it must load a level
     rather than reveal whatever the attract cube left lying around. */
  if(manualFrom === 'scPause'){ show(null); return; }
  goLevel(firstUncleared());
};
$('btnMenu').onclick = function(){
  $('pauseName').textContent = lv ? lv.name : 'PAUSED';
  $('pauseSub').textContent = lv ? ('Vault ' + roman(vaultOf(levelNo)) + ' · ' + vaultName(vaultOf(levelNo)) +
      ' — fewest possible: ' + lv.par + ' turn' + (lv.par===1?'':'s')) : '';
  show('scPause');
};
$('btnResume').onclick = function(){ show(null); };
$('btnPlateGo').onclick = function(){ show(null); };
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
    if(!$('scPlate').classList.contains('hide')){ show(null); return true; }
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
if(window.visualViewport){
  /* Safari collapses and expands its bars as you play; without this the board
     is laid out against a height that stopped being true two seconds ago. */
  var vvT = 0;
  var onVV = function(){ clearTimeout(vvT); vvT = setTimeout(fit, 60); };
  window.visualViewport.addEventListener('resize', onVV);
  window.visualViewport.addEventListener('scroll', onVV);
}

/* ============================================================
   LOOP
   ============================================================ */
var running = true, last = 0;
/* THE LOOP MUST NOT BE ABLE TO DIE.

   An exception thrown in here propagates out before the next
   requestAnimationFrame is queued, and the chain is gone — permanently. The
   game does not crash to an error screen, it silently freezes: taps do
   nothing, nothing redraws, and the only cure is a restart. That is exactly
   what one bad tap did, and no single defect should ever be able to brick
   the app. So the frame is wrapped and the next one is always armed, while
   the error still goes to the console so a test harness watching for it
   fails loudly rather than quietly passing. */
function loop(ts){
  if(!running) return;
  var dt = last ? Math.min(64, ts - last) : 16;
  last = ts;
  try{
    advanceAnim(dt);
    advanceWalk(dt);
    stepMotes(dt); stepParts(dt); stepWave(dt); stepFlip(dt); stepFlyers(dt); stepDemo(dt);
    if(kickV.t < 1) kickV.t = Math.min(1, kickV.t + dt/kickV.dur);
    if(revealT < 2000) revealT += dt;
    if(exitFlash > 0) exitFlash = Math.max(0, exitFlash - dt/620);
    if(exitPull >= 0){ exitPull += dt/260; if(exitPull > 1) exitPull = 1; }
    draw();
  } catch(err){
    console.error('frame error', err);
    walking = null; anim = null; drag = null;
    if(lv) settle();
  }
  requestAnimationFrame(loop);
}
buildGrain(); buildMotes(); setStyle(0);
try{ $('wordmark').src = buildWordmark('TURNKEY', 22).toDataURL('image/png'); }catch(e){}
fit();
show('scTitle');
/* the label has its own node: writing textContent on the button itself wiped
   the play chip out of the DOM every boot */
$('playLabel').textContent = store.reached > 1 ? 'CONTINUE' : 'BEGIN';
requestAnimationFrame(loop);
