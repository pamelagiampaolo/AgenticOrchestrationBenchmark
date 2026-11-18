'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { validateAudit, checkFileEvidence } = require('../scripts/current/Test-OfficialAudit.cjs');

test('persistent audit checks slot linkage, evidence, authorization and replacement rules', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-audit-test-'));
  const evidencePath = path.join(dir, 'evidence.txt');
  const approvalPath = path.join(dir, 'approval.txt');
  try {
    fs.writeFileSync(evidencePath, 'provider log');
    fs.writeFileSync(approvalPath, 'approval');
    const ref = (p) => ({ path: p, sha256: crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex') });
    const slots = Array.from({ length: 54 }, (_, i) => ({ slotId: `slot-${i}`, orderPosition: i + 1, taskId: 'T1', architecture: 'single-agent', repetition: 1 }));
    const plan = { seed: 'seed', slots };
    const first = { runId: 'original', attempt: 1, previousRunId: null, classification: 'INFRA_PROVIDER', evidence: ref(evidencePath), decision: 'invalid', metricsAvailable: false };
    const second = { runId: 'replacement', attempt: 2, previousRunId: 'original', classification: 'completed', evidence: ref(evidencePath), authorizationEvidence: ref(approvalPath), decision: 'valid', metricsAvailable: true };
    const make = (attempts, outcome = 'valid') => ({ schemaVersion: 1, seed: 'seed', slots: [{ ...slots[0], attempts, slotOutcome: outcome }] });
    const persist = (audit) => {
      const auditPath = path.join(dir, 'audit.json');
      fs.writeFileSync(auditPath, JSON.stringify(audit));
      return JSON.parse(fs.readFileSync(auditPath, 'utf8'));
    };
    assert.deepEqual(validateAudit(plan, persist(make([first, second])), checkFileEvidence), []);
    assert.deepEqual(validateAudit(plan, persist(make([{ ...first, classification: 'non-classificabile', decision: 'review' }], 'review')), checkFileEvidence), []);
    assert.deepEqual(validateAudit(plan, persist(make([{ ...first, classification: 'test-failures', decision: 'valid' }])), checkFileEvidence), []);
    assert.deepEqual(validateAudit(plan, persist(make([first, { ...second, classification: 'INFRA_PROVIDER', decision: 'invalid', metricsAvailable: false }], 'incomplete')), checkFileEvidence), []);
    for (const bad of [
      make([first, second, { ...second, runId: 'third', attempt: 3 }]),
      make([{ ...first, classification: 'non-classificabile', decision: 'review' }, second]),
      make([{ ...first, classification: 'test-failures', decision: 'valid' }, second]),
      make([first, { ...second, previousRunId: 'wrong' }]),
      make([first, { ...second, authorizationEvidence: null }]),
      make([first, { ...second, runId: 'original' }]),
      { ...make([first, second]), slots: [{ ...make([first, second]).slots[0], orderPosition: 2 }] },
    ]) assert.notEqual(validateAudit(plan, persist(bad), checkFileEvidence).length, 0);
    fs.writeFileSync(evidencePath, 'changed');
    assert.notEqual(validateAudit(plan, persist(make([first, second])), checkFileEvidence).length, 0);
  } finally {
    for (const name of ['audit.json', 'evidence.txt', 'approval.txt']) {
      const p = path.join(dir, name);
      if (fs.existsSync(p)) fs.unlinkSync(p);
    }
    fs.rmdirSync(dir);
  }
});
