'use strict';

const TASKS = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6'];
const ARCHITECTURES = ['single-agent', 'hierarchical-static', 'hierarchical-adaptive'];

/** Summarize P59 completion results across the planned official design, retaining missing values. */
function summarizeOfficialCompletion(rows) {
  if (!Array.isArray(rows) || rows.length !== 54) throw new Error('Expected 54 planned slots');
  const ids = new Set();
  const combinations = new Set();
  for (const row of rows) {
    const slot = row && row.slot;
    if (!slot || slot.runKind !== 'official' || !TASKS.includes(slot.taskId) || !ARCHITECTURES.includes(slot.architecture) || ![1, 2, 3].includes(slot.repetition) || typeof slot.slotId !== 'string' || !slot.slotId || ids.has(slot.slotId)) throw new Error('Invalid or duplicate official slot');
    const key = `${slot.taskId}/${slot.architecture}/${slot.repetition}`;
    if (combinations.has(key)) throw new Error('Duplicate task, architecture and repetition');
    ids.add(slot.slotId); combinations.add(key);
    if (!['valid', 'incomplete', 'review', 'not-started'].includes(row.outcome) || ![true, false, null].includes(row.correctCompletion) || (row.outcome !== 'valid' && row.correctCompletion !== null)) throw new Error(`${slot.slotId}: invalid completion result`);
  }
  const summarize = (group) => {
    const validRuns = group.filter((row) => row.outcome === 'valid').length;
    const correct = group.filter((row) => row.correctCompletion === true).length;
    const incorrect = group.filter((row) => row.correctCompletion === false).length;
    const available = correct + incorrect;
    return {
      planned: group.length, validRuns, available, correct, incorrect,
      missing: group.length - available, incomplete: available !== group.length,
      rate: available ? correct / available : null,
      values: group.map((row) => ({ slotId: row.slot.slotId, outcome: row.outcome, correctCompletion: row.correctCompletion })),
    };
  };
  return {
    byTaskArchitecture: TASKS.flatMap((taskId) => ARCHITECTURES.map((architecture) => ({ taskId, architecture, ...summarize(rows.filter((row) => row.slot.taskId === taskId && row.slot.architecture === architecture)) }))),
    byArchitecture: ARCHITECTURES.map((architecture) => ({ architecture, ...summarize(rows.filter((row) => row.slot.architecture === architecture)) })),
  };
}

module.exports = { summarizeOfficialCompletion };
