/* CROSS-ENGINE DETERMINISM.
   The no-server design rests on one promise: cube 4,127 is the same cube on
   every device. Re-running the generator in ONE environment cannot test that
   — and twice it passed while the promise was broken. Both causes were
   invisible from inside a single engine:

     1. the search was time-boxed, so a faster machine got through more
        candidates and picked a different winner;
     2. the walk shuffled with `[0,1,2,3].sort(() => rng()-0.5)`, an
        inconsistent comparator whose number of rng() calls is a property of
        the engine's sort, and Node's V8 and Chromium's V8 disagree.

   So this mints the same levels in Node and in a real browser and demands
   the cubes be byte-identical. Two different V8 builds is the closest thing
   available here to two different phones. */
const {chromium}=require('playwright');
const C=require('./core.js');
let bad=0; const ok=(n,c,x='')=>{console.log((c?'  ok  ':'FAIL  ')+n+(x?'   '+x:''));if(!c)bad=1;};

(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:390,height:844},deviceScaleFactor:2});
  await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(400);

  const L=[]; for(let i=11;i<=70;i++) L.push(i);
  [101,222,401,999,5000,123456,1000001].forEach(l=>L.push(l));

  const br=await p.evaluate(levels=>levels.map(l=>{
    const d=mint(l,240);
    return {l, par:d.par, steps:d.steps, vox:d.vox.join(''),
            start:d.start.join(','), goal:d.goal.join(','),
            keys:JSON.stringify(d.keys), doors:JSON.stringify(d.doors)};
  }), L);

  const diff=[];
  L.forEach((l,i)=>{
    const n=C.mint(l,240), o=br[i];
    if(n.vox.join('')!==o.vox) diff.push(l+' geometry');
    else if(n.par!==o.par) diff.push(l+' par '+n.par+'/'+o.par);
    else if(n.start.join(',')!==o.start||n.goal.join(',')!==o.goal) diff.push(l+' endpoints');
    else if(JSON.stringify(n.keys)!==o.keys||JSON.stringify(n.doors)!==o.doors) diff.push(l+' locks');
  });
  ok(`${L.length} levels mint byte-identically in Node and Chromium`,
     diff.length===0, diff.slice(0,5).join(', '));

  // and the clock must not be able to reach the answer
  const budgets=await p.evaluate(()=>{
    const out=[];
    for(const l of [23,44,67,301]){
      const a=mint(l,1), z=mint(l,120000);
      out.push(a.vox.join('')===z.vox.join('') && a.par===z.par);
    }
    return out;
  });
  ok('a 1ms budget and a 120s budget give the same cube', budgets.every(Boolean));

  /* The generator must never draw randomness through a sort comparator.
     Comments are stripped first — the rule is written down in one, and an
     earlier version of this check failed on its own documentation. */
  const src=require('fs').readFileSync('/home/user/Ideas/turnkey/index.html','utf8')
    .replace(/\/\*[\s\S]*?\*\//g,'').replace(/(^|[^:])\/\/[^\n]*/g,'$1');
  ok('no rng is drawn through a sort comparator in the shipped code',
     !/sort\s*\([^)]*rng\s*\(/.test(src) && !/sort\s*\([^)]*Math\.random/.test(src));

  await b.close(); process.exit(bad);
})();
