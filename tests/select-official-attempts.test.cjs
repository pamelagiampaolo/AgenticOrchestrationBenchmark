'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { selectOfficialAttempts } = require('../scripts/current/Select-OfficialAttempts.cjs');

test('selects only the valid official attempt and keeps unfilled slots', () => {
  const slots = Array.from({ length: 54 }, (_, i) => ({ slotId: `slot-${i}`, orderPosition: i + 1, taskId: 'T1', architecture: 'single-agent', repetition: 1, runKind: 'official' }));
  const plan = { seed: 'seed', slots };
  const evidence = { path: path.resolve('evidence'), sha256: 'a'.repeat(64) };
  const attempt = (runId, classification, decision) => ({ runId, attempt: 1, previousRunId: null, classification, decision, metricsAvailable: true, evidence });
  const first = attempt('original', 'INFRA_PROVIDER', 'invalid');
  const replacement = { ...attempt('replacement', 'completed', 'valid'), attempt: 2, previousRunId: 'original', authorizationEvidence: evidence };
  const entries = [
    { ...slots[0], attempts: [first, replacement], slotOutcome: 'valid' },
    { ...slots[1], attempts: [attempt('failure', 'test-failures', 'valid')], slotOutcome: 'valid' },
    { ...slots[2], attempts: [attempt('invalid', 'INFRA_PROVIDER', 'invalid')], slotOutcome: 'incomplete' },
    { ...slots[3], attempts: [attempt('unknown', 'non-classificabile', 'review')], slotOutcome: 'review' },
  ];
  const audit = { schemaVersion: 1, seed: plan.seed, slots: entries };
  const record = (runId, slot) => ({ schemaVersion: 2, runId, taskId: slot.taskId, architecture: slot.architecture, repetition: slot.repetition, orderPosition: slot.orderPosition, orderSeed: plan.seed, runKind: 'official' });
  const records = { replacement: record('replacement', slots[0]), failure: record('failure', slots[1]) };
  const select = (a = audit, reader = (id) => records[id], p = plan) => selectOfficialAttempts(p, a, () => true, reader);
  const selected = select();
  assert.equal(selected.length, 54);
  assert.deepEqual(selected.slice(0, 5).map((entry) => entry.outcome), ['valid', 'valid', 'incomplete', 'review', 'not-started']);
  assert.equal(selected[0].record.runId, 'replacement');
  assert.equal(selected[1].record.runId, 'failure');
  assert.equal(selected[2].record, null);
  for (const change of [{ runKind: 'pilot' }, { runId: 'original' }, { taskId: 'T2' }, { architecture: 'hierarchical-static' }, { repetition: 2 }, { orderPosition: 2 }, { orderSeed: 'wrong' }, { schemaVersion: 1 }]) {
    assert.throws(() => select(audit, (id) => ({ ...records[id], ...change })), /inconsistent/);
  }
  assert.throws(() => select(audit, () => null), /inconsistent/);
  assert.throws(() => select({ ...audit, slots: [{ ...entries[0], slotOutcome: 'incomplete' }] }), /Invalid audit/);
  assert.throws(() => select(audit, undefined, { ...plan, slots: [{ ...slots[0], runKind: 'pilot' }, ...slots.slice(1)] }), /non-official/);
});
