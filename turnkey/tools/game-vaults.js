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
var VAULTS = [
  {name:'THE SHALLOWS',  dF:[96,80,62],  dN:[246,238,222], rF:[15,14,21], rN:[56,61,82],  vd:[10,9,16],  st:[190,200,235], at:[90,110,190]},
  {name:'THE IRONWORKS', dF:[92,74,52],  dN:[240,222,196], rF:[22,14,11], rN:[86,52,38],  vd:[14,9,7],   st:[255,200,150], at:[200,110,50]},
  {name:'GLASSWORKS',    dF:[58,92,96],  dN:[220,248,250], rF:[8,18,22],  rN:[36,72,84],  vd:[5,12,16],  st:[160,235,245], at:[60,180,200]},
  {name:'THE ORCHARD',   dF:[80,92,50],  dN:[232,246,200], rF:[12,18,12], rN:[48,74,46],  vd:[8,13,9],   st:[190,240,180], at:[90,180,90]},
  {name:'CINDERS',       dF:[104,66,48], dN:[250,226,206], rF:[20,10,10], rN:[74,40,36],  vd:[13,7,7],   st:[255,170,130], at:[220,90,50]},
  {name:'THE SALT',      dF:[82,92,104], dN:[240,248,255], rF:[12,15,20], rN:[62,74,92],  vd:[8,10,14],  st:[220,235,255], at:[120,160,220]},
  {name:'THE VEIN',      dF:[96,78,44],  dN:[250,232,182], rF:[16,10,22], rN:[62,44,86],  vd:[11,7,16],  st:[210,180,255], at:[150,90,220]},
  {name:'THE DEEP',      dF:[72,72,78],  dN:[236,236,242], rF:[8,8,10],   rN:[42,44,52],  vd:[5,5,7],    st:[180,185,200], at:[80,90,120]}
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
function enforceBands(st){
  var need = lumOf(st.dF) * MAX_SHADOW * 1.3, guard = 0;
  while(lumOf(st.dF) - lumOf(st.rN) < need && guard++ < 60){
    st.rN = [st.rN[0]*0.94, st.rN[1]*0.94, st.rN[2]*0.94];
    st.rF = [st.rF[0]*0.94, st.rF[1]*0.94, st.rF[2]*0.94];
    need = lumOf(st.dF) * MAX_SHADOW * 1.3;
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
