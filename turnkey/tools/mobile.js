/* MOBILE SUITE — the things that only break on a phone.
   Everything here exists because it actually went wrong on a real device. */
const {chromium}=require('playwright');
const FILE='file:///home/user/Ideas/turnkey/index.html';
const VPS=[
  ['iPhone SE',375,667],['iPhone 12/13',390,844],['iPhone 14 Pro',393,852],
  ['iPhone 15 Pro Max',430,932],['Pixel 7',412,915],['small android',360,780],
  ['Safari bars up',430,745],['Safari bars up small',390,664],['very short',412,560],
  ['tiny',320,568],['landscape-ish',740,400],
];
let bad=0;
const ok=(n,c,x='')=>{console.log((c?'  ok  ':'FAIL  ')+n+(x?'   '+x:''));if(!c)bad=1;};

(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const errs=[];

  /* ---- 1. THE BOARD MUST FIT. Every phone, every screen, every state.
       It did not: the attract cube was sized at 1.12x the smaller side and
       hung 23-26px off both edges of every device tested. */
  let worst=null, over=[];
  for(const [name,w,h] of VPS){
    const p=await b.newPage({viewport:{width:w,height:h},deviceScaleFactor:3,isMobile:true,hasTouch:true});
    p.on('pageerror',e=>errs.push(name+': '+e.message));
    await p.goto(FILE); await p.waitForTimeout(350);
    for(const state of ['title','manual','play','bigcube']){
      if(state==='manual') await p.evaluate(()=>{manualFrom='scTitle';show('scManual');});
      if(state==='play')   await p.evaluate(()=>{store.reached=900;store.taught=1;loadLevel(3);show(null);});
      if(state==='bigcube')await p.evaluate(()=>{loadLevel(44);show(null);});
      await p.waitForTimeout(160);
      const r=await p.evaluate(()=>({W,H,S,CX,CY,N,
        l:CX-N*S/2,rt:CX+N*S/2,t:CY-N*S/2,bt:CY+N*S/2}));
      const slack=Math.min(r.l, r.W-r.rt, r.t, r.H-r.bt);
      if(slack < 0) over.push(`${name}/${state} by ${Math.round(-slack)}px`);
      if(!worst || slack < worst.slack) worst={n:name+'/'+state, slack:Math.round(slack)};
    }
    await p.close();
  }
  ok('the board fits on all 11 viewports in all 4 states', over.length===0,
     over.length?over.slice(0,4).join(', '):`tightest margin ${worst.slack}px at ${worst.n}`);

  /* ---- 2. FOOTING. Every route into a live level must leave the player
       standing on a cell that IS the surface of its column. The attract cube
       used to rotate the real basis while pos stayed put, so entering play
       from the manual put you inside the rock. */
  const p=await b.newPage({viewport:{width:390,height:844},deviceScaleFactor:3,isMobile:true,hasTouch:true});
  p.on('pageerror',e=>errs.push('main: '+e.message));
  p.on('console',m=>{if(m.type()==='error')errs.push('console: '+m.text());});
  await p.goto(FILE); await p.waitForTimeout(350);

  const spin=()=>p.evaluate(()=>{ for(let i=0;i<5;i++) stepDemo(9200); });
  const check=()=>p.evaluate(()=>({
    ori:oriIndex(M), lvl:levelNo,
    footing: !!surfaceAt(N, project(N,lv.vox,M), M, pos),
    deck: lv.vox[vidx(N,pos[0],pos[1],pos[2])]==='+',
    reach: Array.from(reach).reduce((a,c)=>a+c,0),
    solvable: solve(lv,40,{pos,ori:oriIndex(M),kmask,doors}).ok,
    hud: document.getElementById('hud').classList.contains('on')
  }));

  const routes=[];
  // a) first-timer: BEGIN -> manual -> read it while the cube turns -> GOT IT
  await p.evaluate(()=>{try{localStorage.clear();}catch(e){} store.taught=0;});
  await p.evaluate(()=>document.getElementById('btnPlay').click());
  await spin();
  await p.evaluate(()=>document.getElementById('btnManualBack').click());
  await p.waitForTimeout(200); routes.push(['first run: BEGIN -> manual -> GOT IT', await check()]);
  // b) title -> CONTINUE, after idling on the attract cube
  await p.evaluate(()=>show('scTitle'));
  await spin();
  await p.evaluate(()=>document.getElementById('btnPlay').click());
  await p.waitForTimeout(250); routes.push(['title -> CONTINUE', await check()]);
  // c) via the cube list
  await p.evaluate(()=>{store.reached=900;openCubes();viewBand=0;buildCubeGrid();
    document.querySelectorAll('#cubeGrid .cube')[4].click();});
  await p.waitForTimeout(500); routes.push(['cube list', await check()]);
  // d) pause -> manual -> back (must NOT reset, and must NOT spin the board)
  const midOri=await p.evaluate(()=>{ for(let t=0;t<4;t++) if(tryTurn(t)) break; return 1; });
  await p.waitForTimeout(600);
  const beforePause=await check();
  await p.evaluate(()=>{document.getElementById('btnMenu').click();
    document.getElementById('btnManual2').click();});
  await p.waitForTimeout(300);
  const demoOnPauseManual=await p.evaluate(()=>demo);
  await p.evaluate(()=>document.getElementById('btnManualBack').click());
  await p.waitForTimeout(250);
  const afterPause=await check();
  routes.push(['pause -> manual -> back', afterPause]);

  const badRoutes=routes.filter(([n,r])=>!r.footing||!r.deck||!r.solvable||r.reach<1||!r.hud);
  ok('every route into a level leaves valid footing', badRoutes.length===0,
     badRoutes.length?badRoutes.map(([n,r])=>n+' '+JSON.stringify(r)).join(' | '):routes.length+' routes');
  ok('the manual opened mid-level does not become an attract screen', demoOnPauseManual===false);
  ok('...and returns to the level in progress, unchanged',
     afterPause.lvl===beforePause.lvl && afterPause.ori===beforePause.ori,
     `turns/ori ${beforePause.ori} -> ${afterPause.ori}`);

  /* ---- 3. the attract cube must not be able to reach the rules at all */
  const iso=await p.evaluate(()=>{
    show('scTitle');
    const before={ori:oriIndex(M), pos:pos.slice(), surf:surf.map(s=>s?s.w.join(''):'-').join(',')};
    /* Driven the way the attract cube actually works now: one continuous
       sweep, committing a quarter turn each time the angle crosses 90. It
       used to step-and-rest, and this check reset the angle every call —
       which with the new sweep meant it could never reach a commit at all
       and reported the cube had never moved. Odd number of quarter turns
       about one axis, so it cannot land back where it started. */
    let moved=false;
    for(let i=0;i<5;i++){ stepDemo(9200); if(oriIndex(demoM)!==0) moved=true; }
    const after={ori:oriIndex(M), pos:pos.slice(), surf:surf.map(s=>s?s.w.join(''):'-').join(',')};
    return {same: before.ori===after.ori && JSON.stringify(before.pos)===JSON.stringify(after.pos)
                  && before.surf===after.surf,
            demoMoved: moved && oriIndex(demoM)!==0};
  });
  ok('six attract turns leave M, pos and surf untouched', iso.same);
  ok('...while the attract cube itself actually turned', iso.demoMoved);

  /* ---- 4. a tap must land on the tile that was drawn under the finger */
  const taps=await p.evaluate(()=>{
    loadLevel(44); show(null);
    let wrong=0;
    for(let u=0;u<N;u++) for(let v=0;v<N;v++){
      const r=tileRect(u,v), x=r[0]+S/2, y=r[1]+S/2;
      const bu=Math.round((x-CX)/S + N/2 - 0.5), bv=Math.round((CY-y)/S + N/2 - 0.5);
      if(bu!==u||bv!==v) wrong++;
    }
    return wrong;
  });
  ok('tap coordinates round-trip to the tile drawn there', taps===0, `${taps} mismatches`);

  /* ---- 5. anything tappable clears the 44px thumb floor */
  const small=await p.evaluate(()=>{
    show(null);
    const ids=['btnMenu','btnUndo','btnHint'];
    const out=[];
    ids.forEach(id=>{const r=document.getElementById(id).getBoundingClientRect();
      if(r.height<44||r.width<44) out.push(id+' '+Math.round(r.width)+'x'+Math.round(r.height));});
    show('scTitle');
    ['btnPlay','btnCubes','btnManual'].forEach(id=>{const r=document.getElementById(id).getBoundingClientRect();
      if(r.height<44) out.push(id+' h'+Math.round(r.height));});
    return out;
  });
  ok('every tappable control clears 44px', small.length===0, small.join(', '));

  /* ---- 6. the deck ramp must never bottom out into bedrock territory */
  const floor=await p.evaluate(()=>{
    const lum=c=>{const m=/rgb\((\d+),(\d+),(\d+)\)/.exec(c);return 0.2126*m[1]+0.7152*m[2]+0.0722*m[3];};
    let worst=1e9,at='';
    for(let band=0;band<40;band++){ ST=vaultStyle(band); N=7;
      const d=lum(tileFill('+',0));
      if(d<worst){worst=d;at='vault '+(band+1);} }
    return {worst,at};
  });
  ok('the dimmest deck in 40 vaults still reads as a deck', floor.worst>=100,
     `lum ${floor.worst.toFixed(0)} at ${floor.at}`);

  ok('no page errors', errs.length===0, errs.slice(0,3).join(' | '));
  await b.close();
  process.exit(bad);
})();
