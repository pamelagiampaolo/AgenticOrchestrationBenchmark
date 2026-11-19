'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { summarizeOfficialUsage } = require('../scripts/current/Summarize-OfficialUsage.cjs');

test('summarizes planned groups while retaining missing and real zero values', () => {
  const rows = [];
  for (const taskId of ['T1', 'T2', 'T3', 'T4', 'T5', 'T6']) {
    for (const architecture of ['single-agent', 'hierarchical-static', 'hierarchical-adaptive']) {
      for (const repetition of [1, 2, 3]) {
        rows.push({ slot: { slotId: `${taskId}-${architecture}-${repetition}`, taskId, architecture, repetition, runKind: 'official' }, outcome: 'valid', usageComplete: true, totalTokens: repetition * 100 });
      }
    }
  }
  rows[0].totalTokens = 0;
  rows[0].record = { status: 'test-failures' };
  rows[1].usageComplete = false; rows[1].totalTokens = null;
  const original = JSON.stringify(rows);
  const result = summarizeOfficialUsage(rows);
  const group = result.byTaskArchitecture[0];
  assert.deepEqual([group.planned, group.validRuns, group.available, group.missing, group.incomplete], [3, 3, 2, 1, true]);
  assert.deepEqual([group.min, group.max, group.median, group.mean, group.availableSum, group.completeSum], [0, 300, 150, 150, 300, null]);
  assert.deepEqual(group.values.map((v) => v.totalTokens), [0, null, 300]);
  const architecture = result.byArchitecture[0];
  assert.deepEqual([architecture.planned, architecture.validRuns, architecture.available, architecture.missing, architecture.availableSum, architecture.completeSum], [18, 18, 17, 1, 3300, null]);
  assert.equal(result.byTaskArchitecture[1].completeSum, 600);
  assert.equal(result.byArchitecture[1].completeSum, 3600);
  assert.equal(JSON.stringify(rows), original);
  const absent = rows.map((r) => ({ ...r, outcome: 'not-started', usageComplete: false, totalTokens: null }));
  const empty = summarizeOfficialUsage(absent).byArchitecture[0];
  assert.deepEqual([empty.validRuns, empty.available, empty.mean, empty.availableSum, empty.completeSum], [0, 0, null, null, null]);
  assert.throws(() => summarizeOfficialUsage(rows.slice(1)), /54/);
  assert.throws(() => summarizeOfficialUsage([...rows.slice(0, 53), rows[0]]), /duplicate/i);
  assert.throws(() => summarizeOfficialUsage(rows.map((r, i) => i ? r : { ...r, outcome: 'review' })), /invalid token value/);
  assert.throws(() => summarizeOfficialUsage(rows.map((r, i) => i ? r : { ...r, totalTokens: -1 })), /invalid token value/);
});
