/* ============================================================
   PERSISTENCE — one key, and it is allowed to fail.
   A WebView with storage disabled should still play; it just forgets.
   ============================================================ */
/* EFFECTS DEFAULT TO WHAT THE DEVICE ALREADY ASKED FOR. A player who has
   set "reduce motion" at the OS level has already told every app they open
   how much shake they want, and making them find a switch to say it again
   is not a preference, it is a tax. They still get the game — the beats are
   all there at 40% — and the switch is in the pause menu either way. */
var reduceMotion = false;
try{ reduceMotion = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches); }catch(e){}
var store = {best:{}, reached:1, sound:1, haptic:1, depth:0, taught:0, sawPlate:0,
             sawPeek:0, peekHinted:0, togo:1, fx: reduceMotion ? 0 : 1};
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
/* ============================================================
   BUBBLY, NOT GRITTY.

   Every sound in here used to be a filtered noise burst: a stone click, a
   stone scrape, a stone latch. Accurate, and joyless. The sound design that
   goes with a round blue character with eyes is MELODIC — bouncy, pitched,
   and generous, where even the failure states are friendly.

   Two rules do most of the work.

   FIRST, EVERYTHING IS IN A KEY. The whole game plays on one pentatonic
   scale, so no two sounds can ever collide badly no matter how fast they
   are triggered — a player mashing steps is playing a tune rather than
   causing a pile-up. Pentatonic because it has no semitone clashes in it
   at all; you cannot make it sound wrong.

   SECOND, PITCH RISES ON GOOD AND FALLS ON BAD, always, with no exceptions.
   A key sweeps up. A door sweeps up. A turn lands up. A refusal falls, a
   dead end falls. That single convention teaches the whole game's feedback
   without a word of text, and it is the reason a five-year-old knows they
   did something wrong in a Mario game before they have parsed the screen.

   Sines and triangles almost throughout: a sawtooth is a machine and a
   square is a buzzer, where a triangle is a toy xylophone.
   ============================================================ */
var PENT = [523.25, 587.33, 698.46, 783.99, 880.00,        /* C D F G A */
            1046.50, 1174.66, 1396.91, 1567.98, 1760.00];
/* a bell: the note, plus its octave underneath at a third of the level, is
   the cheapest way to make a sine sound like something struck */
