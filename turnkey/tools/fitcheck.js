const {chromium}=require('playwright');
const VPS=[
  ['iPhone SE',375,667],['iPhone 12/13',390,844],['iPhone 14 Pro',393,852],
  ['iPhone 15 Pro Max',430,932],['Pixel 7',412,915],['small android',360,780],
  ['Safari bars up',430,745],['Safari bars up small',390,664],['very short',412,560],
];
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  console.log('viewport              state     board(px)   fits?   S      dead top/bot');
  for(const [name,w,h] of VPS){
    const p=await b.newPage({viewport:{width:w,height:h},deviceScaleFactor:3,isMobile:true,hasTouch:true});
    await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(500);
    for(const state of ['title','play']){
      if(state==='play') await p.evaluate(()=>{store.reached=900;store.taught=1;loadLevel(3);show(null);});
      await p.waitForTimeout(250);
      const r=await p.evaluate(()=>({
        S,CX,CY,N,demo,W,H,
        left:CX-N*S/2, right:CX+N*S/2, top:CY-N*S/2, bot:CY+N*S/2,
      }));
      const fits = r.left>=0 && r.right<=r.W && r.top>=0 && r.bot<=r.H;
      console.log(
        name.padEnd(21)+state.padEnd(10)+
        (Math.round(r.right-r.left)+'x'+Math.round(r.bot-r.top)).padEnd(12)+
        (fits?'yes  ':'NO   ').padEnd(8)+
        r.S.toFixed(0).padEnd(7)+
        Math.round(r.top)+'/'+Math.round(r.H-r.bot)+
        (fits?'':`   overflow L${Math.round(-r.left)} R${Math.round(r.right-r.W)} T${Math.round(-r.top)} B${Math.round(r.bot-r.H)}`));
    }
    await p.close();
  }
  await b.close();
})();
