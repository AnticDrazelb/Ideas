const {chromium}=require('playwright');
let bad=0; const ok=(n,c,x='')=>{console.log((c?'  ok  ':'FAIL  ')+n+(x?'   '+x:''));if(!c)bad=1;};
(async()=>{
  const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p=await b.newPage({viewport:{width:390,height:844},deviceScaleFactor:3,isMobile:true,hasTouch:true});
  const errs=[]; p.on('pageerror',e=>errs.push(e.message));
  p.on('console',m=>{if(m.type()==='error')errs.push(m.text());});
  await p.goto('file:///home/user/Ideas/turnkey/index.html'); await p.waitForTimeout(400);
  await p.evaluate(()=>{store.reached=900;store.taught=1;});

  // no plates before 20, always one from 20 on
  const spread=await p.evaluate(()=>{
    const out={early:[],late:[]};
    for(const L of [5,11,15,19]){ loadLevel(L); out.early.push(lv.vox.filter(c=>c==='A'||c==='B').length); }
    for(const L of [20,21,33,47,80]){ loadLevel(L); out.late.push(lv.vox.filter(c=>c==='A'||c==='B').length); }
    return out;
  });
  ok('no plates before level 20', spread.early.every(c=>c===0), spread.early.join(','));
  ok('exactly one plate from level 20 on', spread.late.every(c=>c===1), spread.late.join(','));

  // step on it and the world really turns over
  const fired=await p.evaluate(async()=>{
    loadLevel(21); show(null);
    await new Promise(r=>setTimeout(r,700));
    // walk the solver's answer until a plate fires
    let before=null, after=null, guard=0;
    while(guard++<60){
      const r=solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world});
      if(!r.ok||!r.first) break;
      if(r.first.kind==='turn'){ tryTurn(r.first.dir); await new Promise(x=>setTimeout(x,430)); }
      else {
        const v=viewOf(N,M,pos),t=TURNS[r.first.dir];
        const target=surf[(v[0]+t.dx)*N+(v[1]+t.dy)];
        const isPlate = target && (target.t==='A'||target.t==='B');
        if(isPlate) before={world, walk:Array.from(reach).reduce((a,c)=>a+c,0),
                            decks:surf.filter(s=>s&&(s.t==='+'||s.t==='A'||s.t==='B')).length};
        tapCell(v[0]+t.dx, v[1]+t.dy);
        await new Promise(x=>setTimeout(x,300));
        if(isPlate){ after={world, walk:Array.from(reach).reduce((a,c)=>a+c,0),
                            decks:surf.filter(s=>s&&(s.t==='+'||s.t==='A'||s.t==='B')).length,
                            tag:document.getElementById('worldTag').textContent,
                            footing:!!surfaceAt(N,project(N,effVox(lv,world),M),M,pos),
                            solvable:solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world}).ok};
          break; }
      }
    }
    return {before,after};
  });
  ok('stepping on a plate changes the world', fired.after && fired.before.world!==fired.after.world,
     fired.after?`world ${fired.before.world} -> ${fired.after.world}, tag "${fired.after.tag}"`:'never reached one');
  ok('...and the walkable set really is a different board', fired.after && fired.before.decks!==fired.after.decks,
     fired.after?`decks ${fired.before.decks} -> ${fired.after.decks}`:'');
  ok('...leaving valid footing and a solvable cube',
     fired.after && fired.after.footing && fired.after.solvable);

  // the promise: standing on a plate, every turn is legal
  const pivot=await p.evaluate(()=>{
    let checked=0, bad=0;
    for(const L of [21,34,52,77]){
      loadLevel(L);
      const g=[]; for(let x=0;x<N;x++)for(let y=0;y<N;y++)for(let z=0;z<N;z++)
        if(lv.vox[vidx(N,x,y,z)]==='A'||lv.vox[vidx(N,x,y,z)]==='B') g.push([x,y,z]);
      for(const w of g) for(let o=0;o<24;o++) for(let t=0;t<4;t++){
        checked++;
        if(!landing(lv, TURNS[t].f(ORIS[o]), w, 0, world)) bad++;
      }
    }
    return {checked,bad};
  });
  ok('a plate gives footing from every face — plates are pivots',
     pivot.bad===0, `${pivot.checked} (orientation x turn) checks`);

  // undo must put the world back
  const undone=await p.evaluate(async()=>{
    loadLevel(21); show(null);
    await new Promise(r=>setTimeout(r,700));
    let guard=0, snap=null;
    while(guard++<60){
      const r=solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world});
      if(!r.ok||!r.first) break;
      if(r.first.kind==='turn'){ tryTurn(r.first.dir); await new Promise(x=>setTimeout(x,430)); }
      else {
        const v=viewOf(N,M,pos),t=TURNS[r.first.dir];
        const tg=surf[(v[0]+t.dx)*N+(v[1]+t.dy)];
        if(tg&&(tg.t==='A'||tg.t==='B')) snap=JSON.stringify([pos,oriIndex(M),world,turns]);
        tapCell(v[0]+t.dx,v[1]+t.dy);
        await new Promise(x=>setTimeout(x,300));
        if(snap) break;
      }
    }
    const flipped=JSON.stringify([pos,oriIndex(M),world,turns]);
    undo();
    await new Promise(r=>setTimeout(r,200));
    return {ok: snap && flipped!==snap && JSON.stringify([pos,oriIndex(M),world,turns])===snap};
  });
  ok('undo puts the world back too', undone.ok);

  ok('no page errors', errs.length===0, errs.slice(0,3).join(' | '));
  await b.close(); process.exit(bad);
})();