function bell(f, dur, gain, slide, send){
  tone(f, dur, 'triangle', gain, slide || 0, send === undefined ? 0.4 : send);
  tone(f*0.5, dur*0.8, 'sine', gain*0.34, 0, 0.2);
}
function arp(notes, step, gain, type){
  notes.forEach(function(f, i){
    setTimeout(function(){ tone(f, 0.34, type || 'triangle', gain, 0, 0.45); }, i*step);
  });
}
var stepN = 0;
var sfx = {
  /* a footfall walks up the scale and resets, so crossing a board is a
     phrase rather than a repeated click */
  step:  function(){
    var f = PENT[stepN % 5] * 0.5;
    stepN++;
    tone(f, 0.13, 'triangle', 0.075, f*1.02, 0.16);
    tone(f*2, 0.07, 'sine', 0.030, 0, 0.10);
  },
  land:  function(){
    stepN = 0;
    tone(196, 0.16, 'sine', 0.085, 262, 0.28);
    tone(392, 0.09, 'triangle', 0.045, 0, 0.2);
  },
  /* the detent: a rising sweep that ARRIVES on a bell, so the turn has a
     landing note and not just a stop */
  turn:  function(){
    tone(220, 0.20, 'sine', 0.075, 660, 0.30);
    noise(0.16, 3200, 0.045, 0.30, 900);
    setTimeout(function(){ bell(783.99, 0.42, 0.10); }, 90);
    tone(87.31, 0.34, 'sine', 0.13, 65, 0.12);
  },
  /* the vault complaining as it moves, pitched instead of scraped */
  creak: function(v){
    tone(120 + v*260, 0.09, 'sine', 0.018 + v*0.022, 0, 0.18);
  },
  /* a bonk. Falls, lands on a soft low note, and is impossible to hear as
     punishing — the cube said no, it did not tell you off */
  deny:  function(){
    tone(392, 0.13, 'triangle', 0.10, 175, 0.14);
    setTimeout(function(){ tone(147, 0.16, 'sine', 0.075, 131, 0.2); }, 70);
  },
  /* the coin. Two notes, a fifth apart, the second late and brighter. */
  key:   function(){
    bell(1046.50, 0.16, 0.10);
    setTimeout(function(){ bell(1567.98, 0.40, 0.095); }, 78);
  },
  keyIn: function(){ bell(2093, 0.22, 0.055); },
  /* a gate opening is the biggest good news short of the exit, so it gets a
     whole ascending triad and a shimmer on top */
  door:  function(){
    arp([523.25, 698.46, 1046.50], 66, 0.085);
    tone(130.81, 0.5, 'sine', 0.10, 196, 0.3);
    setTimeout(function(){ noise(0.30, 5200, 0.05, 0.5, 9000); }, 150);
  },
  win:   function(){
    /* the fanfare: up the pentatonic, then the octave held */
    arp([523.25, 698.46, 880, 1046.50, 1396.91], 92, 0.105);
    setTimeout(function(){ bell(2093, 1.1, 0.10); }, 470);
    tone(130.81, 1.0, 'sine', 0.13, 174.61, 0.35);
    noise(0.5, 6000, 0.045, 0.6, 11000);
  },
  vault: function(){
    arp([392, 523.25, 659.25, 783.99, 1046.50, 1318.51], 128, 0.09);
    tone(98, 1.6, 'sine', 0.11, 130.81, 0.7);
  },
  /* the world turning inside out: a sweep that goes the direction the world
     went, and a bell at the other end of it */
  plate: function(bit){
    var up = bit === 1;
    tone(up ? 262 : 1046, 0.7, 'sine', 0.10, up ? 1046 : 262, 0.55);
    arp(up ? [523.25, 783.99, 1046.50, 1567.98] : [1567.98, 1046.50, 783.99, 523.25], 84, 0.075);
    tone(65.41, 0.9, 'sine', 0.15, 49, 0.25);
    noise(0.6, 2000, 0.05, 0.7, up ? 9000 : 400);
  },
  /* time running backwards, so the phrase does too */
  undo:  function(){ arp([784, 587.33, 440], 52, 0.06, 'sine'); },
  /* two notes falling a tone: the universal "hm" */
  stuck: function(){
    tone(440, 0.24, 'triangle', 0.075, 0, 0.3);
    setTimeout(function(){ tone(349.23, 0.5, 'triangle', 0.075, 0, 0.4); }, 190);
  },
  /* stone to glass and back: a breath in, a soft knock out */
  peek:  function(){
    tone(523.25, 0.5, 'sine', 0.05, 1046.50, 0.6);
    noise(0.34, 900, 0.030, 0.5, 7000);
  },
  peekOff: function(){ tone(659.25, 0.14, 'sine', 0.05, 440, 0.2); }
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
  /* SETTING THE CANVAS SIZE RESETS EVERY BIT OF CONTEXT STATE, so this lives
     here and nowhere else: without it a 16px block scaled up to a 40px tile
     comes back through a bilinear filter as a smear, which is the one thing
     a texture drawn at block resolution must never be. Rotate the phone and
     it would silently come back on. */
  ctx.imageSmoothingEnabled = false;
  layout(); buildSky(); buildMotes(); buildGlow(); buildSmear(); shS = -1;
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
/* ============================================================
   THE OTHER SHAPE OF SCREEN.

   Every measurement in this file was written against a phone held upright,
   where the width binds and the height has slack. A console is the exact
   opposite: 1280x720 in the hand, 1920x1080 on a television, and in both of
   them the HEIGHT binds and there is more horizontal room than the board
   can ever use.

   Two things have to change and only two. The board sizes against the
   height, which the existing clamp already does correctly — it takes the
   smaller of the two and that is the right answer in both orientations. And
   the chrome has to stop being pinned to the far edges: a turn counter
   nine hundred pixels from the cube it is counting is not a HUD, it is
   scenery. So the bars are given the board's own width to live in and
   centred over it, which changes nothing on a phone (where that width is
   the screen) and everything on a television.

   The overscan margin is the third thing and it is not a design decision,
   it is a platform rule: a TV throws away the outer few percent of the
   picture, so nothing that has to be read may live there. It applies only
   on displays wide enough to be a television.
   ============================================================ */
function landscape(){ return W > H * 1.18; }
function layout(){
  if(!N) return;
  var ins = insets();
  if(landscape() && W >= 900){
    /* docked: assume a television is eating the edges until told otherwise */
    var ov = Math.round(Math.min(W, H) * 0.035);
    ins = {t:ins.t + ov, r:ins.r + ov, b:ins.b + ov, l:ins.l + ov};
  }
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
  /* the chrome follows the board rather than the screen */
  document.documentElement.style.setProperty('--barw',
    Math.round(Math.max(300, Math.min(W - ins.l - ins.r, side + S*1.2))) + 'px');
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
  if(a && Math.abs(a.ang) > 1e-4) B = a.axis === 'x' ? yawBasis(B, a.ang) : pitchBasis(B, a.ang);
  /* the peek leans on top of whatever the cube was already doing, so a hold
     that begins mid-spring still arrives at the same pose */
  if(peek.amt > 0.001){
    var e = peekE();
    B = pitchBasis(yawBasis(B, (PEEK_YAW + peek.yaw)*e), (PEEK_PITCH + peek.pitch)*e);
  }
  return B;
}
/* A square rotating about a screen axis projects (cos+sin) times as wide, so
   pulling the camera back by exactly that keeps the cube inside the frame
   through the whole turn instead of clipping at 45 degrees. */
function liveZoom(){
  /* the plinth sticks out past the cube and perspective enlarges whatever is
     nearest, so the fit box is the cube's footprint less a margin for both */
  if(demo) return fitScale(heroBasis(), N*S*0.74, N*S*0.74);
  var a = liveAngle(), z = 1;
  if(a){ var t = Math.abs(a.ang); z = 1 / (Math.cos(t) + Math.sin(t)); }
  /* a cube leaned about two axes at once sweeps a far wider silhouette than
     a turn about one does, so the peek measures its own pose and fits to it
     rather than trusting a closed form that only holds for single turns */
  if(peek.amt > 0.001){
    var e2 = peekE();
    z = z*(1-e2) + fitScale(liveBasis(), N*S*0.82, N*S*0.82)*e2;
  }
  return z;
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
  /* the tinted blocks are keyed on depth, and depth is read against N — a
     cube of a different size means every one of them is the wrong colour */
  texReset();
  /* AND THE SOLVED-STATE CACHE IS PER CUBE. Its keys are positions and
     orientations, which cube nine and cube ten both have plenty of — carrying
     it across a level would answer a question about this cube with the
     distance to a different cube's exit. */
  remainCache = {}; remainN = 0; remain = null; remainOver = false; remainStale = false; goShown = '';
  M = ORI_ID; pos = lv.start.slice(); kmask = 0; doors = 0; turns = 0; world = 0;
  clearEff(lv); flip = null; plateT = 0;
  var k0 = keyIndexAt(lv, pos); if(k0 >= 0) kmask |= (1<<k0);
  undoStack = []; walking = null; anim = null; drag = null; won = false; stuck = false;
  var band = vaultOf(levelNo);
  if(band !== curBand || !skyCv){ curBand = band; setStyle(band); }
  buildFaces();
  layout(); settle();
  /* a new cube starts on a clean camera and an empty stage: debris, rings,
     flashes and a half-decayed shake belong to the level that is over */
  doorT = new Array(lv.doors.length); for(var dz = 0; dz < doorT.length; dz++) doorT[dz] = 0;
  parts = []; rings = []; flashes = []; flyers = []; orbTrail = []; beam = null;
  exitFlash = 0; exitPull = -1; vig = 0;
  CAM.it = 1; CAM.tr = 0; CAM.punch = 0;
  peek.on = false; peek.amt = 0; peek.yaw = 0; peek.pitch = 0;
  var sv = viewOf(N, M, pos);
  startWave('in', sv[0], sv[1], 380);
  revealT = -120;
  ambience(band);
  if(levelNo > store.reached){ store.reached = levelNo; save(); }
  /* THE ONE NUDGE. A gesture nobody can see is a gesture nobody has, and the
     peek is the answer to the exact frustration that starts around here — so
     it is offered once, on the third cube, and never mentioned again. */
  if(!store.sawPeek && !store.peekHinted && levelNo >= 3){
    store.peekHinted = 1; save();
    setTimeout(function(){ if(!won && !walking && !anim) toast('hold anywhere to look inside the cube'); }, 1100);
  }
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
  /* ONE CHOKE POINT. Everything that can change what you may reach — a
     landed turn, a step, a plate firing, an undo, a level loading — comes
     through here, so the live par is asked for here and nowhere else. */
  scheduleRemain();
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
function tileRGB(t, d, lightMul){
  var near = Math.max(0, Math.min(1, d / Math.max(1, N-1)));
  var lm = lightMul === undefined ? 1 : lightMul;
  var a = t === '+' ? ST.dF : ST.rF, b = t === '+' ? ST.dN : ST.rN;
  return [(a[0] + (b[0]-a[0])*near) * lm,
          (a[1] + (b[1]-a[1])*near) * lm,
          (a[2] + (b[2]-a[2])*near) * lm];
}
function tileFill(t, d, lightMul){ return rgbs(tileRGB(t, d, lightMul)); }

/* ============================================================
   THE MATERIAL — every block is a 16x16 texture, and every texture is a
   set of MULTIPLIERS rather than colours.

   A texture carrying its own colours would be a second palette, and this
   board is read off exactly one: deck brighter than bedrock, near brighter
   than far, and nothing else. So a texture never says what colour a block
   is. It says how much lighter or darker each of its 256 texels is than the
   colour the palette already chose for that material at that depth. Change
   vault and every block in the cube restyles itself for free, the depth
   ramp still runs, and the bands cannot drift — because TEX_LO/TEX_HI are
   hard clamps and enforceBands was built against them.

   16x16 is not nostalgia. It is the resolution at which a stone reads as a
   stone and a plank reads as a plank at forty screen pixels, and it is the
   grid the eye already reads as "block".

   THE FLAT BOARD IS A VOXEL FIELD SEEN DOWN ITS OWN AXIS. That was always
   true — project() collapses a cube of cells to one square of surfaces —
   but flat fills let it read as a chart of coloured squares. A block face
   with mortar in it, a bevel at its edge and a neighbour that does not
   match reads as what it is: the front of a cube, with more cube behind it.
   ============================================================ */
var TEXN = 16;
/* THE PATTERN GRID AND THE RENDER GRID ARE NOT THE SAME NUMBER.

   The material is authored at sixteen: bricks four to a face, cobbles the
   size of a cobble, mortar one texel wide. That chunkiness is the look and it
   is not up for negotiation.

   But the LIGHTING on a block — its bevel, its occluded edge, its rim — is
   not made of texels, and at sixteen it came out as a staircase. So the baked
   block is four times that across, the pattern is sampled from it point by
   point (so every brick stays exactly as crisp as it was authored), and only
   the smooth field is drawn at the finer grid. Upscaled to a hundred-odd
   pixels on screen, a gradient quantised to sixty-four steps has each step
   about a pixel and a half wide, which is nothing, and the mortar lines are
   still hard-edged. Crisp material, smooth light, one texture.             */
var TEXR = 64, TEXS = TEXR / TEXN;
function texNew(v){
  var a = new Float32Array(TEXN*TEXN);
  if(v) for(var i = 0; i < a.length; i++) a[i] = v;
  return a;
}
function tmul(a, x, y, v){ a[(y & 15)*TEXN + (x & 15)] *= v; }
/* a stable per-cell shade, so brick 3 of a wall is always brick 3 */
function cellShade(k){ return 0.94 + (((k * 2654435761) >>> 24) % 100) / 100 * 0.15; }

/* COURSED BLOCKS — stone brick. Four 8x8 blocks to a face, one texel of
   mortar between them, each block lit along its top-left and shaded along
   its bottom-right. The regularity is the point: a laid floor is a made
   thing, and a made thing is a thing you are meant to walk on. */
function patBricks(rng, bw, bh, stagger){
  var a = texNew(1), x, y;
  for(y = 0; y < TEXN; y++) for(x = 0; x < TEXN; x++){
    var row = Math.floor(y / bh), off = stagger ? row * (bw >> 1) : 0;
    var lx = (x + off) % bw, ly = y % bh, col = Math.floor((x + off) / bw);
    var v = cellShade(row*7 + col*13 + 3) + (rng()-0.5)*0.025;
    if(lx === 0 || ly === 0) v *= 0.87;                     /* the mortar course */
    else if(lx === 1 || ly === 1) v *= 1.06;                /* the block's lit lip */
    else if(lx === bw-1 || ly === bh-1) v *= 0.92;
    a[y*TEXN + x] = v;
  }
  return a;
}

/* RUBBLE — cobblestone, and the honest way to draw it is to grow it. Sites
   scattered on a torus, every texel taking the shade of the nearest, and the
   seam between two stones darkened by how close the contest was. Wrapping
   the distance means the texture tiles, which matters the moment two blocks
   of it sit edge to edge. */
function patCobble(rng, sites, rough){
  var pts = [], i, x, y;
  for(i = 0; i < sites; i++) pts.push([rng()*TEXN, rng()*TEXN, 0.86 + rng()*0.26]);
  var a = texNew(1);
  for(y = 0; y < TEXN; y++) for(x = 0; x < TEXN; x++){
    var b1 = 1e9, b2 = 1e9, bi = 0;
    for(i = 0; i < sites; i++){
      var dx = Math.abs(x + 0.5 - pts[i][0]); if(dx > TEXN/2) dx = TEXN - dx;
      var dy = Math.abs(y + 0.5 - pts[i][1]); if(dy > TEXN/2) dy = TEXN - dy;
      var d = dx*dx + dy*dy;
      if(d < b1){ b2 = b1; b1 = d; bi = i; } else if(d < b2) b2 = d;
    }
    var edge = Math.sqrt(b2) - Math.sqrt(b1), v = pts[bi][2];
    if(edge < 1.2) v *= 0.70 + 0.26*edge;                   /* the gap between stones */
    else if(edge < 2.0) v *= 1.05;                          /* and the stone's own shoulder */
    a[y*TEXN + x] = v + (rng()-0.5)*rough;
  }
  return a;
}

/* BOARDS — four planks to a face, sawn ends staggered so the grain never
   lines up across the seam. */
function patPlanks(rng){
  var a = texNew(1), x, y, ph = 4;
  for(y = 0; y < TEXN; y++){
    var row = Math.floor(y / ph), ly = y % ph, base = cellShade(row*29 + 11);
    var grain = 1 + (((row * 2654435761) >>> 27) % 3);      /* which line in this board has the grain */
    for(x = 0; x < TEXN; x++){
      var v = base + (rng()-0.5)*0.02;
      if(ly === 0) v *= 0.80;                               /* the seam between boards */
      else if(ly === 1) v *= 1.07;                          /* the next board's lit top */
      else if(ly === grain) v *= 0.93 + 0.05*Math.sin(x*1.3 + row*2.1);
      if(ly > 0){
        var end = (x + row*5) % 8;
        if(end === 0) v *= 0.74;                            /* the sawn end */
        else if(end === 1) v *= 1.05;
      }
      a[y*TEXN + x] = v;
    }
  }
  return a;
}

/* CUT STONE — four polished slabs to a face, scribed rather than mortared,
   close-grained and the quietest of the three, so a vault built out of it
   reads as a smooth place rather than a rough one. */
function patSmooth(rng){
  var a = texNew(1), x, y;
  for(y = 0; y < TEXN; y++) for(x = 0; x < TEXN; x++){
    var v = 1 + Math.sin(y*0.85 + Math.sin(x*0.4)*0.7)*0.03 + (rng()-0.5)*0.018;
    if(x === 8 || y === 8) v *= 0.86;                       /* the scribed joint */
    else if(x === 9 || y === 9) v *= 1.05;                  /* and the lip past it */
    a[y*TEXN + x] = v;
  }
  return a;
}

/* SHOT ROCK — unworked stone with something in it. Speckle, not structure:
   the material you cannot stand on should never look laid. */
function patOre(rng){
  var a = texNew(1), i, x, y;
  for(y = 0; y < TEXN; y++) for(x = 0; x < TEXN; x++)
    a[y*TEXN + x] = 1 + Math.sin(x*0.7 + Math.cos(y*0.5)*1.7)*0.05
                      + Math.sin(y*0.9 + 1.3)*0.035 + (rng()-0.5)*0.04;
  for(i = 0; i < 18; i++){
    var cx = (rng()*TEXN)|0, cy = (rng()*TEXN)|0, br = rng() < 0.45 ? 1.12 : 0.64;
    tmul(a, cx, cy, br); tmul(a, cx+1, cy, br);
    tmul(a, cx, cy+1, (br + 1)/2); tmul(a, cx+1, cy+1, (br + 1)/2);
  }
  return a;
}

/* A VAULT OWNS A MATERIAL, the same way it already owns a palette. Ten
   cubes of stone brick over cobble, then ten of boards over gravel, then
   ten of cut slab over shot rock — and past the authored vaults it keeps
   cycling, so vault ninety is built out of something nobody chose and
   nobody has seen either. Deck and bedrock are never drawn from the same
   generator, because "is this laid or is this raw" is the second channel
   the material read runs on after brightness. */
var TEX_KITS = [
  {deck:function(r){ return patBricks(r, 8, 4, 1); }, rock:function(r){ return patCobble(r, 8, 0.06); }},
  {deck:patPlanks,                                    rock:function(r){ return patCobble(r, 15, 0.11); }},
  {deck:patSmooth,                                    rock:patOre}
];

/* THE EDGE, AND THE CLAMP.
   The bevel is what makes a grid of these read as a field of cubes rather
   than as wallpaper: light along the top and left, shade along the bottom
   and right, one texel each, on every block whatever it is made of. Then
   every texel is clamped into the range enforceBands was promised, which is
   the last line of defence for the whole board's legibility. */
function finishTex(a, t){
  var lo = TEX_LO[t], hi = TEX_HI[t], i, j;
  /* A MOULDED TOP-LIGHT. Every block now reads as an object lit from above
     rather than a swatch of material: a gentle vertical ramp across the
     whole face, brightest at the top edge. It is what separates a soft
     plastic brick from a photograph of a brick, and it costs one multiply
     per texel at build time. */
  for(j = 0; j < TEXN; j++){
    var v = 1.055 - (j/(TEXN-1)) * 0.115;
    for(i = 0; i < TEXN; i++) tmul(a, i, j, v);
  }
  for(i = 0; i < TEXN; i++){
    tmul(a, i, 0, 1.05); tmul(a, i, TEXN-1, 0.93);
    tmul(a, 0, i, 1.04); tmul(a, TEXN-1, i, 0.94);
  }
  for(i = 0; i < a.length; i++) a[i] = a[i] < lo ? lo : (a[i] > hi ? hi : a[i]);
  return a;
}

/* THE TINTED CACHE.
   A texture is a shape; a block on screen is that shape times a colour, and
   the colour is (material, depth, how the light hits it). Depth is quantised
   to half a cell and light to twentieths — fine enough that a turning cube
   shades smoothly, coarse enough that the whole cube costs a few dozen
   16x16 canvases. They are built on demand and thrown away whenever the
   palette, the material or the cube's size changes underneath them. */
var TEXPAT = {'+':null, '#':null}, texCache = {}, texCount = 0;
/* twentieths of the palette colour, and five steps of headroom above it so a
   deck can be lit BRIGHTER than its own material by something standing on it */
var TEX_LIT = 20, TEX_LIT_MAX = 25;
function texReset(){ texCache = {}; texCount = 0; }
function buildPats(band){
  var kit = TEX_KITS[((band % TEX_KITS.length) + TEX_KITS.length) % TEX_KITS.length];
  TEXPAT['+'] = finishTex(kit.deck(mulberry32(0xB10C ^ (band*2654435761))), '+');
  TEXPAT['#'] = finishTex(kit.rock(mulberry32(0xC0BB ^ (band*40503))), '#');
  texReset();
}
function texFor(t, d, lit){
  var mt = isWalkType(t) ? '+' : '#';
  var dq = Math.max(0, Math.min((N-1)*2, Math.round(d*2)));
  /* A DECK MAY BE LIT PAST ITS OWN COLOUR AND BEDROCK MAY NOT — clamped
     here, at the one place a tint can enter, rather than trusted to every
     caller. Brightening a deck can only ever widen the gap the material read
     depends on; brightening bedrock would close it, so bedrock is capped at
     unlit whatever anybody asks for. */
  if(mt === '#' && lit > 1) lit = 1;
  var lq = lit === undefined ? TEX_LIT : Math.max(0, Math.min(TEX_LIT_MAX, Math.round(lit*TEX_LIT)));
  var key = mt + dq + '_' + lq, c = texCache[key];
  if(c) return c;
  /* One cube's worth is a few hundred of these, and the cache is thrown away
     on every level and every vault anyway — this is only the backstop, set
     well clear of what a single cube can ask for so it never fires in the
     middle of a turn. */
  if(texCount > 700) texReset();   /* a baked block is 16 KB now, not 1 */
  var pat = TEXPAT[mt], base = tileRGB(mt, dq/2, lq/TEX_LIT);
  c = document.createElement('canvas'); c.width = c.height = TEXR;
  var g = c.getContext('2d'), id = g.createImageData(TEXR, TEXR), i, m, o, px2, py2;
  var vd = ST.vd, deck = mt === '+';
  for(i = 0; i < TEXR*TEXR; i++){
    o = i*4;
    px2 = i % TEXR; py2 = (i / TEXR) | 0;
    /* the material, point-sampled off the coarse grid so a brick edge stays
       a brick edge, times the smooth field that makes the block a solid */
    m = pat[((py2/TEXS)|0)*TEXN + ((px2/TEXS)|0)] * shadeAt(i, deck);
    /* ROUNDED CORNERS, AND THEY ARE NOT A CLIP.

       A square block is a diagram; a block with its corners taken off is an
       object, and the whole visual language being aimed at here is built out
       of soft-edged things. Doing it with a rounded-rect clip would mean a
       path and a clip per tile in the flat renderer and a second, harder
       version of the same idea for the turning one — and the two would have
       to agree exactly or the board would pop the instant a drag began.

       Instead the corner is baked into the TINT: texels outside the corner
       radius fade toward the void colour. It costs nothing, both renderers
       inherit it from the same pixels, and against a dark sky a corner that
       fades to sky IS a rounded corner.

       It is applied here rather than in the pattern on purpose. The pattern
       clamps are the band guarantee's contract, and this is geometry, not
       material — it darkens deck and bedrock identically and so cannot make
       one look like the other. */
    var cd = cornerFade(px2, py2);
    var r0 = base[0]*m, g0 = base[1]*m, b0 = base[2]*m;
    if(cd < 1){
      r0 = r0*cd + vd[0]*(1-cd);
      g0 = g0*cd + vd[1]*(1-cd);
      b0 = b0*cd + vd[2]*(1-cd);
    }
    id.data[o]   = Math.min(255, r0);
    id.data[o+1] = Math.min(255, g0);
    id.data[o+2] = Math.min(255, b0);
    id.data[o+3] = 255;
  }
  g.putImageData(id, 0, 0);
  texCount++;
  return (texCache[key] = c);
}
/* One block, face on. Half a pixel of overdraw on each side, exactly as the
   flat fills had, so no hairline of void can open between two neighbours. */
function drawBlock(t, d, x, y, w, h, lit){
  ctx.drawImage(texFor(t, d, lit), x, y, w, h);
}

/* How much of a texel survives at (x,y): 1 across the middle of the block,
   falling to 0 outside the corner arc. Precomputed once at the block's RENDER
   resolution, which is what makes the corner an actual arc rather than four
   pixels of staircase. */
var CORNER_R = 4.2, cornerLUT = null;
function cornerFade(x, y){
  if(!cornerLUT){
    var rr = CORNER_R * TEXS;
    cornerLUT = new Float32Array(TEXR*TEXR);
    for(var j = 0; j < TEXR; j++) for(var i = 0; i < TEXR; i++){
      var dx = Math.max(0, rr - 0.5 - Math.min(i, TEXR-1-i));
      var dy = Math.max(0, rr - 0.5 - Math.min(j, TEXR-1-j));
      var d = Math.sqrt(dx*dx + dy*dy);
      cornerLUT[j*TEXR + i] = Math.max(0, Math.min(1, (rr - 0.55) - d + 0.5));
    }
  }
  return cornerLUT[y*TEXR + x];
}

/* ============================================================
   THE BLOCK IS A SOLID, AND THIS IS WHAT MAKES IT ONE.

   Every block used to be a flat square of material. Material alone is a
   swatch: it tells you what a thing is made of and nothing about its shape,
   and forty-nine swatches in a grid read as a chart. What separates this from
   something you would see on a shelf is that a block has to read as a piece
   of matter with a top, an edge and a thickness — and none of that is in the
   pattern, because the pattern is tiling detail and tiling detail has no
   edges to speak of.

   So a second field is laid over it, at a resolution the pattern never has to
   pay for, carrying the four things that make a lump of stone look like one:

     BEVEL      the top edge catches the light and the bottom edge loses it,
                which is the whole of "this has thickness" in one term.
     OCCLUSION  the last sixth of the block darkens toward its own boundary.
                Two of these side by side put a soft seam between them and
                the grid stops being a grid of tiles and becomes a wall of
                separate stones.
     RIM        one thin bright line inside the top-left silhouette. It is
                the difference between an object lit by a room and an object
                lit by a light, and it costs a smoothstep.
     SHEEN      a broad, very weak highlight on the upper-left of DECKS ONLY,
                because a laid floor is a smoother thing than shot rock and
                should catch a little more of the room.

   WHY THE MIDDLE IS LEFT ALONE, precisely. Cast shadows in this renderer stop
   at 42% of a tile so the centre of every block always shows its true
   material undarkened — that is what keeps the deck/bedrock band readable
   under a shadow. This field obeys the same law: everything it does happens
   within a sixth of an edge, and the multiplier at the centre of a block is
   exactly 1. The band guarantee is measured off tileFill() and cannot be
   touched from here, and now it cannot be touched in the picture either.

   AND WHY IT IS BAKED INTO THE TEXTURE rather than laid over the board as a
   pass. There are two renderers — the flat board at rest and the honest solid
   mid-turn — and the one thing they must never do is disagree, because the
   handover happens on the first pixel of a drag. A screen-space overlay would
   have to be written twice, in two coordinate systems, and would pop the
   instant a finger moved. Baked here, both paths inherit it from the same
   pixels for free.                                                          */
function blockShade(nx, ny, deck){
  /* distance to the nearest edge, 0 at the boundary, 0.5 dead centre */
  var e = Math.min(nx, ny, 1 - nx, 1 - ny);
  var m = 1;
  /* occlusion — the outer seventh only */
  if(e < 0.15){
    var q = e / 0.15; q = q*q*(3 - 2*q);
    m *= 0.78 + 0.22*q;
  }
  /* THE LIP HAS A DIRECTION, and the first version of this did not — the rim
     ran all the way round every block, which is not a solid catching a light,
     it is a button. A lit top, a dark underside and a much weaker pair of
     sides is what says "this has thickness and the light is up there". */
  if(ny < 0.12){ var t0 = 1 - ny/0.12; m *= 1 + 0.135*t0*t0; }
  if(ny > 0.84){ var b0 = (ny - 0.84)/0.16; m *= 1 - 0.19*b0*b0; }
  if(nx < 0.10){ var l0 = 1 - nx/0.10; m *= 1 + 0.05*l0*l0; }
  if(nx > 0.90){ var r0 = (nx - 0.90)/0.10; m *= 1 - 0.085*r0*r0; }
  /* the rim: one thin bright line, and only along the two edges facing the
     light. Away from the top-left it falls off to nothing. */
  var lit = Math.max(0, 1 - (nx*0.55 + ny*1.05));
  if(lit > 0){
    var rim = Math.max(0, 1 - Math.abs(e - 0.035)/0.035);
    m *= 1 + 0.20*rim*lit;
  }
  /* and the sheen, decks only */
  if(deck){
    var sx = nx - 0.30, sy = ny - 0.26;
    m *= 1 + 0.05*Math.exp(-(sx*sx + sy*sy)*3.4);
  }
  return m;
}

/* DETAIL AT MORE THAN ONE SCALE, which is most of what separates a rendered
   surface from a painted one.

   The authored patterns carry exactly one: bricks the size of bricks, cobbles
   the size of cobbles. Real stone also has a coarse mottle across a whole
   face — where it is damp, where it has been walked on — and a fine tooth
   below the size of anything you can name. With only the middle scale
   present, a block reads as a printed swatch no matter how well it is lit.

   Both extra scales are computed at the render grid, not the pattern grid, so
   they cost the material nothing: the bricks stay exactly as crisp as they
   were authored and the surface underneath them stops being flat. Amplitudes
   are deliberately tiny — this is the layer you are not supposed to notice,
   and the moment you can name it, it is too strong.

   It is a hash rather than a random, so a given texel is the same shade every
   time the texture is built. A cache that rebuilds after a vault change must
   not hand back a different-looking floor.                                  */
function fineDetail(x, y){
  var h = (x * 374761393 + y * 668265263) | 0;
  h = (h ^ (h >>> 13)) * 1274126177 | 0;
  var n = ((h ^ (h >>> 16)) >>> 0) / 4294967296;
  /* coarse mottle: two low-frequency waves crossing, so it never tiles into
     a visible stripe */
  var mot = Math.sin(x*0.21 + Math.cos(y*0.17)*2.3) * Math.sin(y*0.13 + 1.7);
  return 1 + (n - 0.5)*0.055 + mot*0.022;
}

/* AND BOTH OF THEM ARE A LOOKUP, because they are the same numbers every
   time and there are four thousand of them per block.

   Written straight, the shade and the detail cost several transcendentals per
   texel. That is invisible while a level sits still — the textures are cached
   — and it is a stutter at exactly the wrong moment: a plate flip changes the
   material of every cell at once, so forty blocks are baked inside a single
   frame while the board is mid-flip and the game is already spending a hitstop
   and a slow-motion on it. Measured as the clock losing a fifth of a second
   of wall time to dropped frames.

   The field depends on nothing but the texel and whether the block is a deck,
   so it is two arrays of TEXR², built once, and baking a block becomes one
   multiply per texel.                                                       */
var shadeLUT = null;
function shadeAt(i, deck){
  if(!shadeLUT){
    shadeLUT = [new Float32Array(TEXR*TEXR), new Float32Array(TEXR*TEXR)];
    for(var y = 0; y < TEXR; y++) for(var x = 0; x < TEXR; x++){
      var k = y*TEXR + x, fd = fineDetail(x, y);
      shadeLUT[0][k] = blockShade((x + 0.5)/TEXR, (y + 0.5)/TEXR, false) * fd;
      shadeLUT[1][k] = blockShade((x + 0.5)/TEXR, (y + 0.5)/TEXR, true)  * fd;
    }
  }
  return shadeLUT[deck ? 1 : 0][i];
}

/* THE LAMP YOU ARE CARRYING. Two cells of falloff, decks only, and it is
   applied by choosing a brighter tint for the tile rather than by painting
   light on top of it — see the note at the call site. */
var lightU = -99, lightV = -99;
function deckLight(u, v, t){
  if(!isWalkType(t) || lightU < -50) return 1;
  var du = u - lightU, dv = v - lightV, d2 = du*du + dv*dv;
  if(d2 > 7) return 1;
  return 1 + 0.16 * Math.exp(-d2*0.40);
}

/* One block, turning. Under this projection a face is a parallelogram, so
   the texture goes on with a plain affine map built from three of its four
   corners — no per-texel work, no mesh, one drawImage. The clip is what
   pays for the perspective term: with it on, the quad's fourth corner is
   allowed to disagree with the parallelogram by a fraction of a pixel and
   nothing bleeds onto the block next door. */
function texQuad(pts, tex){
  ctx.save();
  ctx.beginPath();
  ctx.moveTo(pts[0], pts[1]); ctx.lineTo(pts[2], pts[3]);
  ctx.lineTo(pts[4], pts[5]); ctx.lineTo(pts[6], pts[7]);
  ctx.closePath(); ctx.clip();
  ctx.transform((pts[2]-pts[0])/TEXR, (pts[3]-pts[1])/TEXR,
                (pts[6]-pts[0])/TEXR, (pts[7]-pts[1])/TEXR, pts[0], pts[1]);
  ctx.drawImage(tex, 0, 0);
  ctx.restore();
}
/* WHAT A SLOW DEVICE IS ALLOWED TO DROP.

   Texturing the turning solid costs a clip and a transform per face, and a
   seven-cube can put six hundred faces on screen. On this machine that is
   about a millisecond; on a five-year-old WebView it is not, and a turn that
   stutters is worse than a turn drawn in flat colour — the turn is the verb.

   So the gate is not a constant. Faces smaller than it keep the flat fill
   they were always drawn with, and the gate walks up whenever the solid path
   overruns its share of the frame and back down when it stops. A slow phone
   loses the texture on the smallest, most oblique faces first, which are the
   ones carrying the least information, and keeps its frame rate. Nothing
   about it is visible on a device that can afford the detail.              */
var TEX_GATE_MIN = 130, texGate = TEX_GATE_MIN;
function nowMs(){ return (window.performance && performance.now) ? performance.now() : 0; }
/* twice the triangle area, unsigned — the gate that stops a face which is
   three pixels across from paying for a clip nobody can see */
function quadArea(p){
  return Math.abs((p[2]-p[0])*(p[7]-p[1]) - (p[6]-p[0])*(p[3]-p[1]))
       + Math.abs((p[4]-p[2])*(p[7]-p[3]) - (p[6]-p[2])*(p[5]-p[3]));
}

/* Quarried stone, not slate — and lighter than any vault plays in, because
   the hero is lit from behind and every side face is already losing most of
   its value to the shading term before the depth fade touches it. */
var HERO_STYLE = enforceBands({name:'HERO',
  dF:[176,150,86], dN:[255,238,186], rF:[64,46,34], rN:[132,92,54],
  vd:[22,16,32], st:[255,226,180], at:[255,158,70]});
function setStyle(band){
  ST = demo ? HERO_STYLE : vaultStyle(band);
  buildPats(band);
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
  /* ---- CLOUD, so the sky has a far side ------------------------------
     A flat wash and a scatter of points is a backdrop. What makes a sky
     read as DEPTH is that some of it is nearer than the rest of it — so a
     handful of very large, very faint blooms are laid down first, in the
     vault's own ambient and star colours, at alphas low enough that no
     single one is findable. What they do is stop any two regions of the
     screen being the same value, which is the whole of the difference
     between "black behind the board" and "distance behind the board".
     Painted once per vault, so they cost a frame nothing.               */
  var i, x, y, r, a;
  var nb = mulberry32(0xC10D ^ (curBand * 40503));
  for(i = 0; i < 7; i++){
    x = nb()*(W+PAD*2); y = nb()*(H+PAD*2);
    r = (0.22 + nb()*0.42) * Math.max(W, H);
    var col = nb() < 0.5 ? ST.at : ST.st;
    var cg = c.createRadialGradient(x, y, 0, x, y, r);
    cg.addColorStop(0, 'rgba(' + (col[0]|0) + ',' + (col[1]|0) + ',' + (col[2]|0) + ',' + (0.030 + nb()*0.035).toFixed(3) + ')');
    cg.addColorStop(0.55, 'rgba(' + (col[0]|0) + ',' + (col[1]|0) + ',' + (col[2]|0) + ',0.012)');
    cg.addColorStop(1, 'rgba(' + (col[0]|0) + ',' + (col[1]|0) + ',' + (col[2]|0) + ',0)');
    c.fillStyle = cg;
    c.fillRect(x - r, y - r, r*2, r*2);
  }
  var area = (W + PAD*2) * (H + PAD*2);
  c.fillStyle = rgbs(ST.st);
  /* THE BRIGHT ONES — the sky you actually notice, and there are as many of
     them as there always were. Count is off AREA now rather than a flat 190,
     so a tablet does not get a thinner sky than a phone. */
  for(i = 0; i < Math.round(area/2080); i++){
    x = rng()*(W+PAD*2); y = rng()*(H+PAD*2);
    r = rng()*rng()*1.7 + 0.32; a = 0.12 + rng()*rng()*0.7;
    c.globalAlpha = a;
    c.beginPath(); c.arc(x, y, r, 0, 6.284); c.fill();
    if(r > 1.3){ c.globalAlpha = a*0.28; c.beginPath(); c.arc(x, y, r*3.2, 0, 6.284); c.fill(); }
  }
  /* AND THE DUST, WHICH EXISTS FOR THE KEYHOLES.

     The player marker is a hole showing the sky behind the board, in register
     with it to the pixel — and at 190 stars over a whole screen, the expected
     number of stars inside a thirty-pixel disc is FOUR TENTHS. The honest
     window onto a sparse sky is a black dot, which is worse than the dishonest
     magnified one it replaced, and the same is true of every narrow void
     column on the board.

     The fix belongs in the sky, not in the hole: make the starfield dense
     enough to survive being looked at through a keyhole. These are small and
     faint by construction, so open sky reads about as it did — the difference
     is that no aperture of any size now lands on nothing.                  */
  for(i = 0; i < Math.round(area/240); i++){
    x = rng()*(W+PAD*2); y = rng()*(H+PAD*2);
    r = 0.24 + rng()*rng()*0.5;
    c.globalAlpha = 0.05 + rng()*rng()*0.30;
    c.beginPath(); c.arc(x, y, r, 0, 6.284); c.fill();
  }
}

/* WHERE THE SKY IS, THIS FRAME — in screen pixels, and in ONE place.

   The starfield is parallaxed off the view basis, so where it sits moves
   every frame. Two things now have to agree about that to the pixel: the
   background, and the hole in the middle of the board that is supposed to be
   showing the same sky through it. Two copies of this expression would agree
   on the day they were written and drift apart on the first tweak to the
   parallax, and the failure would be silent — the stars in the hole simply
   would not be the stars behind the board any more, which is the entire
   claim the object is making.                                              */
function skyOffset(){
  var m0 = liveBasis();
  return [-PAD - m0.F[0]*11 - m0.R[0]*4, -PAD + m0.U[1]*8 - m0.F[1]*9];
}

/* SEEING THROUGH, LITERALLY — the patch of sky behind (x, y, rr), painted
   into whatever is currently clipped, at the size and position it already
   occupies on screen.

   Two details carry the whole thing.

   IT LEAVES THE CAMERA. The caller is drawing inside camBegin(), so its
   coordinates are BOARD space: shake, squash and zoom are all live in the
   transform. The sky is drawn outside all of that on purpose. So the current
   matrix is read, the hole's centre and radius are pushed through it into
   screen pixels, and the blit is done with the base transform restored. A
   shake now slides the board across a hole whose contents do not budge,
   which is what a hole is.

   IT IS A SOURCE-RECTANGLE BLIT, not a scaled copy of the whole starfield.
   The earlier magnified version asked the compositor to transform a
   screen-sized image every frame and then throw away all but a thumbnail,
   which cost about two milliseconds a frame. This lifts exactly the pixels
   that are visible, so the work is proportional to the hole.

   The radius is taken from the LARGER of the two axis scales with a margin,
   because a squash makes the clipped circle an ellipse and a patch sized off
   the average would come up short along the stretched axis. Over-covering
   costs nothing: the surplus is clipped away.                              */
function skyThrough(x, y, rr){
  var tm = ctx.getTransform ? ctx.getTransform() : null;
  var sx = x, sy = y, sr = rr;
  if(tm){
    sx = (tm.a*x + tm.c*y + tm.e) / DPR;
    sy = (tm.b*x + tm.d*y + tm.f) / DPR;
    sr = rr * Math.max(Math.sqrt(tm.a*tm.a + tm.b*tm.b),
                       Math.sqrt(tm.c*tm.c + tm.d*tm.d)) / DPR * 1.15 + 1;
    ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  }
  var so = skyOffset(), kk = skyCv.width / (W + PAD*2);
  ctx.imageSmoothingEnabled = true;
  ctx.drawImage(skyCv,
    (sx - sr - so[0])*kk, (sy - sr - so[1])*kk, sr*2*kk, sr*2*kk,
    sx - sr, sy - sr, sr*2, sr*2);
  ctx.imageSmoothingEnabled = false;
  if(tm) ctx.setTransform(tm);
}

/* ---------- GRAIN, AND WHY IT IS GONE ------------------------------------
   There was a 64px noise tile overlaid on every tile here, because a flat
   fill reads as plastic and the same fill with a whisper of grain reads as
   stone. The blocks carry their own grain now, at their own resolution and
   inside the clamps the band guarantee is built on — and a 64px noise
   pattern sitting on top of a 16px block texture is just a filter smearing
   the pixels the material is made of. One overlay pass per tile, deleted.
                                                                            */

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
  buildContact();
}

/* ---------- THE BOARD STOPS FLOATING -------------------------------------
   Every block was drawn straight onto the starfield with nothing between
   them, so the cube read as a decal on a wallpaper: correct, legible, and
   obviously two flat things at the same distance from the eye.

   What was missing is the cheapest and oldest trick there is — the block
   throws a shadow into the space around it. It only ever SHOWS in the gaps:
   between two neighbours the shadow is covered by the neighbour, so the only
   places it survives are the void columns and the outside of the board,
   which are exactly the places the eye was being told nothing. A soft dark
   shape offset a little down and out from every block, drawn under all of
   them, and the board lifts off the sky.

   IT CANNOT TOUCH THE MATERIAL READ. It is painted before a single block is,
   so nothing it does can darken a tile's own centre — the band guarantee
   never comes near it. That is also why it is a separate pass rather than
   part of the drops above, which are the shadows blocks throw at each OTHER
   and have to obey the 42% law.

   One blurred sprite, built when the tile size changes and drawn once per
   block. The blur is done by the canvas at build time, so the frame pays for
   a drawImage and nothing else.                                            */
var contactCv = null;
function buildContact(){
  var pad = Math.max(6, S*0.28), sz = Math.ceil(S + pad*2);
  contactCv = document.createElement('canvas');
  contactCv.width = contactCv.height = Math.ceil(sz*DPR);
  var g = contactCv.getContext('2d');
  g.setTransform(DPR, 0, 0, DPR, 0, 0);
  g.filter = 'blur(' + (S*0.085).toFixed(2) + 'px)';
  g.fillStyle = 'rgba(0,0,0,0.62)';
  var r = S*0.26, x = pad, y = pad, w = S, h = S;
  g.beginPath();
  if(g.roundRect) g.roundRect(x, y, w, h, r);
  else {
    g.moveTo(x+r, y); g.arcTo(x+w, y, x+w, y+h, r); g.arcTo(x+w, y+h, x, y+h, r);
    g.arcTo(x, y+h, x, y, r); g.arcTo(x, y, x+w, y, r); g.closePath();
  }
  g.fill();
  g.filter = 'none';
  contactCv.pad = pad; contactCv.sz = sz;
}
function drawContact(SF){
  if(!contactCv) return;
  var pad = contactCv.pad, sz = contactCv.sz, drop = S*0.075;
  for(var u = 0; u < N; u++) for(var v = 0; v < N; v++){
    if(!SF[u*N + v]) continue;
    var r = tileRect(u, v);
    ctx.drawImage(contactCv, r[0] - pad, r[1] - pad + drop, sz, sz);
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

/* ============================================================
   THE CAMERA — one place where every shove the picture gets is added up.

   There used to be a single directional kick here and it was right about the
   important thing: a turn about the screen-up axis should throw the board
   SIDEWAYS, because then the shake is telling you which way the mass went
   rather than merely that something happened. Random shake is noise; aimed
   shake is information, and that is kept.

   What it could not do is stack. A landing, a plate firing under your feet
   and the exit all wanted to move the camera at once, and one damped sine
   cannot hold three events. So there are now three channels that sum:

     IMPULSE  aimed, damped, decaying — the mechanism seating. Unchanged.
     TRAUMA   a 0..1 reservoir, spent as trauma SQUARED across translation,
              a little rotation and a little zoom. Squaring is what makes a
              small event stay small and a big one feel dangerous, and the
              rotation is what separates "the camera was shoved" from "the
              picture is vibrating".
     PUNCH    a zoom pop, fast in and fast out. The cheapest way to make a
              hit land, and the first thing to remove if it ever nauseates.

   Trauma is driven by summed sines rather than by rand(): random per frame
   reads as a broken cable, and two irrational frequencies read as a heavy
   thing rocking. And still only the BOARD moves — the HUD is DOM and stays
   nailed down, which is what lets the impacts be as big as they are.
   ============================================================ */
/* ============================================================
   TIME — the juice nothing else can fake.

   Shake, flash and debris all say "that was big" AFTER the fact. Stopping
   the clock says it AT the fact, and it is the single largest difference
   between a game that feels good and a game that has effects on it. Two
   tools, and they are not the same tool:

     HITSTOP  the world stops dead for 40-140ms and the frame keeps being
              drawn. The eye reads the freeze as impact, and — this is the
              part people miss — the shake that was already running resumes
              on the far side of it, so the picture lurches out of the stop
              rather than easing out. Every hit in this game gets one, sized
              to the hit.

     SLOWMO   the clock runs at a fraction and eases back. Reserved for the
              two moments that are ABOUT something rather than just loud:
              the plate turning the cube inside out, and walking out.

   The loop takes real milliseconds and hands the game scaled ones. Input is
   event-driven and never scaled, so a frozen frame still takes your thumb.
   ============================================================ */
var freezeT = 0, timeScale = 1, slowT = 0, slowDur = 1, slowFrom = 1;
function hitstop(ms){ freezeT = Math.max(freezeT, ms * fxAmt()); }
function slowmo(scale, ms){ slowFrom = scale; slowT = 0; slowDur = ms; timeScale = scale; }
function stepTime(raw){
  if(slowT < slowDur){
    slowT += raw;
    var p = Math.min(1, slowT/slowDur);
    timeScale = slowFrom + (1 - slowFrom) * (p*p);
  } else timeScale = 1;
  if(freezeT > 0){ freezeT -= raw; return 0; }
  return raw * timeScale;
}

var CAM = {ix:0, iy:0, imag:0, it:1, idur:1, tr:0, punch:0, clock:0, sqx:0, sqy:0, sqt:1};
/* One dial for every effect in the file, so "less of this, please" is a
   switch rather than a rebuild. Nothing turns OFF at 0.4 — a game that stops
   answering the hand is worse than a game that moves too much — it is the
   same beats, quieter. */
function fxAmt(){ return store.fx ? 1 : 0.4; }
function kick(mag, dx, dy){
  CAM.imag = mag * fxAmt(); CAM.ix = dx; CAM.iy = dy; CAM.it = 0; CAM.idur = 380;
}
function shake(amt){ CAM.tr = Math.min(1, CAM.tr + amt * fxAmt()); }
function punch(amt){ CAM.punch = Math.min(0.10, CAM.punch + amt * fxAmt()); }
/* SQUASH AND STRETCH, on the whole board. A cube that has just been slammed
   through ninety degrees should be wider than it is tall for a few frames
   and then overshoot the other way — it is the oldest trick in animation and
   the reason a landing reads as mass rather than as a transition ending. The
   axis comes from the turn, so a yaw squashes horizontally and a pitch
   squashes vertically, and the board deforms along the direction the force
   actually came from. */
function squash(ax, amt){
  amt *= fxAmt();
  CAM.sqx = ax === 'x' ? amt : -amt*0.62;
  CAM.sqy = ax === 'x' ? -amt*0.62 : amt;
  CAM.sqt = 0;
}
function stepCam(dt){
  CAM.clock += dt;
  if(CAM.it < 1) CAM.it = Math.min(1, CAM.it + dt/CAM.idur);
  CAM.tr = Math.max(0, CAM.tr - dt/820);
  CAM.punch *= Math.pow(0.0025, dt/1000);
  if(CAM.sqt < 1) CAM.sqt = Math.min(1, CAM.sqt + dt/480);
}
function camState(){
  var t = CAM.clock/1000, tr = CAM.tr*CAM.tr;
  var o = {x:0, y:0, rot:0, zoom:1 + CAM.punch};
  if(CAM.it < 1){
    var p = CAM.it, damp = Math.pow(1 - p, 2.2), w = Math.sin(p * Math.PI * 5.5) * damp;
    o.x += CAM.ix * CAM.imag * w; o.y += CAM.iy * CAM.imag * w;
  }
  if(tr > 0.0002){
    var A = 24 * tr;
    o.x += (Math.sin(t*37.1) + Math.sin(t*61.7)*0.6) * A * 0.6;
    o.y += (Math.sin(t*43.3 + 1.7) + Math.sin(t*71.9)*0.6) * A * 0.6;
    o.rot += Math.sin(t*29.7 + 0.9) * 0.020 * tr;
    o.zoom += tr * 0.010;
  }
  /* the squash rings down as a damped sine rather than easing to zero, so it
     overshoots into a stretch and back — one bounce you can see, two you can
     feel, and nothing after that */
  o.sx = 1; o.sy = 1;
  if(CAM.sqt < 1){
    var q = CAM.sqt, w = Math.sin(q * Math.PI * 2.6) * Math.pow(1-q, 2.4);
    o.sx = 1 + CAM.sqx * w; o.sy = 1 + CAM.sqy * w;
  }
  return o;
}
/* Everything drawn between these two is drawn THROUGH the camera, main
   canvas and glow buffer alike — they must agree to the pixel or the bloom
   detaches from the thing that is glowing. */
function camBegin(){
  var c = camState();
  ctx.save();
  ctx.translate(CX, CY); ctx.rotate(c.rot); ctx.scale(c.zoom*c.sx, c.zoom*c.sy);
  ctx.translate(-CX + c.x, -CY + c.y);
  if(gx){
    gx.save();
    gx.translate(CX, CY); gx.rotate(c.rot); gx.scale(c.zoom*c.sx, c.zoom*c.sy);
    gx.translate(-CX + c.x, -CY + c.y);
  }
}
function camEnd(){ ctx.restore(); if(gx) gx.restore(); }

/* ============================================================
   THE PEEK — hold, and the cube turns to glass.

   This game hides things on purpose. A key can sit behind bedrock in an axis
   you are not looking down, and the manual's answer is "turn" — which is
   correct, and which is also the moment a new player decides the game is
   being unfair rather than that they have not looked yet.

   So: press and hold anywhere and the solid opens. It leans into a
   three-quarter view, every block fades to a lit sheet of glass, the edges
   come up as a wireframe, and every key, door, plate and exit in the cube
   floats in front of it wherever it actually is. Let go and it slams shut.

   THE RULES THIS OBEYS, because a see-through mode in a hidden-information
   game is a design decision and not a filter:

     it shows you GEOMETRY, never a solution — the same facts a turn would
       have shown you, gathered into one pose instead of four;
     it costs a hold, and you cannot act during one. Nothing can be tapped,
       nothing can be turned, no state changes. It is looking, not playing;
     it is never a mode. There is no button, no toggle and nothing to
       remember to switch off — it exists exactly as long as your thumb does.

   Which is what makes it juice as well as information: it is the one gesture
   in the game where the whole cube moves at once, and it is worth holding
   just to watch it happen.
   ============================================================ */
/* THE PEEK IS A CAMERA, NOT A POSE.

   It opened to one fixed three-quarter lean, which answers "what is behind
   that block" only if the thing you wanted happened to be on the side the
   lean chose. So the hold now carries its own orbit: keep holding and drag,
   and the cube turns under your thumb in both axes, as far as you like,
   with no turn committed and no state touched.

   The two gestures cannot collide, and the reason is the order they are
   decided in. Movement BEFORE the hold lands is a turn and cancels the
   peek; movement AFTER it lands is an orbit and can never become a turn,
   because drag.axis is what makes a gesture a turn and the orbit path never
   sets it. Your thumb has already told the game which of the two it meant
   by the time it moves.

   Pitch is clamped a little short of the poles — past that the cube is
   being read end-on, the board's own axes stop meaning anything, and there
   is nothing there worth the gimbal. Yaw is free.

   And the orbit is not stored anywhere. It is multiplied by the same ease
   the peek opens with, so letting go unwinds it in the same 200ms the rock
   takes to come back. There is no camera to reset because there was never a
   camera — only a hold. */
var peek = {on:false, amt:0, yaw:0, pitch:0};
var PEEK_YAW = 0.62, PEEK_PITCH = 0.42, PEEK_MAXP = 1.15;
function peekOn(){
  if(peek.on || demo || won || walking || anim || !lv) return;
  peek.on = true;
  sfx.peek(); buzz(9);
  flash('180,220,255', 0.15, 420);
  ring(CX, CY, {r0:N*S*0.12, r1:N*S*0.66, dur:560, col:'180,220,255', a:0.45, w:2.5, sq:true});
  if(!store.sawPeek){ store.sawPeek = 1; save(); toast('peek — the whole cube, for as long as you hold'); }
}
function peekOff(){
  if(!peek.on) return;
  peek.on = false;
  sfx.peekOff(); punch(0.018); shake(0.10);
}
function stepPeek(dt){
  var to = peek.on ? 1 : 0;
  if(peek.amt === to){
    if(!peek.on && (peek.yaw || peek.pitch)){ peek.yaw = 0; peek.pitch = 0; }
    return;
  }
  var sp = dt / (peek.on ? 240 : 200);
  peek.amt = to > peek.amt ? Math.min(1, peek.amt + sp) : Math.max(0, peek.amt - sp);
  if(!peek.on && peek.amt === 0){ peek.yaw = 0; peek.pitch = 0; }
}
/* smoothstep, so the lean starts and ends at rest — a linear open reads as a
   slide and this has to read as a lid being lifted */
function peekE(){ var p = peek.amt; return p*p*(3 - 2*p); }

/* ---------- THE GLOW BUFFER -----------------------------------------------
   Real bloom, for about the price of the halos it replaces.

   Every glowing thing — halos, sparks, embers, rings, flashes — is drawn a
   second time into an offscreen canvas at a third of the resolution. At the
   end of the frame that canvas is composited back additively, twice, at two
   scales, with SMOOTHING ON. The upscale filter is the blur: no kernel, no
   passes, no readback, and light bleeds past the edges of the thing making
   it, which is the entire visual difference between "coloured shape" and
   "light source".

   A third of the resolution is not a compromise, it is the point — a bloom
   buffer wants to be blurry, so the cheapest version is also the best
   looking one.                                                              */
var glowCv = null, gx = null, GLOW_S = 0.32;
function buildGlow(){
  if(!W || !H) return;
  glowCv = document.createElement('canvas');
  glowCv.width  = Math.max(1, Math.round(W * GLOW_S));
  glowCv.height = Math.max(1, Math.round(H * GLOW_S));
  gx = glowCv.getContext('2d');
}
/* the target for anything that is LIGHT rather than matter: the low-res
   buffer when bloom is on, the frame itself when it is not, so turning
   effects down never turns a marker's glow off entirely */
/* WHEN GLOW MUST OBEY A CLIP. The bloom buffer is composited over the
   whole frame, so anything routed into it escapes whatever the caller
   clipped — which is fine for every marker standing on a block and fatal
   for one being glimpsed through a hole. Turn this on and light draws into
   the frame instead, where the clip can reach it. */
var glowOff = false;
function lightCtx(){ return (!glowOff && store.fx && gx) ? gx : ctx; }
function glowBegin(){
  if(!gx) return;
  gx.setTransform(GLOW_S, 0, 0, GLOW_S, 0, 0);
  gx.clearRect(0, 0, W, H);
}
function glowEnd(){
  if(!gx || !store.fx) return;
  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  ctx.imageSmoothingEnabled = true;
  ctx.globalAlpha = 0.85; ctx.drawImage(glowCv, 0, 0, W, H);
  ctx.globalAlpha = 0.48; ctx.drawImage(glowCv, -W*0.035, -H*0.035, W*1.07, H*1.07);
  ctx.imageSmoothingEnabled = false;
  ctx.restore();
}

/* ---------- THE SMEAR ------------------------------------------------------
   Motion blur, by feeding the frame back into itself.

   A cube being slammed through ninety degrees in a fifth of a second moves
   further between two frames than its own blocks are wide, and every one of
   those frames is perfectly sharp — which is exactly what makes fast motion
   on a 60Hz panel look like a slideshow of poses instead of a movement. The
   fix is not to draw the cube three times: that is triple the cost of the
   most expensive path in the file. It is to keep the LAST frame at half
   resolution and lay it back over the new one additively while the thing is
   moving fast. Bright edges leave trails, the dark board does not, and the
   whole effect is two scaled drawImages.

   It is gated on angular speed, so it exists only during a turn or an orbit
   and costs literally nothing the rest of the time.                        */
var smearCv = null, sx2 = null, smearOn = 0, lastAng = 0, angVel = 0;
function buildSmear(){
  if(!W || !H) return;
  smearCv = document.createElement('canvas');
  smearCv.width = Math.max(1, Math.round(W*0.5));
  smearCv.height = Math.max(1, Math.round(H*0.5));
  sx2 = smearCv.getContext('2d');
}
function stepSmear(dt){
  var a = liveAngle(), cur = a ? a.ang : 0;
  if(peek.amt > 0.01) cur += peek.yaw + peek.pitch;
  var v = dt > 0 ? Math.abs(cur - lastAng)/dt*1000 : 0;
  lastAng = cur;
  angVel = angVel*0.6 + v*0.4;
  /* a hand-paced drag is about 2 rad/s and should stay sharp — you are
     reading the board while you do it. A committed turn is twelve, and that
     is the only thing in the game moving fast enough to need blurring. */
  var want = store.fx ? Math.max(0, Math.min(1, (angVel - 3.0)/5.0)) : 0;
  smearOn = smearOn*0.55 + want*0.45;
}
function drawSmear(){
  if(!sx2 || smearOn < 0.03) return;
  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  /* a quarter, and not a third: the smear is additive, so anything the
     board was already showing bright doubles where the trail crosses it,
     and the deck is the brightest thing on screen by design */
  ctx.globalAlpha = 0.26 * smearOn;
  ctx.imageSmoothingEnabled = true;
  ctx.drawImage(smearCv, 0, 0, W, H);
  ctx.imageSmoothingEnabled = false;
  ctx.restore();
}
var smearHeld = false;
function keepSmear(){
  if(!sx2) return;
  if(smearOn < 0.03){
    /* clear once on the way down, not every frame for the rest of the game */
    if(smearHeld){ sx2.clearRect(0, 0, smearCv.width, smearCv.height); smearHeld = false; }
    return;
  }
  smearHeld = true;
  sx2.globalCompositeOperation = 'copy';
  sx2.drawImage(cv, 0, 0, smearCv.width, smearCv.height);
}

/* ---------- FLASHES AND THE VIGNETTE --------------------------------------
   A flash is not white light on the lens, it is the room the board is in
   being lit for a moment by whatever just happened, so it is coloured by the
   event and it is centred on the board rather than on the screen.

   The vignette is safe to do what it likes, which is worth writing down: it
   is a single smooth gradient over the whole picture, so it darkens any two
   NEIGHBOURING tiles by the same amount. The material read is a comparison
   between neighbours, and a term that cannot separate them cannot break it.
   That is not true of the drop shadows, which is exactly why those are
   capped and this is not.                                                    */
var flashes = [], vig = 0;
function flash(col, amt, dur){
  flashes.push({col:col, a:amt * fxAmt(), t:0, dur:dur || 420});
  if(flashes.length > 6) flashes.shift();
}
function stepFlashes(dt){
  for(var i = flashes.length-1; i >= 0; i--){
    flashes[i].t += dt;
    if(flashes[i].t >= flashes[i].dur) flashes.splice(i, 1);
  }
  vig = Math.max(0, vig - dt/700);
}
function drawFlashes(){
  if(!flashes.length) return;
  var c = lightCtx();
  for(var i = 0; i < flashes.length; i++){
    var f = flashes[i], p = f.t/f.dur, a = f.a * (1-p) * (1-p);
    if(a <= 0.002) continue;
    var r = Math.max(W, H) * (0.55 + p*0.45);
    var g = c.createRadialGradient(CX, CY, 0, CX, CY, r);
    g.addColorStop(0, 'rgba(' + f.col + ',' + a.toFixed(3) + ')');
    g.addColorStop(0.45, 'rgba(' + f.col + ',' + (a*0.42).toFixed(3) + ')');
    g.addColorStop(1, 'rgba(' + f.col + ',0)');
    c.save(); c.globalCompositeOperation = 'lighter';
    c.fillStyle = g; c.fillRect(0, 0, W, H); c.restore();
  }
}
function drawVignette(){
  var g = ctx.createRadialGradient(CX, CY, Math.min(W,H)*0.30, CX, CY, Math.max(W,H)*0.76);
  g.addColorStop(0, 'rgba(0,0,0,0)');
  g.addColorStop(1, 'rgba(0,0,0,' + (0.28 + vig*0.34).toFixed(3) + ')');
  ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
  drawGrain();
}

/* ---------- GRAIN, AND THIS TIME OVER THE WHOLE FRAME ---------------------
   There was a per-tile noise overlay here once and it was deleted for good
   reasons: a 64px noise pattern sitting on top of a 16px block texture is a
   filter smearing the pixels the material is made of, and it fought the very
   thing it was meant to enrich.

   This is the other kind of grain, and the distinction is the whole point.
   That one was MATERIAL — it claimed the stone was rough. This one is the
   IMAGE: it sits over the finished frame, board and sky and interface alike,
   at an amplitude near the limit of what can be seen, and what it does is
   break up the perfectly smooth gradients. A radial vignette on a flat dark
   sky bands on an OLED — visible steps in the falloff — and nothing says
   "rendered in a browser" faster. A film of noise dithers those steps away,
   which is exactly the job grain does on a camera.

   IT MUST BE ONE BLIT, and the first version was thirty-two.

   A 128px tile repeated across the screen looks identical to a single sheet
   and costs thirty-two composited draws a frame. In 'overlay', at three
   device pixels to the CSS pixel, that is three million pixels blended
   through a branchy blend mode every frame — and it did not show up as a
   slow draw() call, it showed up as DROPPED FRAMES, because the cost lands in
   the compositor rather than in the loop that was being timed.

   It was caught by something that had no business noticing: the plate clock
   started losing a fifth of a second of its five to frames longer than the
   64ms dt ceiling. A countdown is a real-time measurement, so it is also an
   accidental frame-health monitor, and it failed the build over a graphics
   change. That is the test suite doing its job from an unexpected direction.

   So the sheet is built once at screen size plus a margin, and each frame
   draws it once at a random whole-pixel offset inside that margin — same
   picture, same movement, one draw.                                        */
var grainCv = null, GRAIN_PAD = 64, grainW = 0, grainH = 0;
function buildGrain(){
  grainW = Math.ceil(W) + GRAIN_PAD; grainH = Math.ceil(H) + GRAIN_PAD;
  grainCv = document.createElement('canvas');
  grainCv.width = grainW; grainCv.height = grainH;
  var g = grainCv.getContext('2d'), id = g.createImageData(grainW, grainH);
  var rng = mulberry32(0x6841);
  for(var i = 0; i < grainW*grainH; i++){
    var o = i*4, v = (rng() + rng() + rng()) / 3;   /* toward gaussian: less salt-and-pepper */
    id.data[o] = id.data[o+1] = id.data[o+2] = 255;
    id.data[o+3] = Math.round(v*255*0.115);
  }
  g.putImageData(id, 0, 0);
}
function drawGrain(){
  if(!store.fx) return;
  if(!grainCv || grainW < W + 1 || grainH < H + 1) buildGrain();
  ctx.save();
  ctx.globalCompositeOperation = 'overlay';
  ctx.globalAlpha = 0.5;
  ctx.drawImage(grainCv, -((Math.random()*GRAIN_PAD)|0), -((Math.random()*GRAIN_PAD)|0));
  ctx.restore();
}

/* ---------- PARTICLES -----------------------------------------------------
   Four kinds, because "particles" is not a look — a thing coming off a stone
   is either matter or light, and matter and light do not move, shade or
   composite the same way.

     CHIP   a knocked-off piece of block. Opaque, tumbling, heavy, drawn as a
            little square with a lit top edge and coloured FROM THE TILE IT
            CAME OFF, which is what sells it as part of the board rather than
            as an effect layer over it.
     SPARK  light. Additive, drawn as a streak along its own velocity rather
            than as a dot, because a dot moving fast is a dot and a streak
            moving fast is speed.
     EMBER  slow light. Rises, flickers, lives a long time. This is what the
            orb sheds.
     RING   an expanding front. Circles for light, SQUARES for anything the
            grid did, because the board is a grid and a square shockwave says
            so without a word.
   ============================================================ */
var parts = [], rings = [];
function partCap(){ return store.fx ? 460 : 130; }
function burst(x, y, n, o){
  o = o || {};
  var cap = partCap(), kind = o.kind || 'spark';
  n = Math.round(n * (store.fx ? 1 : 0.45));
  for(var i = 0; i < n && parts.length < cap; i++){
    var a = o.ang === undefined ? Math.random()*6.284 : o.ang + (Math.random()-0.5)*(o.spread||6.284);
    var sp = (o.spd || 40) * (0.35 + Math.random()*0.9);
    parts.push({x:x, y:y, vx:Math.cos(a)*sp, vy:Math.sin(a)*sp - (o.lift||0),
                life:0, max:(o.life||500)*(0.6+Math.random()*0.7),
                r:(o.r||2)*(0.5+Math.random()), col:o.col || '233,225,209',
                g:(o.g === undefined ? 42 : o.g), fade:o.fade||1, kind:kind,
                rot:Math.random()*6.284, vr:(Math.random()-0.5)*9, drag:o.drag || 0.985});
  }
}
function ring(x, y, o){
  o = o || {};
  if(rings.length > 14) rings.shift();
  rings.push({x:x, y:y, r0:o.r0 || 0, r1:o.r1 || 120, t:0, dur:o.dur || 460,
              col:o.col || '255,255,255', a:(o.a === undefined ? 0.6 : o.a) * fxAmt(),
              w:o.w || 3, sq:!!o.sq});
}
function stepParts(dt){
  var k = dt/1000, i;
  for(i = parts.length-1; i >= 0; i--){
    var p = parts[i];
    p.life += dt;
    if(p.life >= p.max){ parts.splice(i,1); continue; }
    p.x += p.vx*k; p.y += p.vy*k; p.vy += p.g*k;
    p.vx *= p.drag; p.vy *= p.drag;
    if(p.kind === 'chip') p.rot += p.vr*k;
  }
  for(i = rings.length-1; i >= 0; i--){
    rings[i].t += dt;
    if(rings[i].t >= rings[i].dur) rings.splice(i,1);
  }
}
function drawParts(){
  var i, p, t, c = lightCtx();
  /* matter first, under the light */
  if(parts.length){
    ctx.save();
    for(i = 0; i < parts.length; i++){
      p = parts[i];
      if(p.kind !== 'chip') continue;
      t = 1 - p.life/p.max;
      ctx.globalAlpha = Math.max(0, Math.min(1, t*2.2)) * p.fade;
      var s = p.r*1.7*(0.55 + t*0.45);
      ctx.save();
      ctx.translate(p.x, p.y); ctx.rotate(p.rot);
      ctx.fillStyle = 'rgb(' + p.col + ')';
      ctx.fillRect(-s, -s, s*2, s*2);
      ctx.fillStyle = 'rgba(255,255,255,.22)';
      ctx.fillRect(-s, -s, s*2, Math.max(1, s*0.55));
      ctx.restore();
    }
    ctx.restore();
  }
  /* then light, into whichever buffer is doing the glowing */
  if(parts.length){
    c.save(); c.globalCompositeOperation = 'lighter'; c.lineCap = 'round';
    for(i = 0; i < parts.length; i++){
      p = parts[i];
      if(p.kind === 'chip') continue;
      t = 1 - p.life/p.max;
      var fl = p.kind === 'ember' ? (0.65 + 0.35*Math.sin(p.life*0.013 + p.rot)) : 1;
      c.globalAlpha = Math.max(0, t*t*p.fade*fl);
      c.strokeStyle = c.fillStyle = 'rgb(' + p.col + ')';
      if(p.kind === 'spark'){
        var sx = p.x - p.vx*0.022, sy = p.y - p.vy*0.022;
        c.lineWidth = p.r*(0.5 + t*0.9);
        c.beginPath(); c.moveTo(sx, sy); c.lineTo(p.x, p.y); c.stroke();
      } else {
        c.beginPath(); c.arc(p.x, p.y, p.r*(0.4 + t*0.7), 0, 6.284); c.fill();
      }
    }
    c.restore();
  }
  for(i = 0; i < rings.length; i++){
    var g2 = rings[i], q = g2.t/g2.dur, e = 1 - Math.pow(1-q, 3);
    var rr = g2.r0 + (g2.r1 - g2.r0)*e, al = g2.a * (1-q) * (1-q);
    if(al <= 0.003) continue;
    c.save(); c.globalCompositeOperation = 'lighter';
    c.globalAlpha = al; c.strokeStyle = 'rgb(' + g2.col + ')';
    c.lineWidth = g2.w * (1 - q*0.7) + 0.4;
    c.beginPath();
    if(g2.sq) c.rect(g2.x - rr, g2.y - rr, rr*2, rr*2);
    else c.arc(g2.x, g2.y, rr, 0, 6.284);
    c.stroke(); c.restore();
  }
}
/* the colour a chip knocked off this tile should be, so debris always
   belongs to the block it came from */
function chipCol(t, d){
  var c = tileRGB(isWalkType(t) ? '+' : '#', d);
  return (c[0]|0) + ',' + (c[1]|0) + ',' + (c[2]|0);
}

/* ---------- THE BEAM ------------------------------------------------------
   One shaft of light, in the whole game, at the one moment that has earned
   it: the square you just walked out of. It is drawn in board space so it
   shakes with the board, and it is the only effect allowed to leave the
   cube's silhouette. */
var beam = null;
function stepBeam(dt){ if(beam){ beam.t += dt; if(beam.t > beam.dur) beam = null; } }
function drawBeam(){
  if(!beam) return;
  var p = beam.t/beam.dur, c = lightCtx();
  var w = S*0.85 * (1 - p*0.4) * Math.min(1, p*7), a = (1-p)*(1-p)*0.6;
  if(a <= 0.004 || w <= 0) return;
  var g = c.createLinearGradient(beam.x, beam.y - H, beam.x, beam.y + S*0.5);
  g.addColorStop(0, 'rgba(255,196,77,0)');
  g.addColorStop(0.70, 'rgba(255,214,120,' + (a*0.5).toFixed(3) + ')');
  g.addColorStop(1, 'rgba(255,242,214,' + a.toFixed(3) + ')');
  c.save(); c.globalCompositeOperation = 'lighter';
  c.fillStyle = g; c.fillRect(beam.x - w, beam.y - H, w*2, H + S*0.5);
  c.restore();
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
      burst(r.left+r.width*0.32, r.top+r.height/2, 10, {spd:70, life:380, r:1.8, col:'34,224,106', g:0});
    }
  }
}
function drawFlyers(){
  for(var i = 0; i < flyers.length; i++){
    var f = flyers[i], p = f.t/f.dur, e = p*p*(3-2*p);
    var x = f.x0 + (f.x1-f.x0)*e, y = f.y0 + (f.y1-f.y0)*e - Math.sin(p*Math.PI)*70;
    halo(x, y, 24, '34,224,106', 0.5*(1-p*0.5));
    ctx.save(); ctx.fillStyle = '#22e06a';
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
  plateT = world ? PLATE_MS : 0;          /* the flipped cube is on a clock */
  clearEff(lv);
  surf = project(N, effVox(lv, world), M);
  flip = {t:0, fu:v[0], fv:v[1], old:old, col:PLATE_COL[bit], dur:520, stagger:30, rise:150};
  computeReach(); refreshHud();
  revealT = -260;                       /* the reachable sweep follows the front */
  kick(13, 0, 1);
  impact = 1;
  /* THE BIGGEST EVENT IN THE GAME, and the only one that changes the board
     rather than the view of it. Two square fronts leave the plate at
     different speeds, the room floods with the plate's own colour, and the
     trauma is the largest number this file hands out. */
  /* the flash is deliberately short of a white-out: the whole point of the
     flip is that the old material is SEEN leaving, and a frame that blows
     out for a quarter of a second is a frame nobody got to watch */
  faceSet('wide', 700);
  shake(0.95); punch(0.075); flash(PLATE_COL[bit], 0.24, 560);
  /* the world turning inside out is worth a held breath: a long stop, then
     a third of speed easing back over most of a second */
  hitstop(120); slowmo(0.34, 620); squash('x', 0.09);
  ring(cx, cy, {r0:S*0.2, r1:N*S*1.05, dur:760, col:PLATE_COL[bit], a:0.75, w:6, sq:true});
  ring(cx, cy, {r0:S*0.2, r1:N*S*0.62, dur:520, col:'240,240,255', a:0.5, w:3, sq:true});
  sfx.plate(bit); buzz(34);
  burst(cx, cy, 30, {spd:170, life:780, r:2.6, col:PLATE_COL[bit], g:0});
  burst(cx, cy, 14, {spd:56, life:1000, r:3.6, col:'240,240,255', g:-16});
  burst(cx, cy, 16, {kind:'chip', spd:150, life:900, r:2.2, col:chipCol('#', N-1), g:300});
  toast(world ? (PLATE_NAME[bit] + ' — five seconds') : 'restored');
}
function stepFlip(dt){
  if(!flip) return;
  flip.t += dt;
  if(flip.t > flip.dur + N*flip.stagger + flip.rise) flip = null;
}

/* ---------- FIVE SECONDS, AND THEN THE FLOOR GOES ------------------------
   The flipped cube does not stay flipped. Five seconds after a plate fires
   the material springs back to how it was carved, and whatever you were
   standing on goes back to being whatever it was — which for most of the
   board means going back to being rock, or nothing at all. So you are not
   left standing in it: you are thrown to the nearest square that is still a
   square.

   WHY THIS AND NOT A LONGER PLATE PUZZLE. The plate was already the biggest
   verb in the game and it had no cost attached — you flipped, you looked
   around, you thought about it, you flipped back. Every decision it created
   could be made at leisure, which meant the second world was a second board
   rather than a second STATE, and boards are things you study. A clock turns
   the flip from a place you visit into a thing you commit to: you look at
   the inverted cube BEFORE you press it, because afterwards there is only
   time to walk the line you already planned. The plate stops being a door
   and starts being a held breath.

   THE CLOCK RUNS ON GAME TIME, not wall time. The flip itself spends a
   hitstop and most of a second of slow motion, and a player cannot act
   during either — charging them for frames they were not allowed to move in
   would be taking the time back with the other hand. dt is already the
   scaled clock the rest of the file animates on, so the five seconds are
   five seconds of being ABLE TO PLAY.

   IT DOES NOT STOP FOR A PEEK. Holding to look inside the cube is thinking,
   and thinking is the thing being rationed. A peek that paused the clock
   would just be the old untimed plate with an extra button held down.

   IT DOES STOP FOR A CARD. Pause, the manual and the win screen are not
   play, and a clock that ran behind a menu would be a game that punished
   putting the phone down.                                                  */
var PLATE_MS = 5000, plateT = 0;
function screenUp(){ return !$('hud').classList.contains('on'); }
function stepPlateClock(dt){
  if(!world || !lv || won || demo || screenUp()) return;
  /* AN EXPIRY WAITS OUT A TURN IN FLIGHT. A turn is a third of a second of
     the board rotating toward a landing that was checked against the world
     it started in; reverting the material half way through would resolve it
     against a cube that no longer exists. So the clock is allowed to sit at
     zero for the tail of a rotation, which costs the player nothing they
     could have spent and costs the state machine one whole class of bug. */
  if(plateT <= 0){
    if(!anim && !drag) revertWorld();
    return;
  }
  var was = plateT;
  plateT = Math.max(0, plateT - dt);
  /* one tick a second over the last three, so the count can be HEARD while
     the eyes are on the board — where they have to be, and where the number
     is not */
  var lo = Math.ceil(plateT/1000);
  if(lo !== Math.ceil(was/1000) && lo > 0 && lo <= 3)
    tone(880 + (3-lo)*110, 0.09, 'triangle', 0.055, 0, 0.14);
}

/* THE SPRING BACK. Everything firePlate does, aimed the other way: the same
   outward front from the same square, the same reprojection, and then the
   one thing a plate never has to do — deal with a player who is now inside
   the rock.

   It takes an undo point FIRST. The revert is the only thing in the game
   that moves you without you having asked, so the state you were in when it
   landed has to be recoverable, or a five-second limit would be a
   five-second trap. Undo puts you back one instant before the expiry, with
   the clock reset — that is deliberately generous, and it is the difference
   between the timer being a challenge and the timer being a punishment. */
function revertWorld(){
  var v = viewOf(N, M, pos), r = tileRect(v[0], v[1]);
  var cx = r[0] + S/2, cy = r[1] + S/2;
  pushUndo();
  var old = surf;
  world = 0;
  plateT = 0;
  /* any queued route dies with the world it was planned in — the same reason
     pathTo() refuses to route THROUGH a plate */
  walking = null;
  clearEff(lv);
  surf = project(N, effVox(lv, world), M);
  flip = {t:0, fu:v[0], fv:v[1], old:old, col:'255,120,120', dur:520, stagger:30, rise:150};
  revealT = -260;
  sfx.plate(2); buzz(30);
  shake(0.7); punch(0.055); flash('255,120,120', 0.20, 460);
  hitstop(90); slowmo(0.42, 520); squash('y', 0.08);
  ring(cx, cy, {r0:S*0.2, r1:N*S*1.05, dur:700, col:'255,120,120', a:0.7, w:5, sq:true});
  burst(cx, cy, 22, {kind:'chip', spd:150, life:900, r:2.2, col:chipCol('#', N-1), g:300});
  throwToPlate();
  settle();
  afterAction();
}

/* PUT BACK ON A PLATE, NOT DROPPED WHERE YOU STOOD.

   This is the whole reason the clock is survivable, and getting it wrong the
   first time made 39 of the 41 plate cubes unfinishable.

   On almost every cube carrying a plate, THE EXIT DOES NOT EXIST IN WORLD 0.
   It is carved into cells that are rock until the material inverts, which is
   the entire point of the second verb — the way out is only in one of the two
   cubes. So a spring-back that dropped you on the nearest walkable SQUARE was
   dropping you into the world where the level cannot be won, several moves
   from the only thing that could put you back. That reads exactly as it
   played: gates and an exit sitting on stone that you cannot reach, forever.

   So the spring-back returns you to the nearest PLATE you can stand on. A
   plate is exempt from the material flip and is always the surface of its
   column, so one always qualifies — there is no world in which the thing that
   gets you back out is itself out of reach. Running out of time costs you the
   walk, not the level: you are put back at the pivot, and you go again.

   Nearest on SCREEN, not through the cube. The flat face is what the player
   was reading, so two columns left has to mean two columns left as seen; a
   nearest measured through the solid could land you on the far side of the
   world, and that reads as teleportation rather than as falling.

   You are only moved if you have to be — if you were standing on the plate
   when the clock ran out, nothing happens at all.

   LANDING ON A PLATE DOES NOT FIRE IT. Same rule as turning onto one: you
   press a plate with a foot, and being put on one is not pressing it.
   Otherwise the spring-back would flip the world straight back and start a
   clock nobody asked for. Tapping the plate under you is how you press it
   again — see tapCell().                                                   */
function throwToPlate(){
  var v = viewOf(N, M, pos), here = surfaceAt(N, surf, M, pos);
  var onPlate = here && walkable(lv, surf, here[0], here[1], doors)
                     && isGlyph(surf[here[0]*N + here[1]].t);
  if(onPlate) return false;
  var best = null, bd = 1e9, u, w, cell, d;
  for(u = 0; u < N; u++) for(w = 0; w < N; w++){
    cell = walkable(lv, surf, u, w, doors);
    if(!cell || !isGlyph(cell.t)) continue;
    d = (u - v[0])*(u - v[0]) + (w - v[1])*(w - v[1]);
    if(d < bd){ bd = d; best = cell; }
  }
  /* a cube can only be in a flipped world if it has a plate in it, so the
     search above cannot come up empty in practice. The fallback is the old
     behaviour — nearest square of any kind — because a marker with nowhere to
     stand would be a crash, and a crash is worse than a bad landing. */
  if(!best){
    if(here && walkable(lv, surf, here[0], here[1], doors)) return false;
    for(u = 0; u < N; u++) for(w = 0; w < N; w++){
      cell = walkable(lv, surf, u, w, doors);
      if(!cell) continue;
      d = (u - v[0])*(u - v[0]) + (w - v[1])*(w - v[1]);
      if(d < bd){ bd = d; best = cell; }
    }
  }
  if(!best) return false;
  pos = best.w.slice();
  var pv = viewOf(N, M, pos), pr = tileRect(pv[0], pv[1]);
  var px = pr[0] + S/2, py = pr[1] + S/2;
  hopT = 0; faceSet('wide', 620);
  toast('back to the plate — press it again');
  sfx.deny(); buzz(40);
  shake(0.42); kick(9, 0, 1);
  ring(px, py, {r0:S*1.6, r1:S*0.34, dur:380, col:'255,120,120', a:0.8, w:3.5, sq:true});
  burst(px, py, 14, {kind:'chip', spd:120, life:700, r:2.0, col:chipCol('#', N-1), g:320});
  return true;
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
/* an additive halo, so the things that mean something glow rather than
   merely being coloured differently. It paints into the glow buffer now, at
   a third of the resolution — which costs a ninth of what it used to and
   comes back through the bloom composite wider and softer than a full-res
   gradient could be. */
function halo(x, y, r, col, a){
  var c = lightCtx();
  var g = c.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, 'rgba(' + col + ',' + a + ')');
  g.addColorStop(0.5, 'rgba(' + col + ',' + (a*0.32).toFixed(3) + ')');
  g.addColorStop(1, 'rgba(' + col + ',0)');
  c.save(); c.globalCompositeOperation = 'lighter';
  c.fillStyle = g; c.fillRect(x-r, y-r, r*2, r*2); c.restore();
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
  var col = (ST.dN[0]|0) + ',' + (ST.dN[1]|0) + ',' + (ST.dN[2]|0);
  var a2 = alpha + impact*0.5, lw = 1.25 + impact*1.6;
  ctx.save();
  ctx.strokeStyle = 'rgba(' + col + ',' + a2 + ')';
  ctx.lineWidth = lw; ctx.lineJoin = 'round';
  ctx.beginPath();
  for(var e = 0; e < 12; e++){
    ctx.moveTo(pt[CAGE_E[e][0]][0], pt[CAGE_E[e][0]][1]);
    ctx.lineTo(pt[CAGE_E[e][1]][0], pt[CAGE_E[e][1]][1]);
  }
  ctx.stroke(); ctx.restore();
  /* twelve lines is nothing, so the cage is drawn a second time into the
     glow buffer: the box the whole game happens inside should read as lit
     wire rather than as a hairline, and it is the one edge that must survive
     a shake, a flash and a peek all at once */
  var lc = lightCtx();
  if(lc !== ctx){
    lc.save(); lc.globalCompositeOperation = 'lighter';
    lc.strokeStyle = 'rgba(' + col + ',' + Math.min(1, a2*0.9 + peekE()*0.5) + ')';
    lc.lineWidth = lw + 1.4; lc.lineJoin = 'round';
    lc.beginPath();
    for(var e2 = 0; e2 < 12; e2++){
      lc.moveTo(pt[CAGE_E[e2][0]][0], pt[CAGE_E[e2][0]][1]);
      lc.lineTo(pt[CAGE_E[e2][1]][0], pt[CAGE_E[e2][1]][1]);
    }
    lc.stroke(); lc.restore();
  }
}
function tileRect(u, v, z){
  z = z || 1;
  return [CX + (u - N/2)*S*z, CY + (N/2 - 1 - v)*S*z, S*z, S*z];
}

/* IS THIS THING IN THIS WORLD? — and if so, where on screen.

   A key, a gate and the way out are carved into cells, and a plate rewrites
   what those cells are MADE of. On almost every cube that carries a plate,
   the exit is cut into material that is rock until the flip: it exists, it is
   the surface of its column, and it cannot be stood on until the cube turns
   inside out.

   Those used to be drawn anyway, which was the single most misleading thing
   on the board. A gate sitting on stone with no way to reach it does not read
   as "not in this world yet" — it reads as a broken game, and a player is
   right to read it that way, because nothing on screen distinguishes it from
   one they simply have not found the route to. So it is not drawn at all. It
   arrives when the flip arrives, which is also the moment it becomes true.

   THE TEST IS THE MATERIAL, NOT THE ROUTE. isWalkType() asks what the cell is
   made of in the world you are in; it says nothing about whether you can get
   there from where you stand. That distinction is load-bearing: showing you a
   way out you cannot walk to from this face IS the game — it is the whole of
   what the first two cubes teach. Hiding it because it is far away would
   delete the lesson. Hiding it because it is not made of floor yet is the
   opposite: it stops the board claiming something that is not so.

   walkable() would have been the wrong test for the same reason in the other
   direction — it refuses SHUT doors, so every locked gate in the game would
   have vanished, which is precisely the thing you most need to see.        */
function inThisWorld(w){
  return !!lv && isWalkType(effType(lv.vox[vidx(N, w[0], w[1], w[2])], world));
}
function shownAt(cell){
  if(!inThisWorld(cell)) return null;
  var v = surfaceAt(N, surf, M, cell);
  if(!v) return null;
  var s = surf[v[0]*N + v[1]];
  return (s && isWalkType(s.t)) ? v : null;
}

/* THE ORDER OF A FRAME, and every line of it is a decision:

     sky            behind everything, parallaxed, never shaken
     [camera on]
     board          blocks, markers, you
     particles      matter and light, in the board's own space so debris
                    shakes with the thing it came off
     [camera off]
     flyers, motes  screen-space furniture
     vignette       one smooth gradient, cannot separate two neighbours
     bloom          the glow buffer, blurred by being small
     flashes        already inside the bloom, so they bleed like light

   The sky is outside the camera on purpose: shaking the starfield along with
   the board would say the UNIVERSE moved, and the whole point of a shake is
   that something inside it did.                                             */
function draw(){
  ctx.clearRect(0, 0, W, H);
  glowBegin();
  if(!lv) return;
  /* the sky first, parallaxed off the view basis: turn the cube and the
     stars slide, which is the cheapest way to say the WORLD moved */
  if(skyCv){
    var so = skyOffset();
    /* the one image on screen that is NOT made of blocks: the parallax lands
       on fractional offsets, and nearest-neighbour would make the starfield
       crawl a whole pixel at a time as the cube turns */
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(skyCv, so[0], so[1], W + PAD*2, H + PAD*2);
    ctx.imageSmoothingEnabled = false;
  }
  /* the camera moves the BOARD, never the HUD — a shaken readout is an
     unreadable one, and the DOM chrome staying nailed down is what lets the
     impacts be as big as they are */
  camBegin();
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
  if(demo || peek.amt > 0.001 || (a && Math.abs(a.ang) > 1e-4)) draw3d();
  else drawFlat();
  drawBeam();
  drawParts();
  camEnd();
  drawFlyers();
  drawMotes();
  drawVignette();
  if(exitFlash > 0){
    var c1 = lightCtx();
    c1.save(); c1.globalCompositeOperation = 'lighter';
    c1.globalAlpha = exitFlash * 0.5;
    var eg = c1.createRadialGradient(CX, CY, 0, CX, CY, Math.max(W,H)*0.7);
    eg.addColorStop(0, 'rgba(255,196,77,0.5)'); eg.addColorStop(1, 'rgba(255,196,77,0)');
    c1.fillStyle = eg; c1.fillRect(0,0,W,H); c1.restore();
  }
  if(impact > 0){
    var c2 = lightCtx();
    c2.save(); c2.globalCompositeOperation = 'lighter';
    var r = Math.max(W, H)*0.62, g = c2.createRadialGradient(CX, CY, N*S*0.30, CX, CY, r);
    g.addColorStop(0, 'rgba(' + (ST.dN[0]|0) + ',' + (ST.dN[1]|0) + ',' + (ST.dN[2]|0) + ',' + (impact*0.12).toFixed(3) + ')');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    c2.fillStyle = g; c2.fillRect(0, 0, W, H); c2.restore();
  }
  drawSmear();
  drawFlashes();
  glowEnd();
  keepSmear();
}

function drawFlat(){
  var u, v, s, r, i, SF = curSurf(), BM = baseBasis();
  if(!SF) return;
  /* where the lamp is, in cell coordinates, tracked through a walk so the
     light slides across the floor with you instead of jumping a cell at a
     time. The attract cube has nobody holding it, so it has no lamp. */
  if(demo || won){ lightU = -99; }
  else {
    var lp = playerScreen();
    lightU = (lp[0] - CX - S/2)/S + N/2;
    lightV = N/2 - 1 - (lp[1] - CY - S/2)/S;
  }
  buildDrops();
  drawCage(BM, 1, 0.16, 0);
  drawContact(SF);                    /* under everything, seen only in the gaps */
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
      drawBlock(os.t, os.d, orr[0] + S*(1-osc)/2, orr[1] + S*(1-osc)/2, S*osc + 0.6, S*osc + 0.6);
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
      drawBlock(s.t, s.d, ix, iy, S*sc + 0.6, S*sc + 0.6);
      ctx.globalAlpha = 1;
      continue;
    }
    /* The white inset that used to mark a walkable tile is gone with the
       flat fills: the deck is laid stone or boards and the bedrock is
       rubble, so "may I stand there" is now answered twice over — by the
       brightness band, and by whether the block looks made.

       AND THE ORB ACTUALLY LIGHTS THE FLOOR. Not as an additive layer over
       the board — that was tried, and it lifts bedrock as hard as it lifts
       deck, which eats the one separation the game is read off. The light
       goes in at the SOURCE instead: the tile is tinted a few percent
       brighter before its texture is built, and only ever if it is a deck.
       Bedrock next to you stays dark, the light falls where you could walk,
       and the band guarantee never even hears about it. */
    drawBlock(s.t, s.d, r[0], r[1], r[2] + 0.6, r[3] + 0.6, deckLight(u, v, s.t));
  }
  if(anyWave){ drawWaveExtras(); return; }
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
      ctx.globalAlpha = Math.min(MAX_SHADOW/0.45, 0.09 + dd*0.05);
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
      /* WITH THE CORNERS ROUNDED, THE SEAM IS ALREADY DRAWN. Every block
         now ends in sky, so a hard black rule between two of them is a
         second, uglier answer to a question the shape has already settled.
         It survives only as a whisper, and only where the depth actually
         steps. */
      ctx.strokeStyle = 'rgba(0,0,0,' + Math.min(0.34, 0.08 + ad*0.06).toFixed(2) + ')';
      ctx.lineWidth = Math.min(2.2, 1 + ad*0.3);
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
    var brth = 0.85 + 0.15*Math.sin(Date.now()/560 + rk);
    ctx.strokeStyle = 'rgba(255,106,61,' + (0.42*lit*brth).toFixed(3) + ')';
    ctx.strokeRect(r[0] + S*0.07, r[1] + S*0.07, S*0.86, S*0.86);
    if(lit < 1){
      ctx.save(); ctx.globalCompositeOperation = 'lighter';
      ctx.globalAlpha = (1-lit)*0.5;
      ctx.fillStyle = 'rgba(255,106,61,1)';
      ctx.fillRect(r[0] + S*0.07, r[1] + S*0.07, S*0.86, S*0.86);
      ctx.restore();
      /* THE FRONT ITSELF, not just the tiles behind it. The sweep is the
         connectivity graph being traced, so the moment it arrives at a tile
         that tile throws a spark and the eye follows the edge of the
         answer outward instead of watching a region fill in. */
      if(lit < 0.30){
        halo(r[0] + S/2, r[1] + S/2, S*0.85, '255,140,80', 0.30*(1-lit));
        if(Math.random() < 0.22)
          burst(r[0] + S*(0.2 + Math.random()*0.6), r[1] + S*(0.2 + Math.random()*0.6), 1,
                {spd:26, lift:20, life:520, r:1.5, col:'255,170,110', g:-6, fade:0.8});
      }
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
    var dv = shownAt(lv.doors[i]); if(!dv) continue;
    var dr = tileRect(dv[0], dv[1]);
    drawDoor(dr[0] + S/2, dr[1] + S/2, S, doorT[i] || 0);
  }
  for(i = 0; i < lv.keys.length; i++){
    if(kmask & (1<<i)) continue;
    var kv = shownAt(lv.keys[i]); if(!kv) continue;
    var kr = tileRect(kv[0], kv[1]);
    drawKey(kr[0] + S/2, kr[1] + S/2, S);
  }
  var gv = shownAt(lv.goal);
  if(gv){ var grr = tileRect(gv[0], gv[1]); drawGoal(grr[0] + S/2, grr[1] + S/2, S); }
  /* 5b. ONE STEP AWAY. A short lit bridge from the tile you occupy to any
     walkable neighbour that is the goal, a key, or a door — the same
     information nearSpecials() gathers for the 3D view, drawn here as a
     plain glowing line between two tile centres. */
  var nsFlat = nearSpecials();
  if(nsFlat.length){
    var p0f = playerScreen();
    ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineCap = 'round';
    for(i = 0; i < nsFlat.length; i++){
      var nf = nsFlat[i], nr = tileRect(nf.u2, nf.v2);
      var ncol = nf.kind === 2 ? S_KEY : nf.kind === 4 ? S_EXIT : '53,215,161';
      var tx = nr[0] + S/2, ty = nr[1] + S/2;
      var g4 = ctx.createLinearGradient(p0f[0], p0f[1], tx, ty);
      g4.addColorStop(0, 'rgba(206,244,255,0.55)');
      g4.addColorStop(1, 'rgba(' + ncol + ',0.85)');
      ctx.strokeStyle = g4;
      ctx.lineWidth = Math.max(2, S*0.06);
      ctx.beginPath(); ctx.moveTo(p0f[0], p0f[1]); ctx.lineTo(tx, ty); ctx.stroke();
      halo(tx, ty, S*0.6, ncol, 0.30);
    }
    ctx.restore();
  }
  /* 6. you */
  var p = playerScreen();
  drawPlayer(p[0], p[1], S);
}

