#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { checkFileEvidence } = require('./Test-OfficialAudit.cjs');
const { selectOfficialAttempts } = require('./Select-OfficialAttempts.cjs');
const { attachOfficialUsage } = require('./Attach-OfficialUsage.cjs');
const { attachOfficialEvaluation } = require('./Attach-OfficialEvaluation.cjs');
const { computeOfficialPassRates } = require('./Compute-OfficialPassRates.cjs');
const { computeOfficialCompletion } = require('./Compute-OfficialCompletion.cjs');
const { summarizeOfficialUsage } = require('./Summarize-OfficialUsage.cjs');
const { summarizeOfficialCompletion } = require('./Summarize-OfficialCompletion.cjs');

const PLAN_HASH = '441753b52b77cde505af0dd1bb8541c1300288794751a8bb5732e74a731e4573';
const sha256 = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');

/** Read trusted run and evaluation files and calculate descriptive results without writing files. */
function buildReport(plan, audit, resultsRoot, evaluationRoot, expected) {
  const runDir = (id) => path.join(resultsRoot, id);
  const readJson = (file) => JSON.parse(fs.readFileSync(file, 'utf8'));
  const readRecord = (id) => {
    const dir = runDir(id);
    const names = ['session-record.json', 'run-record.json'].filter((name) => fs.existsSync(path.join(dir, name)));
    if (names.length !== 1) throw new Error(`${id}: expected exactly one run record`);
    return readJson(path.join(dir, names[0]));
  };
  let rows = selectOfficialAttempts(plan, audit, checkFileEvidence, readRecord);
  rows = attachOfficialUsage(rows, runDir);
  rows = attachOfficialEvaluation(rows, (id) => readJson(path.join(evaluationRoot, id, 'evidence', 'evaluation-summary.json')));
  rows = computeOfficialPassRates(rows, (task) => expected[task]);
  rows = computeOfficialCompletion(rows);
  return {
    schemaVersion: 1,
    limitations: [
      'The supplied audit anchor and canonical suite sizes require independent trusted custody and verification.',
      'Record and evaluation provenance, submission linkage, complete audit history and all other B5 metrics are not verified.',
    ],
    usage: summarizeOfficialUsage(rows),
    completion: summarizeOfficialCompletion(rows),
    issues: rows.filter((row) => row.outcome === 'valid').map((row) => ({ slotId: row.slot.slotId, runId: row.record.runId, usage: row.usageIssues, evaluation: row.evaluationIssues, passRates: row.passRateIssues, completion: row.completionIssues })),
  };
}

if (require.main === module) {
  try {
    if (process.argv.length !== 8) throw new Error('Usage: node Report-OfficialResults.cjs <plan.json> <audit.json> <trusted-audit-sha256> <results-root> <evaluator-runs-root> <expected-counts.json>');
    const [, , planPath, auditPath, anchor, resultsRoot, evaluationRoot, expectedPath] = process.argv;
    const planBytes = fs.readFileSync(planPath);
    if (sha256(planBytes) !== PLAN_HASH) throw new Error('Official plan hash mismatch');
    const auditBytes = fs.readFileSync(auditPath);
    if (!/^[a-f0-9]{64}$/.test(anchor) || sha256(auditBytes) !== anchor) throw new Error('Audit anchor mismatch');
    const expectedBytes = fs.readFileSync(expectedPath);
    const report = buildReport(JSON.parse(planBytes), JSON.parse(auditBytes), resultsRoot, evaluationRoot, JSON.parse(expectedBytes));
    report.inputs = { planSha256: sha256(planBytes), auditSha256: sha256(auditBytes), expectedCountsSha256: sha256(expectedBytes) };
    console.log(JSON.stringify(report, null, 2));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}

module.exports = { buildReport };
