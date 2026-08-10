var C = require('./core.js');

/* The campaign curve. Each entry is a demand, not a level: generate against
   it until the solver agrees the collapse is load-bearing. */
var CURVE = [
  /* The first two teach by showing. You can SEE the way out and you cannot
     walk to it, so the only lesson left to draw is that the cube must turn.
     From BURIED on, the goal is allowed to be hidden — by then "if it is not
     here it is behind you in an axis you are not looking down" is a rule the
     player already owns. */
  {name:'FOOTING',      n:5, turns:1, locks:0, density:0.46, legMin:2, legMax:4, showGoal:1},
  {name:'THE TURN',     n:5, turns:1, locks:0, density:0.42, legMin:3, legMax:5, showGoal:1},
  {name:'BURIED',       n:5, turns:2, locks:0, density:0.50, legMin:2, legMax:4, hideGoal:1},
  {name:'TWO FACES',    n:5, turns:2, locks:0, density:0.46, legMin:3, legMax:5},
  {name:'THE LONG WAY', n:6, turns:2, locks:1, density:0.44, legMin:3, legMax:6},
  {name:'TUMBLER',      n:6, turns:3, locks:0, density:0.48, legMin:3, legMax:5},
  {name:'WARDEN',       n:6, turns:3, locks:1, density:0.46, legMin:3, legMax:6},
  {name:'OUBLIETTE',    n:6, turns:3, locks:1, density:0.52, legMin:3, legMax:6},
  {name:'THE CANT',     n:6, turns:4, locks:1, density:0.46, legMin:4, legMax:6},
  {name:'SIX WAYS',     n:7, turns:4, locks:1, density:0.46, legMin:4, legMax:7},
  {name:'DEEP CUT',     n:7, turns:4, locks:2, density:0.50, legMin:4, legMax:7},
  {name:'THE VAULT',    n:7, turns:5, locks:2, density:0.48, legMin:4, legMax:8},
  {name:'GIMBAL',       n:7, turns:5, locks:2, density:0.46, legMin:5, legMax:8},
  {name:'TURNKEY',      n:7, turns:6, locks:2, density:0.48, legMin:5, legMax:8}
];

function faceCount(lv){
  /* how many of the 24 orientations give the player somewhere to stand at
     all — a level that only works from two faces is a corridor in disguise */
  var c = 0;
  for(var o = 0; o < 24; o++){
    var s = C.project(lv.n, lv.vox, C.ORIS[o]), any = 0;
    for(var i = 0; i < s.length; i++) if(s[i] && s[i].t === '+') any++;
    if(any > 2) c++;
  }
  return c;
}

var out = [], seed = 1;
for(var i = 0; i < CURVE.length; i++){
  var spec = CURVE[i], found = null, tries = 0;
  while(!found && tries < 30000){
    tries++;
    var lv = C.generate(seed++, spec);
    if(!lv) continue;
    if(lv.keys.length !== spec.locks) continue;
    C.widen(C.mulberry32(seed * 7919), lv, Math.max(2, spec.n + 2 - spec.turns));
    var a = C.assess(lv, spec.turns);
    if(!a) continue;
    if(a.turns > spec.turns + 2) continue;         /* keep the demand honest */
    if(a.fill < 0.30 || a.fill > 0.72) continue;   /* not a wisp, not a brick */
    if(faceCount(lv) < 10) continue;
    var gv = !!C.surfaceAt(lv.n, C.project(lv.n, lv.vox, C.ORI_ID), C.ORI_ID, lv.goal);
    if(spec.showGoal && !gv) continue;
    if(spec.hideGoal && gv) continue;
    lv.name = spec.name; lv.par = a.turns;
    found = lv;
  }
  if(!found){ console.error('no level for ' + spec.name); process.exit(1); }
  console.error(pad(spec.name,13) + ' n=' + found.n + '  par=' + found.par +
                ' turns  locks=' + found.keys.length + '  tries=' + tries);
  out.push(found);
}
function pad(s,w){ while(s.length<w) s+=' '; return s; }

/* emit as compact literals for the game file */
var lines = out.map(function(lv){
  return '{n:' + lv.n + ',name:' + JSON.stringify(lv.name) + ',par:' + lv.par +
    ',vox:' + JSON.stringify(lv.vox.join('')) +
    ',start:[' + lv.start + '],goal:[' + lv.goal + ']' +
    ',keys:[' + lv.keys.map(function(k){return '['+k+']';}).join(',') + ']' +
    ',doors:[' + lv.doors.map(function(d){return '['+d+']';}).join(',') + ']}';
});
console.log('var LEVELS = [\n' + lines.join(',\n') + '\n];');

/* eyeball one: every face of level 6, front-on */
var L = out[5];
console.error('\n--- ' + L.name + ' seen from each of the 6 faces (# bedrock, + deck, · void) ---');
var faces = [0,1,2,3, C.oriIndex(C.TURNS[2].f(C.ORI_ID)), C.oriIndex(C.TURNS[3].f(C.ORI_ID))];
var seenf = {}, shown = 0;
for(var o = 0; o < 24 && shown < 6; o++){
  var m = C.ORIS[o], fk = m.F.join();
  if(seenf[fk]) continue; seenf[fk] = 1; shown++;
  var s = C.project(L.n, L.vox, m), rows = [];
  for(var v = L.n-1; v >= 0; v--){
    var r = '';
    for(var u = 0; u < L.n; u++){ var c = s[u*L.n+v]; r += c ? (c.t === '+' ? '+' : '#') : '·'; }
    rows.push(r);
  }
  console.error('face ' + fk + ':  ' + rows.join('   '));
}
