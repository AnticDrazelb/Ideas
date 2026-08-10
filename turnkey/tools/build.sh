#!/bin/sh
# Assembles the single deliverable. The core is byte-identical to the file the
# generator and the test suite run against, so a rule can never differ between
# the solver that verified a level and the game that serves it.
set -e
cd "$(dirname "$0")"
sed '/^if(typeof module/,$d' core.js > core-inline.js
{
  cat shell-head.html
  echo '/* ============================================================'
  echo '   TURNKEY — CORE'
  echo '   Byte-identical to the module the level generator and the test'
  echo '   suite run against. The solver that verified these cubes and the'
  echo '   game that serves them are the same code.'
  echo '   ============================================================ */'
  cat core-inline.js
  echo
  echo '/* ---------- THE CUBES — every one solver-verified at the par it claims ---------- */'
  cat levels.js
  echo
  cat game.js
  echo '</script>'
  echo '</body>'
  echo '</html>'
} > /home/user/Ideas/turnkey/index.html
node -e "
var s=require('fs').readFileSync('/home/user/Ideas/turnkey/index.html','utf8');
new Function(s.split('<script>')[1].split('</scr'+'ipt>')[0]);
console.log('built  ' + (s.length/1024).toFixed(1) + ' KB, JS parses clean');"
