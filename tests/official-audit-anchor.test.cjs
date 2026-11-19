'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { saveWithAnchor } = require('../scripts/current/Save-OfficialAuditWithAnchor.cjs');

test('saves revisions with separate anchors and stops on mismatch or lost anchor', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-anchors-'));
  const history = path.join(dir, 'history');
  const anchors = path.join(dir, 'anchors');
  const evidence = path.join(dir, 'evidence');
  fs.mkdirSync(history); fs.mkdirSync(anchors);
  fs.writeFileSync(evidence, 'simulated evidence');
  try {
    const slots = Array.from({ length: 54 }, (_, i) => ({ slotId: `slot-${i}`, orderPosition: i + 1, taskId: 'T1', architecture: 'single-agent', repetition: 1 }));
    const plan = { seed: 'seed', slots };
    const ref = { path: evidence, sha256: crypto.createHash('sha256').update(fs.readFileSync(evidence)).digest('hex') };
    const attempt = (id) => ({ runId: id, attempt: 1, previousRunId: null, classification: 'completed', decision: 'valid', metricsAvailable: true, evidence: ref });
    const entry = (index) => ({ ...slots[index], attempts: [attempt(`run-${index}`)], slotOutcome: 'valid' });
    const candidate = (entries) => ({ schemaVersion: 1, seed: 'seed', slots: entries });
    assert.throws(() => saveWithAnchor(plan, history, history, candidate([entry(0)]), 'none'), /separate/);
    const first = saveWithAnchor(plan, history, anchors, candidate([entry(0)]), 'none');
    assert.equal(fs.readFileSync(first.anchorPath, 'utf8').trim(), first.sha256);
    const firstBytes = fs.readFileSync(first.path);
    assert.throws(() => saveWithAnchor(plan, history, anchors, candidate([entry(0), entry(1)]), '0'.repeat(64)), /Trusted previous hash/);
    const second = saveWithAnchor(plan, history, anchors, candidate([entry(0), entry(1)]), first.sha256);
    assert.equal(fs.readFileSync(second.anchorPath, 'utf8').trim(), second.sha256);
    assert.deepEqual(fs.readFileSync(first.path), firstBytes);
    fs.writeFileSync(second.anchorPath, '0'.repeat(64));
    assert.throws(() => saveWithAnchor(plan, history, anchors, candidate([entry(0), entry(1), entry(2)]), second.sha256), /anchor mismatch/);
    fs.unlinkSync(second.anchorPath);
    assert.throws(() => saveWithAnchor(plan, history, anchors, candidate([entry(0), entry(1), entry(2)]), second.sha256), /counts differ/);
    assert.equal(fs.readdirSync(history).length, 2);
  } finally {
    for (const p of fs.readdirSync(history)) fs.unlinkSync(path.join(history, p));
    for (const p of fs.readdirSync(anchors)) fs.unlinkSync(path.join(anchors, p));
    fs.unlinkSync(evidence); fs.rmdirSync(history); fs.rmdirSync(anchors); fs.rmdirSync(dir);
  }
});
