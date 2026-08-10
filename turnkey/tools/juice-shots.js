const {chromium}=require('playwright');
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:412,height:915},deviceScaleFactor:2,isMobile:true,hasTouch:true});
  const errs=[]; p.on('pageerror',e=>errs.push(e.message));
  p.on('console',m=>{if(m.type()==='error')errs.push(m.text());});
  await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(900);
  await p.screenshot({path:'j-title.png'});                       // attract cube behind the title

  await p.evaluate(()=>{store.reached=900;store.taught=1;loadLevel(34);show(null);});
  await p.waitForTimeout(120);
  await p.screenshot({path:'j-assemble.png'});                    // the board arriving
  await p.waitForTimeout(900);

  // a committed turn, caught right on the seat
  await p.evaluate(()=>{ for(let t=0;t<4;t++) if(tryTurn(t)) break; });
  await p.waitForTimeout(300);
  await p.screenshot({path:'j-landing.png'});                     // kick + dust + sweep starting
  await p.waitForTimeout(120);
  await p.screenshot({path:'j-sweep.png'});                       // reachable set tracing outward

  // the exit
  await p.evaluate(async()=>{
    loadLevel(12); show(null);
    await new Promise(r=>setTimeout(r,700));
    let guard=0;
    while(!won && guard++<80){
      const r=solve(lv,40,{pos,ori:oriIndex(M),kmask,doors});
      if(!r.ok||!r.first) break;
      if(r.first.kind==='turn'){ tryTurn(r.first.dir); await new Promise(x=>setTimeout(x,420)); }
      else { const v=viewOf(N,M,pos),t=TURNS[r.first.dir]; tapCell(v[0]+t.dx,v[1]+t.dy); await new Promise(x=>setTimeout(x,240)); }
    }
  });
  await p.waitForTimeout(240);
  await p.screenshot({path:'j-exit.png'});                        // board coming apart from the door
  await p.waitForTimeout(900);
  await p.screenshot({path:'j-card.png'});
  console.log('errors:', errs.length?errs.join(' | '):'none');
  await b.close();
})();
