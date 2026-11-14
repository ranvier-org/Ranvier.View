const test = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');

test('the tool manifest lists the RID-specific Fickl package after the portable tools', () => {
  const manifest = JSON.parse(readFileSync(join(__dirname, '../../.config/dotnet-tools.json'), 'utf8'));
  // A fresh-cache restore on SDK 10.0.402 resolved the tools listed after Fickl as Fickl.
  assert.equal(Object.keys(manifest.tools).at(-1), 'fickl');
  assert.match(manifest.tools.fickl.version, /^\d+\.\d+\.\d+$/);
  for (const [name, command] of [['fantomas', 'fantomas'], ['fsharp-analyzers', 'fsharp-analyzers'], ['easybuild.shipit', 'shipit'], ['fickl', 'fickl']]) {
    assert.deepEqual(manifest.tools[name].commands, [command]);
    assert.equal(manifest.tools[name].rollForward, false);
  }
});
