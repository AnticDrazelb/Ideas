/* CREATE ALL THIRTY-FIVE LEADERBOARDS, AND PRINT THEIR IDS AS ANDROID XML.
 *
 * Thirty-five boards is thirty-five console forms, each with a name, a sort
 * order, a score format and an icon, and getting one of them wrong is a
 * board that silently ranks backwards. The Play Games Publishing API creates
 * them instead, from the same table the game's own board names come from.
 *
 * The seventy-board cap is a hard limit for the life of the game. This uses
 * thirty-five and leaves thirty-five.
 *
 * SETUP
 *   1. Play Console -> your game -> Play Games Services -> Configuration.
 *      Note the numeric project id.
 *   2. Google Cloud console, same project: enable the
 *      "Google Play Game Services Publishing API".
 *   3. Get an OAuth access token for the scope
 *      https://www.googleapis.com/auth/androidpublisher
 *      (easiest: gcloud auth print-access-token, on an account with access)
 *
 * RUN
 *   GPG_TOKEN=ya29....  GPG_APP=123456789012  node gpg-boards.js --dry
 *   GPG_TOKEN=ya29....  GPG_APP=123456789012  node gpg-boards.js
 *
 * It prints every id in the exact shape of res/values/leaderboards.xml, so
 * the output is pasted straight over that file.
 *
 * No dependencies: this is fetch and JSON.
 */
const TOKEN = process.env.GPG_TOKEN;
const APP   = process.env.GPG_APP;
const DRY   = process.argv.includes('--dry');
const API   = 'https://www.googleapis.com/games/v1configuration';

/* THE ORDER OF A BOARD IS PART OF WHAT IT MEANS, and it is not the same for
   all of these. Folds and clear times are LARGER_IS_BETTER = false: fewer is
   better, faster is better. The aggregates carry their period index above the
   score so the newest completed period outranks every older one, which only
   works if bigger wins — so those are larger-is-better, and a board created
   the wrong way round ranks the worst week of the season at the top. */
const BOARDS = [
  ['daily',        'TODAY',        false, 'NUMERIC', 'Fewest folds on today\'s cube'],
  ['daily_week',   'THIS WEEK',    true,  'NUMERIC', 'Seven days of dailies, totalled'],
  ['daily_month',  'THIS MONTH',   true,  'NUMERIC', 'A calendar month of dailies, totalled'],
  ['daily_season', 'SEASON',       true,  'NUMERIC', 'Ninety-one days of dailies, totalled'],
  ['daily_streak', 'STREAK',       true,  'NUMERIC', 'Consecutive days solved']
];
/* THE NAMES MUST BE THE ONES THE PLAYER SEES. Past the authored six the game
   generates a vault's name from two word lists, and a board called
   "VAULT 12" next to an in-game vault called "SHALE MARCH" is two names for
   one thing. This is vaultName() from index.html, transcribed — if that ever
   changes, this changes with it. */
const VAULTS = ['CALIBRATION','REFRACTION','INVERSION','ENTROPY','EMBERFALL','SINGULARITY'];
const VAULT_A = ['IRON','SALT','GLASS','ASH','BONE','SLATE','AMBER','TIDE','EMBER','FROST','COPPER','SHALE'];
const VAULT_B = ['REACH','WELL','SPINE','GATE','HOLLOW','WORKS','MARCH','VAULT','TERRACE','CANT'];
const ROMAN = ['I','II','III','IV','V','VI','VII','VIII','IX','X','XI','XII'];
const vaultName = band => band < VAULTS.length ? VAULTS[band]
  : VAULT_A[(band * 7) % VAULT_A.length] + ' ' + VAULT_B[(band * 3) % VAULT_B.length];
const roman = band => band < ROMAN.length ? ROMAN[band] : String(band + 1);

for (let i = 1; i <= 30; i++) {
  const band = i - 1;
  const key = 'vault_' + String(i).padStart(2, '0');
  const name = vaultName(band);
  /* TIME_DURATION so the console renders a clear time as a time rather than
     as a raw millisecond count nobody can read. */
  BOARDS.push([key, 'VAULT ' + roman(band) + ' / ' + name, false, 'TIME_DURATION',
               'Time to clear every cube in vault ' + roman(band)]);
}

function body(key, name, larger, fmt, desc) {
  return {
    publishedName: { translations: [{ locale: 'en-US', value: name }] },
    scoreOrder: larger ? 'LARGER_IS_BETTER' : 'SMALLER_IS_BETTER',
    scoreFormat: fmt === 'TIME_DURATION'
      ? { numberFormatType: 'TIME_DURATION' }
      : { numberFormatType: 'NUMERIC', numDecimalPlaces: 0 },
    /* the logical name goes in the token so a board can be matched back to
       the game later without guessing from its title */
    token: key,
    description: { translations: [{ locale: 'en-US', value: desc }] }
  };
}

(async () => {
  if (DRY) {
    console.error(`${BOARDS.length} boards would be created:\n`);
    BOARDS.forEach(([k, n, l, f]) =>
      console.error(`  ${k.padEnd(13)} ${n.padEnd(26)} ${l ? 'larger' : 'smaller'}  ${f}`));
    console.error('\nrerun without --dry to create them');
    return;
  }
  if (!TOKEN || !APP) {
    console.error('set GPG_TOKEN and GPG_APP (see the header of this file)');
    process.exit(1);
  }

  const out = [];
  for (const [key, name, larger, fmt, desc] of BOARDS) {
    const res = await fetch(`${API}/applications/${APP}/leaderboards`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(body(key, name, larger, fmt, desc))
    });
    const json = await res.json().catch(() => ({}));
    if (!res.ok) {
      console.error(`FAILED ${key}: ${res.status} ${JSON.stringify(json).slice(0, 300)}`);
      /* stop rather than carry on: a half-created set with no record of which
         half is worse than nothing, and the cap is finite */
      process.exit(1);
    }
    out.push([key, json.id]);
    console.error(`  created ${key.padEnd(13)} ${json.id}`);
  }

  console.log('<?xml version="1.0" encoding="utf-8"?>');
  console.log('<resources>');
  for (const [key, id] of out) {
    console.log(`    <string name="lb_${key}" translatable="false">${id}</string>`);
  }
  console.log('</resources>');
})();
