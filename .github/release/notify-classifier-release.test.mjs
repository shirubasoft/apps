import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { analyzeCommits } from "./application-semantic-release.mjs";

const releaseConfig = JSON.parse(
  await readFile(
    new URL("../../notify-classifier/.releaserc.json", import.meta.url),
    "utf8",
  ),
);
const [applicationPluginName, applicationPluginOptions] = releaseConfig.plugins.find(
  ([pluginName]) => pluginName.endsWith("application-semantic-release.mjs"),
);

assert.equal(applicationPluginName, "../.github/release/application-semantic-release.mjs");

async function getReleaseType(...messages) {
  return analyzeCommits(applicationPluginOptions, {
    commits: messages.map((message, index) => ({
      hash: String(index),
      message,
    })),
    logger: { log() {} },
  });
}

test("versions Notify Classifier conventional commits", async () => {
  assert.equal(await getReleaseType("fix(notify-classifier): repair retry"), "patch");
  assert.equal(await getReleaseType("feat(notify-classifier): add a queue"), "minor");
  assert.equal(
    await getReleaseType(
      "feat(notify-classifier)!: replace storage\n\nBREAKING CHANGE: the database is recreated",
    ),
    "major",
  );
});

test("ignores conventional commits for other applications", async () => {
  assert.equal(await getReleaseType("feat(crap-score): add another report"), null);
  assert.equal(await getReleaseType("feat: add a repository tool"), null);
});
