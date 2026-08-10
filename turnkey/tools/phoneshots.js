const {chromium}=require('playwright');
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:390,height:844},deviceScaleFactor:3,isMobile:true,hasTouch:true});
  await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(900);
  await p.screenshot({path:'m-title.png'});
  await p.evaluate(()=>{store.reached=900;store.taught=1;loadLevel(3);show(null);});
  await p.waitForTimeout(1100);
  await p.screenshot({path:'m-play.png'});
  await p.evaluate(()=>{loadLevel(44);show(null);});
  await p.waitForTimeout(1100);
  await p.screenshot({path:'m-big.png'});
  await b.close();
})();
