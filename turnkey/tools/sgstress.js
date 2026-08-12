const {chromium}=require('playwright');
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  let bad=0;
  for(const [w,h] of [[320,568],[360,640],[375,667],[390,844],[414,600],[430,932]]){
    const p=await b.newPage({viewport:{width:w,height:h},deviceScaleFactor:2,isMobile:true,hasTouch:true});
    const errs=[]; p.on('pageerror',e=>errs.push(e.message));
    await p.goto('file:///home/user/Ideas/singularity/index.html'); await p.waitForTimeout(800);
    await p.evaluate(async()=>{ store.reached=900;store.taught=1;store.sawPeek=1;
      loadLevel(49); show(null); await new Promise(r=>setTimeout(r,700)); show(null);
      await new Promise(r=>setTimeout(r,700)); });
    for(const [label,mut] of [
      ['as played',   ()=>{}],
      ['worst case',  ()=>{ document.getElementById('keyPill').style.display='';
                            document.getElementById('sepKey').style.display='';
                            document.getElementById('turnN').textContent='188';
                            document.getElementById('parN').textContent='/12';
                            document.getElementById('keyN').textContent='12';
                            document.getElementById('keyTot').textContent='/12';
                            document.getElementById('goN').textContent='40+';
                            document.getElementById('hintN').textContent='12'; }],
    ]){
      const r=await p.evaluate((src)=>{
        eval('('+src+')()');
        if(window.fitHud) fitHud();
        const row=document.getElementById('hudVals');
        const chips=[...row.querySelectorAll('.chip')].filter(e=>e.offsetParent!==null);
        const esc=chips.filter(c=>c.scrollWidth>c.clientWidth+1).map(c=>c.id);
        const rowRect=row.getBoundingClientRect();
        const out=chips.filter(c=>{const q=c.getBoundingClientRect();
          return q.left<rowRect.left-1||q.right>rowRect.right+1;}).map(c=>c.id);
        return {k:getComputedStyle(document.documentElement).getPropertyValue('--hud-k').trim()||'1',
                rowOver:row.scrollWidth-row.clientWidth,
                chipEscape:esc, outside:out,
                tight:document.documentElement.classList.contains('hudTight'),
                h:Math.round(chips[0].getBoundingClientRect().height),
                fs:getComputedStyle(chips[0].querySelector('.n')).fontSize};
      }, mut.toString());
      const ok = r.rowOver<=1 && !r.chipEscape.length && !r.outside.length;
      if(!ok) bad=1;
      console.log(`${w}x${h} ${label.padEnd(11)} k=${(+r.k).toFixed(2)} ${r.tight?'noword':'word  '} chip ${r.h}px/${r.fs.padEnd(7)} rowOver ${r.rowOver}` +
        (ok?'  ok':`  *** escape=${r.chipEscape} outside=${r.outside}`));
    }
    if(errs.length) { console.log('  ERRORS', errs.slice(0,2)); bad=1; }
    await p.close();
  }
  console.log(bad?'\nFAIL':'\nall fit');
  await b.close();
})();
