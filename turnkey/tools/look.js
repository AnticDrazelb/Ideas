const {chromium}=require('playwright');
const TAG=process.argv[2]||'look';
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:390,height:844},deviceScaleFactor:3,isMobile:true,hasTouch:true});
  p.on('pageerror',e=>console.log('PAGEERROR',e.message));
  await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(2000);
  await p.screenshot({path:`/home/user/Ideas/turnkey/tools/shot-${TAG}-title.png`});
  await p.evaluate(()=>{store.reached=900;store.taught=1;});
  for(const L of [7,24,39]){
    await p.evaluate(async(L)=>{ loadLevel(L); show(null); await new Promise(r=>setTimeout(r,700)); show(null); await new Promise(r=>setTimeout(r,900)); }, L);
    await p.screenshot({path:`/home/user/Ideas/turnkey/tools/shot-${TAG}-${L}.png`});
  }
  await p.evaluate(()=>{ drag={x0:0,y0:0,axis:'x',ang:Math.PI/5}; });
  await p.waitForTimeout(220);
  await p.screenshot({path:`/home/user/Ideas/turnkey/tools/shot-${TAG}-turning.png`});
  await p.evaluate(()=>{ drag=null; });
  const perf=await p.evaluate(()=>{ loadLevel(44); let t=performance.now(); for(let i=0;i<80;i++)draw(); const rest=(performance.now()-t)/80;
    drag={x0:0,y0:0,axis:'x',ang:Math.PI/4}; t=performance.now(); for(let i=0;i<80;i++)draw(); const mid=(performance.now()-t)/80; drag=null; return {rest,mid}; });
  console.log(`frame: resting ${perf.rest.toFixed(2)}ms  turning ${perf.mid.toFixed(2)}ms`);
  await b.close();
})();
