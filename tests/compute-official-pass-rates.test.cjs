'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { attachOfficialEvaluation } = require('../scripts/current/Attach-OfficialEvaluation.cjs');
const { computeOfficialPassRates } = require('../scripts/current/Compute-OfficialPassRates.cjs');

test('derives canonical pass rates without inventing missing evaluator results', () => {
  const selected = [{ slot: { taskId: 'T1' }, outcome: 'valid', record: { runId: 'r1', taskId: 'T1' } }, { slot: { taskId: 'T1' }, outcome: 'not-started', record: null }];
  const counts = (passed, failed, skipped) => ({ expected: passed + failed + skipped, executed: passed + failed, passed, failed, skipped, missing: 0, unexpected: 0 });
  const summary = { schemaVersion: 1, runId: 'r1', taskId: 'T1', outcome: 'passed', exitCode: 0, integrity: { originalInputUnchanged: true, archivedSubmissionUnchanged: true, suiteManifestsVerified: true }, phases: { build: { status: 'passed' }, public: { status: 'passed' }, reserved: { status: 'passed' } }, publicTests: counts(3, 0, 0), reservedTests: counts(2, 0, 0) };
  const rates = (s, expected = { public: 3, reserved: 2 }) => computeOfficialPassRates(attachOfficialEvaluation(selected, () => s), () => expected);
  assert.deepEqual([rates(summary)[0].publicPassRate, rates(summary)[0].reservedPassRate, rates(summary)[0].canonicalPassRate], [1, 1, 1]);
  const failed = { ...summary, outcome: 'test-failures', exitCode: 10, phases: { ...summary.phases, public: { status: 'failed' } }, publicTests: counts(1, 1, 1) };
  assert.deepEqual([rates(failed)[0].publicPassRate, rates(failed)[0].reservedPassRate, rates(failed)[0].canonicalPassRate], [1 / 3, 1, 3 / 5]);
  const buildFailure = { ...summary, outcome: 'build-failure', exitCode: 11, phases: { build: { status: 'failed' } }, publicTests: null, reservedTests: null };
  assert.equal(rates(buildFailure)[0].canonicalPassRate, 0);
  for (const invalid of [null, { ...summary, outcome: 'evaluator-error', exitCode: 40 }, { ...summary, publicTests: null }, { ...summary, publicTests: { ...summary.publicTests, expected: 4 } }, { ...summary, publicTests: { ...summary.publicTests, missing: 1 } }, { ...summary, publicTests: { ...summary.publicTests, executed: 2 } }, { ...summary, phases: { ...summary.phases, public: { status: 'failed' } } }]) assert.equal(rates(invalid)[0].canonicalPassRate, null);
  assert.equal(rates(summary, null)[0].canonicalPassRate, null);
  assert.equal(rates(summary)[1].canonicalPassRate, null);
});
