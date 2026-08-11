/* ============================================================
   VAULTS — ten cubes to a block, and a block owns a look.

   COLOUR THAT MEANS SOMETHING NEVER CHANGES. You are always rust, the way
   out is always jade, keys are always gold — a player who learns those on
   cube three must still be able to read cube nine hundred at a glance. What
   changes with the vault is the MATERIAL: what the deck is made of, what
   the bedrock is made of, and what colour the nothing behind it is.

   Eight are authored. Past those the palette keeps going by rotating the
   authored hues, so vault ninety has a look nobody chose but nobody has
   seen either — which is the honest deal for a game with no last level.
   ============================================================ */
/* EIGHT WORLDS, NOT EIGHT QUARRIES.

   These were named for industry and lit like one: eight variations on wet
   stone in a dark room, every deck a shade of bone and every sky within a
   few points of black. It read as serious, which is not the same as good,
   and it is the opposite of what this game is — a bright object turning in
   your hands.

   So each vault is a PLACE with a colour it could only be. The rule that
   makes them work is the one Galaxy uses: the ground is candy-saturated and
   the sky behind it is deep and SATURATED TOO — never grey, never black.
   A dark violet sky makes a lime platform sing; a dark grey one makes it
   look like a lime platform in a car park.

   The bands still hold. Deck luminance stays high, bedrock is walked down
   by enforceBands as it always was, and the guarantee has not moved an inch
   — it is the hues that changed, not the contract.                        */
var VAULTS = [
  {name:'MEADOW',     dF:[104,168,72],  dN:[196,246,150], rF:[38,44,30],  rN:[88,74,52],   vd:[14,18,34],  st:[210,240,255], at:[70,120,220]},
  {name:'SANDCASTLE', dF:[186,148,72],  dN:[255,232,168], rF:[62,42,30],  rN:[126,84,52],  vd:[20,16,34],  st:[255,226,180], at:[210,140,70]},
  {name:'BUBBLEGUM',  dF:[196,110,158], dN:[255,196,228], rF:[52,28,58],  rN:[104,58,116], vd:[26,12,36],  st:[255,206,246], at:[214,90,190]},
  {name:'GLACIER',    dF:[112,168,196], dN:[206,244,255], rF:[26,42,64],  rN:[62,92,134],  vd:[10,18,40],  st:[220,244,255], at:[80,150,240]},
  {name:'EMBERFALL',  dF:[204,116,64],  dN:[255,206,158], rF:[62,24,20],  rN:[128,50,34],  vd:[28,10,14],  st:[255,190,150], at:[240,96,54]},
  {name:'LAGOON',     dF:[86,178,166],  dN:[186,250,238], rF:[20,48,52],  rN:[48,104,110], vd:[8,24,34],   st:[190,255,246], at:[54,190,196]},
  {name:'TWILIGHT',   dF:[146,128,208], dN:[218,208,255], rF:[36,28,64],  rN:[78,64,134],  vd:[16,10,34],  st:[226,210,255], at:[140,96,240]},
  {name:'HONEYCOMB',  dF:[204,158,60],  dN:[255,224,146], rF:[58,40,18],  rN:[122,86,34],  vd:[26,18,10],  st:[255,232,176], at:[236,160,50]}
];
var VAULT_A = ['IRON','SALT','GLASS','ASH','BONE','SLATE','AMBER','TIDE','EMBER','FROST','COPPER','SHALE'];
var VAULT_B = ['REACH','WELL','SPINE','GATE','HOLLOW','WORKS','MARCH','VAULT','TERRACE','CANT'];
var NAME_A  = ['THE','LOW','HIGH','OLD','DEEP','LONG','BLIND','CLOSE','FAR','LOST'];
var NAME_B  = ['CANT','HINGE','LATCH','WARD','DROP','SPINE','SEAM','FOLD','TURN','GATE','WELL','STEP','CROSS','MOUTH','KEEP'];

