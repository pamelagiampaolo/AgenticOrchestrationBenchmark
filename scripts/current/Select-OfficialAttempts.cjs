'use strict';

const { validateAudit } = require('./Test-OfficialAudit.cjs');

/** Select one validated record per planned slot; callers must verify the plan and audit provenance. */
function selectOfficialAttempts(plan, audit, checkEvidence, readRecord) {
  const errors = validateAudit(plan, audit, checkEvidence);
  if (errors.length) throw new Error(`Invalid audit: ${errors.join('; ')}`);
  if (plan.slots.some((slot) => slot.runKind !== 'official')) throw new Error('Plan contains a non-official slot');
  const entries = new Map(audit.slots.map((entry) => [entry.slotId, entry]));
  return plan.slots.map((slot) => {
    const entry = entries.get(slot.slotId);
    if (!entry) return { slot, outcome: 'not-started', record: null };
    if (entry.slotOutcome !== 'valid') return { slot, outcome: entry.slotOutcome, record: null };
    const attempt = entry.attempts[entry.attempts.length - 1];
    const record = readRecord(attempt.runId);
    if (!record || record.schemaVersion !== 2 || record.runKind !== 'official' ||
        record.runId !== attempt.runId || record.taskId !== slot.taskId ||
        record.architecture !== slot.architecture || record.repetition !== slot.repetition ||
        record.orderPosition !== slot.orderPosition || record.orderSeed !== plan.seed) {
      throw new Error(`${slot.slotId}: missing or inconsistent official run record`);
    }
    return { slot, outcome: 'valid', record };
  });
}

module.exports = { selectOfficialAttempts };