/* While the board is assembling or coming apart, only the two things that
   are people-shaped stay on screen. Everything else is scenery and scenery
   does not need to be there for the transition. */
function drawWaveExtras(){
  var gv = surfaceAt(N, surf, M, lv.goal);
  if(gv && wave.mode === 'out'){ var gr2 = tileRect(gv[0], gv[1]); drawGoal(gr2[0] + S/2, gr2[1] + S/2, S); }
  if(wave.mode === 'in'){ var p = playerScreen(); drawPlayer(p[0], p[1], S); }
}
/* YOU HOP. You do not slide.

   A marker travelling in a straight line between two cells at a constant
   speed is a cursor being moved. The same distance covered as an ARC —
   leaving the ground, rising, and arriving with a little squash — is a
   thing that is alive, and it costs one sine and one parabola. Everything
   in this file that reads as charm rather than as chrome comes from this
   kind of substitution.

   The hop height is a fraction of a cell, not a leap: you are crossing a
   block, not vaulting one, and a jump big enough to be admired is a jump
   that is in the way by the fortieth time you see it.                     */
var hopT = 1;
function playerScreen(){
  var v = viewOf(N, M, pos), r = tileRect(v[0], v[1]);
  var x = r[0] + S/2, y = r[1] + S/2;
  if(walking){
    var pv = viewOf(N, M, walking.from), pr = tileRect(pv[0], pv[1]);
    var t = walking.t;
    /* along the ground, ease out; through the air, a clean parabola */
    var e = 1 - Math.pow(1 - t, 2.1);
    x = (pr[0] + S/2) + (x - pr[0] - S/2)*e;
    y = (pr[1] + S/2) + (y - pr[1] - S/2)*e - Math.sin(t*Math.PI) * S*0.26;
  } else if(hopT < 1){
    /* and the landing settles: one small bounce out of the arrival */
    y -= Math.abs(Math.sin(hopT*Math.PI*1.6)) * Math.pow(1-hopT, 2) * S*0.10;
  }
  return [x, y];
}