/* The standard hue-rotation matrix, so a rotated palette keeps its
   luminance relationships and therefore keeps the deck/bedrock bands from
   colliding — the one property the whole board's legibility rests on. */
function hueRot(c, deg){
  var a = deg*Math.PI/180, co = Math.cos(a), si = Math.sin(a);
  var r = c[0], g = c[1], b = c[2];
  return [
    Math.max(0, Math.min(255, r*(0.213+co*0.787-si*0.213) + g*(0.715-co*0.715-si*0.715) + b*(0.072-co*0.072+si*0.928))),
    Math.max(0, Math.min(255, r*(0.213-co*0.213+si*0.143) + g*(0.715+co*0.285+si*0.140) + b*(0.072-co*0.072-si*0.283))),
    Math.max(0, Math.min(255, r*(0.213-co*0.213-si*0.787) + g*(0.715-co*0.715+si*0.715) + b*(0.072+co*0.928+si*0.072)))
  ];
}
function vaultOf(level){ return Math.floor((level-1)/10); }
function lumOf(c){ return 0.2126*c[0] + 0.7152*c[1] + 0.0722*c[2]; }

/* THE BAND GUARANTEE.

   Everything the player reads off this board rests on one property: the
   DARKEST deck is brighter than the BRIGHTEST bedrock, so a tile's material
   can never be mistaken however far away it is. A hand-picked palette can
   be checked by eye. A palette produced by rotating hues for vault ninety
   cannot, and one of them did break — vault 22's darkest deck, under the
   deepest cast shadow the renderer draws, came out below its brightest
   bedrock and the two materials became confusable.

   So the property is not trusted, it is ENFORCED. Every style, authored or
   generated, has its bedrock ramp walked down until the gap clears the
   worst shadow the renderer can put on a deck, with a margin. It costs the
   authored palettes a shade of contrast they will not miss, and it means
   there is no vault number at which the board stops being readable.       */
var MAX_SHADOW = 0.40 * 0.45;                 /* peak per-tile alpha x peak gradient alpha */
var DECK_FLOOR = 108;                         /* the dimmest a deck may ever be */

/* THE TEXTURE'S SHARE OF THE SAME BUDGET.

   Every block is painted from a 16x16 texture now, and a texture here is
   nothing but a set of per-texel MULTIPLIERS on the palette colour the
   material already has. Which means a texture can darken a deck texel and
   lighten a bedrock texel — precisely the thing the band guarantee exists
   to forbid. So the guarantee is told about it rather than being quietly
   undermined by it.

   These four numbers are the entire contract. No generator may emit a texel
   outside its material's range (finishTex clamps every one, unconditionally),
   and the walk below is run against the WORST pair the ranges permit: the
   far end of the deck ramp, at its darkest texel, under the deepest drop
   shadow the renderer can cast — against the near end of the bedrock ramp at
   its brightest texel. Widen a range and the bedrock ramp simply darkens to
   pay for it. The two materials cannot collide whatever a generator does.

   The two floors are not the same number, and the asymmetry is the whole
   trick: only the BRIGHT end of bedrock and the DARK end of deck can ever
   make two materials confusable, so bedrock is allowed to go very dark
   indeed. That is what buys rubble its contrast — a mortar line at 0.55 of
   an already-dark rock is visible, where the same line at 0.74 was a rumour. */
