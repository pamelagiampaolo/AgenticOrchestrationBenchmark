'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { computeOfficialCompletion } = require('../scripts/current/Compute-OfficialCompletion.cjs');

test('derives completion conservatively from evaluation, limits and measurement', () => {
  const record = { status: 'completed', timeLimitMs: 240000, tokenBudget: 800000, agentTaskDurationMs: 1000, tokensObserved: 100, protectedIntegrityDifferences: [] };
  const row = { outcome: 'valid', record, evaluationLinked: true, evaluationSummary: { outcome: 'passed' }, canonicalPassRate: 1, passRateIssues: [], usageComplete: true, totalTokens: 100 };
  const calculate = (change = {}) => computeOfficialCompletion([{ ...row, ...change }])[0];
  assert.deepEqual([calculate().functionalSubmissionCorrect, calculate().correctCompletion], [true, true]);
  for (const outcome of ['test-failures', 'build-failure']) {
    const result = calculate({ evaluationSummary: { outcome }, canonicalPassRate: 0 });
    assert.deepEqual([result.functionalSubmissionCorrect, result.correctCompletion], [false, false]);
  }
  for (const status of ['time-limit-exceeded', 'token-limit-exceeded', 'resource-limits-exceeded']) {
    const result = calculate({ record: { ...record, status }, usageComplete: false, totalTokens: null });
    assert.deepEqual([result.functionalSubmissionCorrect, result.correctCompletion], [true, false]);
  }
  assert.equal(calculate({ record: { ...record, status: 'cli-failed' } }).correctCompletion, true);
  for (const change of [
    { evaluationLinked: false }, { canonicalPassRate: null }, { passRateIssues: ['missing'] },
    { usageComplete: false, totalTokens: null }, { totalTokens: 101 },
    { record: { ...record, timeLimitMs: 1 } }, { record: { ...record, tokenBudget: 1 } },
    { record: { ...record, protectedIntegrityDifferences: ['changed'] } },
    { record: { ...record, status: 'usage-measurement-error' } },
    { record: { ...record, agentTaskDurationMs: 240001 } },
  ]) assert.equal(calculate(change).correctCompletion, null);
  assert.equal(calculate({ evaluationLinked: false }).functionalSubmissionCorrect, null);
});
