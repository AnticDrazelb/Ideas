var C = require('./core.js');

function ok(name, cond){ console.log((cond ? '  ok  ' : 'FAIL  ') + name); if(!cond) process.exitCode = 1; }

ok('24 orientations', C.ORIS.length === 24);

// world<->view round trip on every orientation
var n = 6, bad = 0;
for(var o = 0; o < 24; o++){
  var m = C.ORIS[o];
  for(var x = 0; x < n; x++) for(var y = 0; y < n; y++) for(var z = 0; z < n; z++){
    var v = C.viewOf(n, m, [x,y,z]);
    if(v[0]<0||v[0]>=n||v[1]<0||v[1]>=n||v[2]<0||v[2]>=n) bad++;
    var w = C.worldOf(n, m, v);
    if(w[0]!==x||w[1]!==y||w[2]!==z) bad++;
  }
}
ok('view/world round-trip on all 24 oris, all cells', bad === 0);

// four lefts is identity; left then right is identity
var m = C.ORI_ID;
for(var i=0;i<4;i++) m = C.TURNS[0].f(m);
ok('four lefts = identity', C.oriKey(m) === C.oriKey(C.ORI_ID));
ok('left then right = identity', C.oriKey(C.TURNS[1].f(C.TURNS[0].f(C.ORI_ID))) === C.oriKey(C.ORI_ID));
for(var i2=0;i2<4;i2++) m = C.TURNS[2].f(m);
ok('four ups = identity', C.oriKey(m) === C.oriKey(C.ORI_ID));

// the collapse: two decks far apart in depth are neighbours on screen
// n=4 cube, empty except deck at (0,0,0) and deck at (1,0,3)
var n2 = 4, vox = []; for(var i3=0;i3<n2*n2*n2;i3++) vox[i3]='.';
vox[C.vidx(n2,0,0,0)] = '+';
vox[C.vidx(n2,1,0,3)] = '+';
var surf = C.project(n2, vox, C.ORI_ID);   // F=+z toward camera, so depth is z
var a = surf[0*n2 + 0], b = surf[1*n2 + 0];
ok('column (0,0) surface is the z=0 deck', a && a.d === 0);
ok('column (1,0) surface is the z=3 deck', b && b.d === 3);
ok('...and they are screen neighbours despite 3 cells of depth between them',
   a && b && a.t === '+' && b.t === '+');

// burial: bedrock in front hides a deck
var vox2 = []; for(var i4=0;i4<n2*n2*n2;i4++) vox2[i4]='.';
vox2[C.vidx(n2,2,2,1)] = '+';
vox2[C.vidx(n2,2,2,3)] = '#';
var s2 = C.project(n2, vox2, C.ORI_ID);
ok('bedrock in front wins the column', s2[2*n2+2].t === '#');
ok('the buried deck is not the surface', C.surfaceAt(n2, s2, C.ORI_ID, [2,2,1]) === null);
// turn so depth runs along x instead — now nothing is in front of it
var mm = C.TURNS[0].f(C.ORI_ID);
var s3 = C.project(n2, vox2, mm);
ok('after a turn the buried deck is exposed', C.surfaceAt(n2, s3, mm, [2,2,1]) !== null);

// solver: a trivially solvable one-cell-apart level
var vox3 = []; for(var i5=0;i5<n2*n2*n2;i5++) vox3[i5]='.';
vox3[C.vidx(n2,0,0,0)]='+'; vox3[C.vidx(n2,1,0,0)]='+';
var lvA = {n:n2, vox:vox3, start:[0,0,0], goal:[1,0,0], keys:[], doors:[]};
var rA = C.solve(lvA);
ok('solver finds a flat 1-step route with 0 turns', rA.ok && rA.turns === 0);

// solver: unreachable
var vox4 = []; for(var i6=0;i6<n2*n2*n2;i6++) vox4[i6]='.';
vox4[C.vidx(n2,0,0,0)]='+'; vox4[C.vidx(n2,3,3,3)]='+';
var lvB = {n:n2, vox:vox4, start:[0,0,0], goal:[3,3,3], keys:[], doors:[]};
ok('solver reports an isolated goal unreachable', !C.solve(lvB).ok);

console.log('');

// keys must not be farmable: one key, one door, and a loop next to the key
var n5=4, vk=[]; for(var i7=0;i7<n5*n5*n5;i7++) vk[i7]='.';
[[0,0,0],[1,0,0],[2,0,0],[3,0,0]].forEach(function(p){ vk[C.vidx(n5,p[0],p[1],p[2])]='+'; });
var lvK={n:n5, vox:vk, start:[0,0,0], goal:[3,0,0], keys:[[1,0,0]], doors:[[2,0,0],[3,0,0]]};
var rK=C.solve(lvK);
ok('one key cannot open two doors', !rK.ok);
lvK.doors=[[2,0,0]];
ok('one key opens one door', C.solve(lvK).ok);
