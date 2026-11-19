'use strict';

const TASKS = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6'];
const ARCHITECTURES = ['single-agent', 'hierarchical-static', 'hierarchical-adaptive'];

/** Summarize token measures on the 54 validated P54/P55 selections without imputing missing data. */
function summarizeOfficialUsage(rows) {
  if (!Array.isArray(rows) || rows.length !== 54) throw new Error('Expected 54 planned slots');
  const keys = new Set();
  const ids = new Set();
  for (const row of rows) {
    const slot = row && row.slot;
    if (!slot || slot.runKind !== 'official' || !TASKS.includes(slot.taskId) ||
        !ARCHITECTURES.includes(slot.architecture) || ![1, 2, 3].includes(slot.repetition) ||
        typeof slot.slotId !== 'string' || !slot.slotId || ids.has(slot.slotId)) throw new Error('Invalid or duplicate official slot');
    const key = `${slot.taskId}/${slot.architecture}/${slot.repetition}`;
    if (keys.has(key)) throw new Error('Duplicate task, architecture and repetition');
    ids.add(slot.slotId); keys.add(key);
    if (!['valid', 'incomplete', 'review', 'not-started'].includes(row.outcome) || typeof row.usageComplete !== 'boolean') throw new Error(`${slot.slotId}: invalid outcome or usage status`);
    if (row.usageComplete ? row.outcome !== 'valid' || !Number.isSafeInteger(row.totalTokens) || row.totalTokens < 0 : row.totalTokens !== null) throw new Error(`${slot.slotId}: invalid token value`);
  }
  const summarize = (group) => {
    const values = group.filter((row) => row.usageComplete).map((row) => row.totalTokens).sort((a, b) => a - b);
    let sum = 0;
    for (const value of values) { sum += value; if (!Number.isSafeInteger(sum)) throw new Error('Token sum exceeds safe integer range'); }
    const n = values.length;
    const validRuns = group.filter((row) => row.outcome === 'valid').length;
    return {
      planned: group.length, validRuns, available: n, missing: group.length - n, incomplete: n !== group.length,
      values: group.map((row) => ({ slotId: row.slot.slotId, outcome: row.outcome, totalTokens: row.usageComplete ? row.totalTokens : null })),
      min: n ? values[0] : null, max: n ? values[n - 1] : null,
      median: n ? (n % 2 ? values[(n - 1) / 2] : values[n / 2 - 1] / 2 + values[n / 2] / 2) : null,
      mean: n ? sum / n : null, availableSum: n ? sum : null, completeSum: n === group.length ? sum : null,
    };
  };
  return {
    byTaskArchitecture: TASKS.flatMap((taskId) => ARCHITECTURES.map((architecture) => ({ taskId, architecture, ...summarize(rows.filter((row) => row.slot.taskId === taskId && row.slot.architecture === architecture)) }))),
    byArchitecture: ARCHITECTURES.map((architecture) => ({ architecture, ...summarize(rows.filter((row) => row.slot.architecture === architecture)) })),
  };
}

module.exports = { summarizeOfficialUsage };
