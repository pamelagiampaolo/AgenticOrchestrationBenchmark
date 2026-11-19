'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { summarizeOfficialCompletion } = require('../scripts/current/Summarize-OfficialCompletion.cjs');

test('summarizes completion with explicit valid, available and planned denominators', () => {
  const rows = [];
  for (const taskId of ['T1', 'T2', 'T3', 'T4', 'T5', 'T6']) {
    for (const architecture of ['single-agent', 'hierarchical-static', 'hierarchical-adaptive']) {
      for (const repetition of [1, 2, 3]) rows.push({ slot: { slotId: `${taskId}-${architecture}-${repetition}`, taskId, architecture, repetition, runKind: 'official' }, outcome: 'valid', correctCompletion: true });
    }
  }
  rows[1].correctCompletion = false;
  rows[2].correctCompletion = null;
  const snapshot = JSON.stringify(rows);
  const result = summarizeOfficialCompletion(rows);
  const group = result.byTaskArchitecture[0];
  assert.deepEqual([group.planned, group.validRuns, group.available, group.correct, group.incorrect, group.missing, group.incomplete, group.rate], [3, 3, 2, 1, 1, 1, true, 0.5]);
  assert.deepEqual(group.values.map((value) => value.correctCompletion), [true, false, null]);
  const arch = result.byArchitecture[0];
  assert.deepEqual([arch.planned, arch.validRuns, arch.available, arch.correct, arch.incorrect, arch.missing, arch.rate], [18, 18, 17, 16, 1, 1, 16 / 17]);
  assert.equal(result.byArchitecture[1].rate, 1);
  assert.equal(JSON.stringify(rows), snapshot);
  const unstarted = rows.map((row) => ({ ...row, outcome: 'not-started', correctCompletion: null }));
  const empty = summarizeOfficialCompletion(unstarted).byArchitecture[0];
  assert.deepEqual([empty.validRuns, empty.available, empty.correct, empty.incorrect, empty.missing, empty.rate], [0, 0, 0, 0, 18, null]);
  assert.throws(() => summarizeOfficialCompletion(rows.slice(1)), /54/);
  assert.throws(() => summarizeOfficialCompletion([...rows.slice(0, 53), rows[0]]), /duplicate/i);
  assert.throws(() => summarizeOfficialCompletion(rows.map((row, i) => i ? row : { ...row, outcome: 'review' })), /invalid completion/);
  assert.throws(() => summarizeOfficialCompletion(rows.map((row, i) => i ? row : { ...row, correctCompletion: 0 })), /invalid completion/);
});
