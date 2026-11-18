#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { validateAudit, checkFileEvidence } = require('./Test-OfficialAudit.cjs');

const PLAN_HASH = '441753b52b77cde505af0dd1bb8541c1300288794751a8bb5732e74a731e4573';
const hash = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');
const filename = (number) => `audit-${String(number).padStart(6, '0')}.json`;

/** Ensure a new snapshot only appends one slot or one attempt, preserving all earlier attempts. */
function checkTransition(previous, candidate) {
  if (previous.seed !== candidate.seed || previous.schemaVersion !== candidate.schemaVersion) throw new Error('Audit header changed');
  const before = new Map(previous.slots.map((slot) => [slot.slotId, slot]));
  let additions = 0;
  for (const next of candidate.slots) {
    const old = before.get(next.slotId);
    if (!old) { if (next.attempts.length !== 1) throw new Error('New slot must contain one attempt'); additions++; continue; }
    before.delete(next.slotId);
    for (const key of ['orderPosition', 'taskId', 'architecture', 'repetition']) if (next[key] !== old[key]) throw new Error('Existing slot metadata changed');
    if (next.attempts.length < old.attempts.length || next.attempts.length > old.attempts.length + 1) throw new Error('Attempts removed or added in bulk');
    for (let i = 0; i < old.attempts.length; i++) if (JSON.stringify(old.attempts[i]) !== JSON.stringify(next.attempts[i])) throw new Error('Existing attempt changed');
    additions += next.attempts.length - old.attempts.length;
  }
  if (before.size || additions !== 1) throw new Error('A revision must append exactly one attempt');
}

/** Append a new immutable-by-convention snapshot using exclusive creation and a hash-linked history. */
function saveSnapshot(plan, directory, candidate, expectedPreviousHash) {
  if (!fs.statSync(directory).isDirectory()) throw new Error('Audit directory must already exist');
  const files = fs.readdirSync(directory).filter((name) => /^audit-\d{6}\.json$/.test(name)).sort();
  let previous = { schemaVersion: 1, seed: plan.seed, slots: [] };
  let previousHash = 'none';
  files.forEach((name, index) => {
    if (name !== filename(index + 1)) throw new Error('Audit history has a gap');
    const bytes = fs.readFileSync(path.join(directory, name));
    const item = JSON.parse(bytes);
    if (item.revision !== index + 1 || item.previousSha256 !== previousHash) throw new Error('Audit history link mismatch');
    const errors = validateAudit(plan, item, checkFileEvidence);
    if (errors.length) throw new Error(`Invalid audit history: ${errors.join('; ')}`);
    checkTransition(previous, item);
    previous = item; previousHash = hash(bytes);
  });
  if (previousHash !== expectedPreviousHash) throw new Error('Previous hash does not match trusted anchor');
  const errors = validateAudit(plan, candidate, checkFileEvidence);
  if (errors.length) throw new Error(`Invalid candidate: ${errors.join('; ')}`);
  checkTransition(previous, candidate);
  const snapshot = { schemaVersion: candidate.schemaVersion, seed: candidate.seed, revision: files.length + 1, previousSha256: previousHash, slots: candidate.slots };
  const bytes = Buffer.from(`${JSON.stringify(snapshot, null, 2)}\n`, 'utf8');
  const output = path.join(directory, filename(snapshot.revision));
  fs.writeFileSync(output, bytes, { flag: 'wx' });
  return { path: output, sha256: hash(bytes), revision: snapshot.revision };
}

if (require.main === module) {
  try {
    if (process.argv.length !== 6) throw new Error('Usage: node Save-OfficialAudit.cjs <plan.json> <existing-audit-directory> <candidate.json> <previous-sha256|none>');
    const bytes = fs.readFileSync(process.argv[2]);
    if (hash(bytes) !== PLAN_HASH) throw new Error('Official plan hash mismatch');
    const result = saveSnapshot(JSON.parse(bytes), process.argv[3], JSON.parse(fs.readFileSync(process.argv[4], 'utf8')), process.argv[5]);
    console.log(JSON.stringify(result));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}

module.exports = { saveSnapshot };
