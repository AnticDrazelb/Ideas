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
  process.stderr.write(`minting ${last} cubes; this takes a while\n`);

  /* in chunks, so a wedged page surfaces as a timeout on one chunk rather
     than as a ten-minute silence */
  const CH = 60;
  let ids = '';
  const t0 = Date.now();
  for(let from = 1; from <= last; from += CH){
    const to = Math.min(from + CH - 1, last);
    ids += await p.evaluate(([a, z]) => {
      let s = '';
      for(let L = a; L <= z; L++) s += canonId(levelData(L));
      return s;
    }, [from, to]);
    const done = to, rate = (Date.now() - t0) / done;
    process.stderr.write(`\r  ${done}/${last}  eta ${Math.round(rate*(last-done)/1000)}s   `);
  }
  process.stderr.write('\n');

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
  console.log(`wrote ${last} identities (${ids.length} chars, ${dup.length} internal repeats)`);
  await b.close();
  process.exit(0);
})();
