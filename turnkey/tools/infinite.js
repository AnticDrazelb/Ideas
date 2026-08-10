const {chromium} = require('playwright');
let bad=0; const ok=(n,c,x='')=>{console.log((c?'  ok  ':'FAIL  ')+n+(x?'   '+x:''));if(!c)bad=1;};
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:412,height:915},deviceScaleFactor:2,isMobile:true,hasTouch:true});
  const errs=[]; p.on('pageerror',e=>errs.push(e.message));
  p.on('console',m=>{if(m.type()==='error')errs.push(m.text());});
  await p.goto('file:///home/user/Ideas/turnkey/index.html');
  await p.waitForTimeout(400);

  // Every level the game can hand out must be winnable, IN THE GAME, not just in the module.
  const sweep = await p.evaluate(() => {
    const out={bad:[],slow:[],fallback:[],pars:{},worst:0};
    const probe=[];
    for(let i=1;i<=60;i++) probe.push(i);
    [77,101,150,222,400,999,5000,123456].forEach(l=>probe.push(l));
    for(const L of probe){
      const t0=performance.now();
      loadLevel(L);
      const ms=performance.now()-t0;
      if(ms>out.worst) out.worst=ms;
      if(ms>2400) out.slow.push(L+':'+ms.toFixed(0)+'ms');
      if(levelData(L).fallback) out.fallback.push(L);
      // solve the level exactly as the player received it
      const r=solve(lv,60);
      if(!r.ok) out.bad.push(L+' unsolvable');
      else if(r.turns!==lv.par) out.bad.push(L+' par '+r.turns+'!='+lv.par);
      // and the opening position must be real
      if(!surfaceAt(N,project(N,lv.vox,ORI_ID),ORI_ID,pos)) out.bad.push(L+' start buried');
      if(lv.vox[vidx(N,pos[0],pos[1],pos[2])]!=='+') out.bad.push(L+' start not a deck');
      const v=vaultOf(L); (out.pars[v]=out.pars[v]||[]).push(lv.par);
    }
    return out;
  });
  ok('68 levels across 12 vaults are all winnable as served', sweep.bad.length===0, sweep.bad.slice(0,4).join(', '));
  ok('the fallback never fires in the game', sweep.fallback.length===0, sweep.fallback.join(','));
  ok('no level load exceeds 2.4s', sweep.slow.length===0, 'worst '+sweep.worst.toFixed(0)+'ms  '+sweep.slow.slice(0,3).join(', '));

  // difficulty must actually rise across vaults
  const v0=(sweep.pars[0]||[]).reduce((a,c)=>a+c,0)/(sweep.pars[0]||[1]).length;
  const hi=Object.keys(sweep.pars).filter(k=>+k>=4);
  const vN=hi.reduce((a,k)=>a+sweep.pars[k].reduce((x,c)=>x+c,0)/sweep.pars[k].length,0)/hi.length;
  ok('par rises from vault I to the later vaults', vN>v0+1, `vault I avg ${v0.toFixed(1)} -> later avg ${vN.toFixed(1)}`);

  // the same level number is the same cube every time
  const det=await p.evaluate(()=>{loadLevel(347);const a=lv.vox.join('')+lv.par;
    delete mintCache[347];loadLevel(347);return a===lv.vox.join('')+lv.par;});
  ok('level 347 is identical when re-cut from scratch', det);

  // vault palettes actually change the board
  const pal=await p.evaluate(()=>{loadLevel(1);const a=tileFill('+',3);const n1=ST.name;
    loadLevel(41);const c=tileFill('+',3);return {changed:a!==c,a:n1,b:ST.name};});
  ok('crossing vaults repaints the material', pal.changed, pal.a+' -> '+pal.b);

  // the deck/bedrock luminance bands must not collide in ANY vault, ever
  const bands=await p.evaluate(()=>{
    const lum=c=>{const m=/rgb\((\d+),(\d+),(\d+)\)/.exec(c);return 0.2126*m[1]+0.7152*m[2]+0.0722*m[3];};
    let worst=1e9,at='';
    for(let band=0;band<40;band++){
      ST=vaultStyle(band); N=7;
      let darkestDeck=1e9,brightestRock=-1;
      for(let d=0;d<7;d++){
        darkestDeck=Math.min(darkestDeck,lum(tileFill('+',d)));
        brightestRock=Math.max(brightestRock,lum(tileFill('#',d)));
      }
      if(darkestDeck-brightestRock<worst){worst=darkestDeck-brightestRock;at='vault '+(band+1);}
    }
    return {worst,at};
  });
  ok('darkest deck stays brighter than brightest bedrock in 40 vaults',
     bands.worst>0, `margin ${bands.worst.toFixed(1)} at ${bands.at}`);

  // and the same must survive the deepest cast shadow the renderer can draw
  const shadowed=await p.evaluate(()=>{
    const lum=c=>{const m=/rgb\((\d+),(\d+),(\d+)\)/.exec(c);return 0.2126*m[1]+0.7152*m[2]+0.0722*m[3];};
    const MAXSH=MAX_SHADOW;                      // the renderer's own constant, not a copy of it
    let worst=1e9,at='';
    for(let band=0;band<40;band++){
      ST=vaultStyle(band); N=7;
      let dd=1e9,br=-1;
      for(let d=0;d<7;d++){ dd=Math.min(dd,lum(tileFill('+',d))*(1-MAXSH)); br=Math.max(br,lum(tileFill('#',d))); }
      if(dd-br<worst){worst=dd-br;at='vault '+(band+1);}
    }
    return {worst,at};
  });
  ok('...and survives the deepest cast shadow the renderer can draw',
     shadowed.worst>0, `margin ${shadowed.worst.toFixed(1)} at ${shadowed.at}`);

  // frame cost with the whole graphics pass on
  const perf=await p.evaluate(()=>{
    loadLevel(44);
    let t=performance.now(); for(let i=0;i<100;i++)draw(); const rest=(performance.now()-t)/100;
    drag={x0:0,y0:0,axis:'x',ang:Math.PI/4};
    t=performance.now(); for(let i=0;i<100;i++)draw(); const mid=(performance.now()-t)/100;
    drag=null; return {rest,mid};
  });
  ok('frame cost still under 8ms with the full graphics pass', perf.mid<8&&perf.rest<8,
     `resting ${perf.rest.toFixed(2)}ms / turning ${perf.mid.toFixed(2)}ms`);

  ok('no page errors', errs.length===0, errs.slice(0,3).join(' | '));
  await b.close(); process.exit(bad);
})();
