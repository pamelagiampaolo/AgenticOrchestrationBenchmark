'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { saveSnapshot } = require('../scripts/current/Save-OfficialAudit.cjs');

test('audit snapshots preserve prior attempts and reject stale, altered or unauthorized histories', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-audit-save-'));
  const evidencePath = path.join(dir, 'evidence.txt');
  const approvalPath = path.join(dir, 'approval.txt');
  const auditDir = path.join(dir, 'audit');
  fs.mkdirSync(auditDir);
  try {
    fs.writeFileSync(evidencePath, 'provider log'); fs.writeFileSync(approvalPath, 'approval');
    const ref = (p) => ({ path: p, sha256: crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex') });
    const slots = Array.from({ length: 54 }, (_, i) => ({ slotId: `slot-${i}`, orderPosition: i + 1, taskId: 'T1', architecture: 'single-agent', repetition: 1 }));
    const plan = { seed: 'seed', slots };
    const first = { runId: 'original', attempt: 1, previousRunId: null, classification: 'INFRA_PROVIDER', evidence: ref(evidencePath), decision: 'invalid', metricsAvailable: false };
    const second = { runId: 'replacement', attempt: 2, previousRunId: 'original', classification: 'completed', evidence: ref(evidencePath), authorizationEvidence: ref(approvalPath), decision: 'valid', metricsAvailable: true };
    const candidate = (attempts, outcome) => ({ schemaVersion: 1, seed: 'seed', slots: [{ ...slots[0], attempts, slotOutcome: outcome }] });
    const one = saveSnapshot(plan, auditDir, candidate([first], 'incomplete'), 'none');
    const originalBytes = fs.readFileSync(one.path);
    assert.throws(() => saveSnapshot(plan, auditDir, candidate([first, second], 'valid'), 'none'), /trusted anchor/);
    assert.throws(() => saveSnapshot(plan, auditDir, candidate([{ ...first, evidence: ref(approvalPath) }, second], 'valid'), one.sha256), /Existing attempt changed/);
    assert.throws(() => saveSnapshot(plan, auditDir, candidate([first, { ...second, authorizationEvidence: null }], 'valid'), one.sha256), /authorization/);
    const two = saveSnapshot(plan, auditDir, candidate([first, second], 'valid'), one.sha256);
    assert.equal(two.revision, 2);
    assert.deepEqual(fs.readFileSync(one.path), originalBytes);
    assert.equal(JSON.parse(fs.readFileSync(two.path)).previousSha256, one.sha256);
    assert.throws(() => saveSnapshot(plan, auditDir, candidate([first, second, { ...second, runId: 'third', attempt: 3 }], 'valid'), two.sha256), /attempt count/);
    fs.writeFileSync(one.path, '{}');
    assert.throws(() => saveSnapshot(plan, auditDir, candidate([first, second], 'valid'), two.sha256), /history link mismatch/);
  } finally {
    for (const name of fs.readdirSync(auditDir)) fs.unlinkSync(path.join(auditDir, name));
    fs.rmdirSync(auditDir); fs.unlinkSync(evidencePath); fs.unlinkSync(approvalPath); fs.rmdirSync(dir);
  }
});
