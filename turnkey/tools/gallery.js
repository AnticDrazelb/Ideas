const {chromium} = require('playwright');
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:412,height:915},deviceScaleFactor:2,isMobile:true,hasTouch:true});
  await p.goto('file:///home/user/Ideas/turnkey/index.html');
  await p.waitForTimeout(400);
  await p.evaluate(()=>{ store.reached=900; store.taught=1; });
  for(const [lvl,tag] of [[15,'ironworks'],[45,'cinders'],[75,'vein'],[214,'rotated']]){
    await p.evaluate(l=>{loadLevel(l);show(null);},lvl);
    await p.waitForTimeout(450);
    await p.screenshot({path:`v-${tag}.png`});
  }
  // a turn, mid-flight, in a late vault
  await p.evaluate(()=>{loadLevel(45);show(null);});
  await p.waitForTimeout(300);
  await p.mouse.move(206,470); await p.mouse.down();
  for(let k=1;k<=5;k++) await p.mouse.move(206+82*k/5,470);
  await p.waitForTimeout(140);
  await p.screenshot({path:'v-turning.png'});
  await p.mouse.up(); await p.waitForTimeout(500);
  await p.evaluate(()=>{viewBand=4;buildCubeGrid();show('scCubes');});
  await p.waitForTimeout(400);
  await p.screenshot({path:'v-cubes.png'});
  await b.close();
})();
