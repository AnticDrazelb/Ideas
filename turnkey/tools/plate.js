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
                            tag:document.getElementById('worldName').textContent,
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

  /* ---- THE FIVE SECONDS ------------------------------------------------
     A flipped world is on a clock, and when the clock runs out the material
     springs back and the player is put somewhere that is still a square. */
  await p.evaluate(()=>{
    /* walk the solver's own answer until a plate fires, and stop there */
    window.__toFlipped = async function(L){
      loadLevel(L); show(null);
      await new Promise(r=>setTimeout(r,600));
      for(let guard=0; guard<60; guard++){
        const r=solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world});
        if(!r.ok||!r.first) return false;
        if(r.first.kind==='turn'){ tryTurn(r.first.dir); await new Promise(x=>setTimeout(x,430)); continue; }
        const v=viewOf(N,M,pos), t=TURNS[r.first.dir];
        const tg=surf[(v[0]+t.dx)*N+(v[1]+t.dy)];
        const isPlate = tg && (tg.t==='A'||tg.t==='B');
        tapCell(v[0]+t.dx, v[1]+t.dy);
        await new Promise(x=>setTimeout(x,300));
        if(isPlate) return world!==0;
      }
      return false;
    };
    window.__footing = function(){
      const v = surfaceAt(N, surf, M, pos);
      return !!(v && walkable(lv, surf, v[0], v[1], doors));
    };
  });

  const clock=await p.evaluate(async()=>{
    if(!await window.__toFlipped(21)) return null;
    const bar=document.getElementById('worldBar');
    const t0=plateT, w0=world, s0=bar.style.transform;
    await new Promise(r=>setTimeout(r,1200));
    return {t0, w0, s0, t1:plateT, s1:bar.style.transform,
            clock:document.getElementById('worldClock').textContent};
  });
  ok('a flipped world starts a five second clock', clock && clock.t0>4000 && clock.t0<=5000,
     clock?`${clock.t0|0} ms`:'never reached a plate');
  ok('...and it runs down, on the HUD as well as in the state',
     clock && clock.t1 < clock.t0 - 800 && clock.s1 !== clock.s0 && clock.clock.length>0,
     clock?`${clock.t0|0} -> ${clock.t1|0} ms, bar ${clock.s0} -> ${clock.s1}, reads "${clock.clock}"`:'');

  const sprung=await p.evaluate(async()=>{
    if(!await window.__toFlipped(21)) return null;
    const before={world, pos:pos.slice(), on:isGlyph((surf[viewOf(N,M,pos)[0]*N+viewOf(N,M,pos)[1]]||{}).t)};
    await new Promise(r=>setTimeout(r,6200));
    return {before, world, pos:pos.slice(), footing:window.__footing(),
            solvable:solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world}).ok,
            undos:undoStack.length};
  });
  ok('the world springs back on its own after five seconds',
     sprung && sprung.before.world!==0 && sprung.world===0,
     sprung?`world ${sprung.before.world} -> ${sprung.world}`:'never reached a plate');
  ok('...and never leaves the player standing in rock',
     sprung && sprung.footing, sprung?`at ${sprung.pos}`:'');
  ok('...on a square you are standing on, so it stays a cube you can play',
     sprung && sprung.solvable);
  ok('...with an undo point, so the clock is never a trap',
     sprung && sprung.undos>0, sprung?`${sprung.undos} on the stack`:'');
  ok('a plate keeps its footing through the spring-back, so standing on one is safe',
     sprung && (!sprung.before.on || String(sprung.before.pos)===String(sprung.pos)),
     sprung && sprung.before.on ? 'was on the plate' : 'had already stepped off');

  /* the spring-back landing, staged rather than waited for: put the player in
     a flipped world on a cell the carve gives no footing to, spring it back,
     and check they end up ON A PLATE rather than stranded in world 0 — which
     is the state 39 of 41 plate cubes cannot be finished from. */
  const thrown=await p.evaluate(async()=>{
    loadLevel(21); show(null);
    await new Promise(r=>setTimeout(r,500));
    world=1; clearEff(lv); settle(); plateT=PLATE_MS;
    let victim=null;
    for(let u=0;u<N&&!victim;u++)for(let v=0;v<N&&!victim;v++){
      const c=walkable(lv,surf,u,v,doors);
      if(!c || isGlyph(c.t)) continue;
      const s0=project(N,effVox(lv,0),M);
      const back=surfaceAt(N,s0,M,c.w);
      if(!back || !walkable(lv,s0,back[0],back[1],doors)) victim=c;
    }
    if(!victim) return {skipped:true};
    pos=victim.w.slice(); settle();
    const from=viewOf(N,M,pos).slice();
    revertWorld();
    const to=viewOf(N,M,pos), landed=surf[to[0]*N+to[1]];
    // and pressing the plate underfoot must flip it again
    const w0=world;
    tapCell(to[0], to[1]);
    await new Promise(r=>setTimeout(r,200));
    return {from, to, world, onPlate:!!(landed&&isGlyph(landed.t)),
            landedWorld:w0, repressed:world!==w0,
            footing:window.__footing()};
  });
  ok('the clock puts you back on a plate, not down in the world you cannot finish',
     thrown && (thrown.skipped || (thrown.onPlate && thrown.footing)),
     thrown && !thrown.skipped ? `${thrown.from} -> ${thrown.to}` : 'no such cell on this cube');
  ok('...and landing on it does not fire it',
     thrown && (thrown.skipped || thrown.landedWorld===0), thrown?`world ${thrown.landedWorld}`:'');
  ok('...but tapping the plate under you does, so the loop can never lock',
     thrown && (thrown.skipped || thrown.repressed), thrown?`world ${thrown.landedWorld} -> ${thrown.world}`:'');

  /* THE PROMISE UNDER THE CLOCK IS NOT TESTED HERE, AND THE REASON IS WORTH
     WRITING DOWN, BECAUSE THE TEST THAT USED TO SIT HERE WAS WORTHLESS.

     It replayed the SOLVER'S OWN ANSWER at machine speed and reported 12/12
     at par. That only ever proved the route fits in five seconds if you
     already know the route — which is not the question. The question is
     whether a person working the board out can do it, and a harness that is
     handed the answer can never answer that. It shipped a broken game with a
     green tick next to it.

     What replaces it is the pair of properties that make running out of time
     SURVIVABLE rather than fatal, which are testable and are tested above: you
     are put back on a plate, and the plate under you can always be pressed
     again. Those two together mean the clock can cost you the walk and never
     the cube. Whether five seconds is the right number for the hardest cubes
     is a play question, and it is answered by playing, not by a harness that
     has been handed the answer. */

  ok('no page errors', errs.length===0, errs.slice(0,3).join(' | '));
  await b.close(); process.exit(bad);
})();
