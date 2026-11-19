'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { selectOfficialAttempts } = require('../scripts/current/Select-OfficialAttempts.cjs');
const { attachOfficialEvaluation } = require('../scripts/current/Attach-OfficialEvaluation.cjs');

test('links evaluations to selected attempts and leaves uncertain outcomes unavailable', () => {
  const slots = Array.from({ length: 54 }, (_, i) => ({ slotId: `slot-${i}`, orderPosition: i + 1, taskId: 'T1', architecture: 'single-agent', repetition: 1, runKind: 'official' }));
  const plan = { seed: 'seed', slots };
  const evidence = { path: path.resolve('evidence'), sha256: 'a'.repeat(64) };
  const audit = { schemaVersion: 1, seed: plan.seed, slots: [{ ...slots[0], slotOutcome: 'valid', attempts: [{ runId: 'run1', attempt: 1, previousRunId: null, classification: 'completed', decision: 'valid', metricsAvailable: true, evidence }] }] };
  const record = { schemaVersion: 2, runId: 'run1', taskId: 'T1', architecture: 'single-agent', repetition: 1, orderPosition: 1, orderSeed: 'seed', runKind: 'official' };
  const selection = selectOfficialAttempts(plan, audit, () => true, () => record);
  const summary = { schemaVersion: 1, runId: 'run1', taskId: 'T1', outcome: 'passed', exitCode: 0, integrity: { originalInputUnchanged: true, archivedSubmissionUnchanged: true, suiteManifestsVerified: true } };
  const link = (value) => attachOfficialEvaluation(selection, () => value);
  const before = JSON.stringify(selection);
  assert.equal(link(summary)[0].evaluationLinked, true);
  assert.equal(link({ ...summary, outcome: 'test-failures', exitCode: 10 })[0].evaluationLinked, true);
  assert.equal(link({ ...summary, outcome: 'build-failure', exitCode: 11 })[0].evaluationLinked, true);
  for (const value of [null, { ...summary, runId: 'other' }, { ...summary, taskId: 'T2' }, { ...summary, schemaVersion: 2 }, { ...summary, exitCode: 10 }, { ...summary, outcome: 'evaluator-error', exitCode: 40 }, { ...summary, outcome: 'integrity-error', exitCode: 30 }, { ...summary, outcome: 'timeout', exitCode: 12 }, { ...summary, integrity: { ...summary.integrity, suiteManifestsVerified: false } }, { ...summary, integrity: null }]) {
    const row = link(value)[0];
    assert.equal(row.evaluationLinked, false);
    assert.equal(row.evaluationSummary, null);
    assert.ok(row.evaluationIssues.length);
  }
  assert.equal(link(summary)[1].evaluationLinked, false);
  assert.equal(attachOfficialEvaluation(selection, () => { throw new Error('unreadable'); })[0].evaluationSummary, null);
  assert.equal(JSON.stringify(selection), before);
});