/* YOU ARE THE ONLY LIGHT IN THE CUBE.

   Everything else on this board is matter: laid stone, rubble, a slab with a
   mechanism cut into it, all of it painted from a 16x16 texture at the
   resolution the world is built at. You are not matter, and drawing you as
   another blocky object would have lost the one thing the marker has to do,
   which is be found instantly on a field of two hundred blocks.

   So you are an orb, and the whole read is the contrast: the single smooth,
   self-lit, hovering thing in a room made of pixels. It costs nothing to
   find and it cannot be mistaken for a tile, a plate or a door.

   Four passes, and each is doing a job:
     a POOL      the light you throw onto the block underneath, warm and wide
     a CONTACT   a soft dark seat under the pool, so a bright deck cannot
                 swallow the orb — additive light on near-white stone is how
                 a glow becomes a white smudge, and this is the fix
     the BODY    an additive sphere, hot core offset up and left toward the
                 same fixed light the solid is shaded by
     a RIM       one thin bright ring, which is what makes it read as a
                 sphere rather than as a blur

   It breathes on two frequencies that do not divide into each other, so the
   pulse never looks like a loop, and it rides a slow bob — a thing that
   hovers is alive and a thing that sits is furniture.                      */
/* ============================================================
   THE FACE — the single most Nintendo thing in this file.

   A glowing ball is an effect. A glowing ball with two eyes is a CHARACTER,
   and the whole difference in how a player talks about the thing they are
   moving is on the far side of that line: it stops being "the marker" and
   becomes "him". Kirby is a circle with eyes. Boo is a circle with eyes. It
   is the cheapest personality in the medium and nobody has ever bettered it.

   The rules I gave it, which are the ones that keep it charming rather than
   cloying:

     IT LOOKS WHERE IT IS GOING. Pupils lead the movement — they shift a
     fraction of an eye-width toward travel and drift back at rest. Eyes
     that never move are a sticker.

     IT BLINKS ON ITS OWN CLOCK, at an irregular interval, because a blink
     on a metronome is a machine and a blink you cannot predict is alive.

     IT REACTS, and only to things that happened to it: a squeeze of the
     eyes when it lands, wide when a turn is refused, delighted arcs when a
     key is taken or the exit is reached, worried when the cube is dead.
     Every expression is an EVENT with a duration, not a state — it plays
     and it returns to neutral, so the face is never stuck.

     IT LOOKS AROUND WHEN YOU DO NOTHING. Four seconds of no input and it
     glances off to one side, and later the other. The player has stopped
     to think and the game has not stopped with them.

     AND IT NEVER OBSTRUCTS. The eyes sit in the upper half of the sphere,
     they are small, and the expression never changes the silhouette — this
     is still the marker you find at a glance on a board of forty-nine
     blocks, and that job outranks the performance.
   ============================================================ */
