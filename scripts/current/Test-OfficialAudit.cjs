#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const crypto = require('node:crypto');
const path = require('node:path');

const EXPECTED_PLAN_HASH = '441753b52b77cde505af0dd1bb8541c1300288794751a8bb5732e74a731e4573';
const INFRA = new Set(['INFRA_PROVIDER', 'INFRA_HOST_START', 'INFRA_COMMON_SERVICE', 'INFRA_MEASUREMENT']);
const VALID_CLASSIFICATIONS = new Set(['completed', 'test-failures', 'build-failure', 'time-limit-exceeded', 'token-limit-exceeded', 'resource-limits-exceeded', 'architecture-failure']);

/** Validate audit structure against the supplied plan; errors identify the violated invariant. */
function validateAudit(plan, audit, checkEvidence) {
  const errors = [];
  const fail = (message) => errors.push(message);
  if (!plan || !Array.isArray(plan.slots) || plan.slots.length !== 54 || typeof plan.seed !== 'string') return ['Invalid official plan'];
  if (!audit || audit.schemaVersion !== 1 || audit.seed !== plan.seed || !Array.isArray(audit.slots)) return ['Invalid audit header'];
  const slots = new Map(plan.slots.map((slot) => [slot.slotId, slot]));
  const seenSlots = new Set();
  const seenRuns = new Set();
  const verifyEvidence = (reference, label) => {
    if (!reference || typeof reference.path !== 'string' || !path.isAbsolute(reference.path) || !/^[0-9a-f]{64}$/.test(reference.sha256 || '')) {
      fail(`${label}: invalid evidence reference`); return;
    }
    try { if (!checkEvidence(reference)) fail(`${label}: evidence mismatch`); }
    catch { fail(`${label}: evidence unreadable`); }
  };
  for (const entry of audit.slots) {
    if (!entry || typeof entry.slotId !== 'string') { fail('Invalid slot entry'); continue; }
    const label = entry.slotId;
    const expected = slots.get(entry.slotId);
    if (!expected || seenSlots.has(entry.slotId)) { fail(`${label}: unknown or duplicate slot`); continue; }
    seenSlots.add(entry.slotId);
    for (const key of ['orderPosition', 'taskId', 'architecture', 'repetition']) {
      if (entry[key] !== expected[key]) fail(`${label}: ${key} differs from plan`);
    }
    if (!Array.isArray(entry.attempts) || entry.attempts.length < 1 || entry.attempts.length > 2) { fail(`${label}: invalid attempt count`); continue; }
    entry.attempts.forEach((attempt, index) => {
      if (!attempt || typeof attempt.runId !== 'string' || !/^[A-Za-z0-9_-]{1,64}$/.test(attempt.runId) || seenRuns.has(attempt.runId)) { fail(`${label}: invalid or duplicate runId`); return; }
      seenRuns.add(attempt.runId);
      if (attempt.attempt !== index + 1) fail(`${label}: invalid attempt number`);
      if (typeof attempt.metricsAvailable !== 'boolean') fail(`${label}: missing metrics availability`);
      verifyEvidence(attempt.evidence, `${label}/${attempt.runId}`);
      if (index === 0 && attempt.previousRunId !== null) fail(`${label}: first attempt has predecessor`);
      if (index === 1) {
        const first = entry.attempts[0];
        if (!first || attempt.previousRunId !== first.runId) fail(`${label}: broken predecessor`);
        if (!first || !INFRA.has(first.classification) || first.decision !== 'invalid') fail(`${label}: replacement not supported`);
        verifyEvidence(attempt.authorizationEvidence, `${label}/${attempt.runId}/authorization`);
      }
      const validDecision = INFRA.has(attempt.classification) ? attempt.decision === 'invalid'
        : attempt.classification === 'non-classificabile' || attempt.classification === 'integrity-error' ? attempt.decision === 'review'
        : VALID_CLASSIFICATIONS.has(attempt.classification) && attempt.decision === 'valid';
      if (!validDecision) fail(`${label}: invalid classification or decision`);
    });
    const last = entry.attempts[entry.attempts.length - 1];
    if (!last) continue;
    const outcome = last.decision === 'valid' ? 'valid' : last.decision === 'review' ? 'review' : 'incomplete';
    if (entry.slotOutcome !== outcome) fail(`${label}: invalid slot outcome`);
  }
  return errors;
}

/** Verify that an evidence file has the declared SHA-256 without altering it. */
function checkFileEvidence(reference) {
  const stat = fs.lstatSync(reference.path);
  if (!stat.isFile() || stat.isSymbolicLink()) return false;
  return crypto.createHash('sha256').update(fs.readFileSync(reference.path)).digest('hex') === reference.sha256;
}

if (require.main === module) {
  try {
    if (process.argv.length !== 4) throw new Error('Usage: node Test-OfficialAudit.cjs <official-plan.json> <audit.json>');
    const planBytes = fs.readFileSync(process.argv[2]);
    if (crypto.createHash('sha256').update(planBytes).digest('hex') !== EXPECTED_PLAN_HASH) throw new Error('Official plan hash mismatch');
    const errors = validateAudit(JSON.parse(planBytes), JSON.parse(fs.readFileSync(process.argv[3], 'utf8')), checkFileEvidence);
    if (errors.length) { for (const error of errors) console.error(error); process.exitCode = 1; }
    else console.log('Audit structure and evidence hashes are consistent with the official plan.');
  } catch (error) { console.error(error.message); process.exitCode = 2; }
}

module.exports = { validateAudit, checkFileEvidence };
