'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { selectOfficialAttempts } = require('../scripts/current/Select-OfficialAttempts.cjs');
const { attachOfficialUsage } = require('../scripts/current/Attach-OfficialUsage.cjs');

test('connects selected attempts with their streams and preserves unavailable usage', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-usage-'));
  const ids = ['complete', 'missing', 'unreadable'];
  try {
    const slots = Array.from({ length: 54 }, (_, i) => ({ slotId: `slot-${i}`, orderPosition: i + 1, taskId: 'T1', architecture: 'single-agent', repetition: 1, runKind: 'official' }));
    const plan = { seed: 'seed', slots };
    const evidence = { path: path.resolve('evidence'), sha256: 'a'.repeat(64) };
    const audit = { schemaVersion: 1, seed: plan.seed, slots: ids.map((id, i) => ({ ...slots[i], slotOutcome: 'valid', attempts: [{ runId: id, attempt: 1, previousRunId: null, classification: 'completed', decision: 'valid', metricsAvailable: true, evidence }] })) };
    const usage = { inputTokens: 2, cacheReadInputTokens: 0, cacheCreationInputTokens: 3, outputTokens: 4, totalInputTokens: 5, totalTokens: 9 };
    const records = Object.fromEntries(ids.map((id, i) => [id, { schemaVersion: 2, runId: id, taskId: 'T1', architecture: 'single-agent', repetition: 1, orderPosition: i + 1, orderSeed: 'seed', runKind: 'official', tokenMeasurementComplete: true, tokensObserved: 9, authoritativeUsage: usage }]));
    for (const id of ids) fs.mkdirSync(path.join(dir, id));
    const raw = { input_tokens: 2, cache_read_input_tokens: 0, cache_creation_input_tokens: 3, output_tokens: 4 };
    fs.writeFileSync(path.join(dir, 'complete', 'stream.jsonl'), JSON.stringify({ type: 'result', usage: raw }));
    fs.writeFileSync(path.join(dir, 'missing', 'stream.jsonl'), JSON.stringify({ type: 'result', usage: { input_tokens: 2, cache_creation_input_tokens: 3, output_tokens: 4 } }));
    const selected = selectOfficialAttempts(plan, audit, () => true, (id) => records[id]);
    const before = JSON.stringify(selected);
    const rows = attachOfficialUsage(selected, (id) => path.join(dir, id));
    assert.equal(rows.length, 54);
    assert.equal(rows[0].totalTokens, 9);
    assert.equal(rows[0].usageComplete, true);
    assert.equal(rows[1].totalTokens, null);
    assert.match(rows[1].usageIssues.join(' '), /cache_read_input_tokens/);
    assert.equal(rows[2].totalTokens, null);
    assert.match(rows[2].usageIssues.join(' '), /stream unreadable/);
    assert.equal(rows[3].totalTokens, null);
    assert.equal(rows[3].outcome, 'not-started');
    assert.equal(JSON.stringify(selected), before);
    const failedResolver = attachOfficialUsage(selected, () => { throw new Error('directory unavailable'); });
    assert.equal(failedResolver[0].totalTokens, null);
    assert.match(failedResolver[0].usageIssues[0], /directory unavailable/);
  } finally {
    for (const id of ids) {
      const stream = path.join(dir, id, 'stream.jsonl');
      if (fs.existsSync(stream)) fs.unlinkSync(stream);
      fs.rmdirSync(path.join(dir, id));
    }
    fs.rmdirSync(dir);
  }
});