var face = {mood:'', t:0, dur:0, blink:0, nextBlink:1800, lookX:0, lookY:0,
            tgtX:0, tgtY:0, idle:0, squash:0};
function faceSet(mood, dur){ face.mood = mood; face.t = 0; face.dur = dur || 420; }
function stepFace(dt){
  if(face.dur > 0){
    face.t += dt;
    if(face.t >= face.dur){ face.mood = ''; face.dur = 0; }
  }
  /* the blink: closed for 90ms, then a fresh irregular wait */
  face.blink -= dt;
  if(face.blink < -90){ face.blink = face.nextBlink; face.nextBlink = 1500 + Math.random()*3400; }
  /* where it is looking. While walking, ahead; otherwise back to centre —
     unless nothing has happened for a while, in which case it wanders. */
  if(walking){
    var pv = viewOf(N, M, walking.from), cv2 = viewOf(N, M, pos);
    face.tgtX = Math.max(-1, Math.min(1, cv2[0] - pv[0]));
    face.tgtY = Math.max(-1, Math.min(1, pv[1] - cv2[1]));
    face.idle = 0;
  } else if(anim || drag){
    face.tgtX = 0; face.tgtY = -0.25; face.idle = 0;
  } else {
    face.idle += dt;
    if(face.idle > 4000){
      var ph = (face.idle - 4000) / 2600;
      face.tgtX = Math.sin(ph*1.9) * 0.85;
      face.tgtY = Math.sin(ph*0.7) * 0.3;
    } else { face.tgtX = 0; face.tgtY = 0; }
  }
  face.lookX += (face.tgtX - face.lookX) * Math.min(1, dt/90);
  face.lookY += (face.tgtY - face.lookY) * Math.min(1, dt/90);
  /* AND IT GETS BORED. Every five and a half seconds of a player thinking,
     it bounces once on the spot. No sound, no particles, nothing that could
     be mistaken for the game asking for something — just a small sign of
     life in the corner of the eye of somebody staring at a puzzle. This is
     the whole of "surprise and delight" in eight lines: a character that
     does something when you do nothing. */
  if(face.idle > 5500 && hopT >= 1 && !won){
    face.idle = 4000;
    hopT = 0;
  }
}
/* THE ORB IS ALIVE BETWEEN FRAMES TOO.
   It sheds embers while it stands and a comet's worth while it walks, and
   the trail is positions rather than a stretched sprite because the board is
   a grid and the path it took should read as the path it took. */
var orbEmit = 0, orbTrail = [];
function stepOrb(dt){
  var i;
  for(i = orbTrail.length-1; i >= 0; i--){
    orbTrail[i].t += dt;
    if(orbTrail[i].t > 300) orbTrail.splice(i, 1);
  }
  if(!lv || demo || won || !$('hud').classList.contains('on')) return;
  var p = playerScreen();
  if(walking && orbTrail.length < 40) orbTrail.push({x:p[0], y:p[1], t:0});
  orbEmit -= dt;
  if(orbEmit <= 0){
    orbEmit = walking ? 42 : 150;
    /* INFALL. The emitter used to shed embers upward, which is what a lamp
       does; this thing is the opposite of a lamp. Debris is spawned out on
       a ring and thrown INWARD, so what you see around the character is
       light being pulled in and going out at the horizon. Same particle
       system, same cost — the velocity is simply aimed the other way. */
    var ia = Math.random()*6.28318, ir = S*(0.38 + Math.random()*0.14);
    burst(p[0] + Math.cos(ia)*ir, p[1] + Math.sin(ia)*ir, 1,
          {kind:'ember', spd:0, life:520, r:1.4, col:'150,225,255',
           g:0, fade:0.8, drag:0.97});
    var pt = parts[parts.length-1];
    if(pt){
      /* aimed at the centre, with a sideways kick so it arrives on a spiral
         rather than falling straight down a drain */
      pt.vx = (p[0] - pt.x)*2.1 + Math.cos(ia + 1.57)*70;
      pt.vy = (p[1] - pt.y)*2.1 + Math.sin(ia + 1.57)*70;
    }
  }
}
function drawOrbTrail(){
  if(!orbTrail.length) return;
  var c = lightCtx();
  c.save(); c.globalCompositeOperation = 'lighter';
  for(var i = 0; i < orbTrail.length; i++){
    var q = orbTrail[i], a = 1 - q.t/300;
    c.globalAlpha = a*a*0.34;
    c.fillStyle = 'rgb(56,214,255)';
    c.beginPath(); c.arc(q.x, q.y, S*0.16*a, 0, 6.284); c.fill();
  }
  c.restore();
}
function drawPlayer(x, y, sz, k){
  /* on the way out you do not blink off the board, you are drawn into the
     door — down to nothing, brightening as it closes, over a quarter second */
  if(exitPull >= 0){
    k = 1 - exitPull;
    if(k <= 0.02) return;
  }
  k = k === undefined ? 1 : k;
  if(k > 0.9) drawOrbTrail();
  var t = Date.now() / 1000;
  var br = 0.90 + 0.10*Math.sin(t*3.1) + 0.045*Math.sin(t*7.3);
  var rr = sz*0.325*k*br;
  y -= sz*0.022*Math.sin(t*2.2)*k;
  /* SQUASH ON THE WAY DOWN, STRETCH ON THE WAY UP. The ball is drawn into
     a scaled space rather than as an ellipse so the face, the rim and the
     specular all deform with it — a squashed ball with a round face on it
     is a sticker on a balloon. */
  var sqy = 1, sqx = 1;
  if(walking){
    var vv = Math.cos(walking.t*Math.PI);              /* +1 rising, -1 falling */
    sqy = 1 + vv*0.10; sqx = 1 - vv*0.09;
  } else if(hopT < 1){
    var hb = Math.sin(hopT*Math.PI*1.6) * Math.pow(1-hopT, 2.2);
    sqy = 1 - hb*0.30; sqx = 1 + hb*0.24;
  }
  var pushed = (sqx !== 1 || sqy !== 1);
  if(pushed){
    ctx.save();
    ctx.translate(x, y + rr*(1-sqy)); ctx.scale(sqx, sqy); ctx.translate(-x, -y);
  }

  /* THE POOLS ARE GONE, and their absence is the point.

     Two centred halos used to sit here — the light the orb threw on the
     block it stood over. They are drawn into the bloom buffer, which
     composites over EVERYTHING, so they were filling in the one part of
     this character that has to stay empty: the horizon came out a flat
     teal disc with a bright ring round it. A black hole does not light the
     floor it is standing on.

     What is left is the contact seat — a shadow, which is the only kind of
     mark this object has any business making on a block — and the halo
     that survives is the lensing donut further down, which starts outside
     the silhouette and leaves the middle alone. */
  ctx.save();
  var sg = ctx.createRadialGradient(x, y + sz*0.06, 0, x, y + sz*0.06, sz*0.52*k);
  sg.addColorStop(0, 'rgba(0,0,0,0.42)'); sg.addColorStop(0.6, 'rgba(0,0,0,0.20)');
  sg.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = sg;
  ctx.fillRect(x - sz*0.6, y - sz*0.5, sz*1.2, sz*1.2);
  ctx.restore();

  /* A SINGULARITY: A HOLE, AND SOMETHING ON THE OTHER SIDE OF IT.

     The discs and the face were a costume. A black hole does not have a
     face, and hanging accretion rings off one made it a planet — the ring
     was doing the work of being found, which left the hole itself as
     decoration on its own character. Taken away, what is left has to earn
     its legibility the way the real thing does: with an edge, and with what
     can be seen through it.

     THROUGH IT IS THE POINT, AND IT IS THE SAME SKY. What is inside the
     horizon is the actual starfield behind the board, sampled at its actual
     position and its actual scale — the identical pixels the void columns
     let through, in register with them to the pixel. Stand the marker beside
     a gap in the cube and the stars run straight across from one to the
     other, because they are one starfield seen through two holes.

     THAT REGISTRATION IS THE WHOLE OF THE EFFECT, and it is why none of the
     obvious enhancements are here. It was a magnified copy for a while,
     turning slowly at half strength, and every one of those was a reason the
     picture inside could not be the picture behind: a hole showing its own
     private sky is not a hole, it is a porthole onto somewhere else, and the
     eye reads the difference long before it can name it.

     So the sky is blitted in SCREEN space, not board space. The starfield
     deliberately sits outside the camera — shaking it would say the universe
     moved — so a shake, a squash or a zoom must slide the board across the
     hole while what shows through it stays nailed to the same stars. Sampling
     under the camera transform would have dragged the sky along with the
     board and quietly undone the thing being built.

     NOTHING IS PAINTED BACK OVER IT. There was a fall to absolute black
     across the inner two thirds, on the argument that nothing may come out of
     a black hole. It is gone, along with the seven fake lensed points that
     existed to give the resulting smear some structure: both were opaque
     paint standing between the player and the one claim this object makes.
     The sky at the void colour is already far darker than any deck it can
     stand on, so the marker still reads as a hole cut clean through — which
     is exactly what the void columns are, and now it is made of the same
     nothing they are.

     THE EDGE DOES THE FINDING. A photon ring — one thin, very bright circle
     right on the silhouette — with a soft lensing halo outside it. On a
     pale deck that ring is the whole difference between "a hole in the
     world" and "a hole in the floor", and it is the only reason a black
     object is allowed to be the player marker in a game where dark already
     means impassable.

     IT REACTS WITHOUT A FACE. Everything the character used to say with
     eyes now happens as gravity: the rim flares and the halo swells on a
     key or a gate, it flinches on a refusal. A thing with no face that
     answers what you did is not less expressive than one with eyes — it is
     expressive in the only language it has. */
  var glow = lightCtx(), mood = face.mood;
  var flare = (mood === 'happy' || mood === 'win') ? 1 - face.t/face.dur
            : mood === 'wide' ? 0.6*(1 - face.t/face.dur) : 0;
  var rim = 1 + flare*0.9;

  if(glow !== ctx){
    glow.save(); glow.globalCompositeOperation = 'lighter';
    /* A RADIAL GRADIENT FILLS ITS INNER CIRCLE WITH THE STOP-0 COLOUR.
       Giving it an inner radius does NOT punch a hole — everything inside
       r0 is painted with whatever stop 0 says, so a "donut" whose first
       stop is bright is a filled disc with extra steps. That is what was
       flooding the horizon and turning it slate grey. Stop 0 is
       transparent; the light starts a hair outside it. */
    var au = glow.createRadialGradient(x, y, rr*0.86, x, y, rr*(2.1 + flare*0.7));
    au.addColorStop(0,    'rgba(190,240,255,0)');
    au.addColorStop(0.04, 'rgba(190,240,255,' + (0.34*k*rim).toFixed(3) + ')');
    au.addColorStop(0.22, 'rgba(120,215,255,' + (0.15*k*rim).toFixed(3) + ')');
    au.addColorStop(1,    'rgba(40,120,240,0)');
    glow.fillStyle = au;
    glow.beginPath(); glow.arc(x, y, rr*(2.1 + flare*0.7), 0, 6.284); glow.fill();
    glow.restore();
  }

  ctx.save();
  ctx.beginPath(); ctx.arc(x, y, rr, 0, 6.284); ctx.clip();
  /* THE DECK UNDER IT IS REMOVED, not covered. clearRect obeys the clip, so
     this punches the disc clean out of everything already painted — the tile
     the marker is standing on included — down to bare canvas, which is what
     the void columns are. Painting a dark disc instead would have been a
     spot of black ON the board; this is an absence OF board, and the sky
     that goes in next is then the same sky at the same brightness the gaps
     show, with nothing of the deck left underneath to tint it. */
  ctx.clearRect(x - rr, y - rr, rr*2, rr*2);
  if(skyCv) skyThrough(x, y, rr);
  /* AND WHATEVER IS DIRECTLY OPPOSITE YOU, seen through it — the far side of
     the cube in front of the far side of the sky, which is the order they
     are actually in. Dimmed and rocking very slightly, because the point is
     that it is GLIMPSED. Its halo is forced into the frame rather than the
     bloom buffer for the duration, or the glow of a key on the far side of
     the cube would spill out around a hole that is meant to be letting
     almost nothing out. */
  var thru = (!demo && !won) ? throughLook() : null;
  if(thru){
    ctx.save();
    glowOff = true;
    ctx.globalAlpha = 0.58*k;
    ctx.translate(x, y);
    ctx.rotate(Math.sin(t*0.42)*0.07);
    ctx.scale(0.86, 0.86);
    ctx.translate(-x, -y);
    if(thru.kind === 2) drawKey(x, y, rr*1.5);
    else if(thru.kind === 4) drawGoal(x, y, rr*1.4);
    else drawDoor(x, y, rr*1.6, doorT[thru.i] || 0);
    glowOff = false;
    ctx.restore();
    ctx.globalAlpha = 1;
  }
  ctx.restore();

  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  ctx.strokeStyle = 'rgba(214,245,255,' + Math.min(1, 0.95*k*rim).toFixed(3) + ')';
  ctx.lineWidth = Math.max(1.1, rr*0.075*rim);
  ctx.beginPath(); ctx.arc(x, y, rr*0.965, 0, 6.284); ctx.stroke();
  /* brighter over one arc, travelling: the whole of "it is turning" */
  ctx.strokeStyle = 'rgba(255,255,255,' + (0.55*k*rim).toFixed(3) + ')';
  ctx.lineWidth = Math.max(0.9, rr*0.05*rim);
  var ph = t*0.7;
  ctx.beginPath(); ctx.arc(x, y, rr*0.965, ph, ph + 2.0); ctx.stroke();
  ctx.restore();
  if(pushed) ctx.restore();
}
/* ============================================================
   SOLIDS — the four things in the cube that are not stone

   The blocks are matter and they are drawn as matter: a texture, face on,
   at block resolution. The key, the gate, the way out and you are not
   matter, and drawing them with a circle and two strokes was the last place
   in this game where something important looked like a diagram of itself.

   WHY THIS IS NOT A WEBGL LAYER, written down so nobody has to re-derive it.

   Markers depth-sort BETWEEN the cube's own faces — a key four blocks deep
   is occluded by the block in front of it, and that occlusion is how you
   learn the key is buried. A second canvas holding a GL context cannot
   interleave with 2D draws: everything it renders lands in front of the
   whole cube or behind all of it. Fixing that means porting the blocks, the
   textures, the palette bands, the bloom and the particles to GL as well —
   a renderer rewrite of a shipping game — and inlining a library several
   times the size of the entire game into a file that has to run offline in
   a WebView.

   And it would buy the wrong thing. The look being asked for is flat-shaded
   low-poly with a tight palette and light that reads as light: faceted
   solids, hard shading steps between facets, a bright rim, an inner glow.
   That is a painter's-algorithm renderer's home ground. So there is one
   here: about a hundred lines, riding the camera the rest of the game
   already uses, sorting into the same list as the blocks.

   Every solid is drawn TWICE — its far facets first at low alpha, then its
   near ones over the top — which is the whole trick to making a lump of
   coloured polygons read as a cut gem. Light comes from a fixed direction
   in view space, the same over-the-left-shoulder direction the cube itself
   is shaded by, so a gem and the block under it agree about where the sun
   is.
   ============================================================ */
