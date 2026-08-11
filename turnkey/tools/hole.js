/* THE MARKER IS A HOLE, AND THIS IS THE PROOF.

   Two claims are checked here, and they are the two the black hole makes.

   IT SHOWS THE SKY BEHIND THE BOARD. Not a starfield of its own, not a
   magnified copy, not a tinted one: the same pixels that would be there if
   the deck were not. So the test renders a frame, reads what is inside the
   horizon, and compares it against the starfield composited by itself at the
   same screen position. Anything that magnified, rotated, dimmed or painted
   over the interior would move those numbers apart, which is exactly what
   every one of the earlier versions of this object did.

   AND NOTHING CRASHES WHILE THE CUBE IS GLASS. A peek used to call a tunnel
   generator that was never written. The loop's own guard swallowed it once
   per frame, so it never showed as a crash — it showed as a game that reset
   your walk and re-settled the board sixty times a second for as long as you
   held your thumb down. A held peek is now a test, and it fails loudly.    */
const {chromium} = require('playwright');
const fs = require('fs');
let bad = 0;
const ok = (n, c, x = '') => { console.log((c ? '  ok  ' : 'FAIL  ') + n + (x ? '   ' + x : '')); if(!c) bad = 1; };

(async () => {
  const b = await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome'});
  const p = await b.newPage({viewport:{width:390,height:844}, deviceScaleFactor:2, isMobile:true, hasTouch:true});
  const errs = [];
  p.on('pageerror', e => errs.push('PAGEERROR: ' + e.message));
  p.on('console', m => { if(m.type() === 'error') errs.push('CONSOLE: ' + m.text()); });
  await p.goto('file:///home/user/Ideas/turnkey/index.html');
  await p.waitForTimeout(500);

  /* the conduit is not merely unreferenced, it is not in the shipped bytes */
  const src = fs.readFileSync('/home/user/Ideas/turnkey/index.html', 'utf8');
  ok('no conduit left in the shipped file',
     !/drawConduit|ConduitTunnel/.test(src),
     src.match(/[Cc]onduit/g) ? 'still ' + src.match(/[Cc]onduit/g).length + ' mentions' : '');

  /* ---- the hole is a window ------------------------------------------- */
  const seen = await p.evaluate(async () => {
    store.fx = false;                    /* bloom composites additively over
                                            the whole frame, hole included —
                                            switch the glare off and the
                                            comparison is exact rather than
                                            approximate */
    store.reached = 900; store.taught = 1;
    loadLevel(24); show(null);
    await new Promise(r => setTimeout(r, 900));

    /* a still frame: no shake, no squash, no hop, and nothing on the far
       side of the cube to be glimpsed through the opening */
    const realThrough = window.throughLook;
    window.throughLook = () => null;
    CAM.tr = 0; CAM.punch = 0; CAM.sqx = 0; CAM.sqy = 0; CAM.sqt = 1;
    CAM.imag = 0; CAM.it = 1; hopT = 1; walking = null; anim = null; drag = null;
    wave = null; flip = null; exitPull = -1;
    draw();

    const pt = playerScreen(), cx = pt[0], cy = pt[1];

    /* what the sky alone would put at those pixels */
    const off = document.createElement('canvas');
    off.width = Math.round(W*DPR); off.height = Math.round(H*DPR);
    const oc = off.getContext('2d');
    oc.setTransform(DPR, 0, 0, DPR, 0, 0);
    const so = skyOffset();
    oc.imageSmoothingEnabled = true;
    oc.drawImage(skyCv, so[0], so[1], W + PAD*2, H + PAD*2);

    /* sample well inside the horizon: the photon ring lives on the
       silhouette and the rim is where any lensing would be */
    const pts = [];
    for(let i = 0; i < 8; i++){
      const a = i*Math.PI/4, r = S*0.11;
      pts.push([cx + Math.cos(a)*r, cy + Math.sin(a)*r]);
    }
    pts.push([cx, cy]);

    let worst = 0, worstAt = null, live = [], want = [];
    for(const [x, y] of pts){
      const gx2 = Math.round(x*DPR), gy2 = Math.round(y*DPR);
      const a = ctx.getImageData(gx2, gy2, 1, 1).data;
      const e = oc.getImageData(gx2, gy2, 1, 1).data;
      live.push([a[0],a[1],a[2],a[3]]); want.push([e[0],e[1],e[2],e[3]]);
      for(let c = 0; c < 4; c++){
        const d = Math.abs(a[c] - e[c]);
        if(d > worst){ worst = d; worstAt = [x|0, y|0, c]; }
      }
    }

    /* and the deck it is standing on, for contrast: if the hole matched the
       deck too then "matches the sky" would be saying nothing */
    const dk = ctx.getImageData(Math.round((cx + S*0.62)*DPR), Math.round(cy*DPR), 1, 1).data;

    window.throughLook = realThrough;
    store.fx = true;
    return {worst, worstAt, live, want, deck:[dk[0],dk[1],dk[2],dk[3]], S};
  });

  const lum = c => 0.2126*c[0] + 0.7152*c[1] + 0.0722*c[2];
  ok('what is inside the horizon IS the sky behind the board',
     seen.worst <= 8,
     `worst channel error ${seen.worst}/255 over 9 samples` + (seen.worstAt ? ` at ${seen.worstAt.slice(0,2)}` : ''));
  ok('...and that is a real claim: the deck beside it is nothing like it',
     Math.abs(lum(seen.live[0]) - lum(seen.deck)) > 40 || seen.live[0][3] < 200,
     `hole a=${seen.live[0][3]} lum ${lum(seen.live[0]).toFixed(0)} vs deck lum ${lum(seen.deck).toFixed(0)}`);
  ok('...and the deck under the marker is removed, not covered',
     seen.live.every(c => c[3] < 250),
     `alpha ${seen.live.map(c => c[3]).join(',')}`);

  /* ---- a held peek, which is where the conduit used to throw ----------- */
  const peeked = await p.evaluate(async () => {
    loadLevel(24); show(null);
    await new Promise(r => setTimeout(r, 600));
    const before = pos.slice(), bo = oriIndex(M);
    peekOn();
    for(let i = 0; i < 40; i++){
      peek.yaw = 0.4 + i*0.02; peek.pitch = 0.2 - i*0.008;
      await new Promise(r => setTimeout(r, 25));
    }
    const amt = peek.amt;
    peekOff();
    await new Promise(r => setTimeout(r, 300));
    return {amt, moved: String(before) !== String(pos) || bo !== oriIndex(M)};
  });
  ok('a peek held for a second reaches full glass', peeked.amt > 0.9, `amt ${peeked.amt.toFixed(2)}`);
  ok('...without moving the player or the cube', !peeked.moved);
  ok('no page errors', errs.length === 0, errs.slice(0, 3).join(' | '));

  await b.close();
  process.exit(bad);
})();
