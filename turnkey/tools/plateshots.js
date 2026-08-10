const {chromium}=require('playwright');
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:390,height:844},deviceScaleFactor:3,isMobile:true,hasTouch:true});
  await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(400);
  await p.evaluate(()=>{store.reached=900;store.taught=1;store.sawPlate=0;loadLevel(20);show(null);});
  await p.waitForTimeout(700); await p.screenshot({path:'p-card.png'});
  await p.evaluate(()=>show(null)); await p.waitForTimeout(900);
  await p.screenshot({path:'p-before.png'});
  // walk to the plate and stop right on the flip
  const hit = await p.evaluate(async()=>{
    let guard=0;
    while(guard++<60){
      const r=solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world});
      if(!r.ok||!r.first) return false;
      if(r.first.kind==='turn'){ tryTurn(r.first.dir); await new Promise(x=>setTimeout(x,430)); }
      else {
        const v=viewOf(N,M,pos),t=TURNS[r.first.dir];
        const tg=surf[(v[0]+t.dx)*N+(v[1]+t.dy)];
        const isP = tg&&(tg.t==='A'||tg.t==='B');
        tapCell(v[0]+t.dx,v[1]+t.dy);
        await new Promise(x=>setTimeout(x,isP?130:260));
        if(isP) return true;
      }
    }
    return false;
  });
  await p.screenshot({path:'p-flip.png'});
  await p.waitForTimeout(1100);
  await p.screenshot({path:'p-after.png'});
  console.log('reached a plate:', hit);
  await b.close();
})();
