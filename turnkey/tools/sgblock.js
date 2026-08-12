/* THE BLACKLIST, MAINTAINED BY A TOOL RATHER THAN BY HAND.

   A report arrives by mail carrying an eight-character identity. That
   identity goes in the BLOCKED table and the next build refuses the cube —
   on import, and by sweeping it out of any shelf it is already sitting in.

   Doing that with a text editor is how a trailing comma or a seven-character
   id gets shipped, and a broken table does not fail loudly: the game keeps
   running and simply stops blocking anything. So this checks the shape,
   refuses a duplicate, and keeps the entries sorted.

     node sgblock.js add <id> [note...]   block a cube
     node sgblock.js rm  <id>             unblock one
     node sgblock.js list                 show the table

   The identity is taken over all twenty-four orientations, so blocking one
   also blocks the same cube shared rotated. Nothing else needs doing.     */
const fs = require('fs');
const PAGE = '/home/user/Ideas/singularity/index.html';
const RE = /var BLOCKED = \{[\s\S]*?\n\};/;
const ID = /^[A-Za-z0-9_-]{8}$/;

const src = fs.readFileSync(PAGE, 'utf8');
const m = src.match(RE);
if(!m){ console.error('BLOCKED table not found in ' + PAGE); process.exit(1); }

/* read what is there now: real entries only, never the comment sample */
const entries = {};
m[0].split('\n').forEach(line => {
  const e = line.match(/^\s*'([^']+)'\s*:\s*'([^']*)'\s*,?\s*$/);
  if(e) entries[e[1]] = e[2];
});

const [cmd, id, ...rest] = process.argv.slice(2);
const note = rest.join(' ');

function write(){
  const keys = Object.keys(entries).sort();
  const body = keys.length
    ? keys.map(k => `  '${k}': '${entries[k].replace(/'/g, '')}'`).join(',\n') + '\n'
    : "  /* 'aBcDeFgH': '2026-08-12 reported' */\n";
  fs.writeFileSync(PAGE, src.replace(RE, 'var BLOCKED = {\n' + body + '};'));
  console.log(`${keys.length} cube${keys.length === 1 ? '' : 's'} blocked`);
}

if(cmd === 'list' || !cmd){
  const keys = Object.keys(entries).sort();
  if(!keys.length) console.log('nothing blocked');
  keys.forEach(k => console.log('  ' + k + '   ' + entries[k]));
  process.exit(0);
}
if(cmd === 'add'){
  if(!ID.test(id || '')){
    console.error(`"${id}" is not an identity — expected eight of A-Z a-z 0-9 - _`);
    process.exit(1);
  }
  if(entries[id]){ console.error(`${id} is already blocked: ${entries[id]}`); process.exit(1); }
  entries[id] = (new Date().toISOString().slice(0, 10)) + (note ? ' ' + note : ' reported');
  write();
  console.log(`blocked ${id} — rebuild and ship to have it take effect`);
  process.exit(0);
}
if(cmd === 'rm'){
  if(!entries[id]){ console.error(`${id} is not blocked`); process.exit(1); }
  delete entries[id];
  write();
  process.exit(0);
}
console.error('usage: sgblock.js add <id> [note] | rm <id> | list');
process.exit(1);
