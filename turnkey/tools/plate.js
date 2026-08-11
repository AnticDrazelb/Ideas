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

  /* the throw itself, staged rather than waited for: put the player in a
     flipped world on a cell the carve does not give footing to, spring it
     back, and see where they end up */
  const thrown=await p.evaluate(async()=>{
    loadLevel(21); show(null);
    await new Promise(r=>setTimeout(r,500));
    /* find a cell that is deck in world 1 and NOT deck in world 0 — exactly
       the ground the spring-back pulls out from under you */
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
    const to=viewOf(N,M,pos);
    return {from, to, world, footing:window.__footing(),
            moved:String(from)!==String(to),
            dist:Math.max(Math.abs(from[0]-to[0]),Math.abs(from[1]-to[1]))};
  });
  ok('losing your footing to the clock throws you to a square that is real',
     thrown && (thrown.skipped || (thrown.footing && thrown.moved)),
     thrown && !thrown.skipped ? `${thrown.from} -> ${thrown.to}` : 'no such cell on this cube');
  ok('...to the NEAREST one, not across the board',
     thrown && (thrown.skipped || thrown.dist<=2), thrown&&!thrown.skipped?`${thrown.dist} squares`:'');
  ok('...and being thrown onto a plate does not fire it',
     thrown && (thrown.skipped || thrown.world===0), thrown?`world ${thrown.world}`:'');

  /* THE PROMISE, WITH THE CLOCK RUNNING.

     The solver has no clock in it, so par is a lower bound and nothing in the
     model can tell you whether five seconds is enough to walk the line it
     found. That is a question only the running game can answer, so it is
     asked here: twelve plate cubes played through the real input path at
     human-ish timings, re-asking the solver after every action the way a
     player re-reads the board.

     If a cube needed more turns inside one flip than five seconds allows,
     this is where it would show up — as a level that never finishes, or one
     that only finishes after a spring-back has thrown the player and the
     route has been replanned from wherever they landed. */
  const clocked=await p.evaluate(async()=>{
    const rows=[];
    for(let L=20; L<=31; L++){
      loadLevel(L); show(null);
      await new Promise(r=>setTimeout(r,420));
      let acts=0, sprung=0;
      while(acts++ < 200 && !won){
        const r=solve(lv,40,{pos,ori:oriIndex(M),kmask,doors,world});
        if(!r.ok||!r.first) break;
        const w0=world;
        if(r.first.kind==='turn'){ tryTurn(r.first.dir); await new Promise(x=>setTimeout(x,360)); }
        else {
          const v=viewOf(N,M,pos), t=TURNS[r.first.dir];
          tapCell(v[0]+t.dx, v[1]+t.dy);
          await new Promise(x=>setTimeout(x,170));
        }
        if(world!==w0 && world===0 && plateT===0) sprung++;
      }
      rows.push({L, won, turns, par:lv.par, sprung});
      won=false;
    }
    return rows;
  });
  const beaten=clocked.filter(r=>r.won).length;
  ok('every plate cube is still beatable with the clock running',
     beaten===clocked.length, `${beaten}/${clocked.length} cubes 20-31`);
  ok('...at exactly par, so the clock costs turns nobody has to spend',
     clocked.every(r=>r.won && r.turns===r.par),
     clocked.map(r=>`${r.turns}/${r.par}`).join(' '));
  ok('...and a line walked at par never even reaches the spring-back',
     clocked.every(r=>r.sprung===0),
     `${clocked.reduce((a,r)=>a+r.sprung,0)} spring-backs across 12 cubes`);

  ok('no page errors', errs.length===0, errs.slice(0,3).join(' | '));
  await b.close(); process.exit(bad);
})();
