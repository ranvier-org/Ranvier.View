// The F# samples of src/Ranvier.View.Anime/README.md are the declarations of Fixtures.fs: the opening block's
// lines open Fixtures.fs, and every other block appears in Fixtures.fs verbatim. The host installation sample needs a
// WebCore host and is compiled in ranvier-org/Ranvier.Anime (tests/Ranvier.Anime.NativeSmoke/MotionFixtures.fs).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = path => readFileSync(new URL(path, import.meta.url), "utf8").replace(/\r\n/g, "\n");
const readme = read("../../src/Ranvier.View.Anime/README.md");
const fixtures = read("Fixtures.fs");
const [opening, ...samples] = [...readme.matchAll(/```fsharp\n([\s\S]*?)\n```/g)].map(match => match[1]);

test("the opening block's lines open Fixtures.fs", () => {
  for (const line of opening.split("\n")) assert.ok(fixtures.includes(`${line}\n`), line);
});

test("every other README sample is in Fixtures.fs", () => {
  const compiledHere = samples.filter(sample => !sample.startsWith("let installMotion (host: WebCoreHost) ="));
  assert.equal(samples.length - compiledHere.length, 1);
  for (const sample of compiledHere) assert.ok(fixtures.includes(sample), sample.split("\n")[0]);
});
