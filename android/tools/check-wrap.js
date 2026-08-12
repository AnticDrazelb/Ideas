
/* THE WRAPPER HAS TO KNOW EVERY BOARD THE GAME CAN SEND.
 *
 * index.html sends logical names. Boards.kt maps them to string resources and
 * leaderboards.xml holds the ids. Three files, one list, and nothing connects
 * them at compile time: a board the game posts to and the wrapper has never
 * heard of is a score that vanishes with a line in logcat nobody reads.
 *
 * Also checks the asset copy is actually the game, because it is a COPY and
 * the commonest way for this project to be wrong is for it to be an old one.
 *
 *   node tools/check-wrap.js
 */
const fs = require('fs');
const path = require('path');
const HERE = path.join(__dirname, '..');
const GAME = path.join(HERE, '..', 'singularity', 'index.html');
const ASSET = path.join(HERE, 'app/src/main/assets/index.html');

let bad = 0;
const ok = (n, c, x = '') => { console.log((c ? '  ok  ' : 'FAIL  ') + n + (x ? '   ' + x : '')); if (!c) bad = 1; };

const src = fs.readFileSync(GAME, 'utf8');
const asset = fs.existsSync(ASSET) ? fs.readFileSync(ASSET, 'utf8') : '';
ok('the asset is the current game, byte for byte', src === asset,
   src === asset ? `${src.length} bytes` : `game ${src.length} vs asset ${asset.length} — re-copy it`);

/* every logical name the game can hand the bridge */
const wanted = new Set();
for (const m of src.matchAll(/GPG\.(?:submit|show)\(\s*'([a-z_]+)'/g)) wanted.add(m[1]);
/* the aggregates are named in the DAILY_PERIODS table, whose rows are
   [periodKey, boardName, ...] — the board name is the SECOND field, and
   matching the first collected 'w', 'm' and 's' as if they were boards */
for (const m of src.matchAll(/'(daily_[a-z]+)'/g)) wanted.add(m[1]);
const ranked = Number((src.match(/RANKED_VAULTS\s*=\s*(\d+)/) || [])[1] || 0);
ok('the game declares a ranked vault count', ranked > 0, String(ranked));
for (let i = 1; i <= ranked; i++) wanted.add('vault_' + String(i).padStart(2, '0'));

const kt = fs.readFileSync(path.join(HERE, 'app/src/main/java/com/anticdrazelb/singularity/Boards.kt'), 'utf8');
const xml = fs.readFileSync(path.join(HERE, 'app/src/main/res/values/leaderboards.xml'), 'utf8');
const mapped = new Set([...kt.matchAll(/put\("([a-z0-9_]+)"/g)].map(m => m[1]));
const declared = new Set([...xml.matchAll(/name="lb_([a-z0-9_]+)"/g)].map(m => m[1]));

const missingKt = [...wanted].filter(k => !mapped.has(k)).sort();
const missingXml = [...mapped].filter(k => !declared.has(k)).sort();
const strayKt = [...mapped].filter(k => !wanted.has(k)).sort();

ok('every board the game can send is mapped in Boards.kt',
   missingKt.length === 0, missingKt.join(', ') || `${wanted.size} names`);
ok('every mapped board has a string resource',
   missingXml.length === 0, missingXml.join(', ') || `${declared.size} resources`);
ok('...and the wrapper maps nothing the game never sends',
   strayKt.length === 0, strayKt.join(', ') || 'no strays');

/* the script that creates them has to create exactly that set */
const boards = fs.readFileSync(path.join(HERE, 'tools/gpg-boards.js'), 'utf8');
const scriptCount = (boards.match(/i <= (\d+)/) || [])[1];
ok('the creation script covers the same vault count',
   Number(scriptCount) === ranked, `script ${scriptCount}, game ${ranked}`);

/* things that are silently fatal if missing */
const man = fs.readFileSync(path.join(HERE, 'app/src/main/AndroidManifest.xml'), 'utf8');
ok('the manifest can see a mail client on Android 11+',
   /<queries>[\s\S]*mailto[\s\S]*<\/queries>/.test(man));
ok('the Play Games app id is a string resource, not an inline number',
   /APP_ID"\s*\n?\s*android:value="@string\//.test(man));
const main = fs.readFileSync(path.join(HERE, 'app/src/main/java/com/anticdrazelb/singularity/MainActivity.kt'), 'utf8');
ok('DOM storage is enabled, so the save survives a restart',
   /domStorageEnabled\s*=\s*true/.test(main));
ok('the game is served from an https asset origin, not file://',
   /appassets\.androidplatform\.net/.test(main) && !/loadUrl\("file:/.test(main));
const rules = fs.readFileSync(path.join(HERE, 'app/proguard-rules.pro'), 'utf8');
ok('R8 keeps the JavaScript bridges', /JavascriptInterface/.test(rules));

process.exit(bad);
