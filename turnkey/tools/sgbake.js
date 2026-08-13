/* THE CATALOGUE'S IDENTITIES, COMPUTED ONCE SO THE PHONE NEVER HAS TO.

   Minting one cube costs about a quarter of a second, so answering "does
   this level already exist" against the ranked game is nine minutes of work
   on a desktop and the better part of an hour on a phone. It is also the
   same answer every time, on every device, forever — that is the whole
   determinism guarantee — so it is work that belongs at build time.

   This walks the ranked region in level order, canonicalises each cube over
   the twenty-four orientations, and writes the eight-character identities
   out as one string. Level order is load-bearing: a hit's position in the
   string is its level number, which is how a collision can name what it
   collided with.

   Run it after ANY change to the generator, the spec curve, or the
   canonical form. A stale index does not fail loudly — it quietly stops
   recognising cubes, or worse, starts accusing new ones.

     node sgbake.js            write the index into singularity/index.html
     node sgbake.js --check    verify the index in the file is current      */
const {chromium} = require('playwright');
const fs = require('fs');
const os = require('os');
const PAGE = '/home/user/Ideas/singularity/index.html';
const check = process.argv.includes('--check');

(async () => {
  const b = await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p = await b.newPage();
  const errs = [];
  p.on('pageerror', e => errs.push(e.message));
  await p.goto('file://' + PAGE);
  await p.waitForTimeout(400);

  const last = await p.evaluate(() => vaultStart(RANKED_VAULTS - 1) + vaultSize(RANKED_VAULTS - 1) - 1);

  /* PARALLEL, AND RESUMABLE, BECAUSE THIS GOT EXPENSIVE.
     Cutting a nine-cube costs about 1.4s, so the whole catalogue is roughly
     three quarters of an hour on one page — long enough that a container
     restart in the middle throws all of it away, which is exactly what
     happened the first time. Several pages share the work and each finished
     level is appended to a cache file as it lands, so a rerun resumes rather
     than restarts. The pages are independent by construction: a cube is a
     pure function of its level number, which is the whole determinism
     guarantee, so who computes it cannot matter. */
  const LANES = Math.max(1, Math.min(4, os.cpus().length));
  const CACHE = PAGE.replace(/index\.html$/, '.bake-cache.json');
  let done = {};
  try{ done = JSON.parse(fs.readFileSync(CACHE, 'utf8')); }catch(e){}
  const already = Object.keys(done).length;
  process.stderr.write(`minting ${last} cubes on ${LANES} lanes` +
                       (already ? ` (${already} already cached)` : '') + `\n`);

  const pages = [p];
  for(let i = 1; i < LANES; i++){
    const q = await b.newPage();
    await q.goto('file://' + PAGE);
    pages.push(q);
  }
  await new Promise(r => setTimeout(r, 400));

  const todo = [];
  for(let L = 1; L <= last; L++) if(done[L] === undefined) todo.push(L);
  const t0 = Date.now();
  let finished = 0;
  let nextIdx = 0;
  const flush = () => fs.writeFileSync(CACHE, JSON.stringify(done));

  await Promise.all(pages.map(async (pg) => {
    while(true){
      const chunk = todo.slice(nextIdx, nextIdx + 12);
      if(!chunk.length) break;
      nextIdx += chunk.length;
      const got = await pg.evaluate(ls => ls.map(L => canonId(levelData(L))), chunk);
      chunk.forEach((L, i) => { done[L] = got[i]; });
      finished += chunk.length;
      flush();
      const rate = (Date.now() - t0) / Math.max(1, finished);
      process.stderr.write(`\r  ${already + finished}/${last}  eta ` +
        `${Math.round(rate * (todo.length - finished) / 1000)}s   `);
    }
  }));
  process.stderr.write('\n');

  let ids = '';
  for(let L = 1; L <= last; L++){
    if(done[L] === undefined){ console.error(`missing level ${L}`); process.exit(1); }
    ids += done[L];
  }

  if(errs.length){ console.error('page errors:', errs.slice(0,3)); process.exit(1); }
  if(ids.length !== last*8){ console.error(`bad length ${ids.length}, wanted ${last*8}`); process.exit(1); }

  /* a duplicate inside the catalogue itself would mean the generator emits
     the same cube for two different level numbers — worth knowing about */
  const seen = {}, dup = [];
  for(let i = 0; i < ids.length; i += 8){
    const id = ids.substr(i, 8), L = i/8 + 1;
    if(seen[id]) dup.push([seen[id], L]); else seen[id] = L;
  }
  if(dup.length) console.error(`NOTE: ${dup.length} cubes are repeats, first ${JSON.stringify(dup[0])}`);

  const src = fs.readFileSync(PAGE, 'utf8');
  const re = /var MINTED_IDS = '[^']*';/;
  if(!re.test(src)){ console.error('no MINTED_IDS declaration found'); process.exit(1); }
  const line = `var MINTED_IDS = '${ids}';`;

  if(check){
    const cur = src.match(re)[0];
    const same = cur === line;
    console.log(same ? `  ok  the index matches the generator (${last} cubes)`
                     : `FAIL  the index is stale — run: node sgbake.js`);
    process.exit(same ? 0 : 1);
  }

  fs.writeFileSync(PAGE, src.replace(re, line));
  /* the cache is keyed by nothing but level number, so it MUST NOT outlive
     the generator that filled it — a stale entry would be baked in silently */
  try{ fs.unlinkSync(PAGE.replace(/index\.html$/, '.bake-cache.json')); }catch(e){}
  console.log(`wrote ${last} identities (${ids.length} chars, ${dup.length} internal repeats)`);
  await b.close();
  process.exit(0);
})();
