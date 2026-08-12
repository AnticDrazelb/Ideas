/* ============================================================
   VAULTS — ten cubes to a block, and a block owns a look.

   COLOUR THAT MEANS SOMETHING NEVER CHANGES. You are always arc, the way
   out is always core, keys are always node, gates are always lock — a player
   who learns those on cube three must still be able to read cube nine hundred
   at a glance. What changes with the vault is the MATERIAL: what the live
   surface is made of, what the dead lattice is made of, and what colour the
   unrendered space behind it is.

   Eight are authored. Past those the palette keeps going by rotating the
   authored hues, so vault ninety has a look nobody chose but nobody has
   seen either — which is the honest deal for a game with no last level.
   ============================================================ */
/* EIGHT SECTIONS OF ONE ENGINE, NOT EIGHT QUARRIES.

   These were named for industry and lit like one: brown brick, bone slab,
   jade gem. That art was fighting the mechanics. Nothing in this game is a
   PLACE — you are not walking a dungeon, you are folding a projection until
   two things that were never adjacent become adjacent, and the one view that
   ever said so honestly was the wireframe you get when you hold the cube up
   to the light. Stone was the flat board disagreeing with its own peek.

   So the palette is no longer material, it is STATE. A deck is a live
   surface: lit, saturated, carrying current. Bedrock is dead lattice —
   near-black, cold, structured but unpowered. Void is unrendered space
   rather than a pit. Each vault is one section of the same engine, and what
   changes between them is which frequency it runs at.

   THE BANDS HAVE NOT MOVED AN INCH. Deck luminance stays high, bedrock is
   walked down by enforceBands exactly as before, and the guarantee is
   measured the same way it always was. Hues changed; the contract did not.
   Which is also why the traces cannot be bright lines on a dark board —
   see the note over TEX_LO.                                              */
var VAULTS = [
  {name:'ARC CORE',    dF:[54,124,156],  dN:[142,214,238], rF:[12,18,26],  rN:[30,42,56],  vd:[4,7,12],   st:[196,232,250], at:[38,138,196]},
  {name:'COLD BUS',    dF:[72,110,164],  dN:[158,192,238], rF:[14,18,30],  rN:[34,42,64],  vd:[5,7,15],   st:[204,220,248], at:[58,106,206]},
  {name:'PHASE DRIFT', dF:[116,106,172], dN:[194,186,238], rF:[20,17,30],  rN:[46,40,66],  vd:[8,6,15],   st:[216,208,246], at:[118,92,204]},
  {name:'NULL FIELD',  dF:[104,118,138], dN:[198,214,232], rF:[15,18,23],  rN:[38,44,54],  vd:[6,7,10],   st:[214,228,244], at:[92,114,148]},
  {name:'EMBER TRACE', dF:[168,110,62],  dN:[240,196,144], rF:[28,16,10],  rN:[64,38,22],  vd:[12,6,5],   st:[248,210,176], at:[210,110,48]},
  {name:'LIME LATTICE',dF:[118,150,72],  dN:[204,232,152], rF:[19,25,13],  rN:[44,56,28],  vd:[7,10,6],   st:[222,242,190], at:[124,176,64]},
  {name:'TEAL SPINE',  dF:[52,144,138],  dN:[148,226,216], rF:[10,24,23],  rN:[26,54,52],  vd:[4,10,10],   st:[196,244,236], at:[38,172,164]},
  {name:'SHUNT ROW',   dF:[150,104,124], dN:[228,190,204], rF:[24,15,19],  rN:[54,36,44],  vd:[10,5,7],   st:[240,212,222], at:[178,88,116]}
];
var VAULT_A = ['ARC','FLUX','NULL','PHASE','DRIFT','CORE','ECHO','GRID','PULSE','RAIL','SHUNT','VECTOR'];
var VAULT_B = ['ARRAY','BUS','LATTICE','MANIFOLD','SPINE','SECTOR','TRACE','WELL','NODE','LOOP'];
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
/* THE CLAMP IS WHY A TRACE IS NOT A BRIGHT LINE ON A DARK BOARD.

   Every texel of a live surface is held between 0.80 and 1.12 of its palette
   colour — a contrast range of 1.4:1 — because that is what keeps the
   DARKEST deck texel above the BRIGHTEST bedrock texel with a shadow on it.
   A schematic drawn the obvious way, dark substrate with glowing copper on
   top, would put its substrate squarely inside the bedrock band and destroy
   the one question the player asks every frame.

   So the technical read is carried by GEOMETRY instead: orthogonal runs,
   junction pads, right angles, registration ticks. Shape says "system" at
   exactly the luminance that brick said "stone", and the guarantee never
   hears about it.                                                         */
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