var SOLID_L = [-0.40, -0.62, 0.68];                 /* light, view space, normalised */
var SOLID_H = [-0.26, -0.40, 0.88];                 /* half vector, for the sheen */
var SOLID_PV = 0.30;                                /* a gem's own perspective */
var sxA = [], syA = [], vxA = [], vyA = [], vzA = [], fzA = [], fiA = [];

/* ---- mesh builders. A mesh is {v:[[x,y,z]…], f:[[i,j,k…]…]} and nothing
        else: no normals, no uvs, no materials. Normals are computed per
        frame because these solids rotate every frame anyway. ---- */

/* A TORUS, ring segments by tube segments, and the only closed surface in
   the game. Twenty-four by twelve with the seam pass on: fine enough that
   the shading steps fall below what the eye separates at tile size, so it
   reads as a turned metal ring rather than as a bag of triangles. Back
   faces are culled — it is solid gold, not glass, and nothing behind its
   own surface is anybody's business. */
function meshTorus(seg, side, R, r){
  var v = [], f = [], i, j;
  for(i = 0; i < seg; i++){
    var a = i/seg * 6.28318, ca = Math.cos(a), sa = Math.sin(a);
    for(j = 0; j < side; j++){
      var b = j/side * 6.28318;
      var rr = R + r*Math.cos(b);
      v.push([ca*rr, r*Math.sin(b), sa*rr]);
    }
  }
  for(i = 0; i < seg; i++){
    var i2 = (i+1) % seg;
    for(j = 0; j < side; j++){
      var j2 = (j+1) % side;
      f.push([i*side + j, i2*side + j, i2*side + j2, i*side + j2]);
    }
  }
  return {v:v, f:f};
}

/* AN OCTAHEDRON — and it used to be the player. Eight facets is enough to
   flash as it turns and few enough that each flash is a whole face rather
   than a highlight crawling over a sphere, which is exactly the property a
   thing lying on a shelf waiting to be picked up wants. The player has gone
   back to being a sphere of light, which is what it always read as best,
   and the diamond is now the key. */
function meshOcta(r){
  return {v:[[r,0,0],[-r,0,0],[0,r,0],[0,-r,0],[0,0,r],[0,0,-r]],
          f:[[0,2,4],[2,1,4],[1,3,4],[3,0,4],[2,0,5],[1,2,5],[3,1,5],[0,3,5]]};
}

/* A BOX, and a merge, so the gate can be built out of posts and a lintel
   and bars the way an actual gate is. */
function meshBox(w, h, d, ox, oy, oz){
  var x0 = ox-w, x1 = ox+w, y0 = oy-h, y1 = oy+h, z0 = oz-d, z1 = oz+d;
  return {v:[[x0,y0,z0],[x1,y0,z0],[x1,y1,z0],[x0,y1,z0],
             [x0,y0,z1],[x1,y0,z1],[x1,y1,z1],[x0,y1,z1]],
          f:[[0,3,2,1],[4,5,6,7],[0,1,5,4],[2,3,7,6],[1,2,6,5],[0,4,7,3]]};
}
/* A merged mesh remembers which piece each face came from, so one solid can
   be made of two materials and still be ONE depth-sorted list. That matters
   the moment a bright thing has to pass both behind and in front of a dark
   thing: split them into two draws and the sort is gone. */
function meshMerge(list){
  var v = [], f = [], g = [], i, j, k;
  for(i = 0; i < list.length; i++){
    var m = list[i], base = v.length;
    for(j = 0; j < m.v.length; j++) v.push(m.v[j]);
    for(j = 0; j < m.f.length; j++){
      var src = m.f[j], dst = [];
      for(k = 0; k < src.length; k++) dst.push(src[k] + base);
      f.push(dst); g.push(i);
    }
  }
  return {v:v, f:f, g:g};
}

/* ---- the renderer ----------------------------------------------------
   Rotate, project, sort, fill. The only thing here that is not obvious is
   that back facets are kept rather than culled: a cut gem is mostly the
   light that got through it and came back, and painting the far side at a
   third of the alpha under the near side is the cheapest honest version of
   that. Opaque solids pass cull:1 and skip it. */
function drawSolid(m, x, y, s, ry, rx, baseCol, o){
  o = o || {};
  var c = lightCtx(), i, j, n = m.v.length, nf = m.f.length;
  var cy = Math.cos(ry), sy = Math.sin(ry), cx = Math.cos(rx), sx = Math.sin(rx);
  var alpha = o.alpha === undefined ? 1 : o.alpha;
  if(alpha <= 0.004) return;
  for(i = 0; i < n; i++){
    var p = m.v[i];
    var X = p[0]*cy + p[2]*sy, Z = p[2]*cy - p[0]*sy, Y = p[1];
    var Y2 = Y*cx - Z*sx, Z2 = Z*cx + Y*sx;
    vxA[i] = X; vyA[i] = Y2; vzA[i] = Z2;
    var k = 1/(1 - SOLID_PV*Z2);
    sxA[i] = x + X*s*k; syA[i] = y - Y2*s*k;
  }
  for(i = 0; i < nf; i++){
    var f = m.f[i], a = f[0], b = f[1], d = f[2], z = 0;
    for(j = 0; j < f.length; j++) z += vzA[f[j]];
    fzA[i] = z/f.length; fiA[i] = i;
  }
  /* insertion sort: these meshes are twenty faces, and a comparator closure
     per solid per frame is more garbage than the sort is work */
  for(i = 1; i < nf; i++){
    var key = fiA[i], kz = fzA[key];
    for(j = i-1; j >= 0 && fzA[fiA[j]] > kz; j--) fiA[j+1] = fiA[j];
    fiA[j+1] = key;
  }
  ctx.save();
  ctx.lineJoin = 'round';
  for(i = 0; i < nf; i++){
    var fc = m.f[fiA[i]];
    var ax = vxA[fc[0]], ay = vyA[fc[0]], az = vzA[fc[0]];
    var ux = vxA[fc[1]]-ax, uy = vyA[fc[1]]-ay, uz = vzA[fc[1]]-az;
    var wx = vxA[fc[2]]-ax, wy = vyA[fc[2]]-ay, wz = vzA[fc[2]]-az;
    var nx = uy*wz - uz*wy, ny = uz*wx - ux*wz, nz = ux*wy - uy*wx;
    var len = Math.sqrt(nx*nx + ny*ny + nz*nz) || 1;
    nx /= len; ny /= len; nz /= len;
    var front = nz > 0;
    if(!front){
      if(o.cull) continue;
      nx = -nx; ny = -ny; nz = -nz;
    }
    var col = (o.cols && m.g) ? o.cols[m.g[fiA[i]]] : baseCol;
    var dif = Math.max(0, nx*SOLID_L[0] + ny*SOLID_L[1] + nz*SOLID_L[2]);
    var amb = (o.ambs && m.g) ? o.ambs[m.g[fiA[i]]] : (o.amb === undefined ? 0.34 : o.amb);
    var lum = amb + (1-amb)*dif;
    /* THE SHEEN HAS TO KNOW WHAT IT IS SHINING ON. It was a flat white
       term added on top of whatever colour the facet was, which is fine
       until a material is meant to be BLACK — an event horizon with
       specular highlights is a shiny marble, and the whole point of the
       thing is that no light comes off it. Materials can now turn it off. */
    var spc = Math.pow(Math.max(0, nx*SOLID_H[0] + ny*SOLID_H[1] + nz*SOLID_H[2]), 18);
    if(o.specs && m.g) spc *= o.specs[m.g[fiA[i]]];
    var g = front ? 1 : 0.42;                       /* the far side, seen through */
    var r0 = Math.min(255, col[0]*lum*g + 255*spc*(front?0.85:0));
    var g0 = Math.min(255, col[1]*lum*g + 255*spc*(front?0.85:0));
    var b0 = Math.min(255, col[2]*lum*g + 255*spc*(front?0.85:0));
    ctx.globalAlpha = alpha * (front ? (o.face === undefined ? 0.94 : o.face) : 0.34);
    ctx.fillStyle = 'rgb(' + (r0|0) + ',' + (g0|0) + ',' + (b0|0) + ')';
    ctx.beginPath();
    ctx.moveTo(sxA[fc[0]], syA[fc[0]]);
    for(j = 1; j < fc.length; j++) ctx.lineTo(sxA[fc[j]], syA[fc[j]]);
    ctx.closePath();
    ctx.fill();
    /* THE SEAM PASS, and it is what "smooth" means for a painter's-algorithm
       renderer. Two filled paths sharing an edge do not meet: canvas
       antialiases each one independently, so half a pixel of background
       shows through along every join and a finely tesselated solid comes out
       looking cracked — worse than a coarse one. Stroking each face with its
       OWN fill colour closes the gap with the colour that should be there.
       No edge is drawn; the facet simply reaches its neighbour. */
    if(o.seam){
      ctx.strokeStyle = ctx.fillStyle;
      ctx.lineWidth = o.seam;
      ctx.stroke();
    }
    /* the edge is where a crystal actually lives: one hairline per facet,
       brightest on the facets turned toward the light */
    if(front && o.edge){
      ctx.globalAlpha = alpha * o.edge * (0.35 + 0.65*dif);
      ctx.strokeStyle = o.edgeCol || '#fff';
      ctx.lineWidth = o.edgeW || 1;
      ctx.stroke();
    }
  }
  ctx.restore();
  /* and the light it throws, into the bloom buffer where light belongs */
  if(o.glow && c !== ctx){
    c.save(); c.globalCompositeOperation = 'lighter';
    var gg = c.createRadialGradient(x, y, 0, x, y, s*(o.glowR || 2.2));
    var cs = (col[0]|0) + ',' + (col[1]|0) + ',' + (col[2]|0);
    gg.addColorStop(0, 'rgba(' + cs + ',' + (o.glow*alpha).toFixed(3) + ')');
    gg.addColorStop(0.45, 'rgba(' + cs + ',' + (o.glow*alpha*0.34).toFixed(3) + ')');
    gg.addColorStop(1, 'rgba(' + cs + ',0)');
    c.fillStyle = gg;
    c.fillRect(x - s*3, y - s*3, s*6, s*6);
    c.restore();
  }
}

/* A plate reads as a thing cut INTO the deck rather than set on top of it:
   a recessed ring, a bar across it for INVERT (the axis that flips) or a
   level line for DRAIN, and a slow breath so it is visibly live. It is drawn
   at its own depth like everything else, so a plate far down the cube is dim
   — but it is never occluded, which is the one thing that makes planning
   with them possible. */
/* THE PLATE IS A REFRESH, because that is exactly what it does.

   It used to be a ring with a bar across it, which is the international
   symbol for "no" — the one thing the plate is not. Two arrows chasing each
   other round a circle is the symbol every person alive already reads as
   "this puts the world through a change and can be done again", and the
   plate is the only reversible thing in the game.

   The two kinds turn opposite ways. INVERT and DRAIN are different rewrites
   and a player standing on a cube with both needs to know which one is
   under them without reading a label, so one winds clockwise and the other
   anticlockwise. Direction is a channel nothing else in this game uses. */
function drawPlate(r, bit, depth){
  var x = r[0] + S/2, y = r[1] + S/2, col = PLATE_COL[bit];
  var t = (Date.now() % 2600) / 2600, br = 0.55 + 0.45*Math.sin(t*6.284);
  var lit = 0.35 + 0.65 * (depth / Math.max(1, N-1));
  var dir = bit === 1 ? 1 : -1;
  var spin = (Date.now() % 5200) / 5200 * 6.28318 * dir;
  /* SIZED FOR A TILE, not for a screenshot. At S*0.26 with a 6% stroke and
     a 10% head this came out on a real phone as two thin brackets with
     specks on the ends — the symbol was right and the weight was not.
     Bigger radius, half again the stroke, a head you could not mistake for
     a serif, and the arcs closed up so the two gaps read as a rotation
     rather than as a pair of parentheses. */
  var rad = S*0.29, lw = Math.max(2.2, S*0.085), head = Math.max(5, S*0.155);
  halo(x, y, S*0.95, col, 0.30*br*lit);
  ctx.save();
  ctx.strokeStyle = ctx.fillStyle = 'rgba(' + col + ',' + (0.60 + 0.36*br*lit).toFixed(3) + ')';
  ctx.lineWidth = lw; ctx.lineCap = 'butt';
  var headArc = (head*1.5) / rad;                 /* what the head costs in angle */
  for(var i = 0; i < 2; i++){
    var a0 = spin + i*Math.PI, a1 = a0 + 2.62*dir, aEnd = a1 - dir*headArc;
    /* THE ARC STOPS WHERE THE HEAD BEGINS. Drawn to a1 and then a triangle
       dropped on top of the end, the head straddles the stroke and leaves a
       notch on the inside of the curve — the arrow reads as a shape stuck
       onto a ring rather than as the end of it. The arc gives up exactly the
       angle the head occupies, the head's base sits on that point, and its
       tip carries on to where the stroke would have finished. */
    ctx.beginPath();
    ctx.arc(x, y, rad, Math.min(a0, aEnd), Math.max(a0, aEnd));
    ctx.stroke();
    var ce = Math.cos(aEnd), se = Math.sin(aEnd);
    var bx = x + ce*rad, by = y + se*rad, w2 = head*0.68;
    ctx.beginPath();
    ctx.moveTo(x + Math.cos(a1)*rad, y + Math.sin(a1)*rad);
    ctx.lineTo(bx + ce*w2, by + se*w2);
    ctx.lineTo(bx - ce*w2, by - se*w2);
    ctx.closePath(); ctx.fill();
  }
  ctx.restore();
}
/* ---- the four solids, built once ---------------------------------------
   Cheap enough to build at boot and never think about again: the largest is
   the gate at thirty-six faces, and the whole set is under a hundred. */
var GEM_KEY = meshOcta(1);
var GEM_EXIT = meshTorus(24, 12, 0.74, 0.26);
var GATE = meshMerge([
  meshBox(0.10, 0.62, 0.10, -0.62, 0.02, 0),        /* posts */
  meshBox(0.10, 0.62, 0.10,  0.62, 0.02, 0),
  meshBox(0.78, 0.11, 0.11,  0,    0.72, 0),        /* lintel */
  meshBox(0.72, 0.09, 0.09,  0,   -0.58, 0)         /* threshold */
]);
var GATE_BARS = meshMerge([
  meshBox(0.055, 0.56, 0.055, -0.30, 0.02, 0),
  meshBox(0.055, 0.56, 0.055,  0,    0.02, 0),
  meshBox(0.055, 0.56, 0.055,  0.30, 0.02, 0)
]);

/* A gate does not blink open. The bars drop into the threshold over a third
   of a second and the frame goes quiet behind them, so unlocking is a thing
   that happens rather than a state that changed. One number per door, and
   it is rebuilt with the level. */
var doorT = [];
function stepDoors(dt){
  if(!lv) return;
  for(var i = 0; i < lv.doors.length; i++){
    var want = (doors & (1<<i)) ? 1 : 0, cur = doorT[i] === undefined ? want : doorT[i];
    if(cur === want) { doorT[i] = want; continue; }
    doorT[i] = want > cur ? Math.min(1, cur + dt/340) : Math.max(0, cur - dt/340);
  }
}

/* THE KEY — the diamond, in emerald, turning on a slow bob. Eight facets on
   two axes of spin means a key is never showing you the same face twice,
   which is what makes an object on a shelf read as worth crossing a board
   for. */
var C_KEY = [34, 224, 106], S_KEY = '34,224,106';
function drawKey(cx, cy, sz){
  var t = Date.now()/1000;
  var y = cy - sz*0.05 - sz*0.045*Math.sin(t*1.9);
  halo(cx, y, sz*1.0, S_KEY, 0.22);
  drawSolid(GEM_KEY, cx, y, sz*0.30, t*1.15, 0.42 + 0.10*Math.sin(t*1.4),
            C_KEY, {glow:0.30, glowR:1.9, edge:0.70, edgeCol:'#dcffe8', amb:0.34});
}

/* THE WAY OUT — a gold torus, hanging over the square and turning about its
   own axis.

   A ring is the right shape for this and it took three goes to get there. It
   is the only closed form on the board, so it can never be confused with a
   gem lying on a shelf; it is a HOLE, which is what a way out is; and
   spinning it about its own axis means the silhouette never changes — it
   reads as a ring from the first frame to the last while the light travels
   around it. Tilted a little off face-on so it is plainly a solid and not a
   drawn circle. */
var C_EXIT = [255, 196, 77], S_EXIT = '255,196,77';
function drawGoal(cx, cy, sz){
  var t = Date.now()/1000, p = (Date.now() % 2400)/2400;
  var y = cy - sz*0.06 - sz*0.04*Math.sin(t*1.35);
  halo(cx, y, sz*1.45, S_EXIT, 0.30);
  /* the pulse on the deck: the ring says "the way out", this says "this
     square", and those are two different facts */
  ctx.save();
  ctx.strokeStyle = 'rgba(' + S_EXIT + ',' + (0.50*(1-p)).toFixed(3) + ')';
  ctx.lineWidth = Math.max(1.4, sz*0.045);
  ctx.beginPath(); ctx.ellipse(cx, cy + sz*0.32, sz*(0.20 + 0.30*p), sz*(0.09 + 0.13*p), 0, 0, 6.284);
  ctx.stroke(); ctx.restore();
  drawSolid(GEM_EXIT, cx, y, sz*0.40, t*0.75, 1.31 + 0.08*Math.sin(t*0.8), C_EXIT,
            {glow:0.34, glowR:2.2, amb:0.30, face:1, cull:1, seam:1.1});
}

/* THE GATE — posts, a lintel, a threshold and three bars, and the bars sink
   into the threshold when it opens. Shut it is lit and solid; open it is a
   doorway you can see straight through, which is exactly what it now is. */
function drawDoor(cx, cy, sz, op){
  op = op || 0;                                   /* 0 shut, 1 open, and every frame between */
  drawSolid(GATE, cx, cy, sz*0.42, 0, 0.24, [53, 215, 161],
            {cull:1, face:1, alpha:0.36 + 0.64*(1-op), amb:0.30,
             edge:0.30*(1-op*0.6), edgeCol:'#bfffe6'});
  if(op < 0.995){
    /* the bars sink into the threshold and thin as they go */
    drawSolid(GATE_BARS, cx, cy + sz*0.26*op, sz*0.42*(1 - op*0.85), 0, 0.24,
              [90, 240, 190], {cull:1, face:1, alpha:1 - op, amb:0.38,
                               glow:0.15*(1-op), glowR:1.4, edge:0.40*(1-op), edgeCol:'#eafff6'});
  }
}