var TEX_LO = {'+':0.80, '#':0.55};            /* darkest texel each material may paint */
var TEX_HI = {'+':1.12, '#':1.10};            /* brightest */
function bandGap(st){
  return lumOf(st.dF) * TEX_LO['+'] * (1 - MAX_SHADOW) - lumOf(st.rN) * TEX_HI['#'];
}
function enforceBands(st){
  /* A deck at the far end of the cube was coming out near-black-brown and
     reading as bedrock on a real phone in real light — which is the one
     mistake this game cannot afford, because "may I stand there" is the
     question every frame is asked. The far end of the deck ramp now has a
     floor, and the bedrock ramp is walked down from wherever that lands. */
  var g0 = 0;
  while(lumOf(st.dF) < DECK_FLOOR && g0++ < 60)
    st.dF = [Math.min(255, st.dF[0]*1.05 + 3), Math.min(255, st.dF[1]*1.05 + 3), Math.min(255, st.dF[2]*1.05 + 3)];
  var margin = lumOf(st.dF) * 0.06, guard = 0;
  while(bandGap(st) < margin && guard++ < 60){
    st.rN = [st.rN[0]*0.94, st.rN[1]*0.94, st.rN[2]*0.94];
    st.rF = [st.rF[0]*0.94, st.rF[1]*0.94, st.rF[2]*0.94];
  }
  return st;
}
var styleCache = {};
function vaultStyle(band){
  if(styleCache[band]) return styleCache[band];
  var base = VAULTS[band % VAULTS.length], lap = Math.floor(band / VAULTS.length), st;
  if(!lap){
    st = {name:base.name, dF:base.dF.slice(), dN:base.dN.slice(), rF:base.rF.slice(),
          rN:base.rN.slice(), vd:base.vd.slice(), st:base.st.slice(), at:base.at.slice()};
  } else {
    var deg = lap * 37 % 360;
    st = {name: VAULT_A[(band*7) % VAULT_A.length] + ' ' + VAULT_B[(band*5) % VAULT_B.length],
          dF:hueRot(base.dF,deg), dN:hueRot(base.dN,deg), rF:hueRot(base.rF,deg),
          rN:hueRot(base.rN,deg), vd:hueRot(base.vd,deg), st:hueRot(base.st,deg), at:hueRot(base.at,deg)};
  }
  return (styleCache[band] = enforceBands(st));
}
function vaultName(band){ return vaultStyle(band).name; }
function levelName(level){
  if(level <= BAKED.length) return BAKED[level-1].name;
  /* hashSeed is unsigned 32-bit, and `>>` would coerce it back to a SIGNED
     int32 — every hash above 2^31 then indexed negatively and named half the
     cubes past level 40 "undef". Unsigned shift throughout. */
  var h = hashSeed(level) >>> 0;
  return NAME_A[h % NAME_A.length] + ' ' + NAME_B[(h >>> 5) % NAME_B.length];
}

/* ============================================================
   THE SUPPLY — where a level comes from, and the promise attached to it

   Vault I is authored: ten cubes, verified offline, carrying the teaching
   beats (the first two show you a way out you cannot walk to, which is the
   entire lesson and is not something a generator can be trusted to stage).

   Everything from level eleven is cut on demand by mint(), and mint()
   cannot return an unwinnable cube — solvability is constructed by the
   carve and then proved by the solver before the level is handed over.
   A level is minted from its NUMBER, so cube 4,127 is the same cube on
   every phone on earth with no server involved.

   The cost is real — a couple of hundred milliseconds — so the next level
   is always cut in the background while the current one is being played,
   and the player never waits for the common case.
   ============================================================ */
var MINT_BUDGET = 240, mintCache = {}, prebuildT = 0;
function levelData(level){
  if(level <= BAKED.length){
    var s = BAKED[level-1];
    return {n:s.n, vox:s.vox.split(''), start:s.start, goal:s.goal, keys:s.keys,
            doors:s.doors, par:s.par, level:level, band:vaultOf(level), authored:true};
  }
  if(mintCache[level]) return mintCache[level];
  var lv = mint(level, MINT_BUDGET);
  mintCache[level] = lv;
  var ks = Object.keys(mintCache);
  if(ks.length > 24) delete mintCache[ks[0]];
  return lv;
}
function prebuild(level){
  clearTimeout(prebuildT);
  if(level <= BAKED.length || mintCache[level]) return;
  prebuildT = setTimeout(function(){
    if(!mintCache[level]) mintCache[level] = mint(level, MINT_BUDGET * 2);
  }, 900);
}
