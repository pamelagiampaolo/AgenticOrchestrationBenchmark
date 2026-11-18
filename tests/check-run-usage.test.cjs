'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { checkRun } = require('../scripts/current/Check-RunUsage.cjs');

test('checks raw usage, zeros, missing fields and record inconsistencies', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'run-usage-test-'));
  const stream = path.join(dir, 'stream.jsonl');
  const totals = { inputTokens: 2, cacheReadInputTokens: 0, cacheCreationInputTokens: 3, outputTokens: 4, totalInputTokens: 5, totalTokens: 9 };
  const record = { schemaVersion: 2, runId: 'fixture', architecture: 'single-agent', tokenMeasurementComplete: true, authoritativeUsage: totals, tokensObserved: 9 };
  const write = (usage) => fs.writeFileSync(stream, `${JSON.stringify({ type: 'result', usage })}\n`);
  try {
    write({ input_tokens: 2, cache_read_input_tokens: 0, cache_creation_input_tokens: 3, output_tokens: 4 });
    assert.equal(checkRun(dir, record).usageComplete, true);
    write({ input_tokens: 2, cache_creation_input_tokens: 3, output_tokens: 4 });
    assert.match(checkRun(dir, record).issues.join(' '), /cache_read_input_tokens/);
    write({});
    assert.equal(checkRun(dir, record).usageComplete, false);
    write({ input_tokens: 2, cache_read_input_tokens: 0, cache_creation_input_tokens: 3, output_tokens: 4 });
    assert.equal(checkRun(dir, { ...record, tokenMeasurementComplete: false }).usageComplete, false);
    assert.equal(checkRun(dir, { ...record, tokensObserved: 0 }).usageComplete, false);
    fs.writeFileSync(stream, '{invalid-json}\n');
    assert.equal(checkRun(dir, record).usageComplete, false);
  } finally {
    if (fs.existsSync(stream)) fs.unlinkSync(stream);
    fs.rmdirSync(dir);
  }
});

test('reconciles hierarchical agent sums and rejects missing results', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'run-usage-hierarchy-'));
  const agents = ['one', 'two'];
  const usage = { inputTokens: 1, cacheReadInputTokens: 2, cacheCreationInputTokens: 3, outputTokens: 4, totalInputTokens: 6, totalTokens: 10 };
  const record = { schemaVersion: 2, runId: 'fixture', architecture: 'hierarchical-static', tokenMeasurementComplete: true, tokensObserved: 20, totalUsage: { inputTokens: 2, cacheReadInputTokens: 4, cacheCreationInputTokens: 6, outputTokens: 8, totalInputTokens: 12, totalTokens: 20 }, agents: agents.map((name) => ({ agentId: name, directory: name, tokenMeasurementComplete: true, authoritativeUsage: usage })) };
  try {
    for (const name of agents) {
      fs.mkdirSync(path.join(dir, name));
      fs.writeFileSync(path.join(dir, name, 'stream.jsonl'), `${JSON.stringify({ type: 'result', usage: { input_tokens: 1, cache_read_input_tokens: 2, cache_creation_input_tokens: 3, output_tokens: 4 } })}\n`);
    }
    assert.equal(checkRun(dir, record).usageComplete, true);
    assert.equal(checkRun(dir, { ...record, totalUsage: { ...record.totalUsage, outputTokens: 9 } }).usageComplete, false);
    fs.writeFileSync(path.join(dir, 'two', 'stream.jsonl'), '{}\n');
    assert.match(checkRun(dir, record).issues.join(' '), /expected one result event/);
    assert.throws(() => checkRun(dir, { ...record, agents: [{ ...record.agents[0], directory: '../outside' }] }), /escapes/);
  } finally {
    for (const name of agents) { fs.unlinkSync(path.join(dir, name, 'stream.jsonl')); fs.rmdirSync(path.join(dir, name)); }
    fs.rmdirSync(dir);
  }
});