/* ---------- the turning cube ---------- */
function draw3d(){
  var t0 = nowMs();
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
  var tmp = [0,0,0], xr = peekE();
  for(i = 0; i < faces.length; i++){
    var fc = faces[i], d = FACE_D[fc.f];
    var nz = m.F[0]*d[0] + m.F[1]*d[1] + m.F[2]*d[2];
    var back = nz <= 0.02;
    /* pointing away — never drawn, until the solid is glass and the far side
       of it is half the reason to be looking */
    if(back && xr < 0.12) continue;
    var nx = m.R[0]*d[0] + m.R[1]*d[1] + m.R[2]*d[2];
    var ny = m.U[0]*d[0] + m.U[1]*d[1] + m.U[2]*d[2];
    var q = FACE_Q[fc.f], pts = [], dep = 0;
    for(var k = 0; k < 4; k++){
      px(fc.x + q[k][0], fc.y + q[k][1], fc.z + q[k][2], tmp);
      pts.push(tmp[0], tmp[1]); dep += tmp[2];
    }
    px(fc.x, fc.y, fc.z, tmp);
    var mo = ((fc.x*73856093 ^ fc.y*19349663 ^ fc.z*83492791) >>> 8) % 100;
    list.push({d:dep/4, k:0, pts:pts, t:fc.t, dep:tmp[2] + c,
               lit: back ? LIT(nx, ny, -nz)*0.72 : LIT(nx, ny, nz),
               back: back, up: ny > 0.55, moss: mo < 34 ? (mo/34) : 0});
  }
  /* markers ride half a cell proud of their own face so they stay visible
     as the solid turns under them — and once the rock is glass they are
     sorted in front of ALL of it, because a key you can only just make out
     through six translucent blocks is a key you have not been shown */
  function marker(w, kind, extra){
    px(w[0] + m.F[0]*0.62, w[1] + m.F[1]*0.62, w[2] + m.F[2]*0.62, tmp);
    list.push({d:tmp[2] + 0.62 + (xr > 0.35 ? 1000 : 0), k:kind, x:tmp[0], y:tmp[1], extra:extra});
  }
  if(!demo){
    /* same rule as the flat board: a thing that is rock in this world is not
       in this world, and looking inside the cube does not make it so */
    for(i = 0; i < lv.doors.length; i++) if(inThisWorld(lv.doors[i])) marker(lv.doors[i], 3, i);
    for(i = 0; i < lv.keys.length; i++) if(!(kmask & (1<<i)) && inThisWorld(lv.keys[i])) marker(lv.keys[i], 2, 0);
    if(inThisWorld(lv.goal)) marker(lv.goal, 4, 0);
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
    /* and the plinth is cut from the same rock as the vault, so it takes the
       same texture — one block's worth of it, stretched over the slab */
    texQuad([top[0][0],top[0][1], top[1][0],top[1][1], top[2][0],top[2][1], top[3][0],top[3][1]],
            texFor('#', N-1, 0.85));
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
  /* THE WIREFRAME IS FOUR STROKES, NOT TWELVE HUNDRED.

     Every face under a peek wants an edge, and a stroke call per face on a
     seven-cube meant over a thousand of them: it measured at 4ms a frame,
     which is a quarter of the budget for one held gesture. But the edges
     only come in four flavours — deck or bedrock, facing you or facing
     away — so they are accumulated into four paths and stroked once each.
     The picture is identical and the call count is a rounding error.

     They flush the moment the sorted list reaches its first marker, so the
     wire lands over the glass and under everything that matters. */
  var wire = [[],[],[],[]], wireDone = false;
  function wireCol(gi){
    var c = (gi < 2) ? ST.dN : ST.rN, back = gi & 1;
    return 'rgba(' + (c[0]|0) + ',' + (c[1]|0) + ',' + (c[2]|0) + ','
      + (xr * (back ? 0.22 : 0.62) * (gi < 2 ? 1 : 0.66)).toFixed(3) + ')';
  }
  function flushWire(){
    if(wireDone) return;
    wireDone = true;
    ctx.save(); ctx.globalCompositeOperation = 'lighter'; ctx.lineJoin = 'round';
    for(var g3 = 0; g3 < 4; g3++){
      var arr = wire[g3];
      if(!arr.length) continue;
      ctx.strokeStyle = wireCol(g3);
      ctx.lineWidth = 1 + xr*0.5;
      ctx.beginPath();
      for(var w3 = 0; w3 < arr.length; w3++){
        var q3 = arr[w3];
        ctx.moveTo(q3[0], q3[1]); ctx.lineTo(q3[2], q3[3]);
        ctx.lineTo(q3[4], q3[5]); ctx.lineTo(q3[6], q3[7]); ctx.closePath();
      }
      ctx.stroke();
    }
    ctx.restore();
  }
  /* THE CONDUIT IS GONE.

     A lit throat used to be run from the cell underfoot, through the centre
     of the cube, to the cell opposite — drawn only under a peek, on the
     argument that a black hole which is a way through ought to show the way
     through. It was wrong twice over.

     It was wrong in code: the ring generator it called was never written, so
     every peek held past the fade threw straight out of the frame — and the
     loop's own guard caught it, reset the walk and re-settled the board, once
     per frame, for as long as the finger stayed down.

     It was wrong in design, which is the half worth keeping written down. The
     antipode is a LOOK and nothing else: no reach, no route, no bearing on
     any turn. Drawing a tube to it says the opposite — a pipe between two
     cells is a promise you can travel it, and the game has exactly one way to
     move and it is not that. The horizon showing a glimpse of the far side
     was always the whole of the idea; the throat was the idea explained a
     second time, in a louder voice, incorrectly.                            */
  /* ONE STEP AWAY, in 3D: the same short bridges as the flat view, run
     between the projected marker points for the cell underfoot and any
     walkable neighbour that is the goal, a key, or a door. Always on, not
     gated behind a peek — this is a navigation aid, not a decoration. */
  function drawNearLinks(){
    if(demo || !lv || won) return;
    var specials = nearSpecials();
    if(!specials.length) return;
    px(pos[0] + m.F[0]*0.62, pos[1] + m.F[1]*0.62, pos[2] + m.F[2]*0.62, tmp);
    var p0 = [tmp[0], tmp[1]];
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.lineCap = 'round';
    for(var si = 0; si < specials.length; si++){
      var sp = specials[si];
      px(sp.w[0] + m.F[0]*0.62, sp.w[1] + m.F[1]*0.62, sp.w[2] + m.F[2]*0.62, tmp);
      var col = sp.kind === 2 ? S_KEY : sp.kind === 4 ? S_EXIT : '53,215,161';
      var g5 = ctx.createLinearGradient(p0[0], p0[1], tmp[0], tmp[1]);
      g5.addColorStop(0, 'rgba(206,244,255,0.55)');
      g5.addColorStop(1, 'rgba(' + col + ',0.85)');
      ctx.strokeStyle = g5;
      ctx.lineWidth = 2 + xr*1.5;
      ctx.beginPath(); ctx.moveTo(p0[0], p0[1]); ctx.lineTo(tmp[0], tmp[1]); ctx.stroke();
      halo(tmp[0], tmp[1], sz*0.6, col, 0.28);
    }
    ctx.restore();
  }
  drawCage(m, z, (demo ? 0.10 : 0.46) + xr*0.40, pv);
  for(i = 0; i < list.length; i++){
    var e = list[i];
    if(e.k === 0){
      /* GLASS. The solid does not vanish under a peek, it thins: the fill
         drops toward a sheet, the texture goes first because a 16x16 pattern
         at a tenth of an alpha is mud, and the edges come UP as it goes —
         so what is left at the end of the lean is a lit wireframe with a
         film of material still stretched across it. */
      if(e.back){
        /* the far side of the cube is wire only. A translucent fill behind a
           translucent fill behind a translucent fill is mud, and mud is the
           opposite of what a look-inside is for. */
        wire[isWalkType(e.t) ? 1 : 3].push(e.pts);
        continue;
      }
      ctx.globalAlpha = 1 - 0.78*xr;
      ctx.fillStyle = tileFill(e.t, e.dep, e.lit);
      ctx.beginPath();
      ctx.moveTo(e.pts[0], e.pts[1]); ctx.lineTo(e.pts[2], e.pts[3]);
      ctx.lineTo(e.pts[4], e.pts[5]); ctx.lineTo(e.pts[6], e.pts[7]);
      ctx.closePath(); ctx.fill();
      /* the flat colour underneath is not waste: it is what the quad's rim
         is made of, so a texel that lands half outside the clip still has
         the right material behind it. A face a few pixels across keeps the
         fill and skips the texture, because at that size the clip costs
         more than the detail is worth and nobody can see either. */
      if(xr < 0.45 && !e.back && quadArea(e.pts) > texGate) texQuad(e.pts, texFor(e.t, e.dep, e.lit));
      /* the path is rebuilt rather than reused: texQuad begins its own, and
         a current path is not part of what save/restore puts back */
      ctx.beginPath();
      ctx.moveTo(e.pts[0], e.pts[1]); ctx.lineTo(e.pts[2], e.pts[3]);
      ctx.lineTo(e.pts[4], e.pts[5]); ctx.lineTo(e.pts[6], e.pts[7]);
      ctx.closePath();
      ctx.globalAlpha = 1;
      if(xr > 0.02){
        /* the wireframe is coloured by MATERIAL — a deck edge glows bone and
           a bedrock edge stays cold — so the thing you are looking through
           still answers the only question that matters. Batched: see
           flushWire above. */
        wire[isWalkType(e.t) ? 0 : 2].push(e.pts);
      } else {
        ctx.strokeStyle = 'rgba(10,9,16,.5)'; ctx.lineWidth = 0.7; ctx.stroke();
      }
      /* weathering, and only where weather would reach: the faces that point
         up. Seeded off the cell so a given block is always mossy. */
      if(e.up && e.moss && xr < 0.5){
        ctx.save(); ctx.globalAlpha = (0.16 + 0.2*e.moss) * (1 - xr*2);
        ctx.fillStyle = 'rgb(126,150,86)';
        ctx.beginPath();
        ctx.moveTo(e.pts[0], e.pts[1]); ctx.lineTo(e.pts[2], e.pts[3]);
        ctx.lineTo(e.pts[4], e.pts[5]); ctx.lineTo(e.pts[6], e.pts[7]);
        ctx.closePath(); ctx.fill(); ctx.restore();
      }
      ctx.globalAlpha = 1;
    } else {
      /* under a peek every marker gets a lantern of its own, because the
         thing being revealed has to be the thing you notice */
      if(xr > 0.02){
        flushWire();
        var mc = e.k === 2 ? S_KEY : e.k === 1 ? '56,214,255' : e.k === 4 ? S_EXIT : '53,215,161';
        halo(e.x, e.y, sz*(1.0 + xr*0.8), mc, (e.k === 1 ? 0.52 : 0.32)*xr);
      }
      /* and YOU grow under a peek. Every other marker is a place; you are
         the one the eye has to find first, and the cube it is floating in
         has just filled with light. */
      if(e.k === 1) drawPlayer(e.x, e.y, sz*(1 + xr*0.22));
      else if(e.k === 2) drawKey(e.x, e.y, sz);
      else if(e.k === 3) drawDoor(e.x, e.y, sz, doorT[e.extra] || 0);
      else if(e.k === 4) drawGoal(e.x, e.y, sz);
    }
  }
  flushWire();                    /* a cube with no markers on screen at all */
  drawNearLinks();
  if(t0){
    var ms = nowMs() - t0;
    if(ms > 9) texGate = Math.min(6000, texGate * 1.4);
    else if(ms < 5 && texGate > TEX_GATE_MIN) texGate = Math.max(TEX_GATE_MIN, texGate / 1.12);
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
  /* undoing INTO a flipped world hands back a full five seconds rather than
     whatever was left when the snapshot was taken. Storing the remainder
     would be the more literal restore and the worse one: it would drop the
     player back in with a fifth of a second on the clock, which is not a
     position anybody can play out of, and undo would be a button that
     re-served the same death. This is a retry, so it is a whole run at it. */
  plateT = world ? PLATE_MS : 0;
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

/* A REFUSED TAP POINTS AT ITSELF. The toast says why; the ring says WHERE,
   which is the half the toast could never do — on a board of forty-nine
   blocks, "not connected" is only useful if you know which one it meant. */
function denyAt(u, v, col){
  var r = tileRect(u, v);
  ring(r[0] + S/2, r[1] + S/2, {r0:S*0.62, r1:S*0.30, dur:340, col:col || '255,90,90', a:0.75, w:3, sq:true});
  vig = Math.max(vig, 0.55); shake(0.10);
}
function tapCell(u, v){
  if(walking || anim || won) return;
  var s = surf[u*N + v];
  if(!s){ toast('nothing there'); sfx.deny(); denyAt(u, v); return; }
  if(!isWalkType(s.t)){ toast('bedrock — no footing on this face'); sfx.deny(); denyAt(u, v); return; }
  var di = doorIndexAt(lv, s.w), shut = di >= 0 && !(doors & (1<<di));
  if(shut && keysHeld() < 1){ toast('locked — find a key'); sfx.deny(); buzz(14); denyAt(u, v, '34,224,106'); return; }
  var path = pathTo(u, v);
  if(!path){ toast(shut ? 'no way to that door' : 'not connected — turn the cube'); sfx.deny(); denyAt(u, v); return; }
  /* Tapping the cell you already stand on yields an EMPTY path, which is
     truthy — it used to start a walk with nothing in it, and the next frame
     read surf[undefined].w and took the render loop down with it. */
  if(!path.length){
    /* EXCEPT ON A PLATE, WHERE IT PRESSES IT AGAIN.

       The spring-back puts you back on the plate, and without this the only
       way to fire it again would be to step off and step back on — which
       needs a walkable neighbour, and a plate cut through a wall of rock in
       world 0 does not always have one. That is a soft lock: standing on the
       one thing that can change the board, unable to use it.

       It is also the better verb. You are pressing it with a foot either
       way; the rule that a plate does not fire when you TURN onto it or when
       the clock PUTS you on it is about things that happen to you, and a tap
       is not one of those. */
    if(isGlyph(s.t)){
      pushUndo();
      var r0 = tileRect(u, v);
      firePlate(GLYPH_BIT[s.t], r0[0] + S/2, r0[1] + S/2);
      afterAction();
    }
    return;
  }
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
    /* A DOOR OPENING IS THE LOUDEST THING THAT IS NOT A TURN. It is the only
       moment a key stops being a number and becomes a thing that did
       something, so it gets the whole kit: shards off the bars, a jade front
       running out across the board, and the room lit green for a beat. */
    doors |= (1<<di); sfx.door(); buzz(24); toast('unlocked'); faceSet('happy', 800);
    kick(5, 0, 1); shake(0.34); punch(0.030); hitstop(46); flash('53,215,161', 0.30, 520);
    burst(cx2, cy2, 26, {spd:150, life:640, r:2.4, col:'53,215,161', g:110});
    burst(cx2, cy2, 10, {kind:'chip', spd:110, life:760, r:2.0, col:'40,150,116', g:340});
    ring(cx2, cy2, {r0:S*0.2, r1:S*2.6, dur:520, col:'53,215,161', a:0.65, w:3.5});
  } else {
    sfx.step();
    /* a footfall knocks grit off the block, and the block answers with a
       square front one cell wide — small, but it is the difference between
       a marker moving and a thing landing on stone */
    burst(cx2, cy2 + S*0.20, 4, {kind:'chip', spd:34, life:420, r:1.5,
                                 col:chipCol(cell.t, cell.d), g:260, fade:0.9});
    ring(cx2, cy2, {r0:S*0.30, r1:S*0.62, dur:260, col:'56,214,255', a:0.30, w:2, sq:true});
    shake(0.045);
  }
  var ki = keyIndexAt(lv, pos);
  if(ki >= 0 && !(kmask & (1<<ki))){
    kmask |= (1<<ki); sfx.key(); buzz(12); toast('key'); faceSet('happy', 900);
    shake(0.20); punch(0.022); hitstop(30); flash('34,224,106', 0.26, 460);
    burst(cx2, cy2, 20, {spd:130, life:560, r:2.2, col:'34,224,106', g:20});
    burst(cx2, cy2, 8, {kind:'ember', spd:26, lift:40, life:900, r:2.0, col:'170,255,200', g:-24});
    ring(cx2, cy2, {r0:S*0.15, r1:S*1.9, dur:460, col:'34,224,106', a:0.7, w:3});
    flyKey();
  }
  var gb = glyphAt(lv, pos);
  if(gb){ firePlate(gb, cx2, cy2); }
  walking.t = 0;
  if(!walking.queue.length){
    walking = null; sfx.land(); kick(3, 0, 1); shake(0.10);
    hopT = 0; faceSet('land', 260);
    burst(cx2, cy2 + S*0.24, 7, {kind:'chip', spd:44, life:460, r:1.7,
                                 col:chipCol(cell.t, cell.d), g:300, fade:0.8});
    burst(cx2, cy2, 5, {kind:'ember', spd:20, lift:26, life:700, r:1.6, col:'150,250,255', g:-18});
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
  /* A REFUSAL IS A COLLISION. It gets more trauma than a successful turn and
     none of the reward: no flash, no front going out, no zoom in — the frame
     jolts, the walls close in for a beat, and stone chips off the stop it
     just hit. The vignette doing the work here is deliberate: light means
     yes in this game, so no has to be told in the dark. */
  /* a refusal gets a LONGER stop than a landing and no reward at all: the
     cube hit a wall, and a wall is the one thing in this game that should
     take a frame away from you */
  kick(5, t < 2 ? -sgn : 0, t < 2 ? 0 : sgn);
  shake(0.5); vig = 1; punch(-0.012); hitstop(78); squash(t < 2 ? 'x' : 'y', 0.045);
  var rr0 = tileRect(N/2 - 0.5 + (t < 2 ? -sgn*N*0.42 : 0), N/2 - 0.5 + (t < 2 ? 0 : sgn*N*0.42));
  burst(rr0[0] + S/2, rr0[1] + S/2, 12, {kind:'chip', spd:120, life:620, r:1.8,
                                         col:chipCol('#', N-1), g:420, fade:0.8,
                                         ang: t < 2 ? (sgn > 0 ? 0 : Math.PI) : (sgn > 0 ? -1.57 : 1.57),
                                         spread:1.6});
  sfx.deny(); buzz(18); toast('no footing that way'); faceSet('wide', 620);
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
  /* THE LANDING IS THE VERB. Everything else in this file is scored under
     it, and it gets four separate things happening at once on purpose:

       the AIMED kick, along the turn's own axis, so the shove has a direction
       TRAUMA on top of it, so the whole frame is briefly unsafe
       a PUNCH of zoom, which is what makes it read as a hit rather than a slide
       a SQUARE front leaving the cube at board scale, because the thing that
         just moved was a grid and a circle would be lying about that

     and then grit rains off every part of the board, seeded across the whole
     face rather than at the player, because the mass that arrived was the
     cube's and not yours. */
  var sgn = (t === 1 || t === 2) ? 1 : -1;
  kick(9, t < 2 ? -sgn : 0, t < 2 ? 0 : sgn);
  shake(0.62); punch(0.055); hitstop(52); squash(t < 2 ? 'x' : 'y', 0.075);
  flash((ST.dN[0]|0) + ',' + (ST.dN[1]|0) + ',' + (ST.dN[2]|0), 0.20, 480);
  ring(CX, CY, {r0:N*S*0.18, r1:N*S*0.92, dur:620, col:(ST.dN[0]|0)+','+(ST.dN[1]|0)+','+(ST.dN[2]|0), a:0.5, w:5, sq:true});
  var pv = viewOf(N, M, pos), pr = tileRect(pv[0], pv[1]);
  for(var g = 0; g < 9; g++){
    var gu = (Math.random()*N)|0, gv = (Math.random()*N)|0, gs = surf[gu*N + gv];
    var grr = tileRect(gu, gv);
    burst(grr[0] + S*Math.random(), grr[1] + S*Math.random(), 2,
          {kind:'chip', spd:30, life:760, r:1.6, g:300, fade:0.65,
           col: gs ? chipCol(gs.t, gs.d) : '190,180,160'});
  }
  burst(pr[0]+S/2, pr[1]+S/2, 10, {spd:78, life:440, r:2.1, col:'150,250,255', g:60});
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
  /* the dead-end check used to live here as its own solver run. It is the
     same question the live readout asks every time the state changes, so
     there is one run now and this is just the trigger. */
  scheduleRemain();
}

/* ============================================================
   FEWEST FROM HERE — par, live, for the position you are actually in.

   The turn counter says what a run has cost. Par says what the cube costs.
   Neither of them answers the question a player is actually holding: is the
   line I am on still a good one? Two turns over par is a fact you learn on
   the win card, ten moves after the mistake, when it is too late to be
   information and can only be a verdict.

   So the solver runs from the CURRENT state and the number goes on the HUD.
   Read it as a countdown: three to go, two to go, out. And read the colour
   as the only judgement it makes —

     jade   turns spent + turns to come is still exactly par. Perfect line.
     gold   it is not. You have lost turns you cannot get back, and you knew
            the instant it happened rather than at the end.
     red    there is no way on from here at all. Undo.

   This is the same search the dead-end check was already running after every
   action, so it costs one solve per state change exactly as before — and
   states are cached, which makes undo instant and makes walking back and
   forth free.

   IT IS NEVER ALLOWED TO COST A FRAME. The run is deferred, it refuses to
   start while anything is animating, and until it lands the pill shows a dot
   rather than a stale number pretending to be current.
   ============================================================ */
var remain = null, remainOver = false, remainStale = false, remainT = 0, remainCache = {}, remainN = 0;
function stateKey(){
  return vidx(N, pos[0], pos[1], pos[2]) + ':' + oriIndex(M) + ':' + kmask + ':' + doors + ':' + world;
}
function scheduleRemain(){
  /* the attract cube has a level loaded and nobody playing it */
  if(!lv || won || demo) return;
  clearTimeout(remainT);
  var k = stateKey(), c = remainCache[k];
  if(c !== undefined){                     /* seen this exact state before — undo is instant */
    remain = c.v; remainOver = c.o; remainStale = false; stuck = remain < 0; paintGo(); return;
  }
  /* THE LAST ANSWER STAYS UP, DIMMED, while the next one is found. Blanking
     it to a dot after every single turn made the readout flicker at exactly
     the moment the player was looking at it, and a number that is two
     hundred milliseconds old and visibly greyed is more use than no number
     that is perfectly current. The dot is only for the very first answer of
     a cube, when there is nothing honest to leave up. */
  remainStale = true; paintGo();
  remainT = setTimeout(function(){ idle(runRemain); }, 70);
}
/* THE SEARCH GOES IN THE GAPS BETWEEN FRAMES.
   The hardest cube in the game is twenty-five milliseconds of search here and
   several times that on a slow phone, which is a dropped frame wherever it
   lands. requestIdleCallback puts it in the space after a frame is done
   rather than in the middle of one, with a timeout so it can never be
   starved into never answering. Where there is no rIC — an old WebView — the
   deferred call runs as it always did. */
function idle(fn){
  if(window.requestIdleCallback) requestIdleCallback(fn, {timeout:500});
  else fn();
}
function runRemain(){
  if(!lv || won) return;
  /* never mid-motion: the worst cube in the game is about a tenth of a
     second of search on a slow phone, and a tenth of a second landing inside
     a turn is the one stutter the whole file is written to avoid */
  if(walking || anim){ remainT = setTimeout(function(){ idle(runRemain); }, 70); return; }
  var k = stateKey();
  if(remainCache[k] === undefined){
    var r = solve(lv, 40, {pos:pos, ori:oriIndex(M), kmask:kmask, doors:doors, world:world});
    if(remainN > 600){ remainCache = {}; remainN = 0; }      /* a long session, bounded */
    remainCache[k] = {v: r.ok ? r.turns : -1, o: !r.ok && !!r.over};
    remainN++;
  }
  var e = remainCache[k];
  var wasStuck = stuck;
  remain = e.v; remainOver = e.o; remainStale = false;
  stuck = remain < 0 && !remainOver;
  if(stuck && !wasStuck){
    toast('no way on from here — undo'); sfx.stuck(); buzz(30); faceSet('wide', 1400);
    /* nothing flashes and nothing sparks: a dead end is the absence of a way
       on, so the room simply closes in */
    vig = 1; shake(0.22);
  }
  paintGo();
}
/* the pill, and every state it can be in */
var goShown = '';
function paintGo(){
  var el = $('goPill'); if(!el) return;
  if(!lv || !store.togo){ el.style.display = 'none'; return; }
  el.style.display = won ? 'none' : '';
  var txt, slip = false, dead = false, think = remainStale;
  if(remain === null){ txt = '·'; think = true; }
  else if(remainOver){ txt = '40+'; }
  else if(remain < 0){ txt = '✕'; dead = true; }
  else { txt = String(remain); slip = (turns + remain) > lv.par; }
  $('goN').textContent = txt;
  el.classList.toggle('think', think);
  if(think && remain !== null){
    /* A STALE NUMBER MUST NOT BE JUDGED. Turns have already gone up and the
       count has not come down yet, so "spent + to come" reads one over for a
       few frames — and a colour that means "you just lost the perfect line"
       is the one thing in this game that must never fire by accident. The
       verdict freezes at its last true value until the answer catches up. */
    return;
  }
  el.classList.toggle('slip', slip);
  el.classList.toggle('dead', dead);
  /* the bump fires on a CHANGED answer only, so a number that is holding
     steady is not flashing at somebody who is trying to think */
  if(txt !== goShown && !think){
    goShown = txt;
    el.classList.remove('bump'); void el.offsetWidth; el.classList.add('bump');
  } else if(think) goShown = txt;
}
function finish(){
  won = true;
  faceSet('win', 1200);
  clearTimeout(remainT); paintGo();          /* the count is over; the card takes it from here */
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
  /* THE EXIT, and the only place the effects budget is spent without
     restraint — you are leaving, so nothing after this has to stay legible.
     The orb is drawn INTO the door: a ring of sparks falls inward first,
     then everything leaves outward. */
  burst(gx, gy, 34, {spd:190, life:800, r:2.6, col:'255,196,77', g:0});
  burst(gx, gy, 18, {spd:70, life:1000, r:3.4, col:'255,246,224', g:-14});
  burst(gx, gy, 22, {kind:'ember', spd:14, lift:70, life:1500, r:2.4, col:'255,226,150', g:-40, fade:0.8});
  beam = {x:gx, y:gy, t:0, dur:1100};
  ring(gx, gy, {r0:S*0.2, r1:N*S*1.2, dur:900, col:'255,196,77', a:0.85, w:7, sq:true});
  ring(gx, gy, {r0:S*0.2, r1:N*S*0.7, dur:600, col:'255,246,224', a:0.6, w:4});
  kick(11, 0, 1); shake(1); punch(0.09);
  /* THE ONE PLACE THE CLOCK REALLY BENDS. A long stop, then a third of
     speed for most of a second, so the board comes apart at the pace of
     something ending rather than something being dismissed. */
  hitstop(150); slowmo(0.30, 900);
  /* exitFlash already floods the frame green for most of a second; this one
     only has to be the leading edge of it, and two full-strength washes
     stacked read as a white-out rather than as a door opening */
  flash('255,196,77', 0.22, 620);
  exitFlash = 1;
  /* A PERFECT LINE IS A DIFFERENT EVENT, and it has never once looked like
     one. Par is the whole scoring system in this game; clearing at par with
     the same exit as clearing four over says the game does not care, and
     the card saying FEWEST POSSIBLE forty frames later is a receipt, not a
     celebration. Gold, because gold is what the marks on the level select
     are made of, and thirty chips of it thrown up through the jade. */
  if(turns <= lv.par){
    setTimeout(function(){
      /* the exit is gold now, so a perfect line cannot also be gold — it
         goes WHITE, which is the one step past gold anybody reads without
         being told */
      flash('255,252,240', 0.30, 900);
      shake(0.7); punch(0.05); sfx.vault();
      ring(gx, gy, {r0:S*0.2, r1:N*S*1.3, dur:1000, col:'255,252,240', a:0.85, w:6, sq:true});
      burst(gx, gy, 34, {kind:'chip', spd:210, life:1500, r:2.2, col:'255,248,228', g:180, fade:0.9});
      burst(gx, gy, 22, {kind:'ember', spd:30, lift:120, life:1800, r:2.4, col:'255,255,245', g:-46, fade:0.85});
    }, 210);
  }
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
/* THE THIRD VERB, AND IT COSTS THE OTHER TWO NOTHING.
   A gesture is a tap until it travels (drag) or until it stays (peek). The
   timer is armed on every touch and disarmed by the first pixel of travel,
   so a drag can never become a peek and a peek can never become a walk —
   once the cube is glass, letting go only closes it. */
var HOLD_MS = 210, holdT = 0;
function armHold(){
  clearTimeout(holdT);
  holdT = setTimeout(function(){ if(drag && !drag.axis) peekOn(); }, HOLD_MS);
}
function dropHold(){ clearTimeout(holdT); }
function onDown(e){
  if(won || anim || walking || !lv) return;
  if(!$('hud').classList.contains('on')) return;
  audio();
  var p = pointer(e);
  if(drag && drag.pad) drag = null;          /* a finger outranks a stick */
  drag = {x0:p.x, y0:p.y, lx:p.x, ly:p.y, axis:null, ang:0};
  armHold();
}
function onMove(e){
  if(!drag) return;
  var p = pointer(e), dx = p.x - drag.x0, dy = p.y - drag.y0;
  /* ORBIT. The hold has already landed, so this gesture is looking rather
     than turning: it moves the camera one-to-one with the thumb, at the same
     pixels-per-quarter-turn the drag uses, and it returns before anything
     that could commit a turn ever runs. */
  if(peek.on){
    if(e.cancelable) e.preventDefault();
    var q = pxPerQuarter() * 0.66;
    peek.yaw += (p.x - drag.lx) / q * (Math.PI/2);
    peek.pitch = Math.max(-PEEK_MAXP, Math.min(PEEK_MAXP,
                 peek.pitch - (p.y - drag.ly) / q * (Math.PI/2)));
    drag.lx = p.x; drag.ly = p.y;
    return;
  }
  if(!drag.axis){
    if(Math.abs(dx) < TAP_SLOP && Math.abs(dy) < TAP_SLOP) return;
    dropHold(); peekOff();
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
  dropHold();
  if(!drag){ peekOff(); return; }
  if(!drag.axis){
    var wasPeek = peek.on;
    var p = {x:drag.x0, y:drag.y0};
    drag = null;
    peekOff();
    if(wasPeek) return;                 /* a hold is looking, never walking */
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
cv.addEventListener('touchcancel', function(){ dropHold(); peekOff(); springBack(); });
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
  /* the peek, for a keyboard: hold it exactly the way a thumb would */
  else if((e.key === ' ' || e.key === 'Shift') && !e.repeat){ e.preventDefault(); peekOn(); }
});
/* the arrow keys turn the cube; held down with the peek open they orbit it,
   which is the same split the thumb gets */
window.addEventListener('keydown', function(e){
  if(!peek.on) return;
  var m = {ArrowLeft:[-1,0], ArrowRight:[1,0], ArrowUp:[0,1], ArrowDown:[0,-1]}[e.key];
  if(!m) return;
  e.preventDefault(); e.stopImmediatePropagation();
  peek.yaw += m[0]*0.18;
  peek.pitch = Math.max(-PEEK_MAXP, Math.min(PEEK_MAXP, peek.pitch + m[1]*0.18));
}, true);
window.addEventListener('keyup', function(e){
  if(e.key === ' ' || e.key === 'Shift') peekOff();
});
window.addEventListener('blur', function(){ dropHold(); peekOff(); });

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
  /* any screen opening ends a peek: the cube must never be left as glass
     behind a card, and a thumb still down when a menu appears is no longer
     holding anything */
  dropHold(); peekOff(); drag = null;
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
  /* thin glass over the attract cube, a solid plate over a live puzzle */
  $('scManual').classList.toggle('solid', !demo);
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
  /* the tag owns child nodes now — the clock and the drain bar — so the name
     goes in its own span. Writing textContent on the tag itself would delete
     both of them and the countdown would silently stop existing. */
  $('worldName').textContent = names[world] || '';
  wt.classList.toggle('on', world !== 0);
  wt.style.color = 'rgb(' + (world === 2 ? PLATE_COL[2] : PLATE_COL[1]) + ')';
  wt.style.boxShadow = 'inset 0 0 0 1px rgba(' + (world === 2 ? PLATE_COL[2] : PLATE_COL[1]) + ',.34)';
  wt.style.background = 'rgba(' + (world === 2 ? PLATE_COL[2] : PLATE_COL[1]) + ',.10)';
  var kp = $('keyPill'), held = keysHeld();
  kp.style.display = lv.keys.length ? '' : 'none';
  $('keyN').textContent = held;
  $('btnUndo').disabled = !undoStack.length;
  /* the live par is painted from here too, because whether it is jade or
     gold depends on the turn count this function just wrote */
  paintGo();
  paintPlateClock();
  refreshTicks();
}

/* THE COUNTDOWN IS PAINTED PER FRAME, and it is the only piece of DOM in the
   game that is. Every other readout changes when the STATE changes, which is
   what refreshHud() is for; this one changes because time passed, and a
   sixty-times-a-second bar drawn from a state hook would sit still.

   It is three writes and two of them are guarded, so the cost is a class
   toggle and a transform on the frames where anything actually moved. */
var plateShown = '';
function paintPlateClock(){
  var bar = $('worldBar'), clk = $('worldClock'), wt = $('worldTag');
  if(!world){
    if(plateShown !== ''){ plateShown = ''; clk.textContent = ''; bar.style.transform = 'scaleX(1)'; wt.classList.remove('hot'); }
    return;
  }
  var left = Math.max(0, plateT) / PLATE_MS;
  bar.style.transform = 'scaleX(' + left.toFixed(4) + ')';
  /* one decimal under two seconds, whole seconds above it: the extra digit
     is only worth reading when a tenth is the difference between one more
     tap and none */
  var s = plateT <= 2000 ? (plateT/1000).toFixed(1) : String(Math.ceil(plateT/1000));
  if(s !== plateShown){ plateShown = s; clk.textContent = s; }
  wt.classList.toggle('hot', plateT <= 2000);
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
  sfx.undo();
  /* an undo is time running backwards, so the front runs INWARD — same
     mechanism as every other beat, sign flipped, and it reads instantly as
     the opposite of the thing that put you here */
  ring(CX, CY, {r0:N*S*0.75, r1:N*S*0.10, dur:420, col:'150,190,255', a:0.5, w:3, sq:true});
  flash('120,170,255', 0.13, 320); shake(0.16); punch(0.02);
  revealT = 0;
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
  /* one line, and it fits on one line: the vault, its name, and par. The
     old string wrapped to two on every phone, which is why the card opened
     with a paragraph instead of a heading. */
  $('pauseSub').textContent = lv ? ('VAULT ' + roman(vaultOf(levelNo)) + ' · ' + vaultName(vaultOf(levelNo)) +
      ' · PAR ' + lv.par) : '';
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
bindSwitch('swFx', 'fx');
bindSwitch('swToGo', 'togo');

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
   A CONTROLLER, BECAUSE DOCKED THERE IS NO TOUCHSCREEN.

   Handheld this game is a touchscreen game and stays one — every gesture in
   the file is the primary way to play it. But a console is the same
   software on a television with a pad in your hands and no glass to touch
   at all, and a build that cannot be played that way is not a build.

   The mapping is chosen so the pad expresses the same three verbs the thumb
   does, and nothing else:

     STICK / DPAD   walk one block. The board is a four-way grid and the
                    game already routes a tap to an adjacent cell, so a
                    direction IS a step — no cursor to drive, nothing to
                    select, no second layer of interface.
     R / ZR (hold)  THE CUBE IS IN YOUR HAND. Stick tilts it live; dpad
                    snaps quarter turns.
     L / ZL / Y     peek, held. Right stick orbits it.
     B  undo.   X  hint.   A  confirm on a card.   + or -  pause.

   THE SHOULDER IS THE THUMB, and this is the part worth explaining.

   Four buttons for four turns worked and felt like nothing, because the
   touch gesture it was standing in for is not a button — it is an ANALOG
   drag with a detent in the middle of it. You push the cube, it resists,
   you feel where halfway is, and past that it commits. Binding that to a
   press throws away the whole mechanism.

   So the shoulder does not turn anything. It hands the left stick over to
   the cube, and the stick then drives the exact same `drag` object a finger
   drives: the same axis lock on first deflection, the same creak that
   climbs as you approach the detent, the same tick arming past halfway, the
   same spring-back if you let go short. The cube is under your thumb, not
   behind a button.

   It commits two ways, and both are the same rule from opposite ends. Push
   the stick past four fifths and it clicks THROUGH the detent — a stop you
   shove past, latched so holding it there cannot spin the cube twice. Or
   let go of the shoulder while it is past halfway, which is exactly what
   lifting a finger does. Short of halfway, either way, it springs back.

   The dpad stays bound to quarter turns while the shoulder is held, because
   an analog stick is a poor way to ask for precisely one turn and some
   players will always want the discrete answer.

   Repeat on walking is deliberately slow — this is a puzzle, and a stick
   that fires four steps because it was held a beat too long is a stick that
   undoes your plan.
   ============================================================ */
var padPrev = {}, padRepeat = 0, padWasPeek = false;
function padGet(){
  if(!navigator.getGamepads) return null;
  var list = navigator.getGamepads();
  for(var i = 0; i < list.length; i++) if(list[i] && list[i].connected) return list[i];
  return null;
}
function padPoll(raw){
  var g = padGet();
  if(!g){ if(padWasPeek){ padWasPeek = false; peekOff(); } return; }
  var b = g.buttons, ax = g.axes || [], i;
  function down(n){ return !!(b[n] && (b[n].pressed || b[n].value > 0.4)); }
  function hit(n){
    var d = down(n), was = padPrev[n];
    padPrev[n] = d;
    return d && !was;
  }
  /* the two held controls, and they are mutually exclusive: you are either
     looking into the cube or turning it, never both */
  var wantPeek = down(3) || down(4) || down(6);             /* Y, L, ZL */
  var rotHold = !wantPeek && (down(5) || down(7));          /* R, ZR */
  if(wantPeek && !padWasPeek){ padWasPeek = true; audio(); peekOn(); }
  else if(!wantPeek && padWasPeek){ padWasPeek = false; peekOff(); }
  if(peek.on){
    var rx = ax[2] || 0, ry = ax[3] || 0;
    if(Math.abs(rx) > 0.16) peek.yaw += rx * raw/1000 * 2.4;
    if(Math.abs(ry) > 0.16) peek.pitch = Math.max(-PEEK_MAXP, Math.min(PEEK_MAXP, peek.pitch - ry * raw/1000 * 2.0));
  }
  /* a card is open: the pad drives the card, not the cube */
  if(!$('hud').classList.contains('on')){
    if(hit(0) || hit(9) || hit(1)){
      var layerOn = ['scWin','scPause','scPlate','scManual','scCubes','scTitle'].filter(function(id){
        return !$(id).classList.contains('hide');
      })[0];
      if(layerOn){
        var btn = $(layerOn).querySelector('.btn.primary') || $(layerOn).querySelector('.btn');
        if(btn) btn.click();
      }
    }
    for(i = 0; i < 12; i++) padPrev[i] = down(i);
    return;
  }
  if(hit(9) || hit(8)){ show('scPause'); return; }
  if(hit(1)) undo();
  if(hit(2)) hint();                                        /* X */

  /* ---- the shoulder, and the stick that becomes the cube ---- */
  var padDrag = drag && drag.pad;
  if(rotHold && !walking && !anim && !won){
    if(!padDrag){ drag = {x0:0, y0:0, lx:0, ly:0, axis:null, ang:0, pad:1}; padDrag = true; }
    var lx = ax[0] || 0, ly = ax[1] || 0;
    /* the dpad answers in whole turns while the shoulder is down */
    if(hit(14)) { drag = null; if(!tryTurn(0)) refuseTurn(0); }
    else if(hit(15)) { drag = null; if(!tryTurn(1)) refuseTurn(1); }
    else if(hit(12)) { drag = null; if(!tryTurn(2)) refuseTurn(2); }
    else if(hit(13)) { drag = null; if(!tryTurn(3)) refuseTurn(3); }
    else if(drag){
      if(!drag.axis && (Math.abs(lx) > 0.34 || Math.abs(ly) > 0.34))
        drag.axis = Math.abs(lx) > Math.abs(ly) ? 'x' : 'y';
      if(drag.axis){
        var amt = drag.axis === 'x' ? lx : -ly;
        var q = Math.max(-1, Math.min(1, amt * 1.12));
        var was = Math.abs(drag.ang);
        drag.ang = q * Math.PI/2;
        if(Math.abs(drag.ang) - was > 0.12) sfx.creak(Math.abs(q));
        if(was < Math.PI/4 && Math.abs(drag.ang) >= Math.PI/4) buzz(8);
        refreshTicks();
        /* Through the detent — and the latch lives on padPrev, not on the
           drag. The drag object is destroyed and rebuilt every time a turn
           animation starts and ends, so a latch stored there is a latch that
           forgets: hold the stick at the stop and the cube would turn, land,
           rebuild the drag, and turn again. It stays until the stick comes
           back to centre, wherever the drag has been in the meantime. */
        if(!padPrev.rotLatch && Math.abs(q) >= 0.82){
          var t2 = drag.axis === 'x' ? (q > 0 ? 1 : 0) : (q > 0 ? 2 : 3);
          drag = null; padPrev.rotLatch = 1;
          if(!tryTurn(t2)) refuseTurn(t2);
        }
      }
    }
    if(padPrev.rotLatch && Math.abs(ax[0] || 0) < 0.3 && Math.abs(ax[1] || 0) < 0.3) padPrev.rotLatch = 0;
  } else if(padDrag){
    /* the shoulder came up: exactly what lifting a finger does */
    var pa = drag.ang, pax = drag.axis;
    drag = null; padPrev.rotLatch = 0;
    if(pax && Math.abs(pa) > Math.PI/4){
      var t3 = pax === 'x' ? (pa > 0 ? 1 : 0) : (pa > 0 ? 2 : 3);
      if(!tryTurn(t3)) refuseTurn(t3);
    }
    refreshTicks();
  }
  /* walking: dpad or the left stick, one block a press and four a second held */
  var dx = (down(15) ? 1 : 0) - (down(14) ? 1 : 0) + ((ax[0] || 0) > 0.55 ? 1 : (ax[0] || 0) < -0.55 ? -1 : 0);
  var dy = (down(13) ? 1 : 0) - (down(12) ? 1 : 0) + ((ax[1] || 0) > 0.55 ? 1 : (ax[1] || 0) < -0.55 ? -1 : 0);
  dx = Math.max(-1, Math.min(1, dx)); dy = Math.max(-1, Math.min(1, dy));
  if(!dx && !dy){ padRepeat = 0; }
  else if(!peek.on && !rotHold && !walking && !anim && !won){
    padRepeat -= raw;
    if(padRepeat <= 0){
      padRepeat = padPrev.walked ? 250 : 330;
      padPrev.walked = 1;
      var v = viewOf(N, M, pos);
      /* THE BOARD'S V AXIS POINTS UP THE SCREEN and the stick's does not:
         tileRect puts v = 0 at the BOTTOM (y = CY + (N/2 - 1 - v)*S), while
         a stick pushed down reads +1. Adding them walked the player away
         from the direction the thumb was pointing, which is the one thing a
         d-pad is not allowed to do. */
      tapCell(v[0] + dx, v[1] - (dy > 0 ? 1 : dy < 0 ? -1 : 0));
    }
  }
  if(!dx && !dy) padPrev.walked = 0;
  for(i = 0; i < 18; i++) padPrev[i] = down(i);
}
/* naming the two held controls is the whole tutorial: everything else on
   the pad is where a player would guess it is */
window.addEventListener('gamepadconnected', function(){ toast('controller — hold R to turn, L to look inside'); });

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
  var raw = last ? Math.min(64, ts - last) : 16;
  last = ts;
  var dt = stepTime(raw);
  try{
    padPoll(raw);                       /* input is never scaled, and never frozen */
    advanceAnim(dt);
    advanceWalk(dt);
    stepPlateClock(dt); paintPlateClock();          /* before anything reads the board */
    stepMotes(dt); stepParts(dt); stepWave(dt); stepFlip(dt); stepFlyers(dt); stepDemo(dt);
    stepCam(dt); stepFlashes(dt); stepPeek(dt); stepOrb(dt); stepBeam(dt); stepDoors(dt);
    stepFace(dt);
    if(hopT < 1) hopT = Math.min(1, hopT + dt/300);
    stepSmear(raw);
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
buildMotes(); setStyle(0);
try{ $('wordmark').src = buildWordmark('TURNKEY', 22).toDataURL('image/png'); }catch(e){}
fit();
show('scTitle');
/* the label has its own node: writing textContent on the button itself wiped
   the play chip out of the DOM every boot */
$('playLabel').textContent = store.reached > 1 ? 'CONTINUE' : 'BEGIN';
requestAnimationFrame(loop);
