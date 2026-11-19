'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { checkFileEvidence } = require('../scripts/current/Test-OfficialAudit.cjs');
const { selectOfficialAttempts } = require('../scripts/current/Select-OfficialAttempts.cjs');
const { attachOfficialUsage } = require('../scripts/current/Attach-OfficialUsage.cjs');
const { attachOfficialEvaluation } = require('../scripts/current/Attach-OfficialEvaluation.cjs');
const { computeOfficialPassRates } = require('../scripts/current/Compute-OfficialPassRates.cjs');
const { computeOfficialCompletion } = require('../scripts/current/Compute-OfficialCompletion.cjs');
const { summarizeOfficialUsage } = require('../scripts/current/Summarize-OfficialUsage.cjs');
const { summarizeOfficialCompletion } = require('../scripts/current/Summarize-OfficialCompletion.cjs');

test('reconciles a replacement, failure and missing usage through the reporting chain', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-report-'));
  const ids = ['replacement', 'failure', 'missing'];
  const hash = (p) => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
  const writeJson = (p, data) => fs.writeFileSync(p, JSON.stringify(data));
  try {
    const evidencePath = path.join(dir, 'evidence.txt');
    const approvalPath = path.join(dir, 'approval.txt');
    fs.writeFileSync(evidencePath, 'simulated provider evidence');
    fs.writeFileSync(approvalPath, 'simulated authorization');
    const ref = (p) => ({ path: p, sha256: hash(p) });
    const slots = [];
    for (const taskId of ['T1', 'T2', 'T3', 'T4', 'T5', 'T6']) {
      for (const architecture of ['single-agent', 'hierarchical-static', 'hierarchical-adaptive']) {
        for (const repetition of [1, 2, 3]) slots.push({ slotId: `${taskId}-${architecture}-${repetition}`, taskId, architecture, repetition, orderPosition: slots.length + 1, runKind: 'official' });
      }
    }
    const plan = { seed: 'fixture-seed', slots };
    const attempt = (id, classification, decision) => ({ runId: id, attempt: 1, previousRunId: null, classification, decision, metricsAvailable: true, evidence: ref(evidencePath) });
    const audit = { schemaVersion: 1, seed: plan.seed, slots: ids.map((id, i) => ({ ...slots[i], slotOutcome: 'valid', attempts: i === 0 ? [attempt('original', 'INFRA_PROVIDER', 'invalid'), { ...attempt(id, 'completed', 'valid'), attempt: 2, previousRunId: 'original', authorizationEvidence: ref(approvalPath) }] : [attempt(id, i === 1 ? 'build-failure' : 'completed', 'valid')] })) };
    const totals = { inputTokens: 2, cacheReadInputTokens: 0, cacheCreationInputTokens: 3, outputTokens: 4, totalInputTokens: 5, totalTokens: 9 };
    const raw = { input_tokens: 2, cache_read_input_tokens: 0, cache_creation_input_tokens: 3, output_tokens: 4 };
    const counts = (n) => ({ expected: n, executed: n, passed: n, failed: 0, skipped: 0, missing: 0, unexpected: 0 });
    for (const [i, id] of ids.entries()) {
      const runDir = path.join(dir, id);
      fs.mkdirSync(runDir);
      const record = { schemaVersion: 2, runId: id, ...slots[i], orderSeed: plan.seed, status: 'completed', timeLimitMs: 240000, tokenBudget: 800000, agentTaskDurationMs: 1000, tokenMeasurementComplete: true, tokensObserved: 9, authoritativeUsage: totals, protectedIntegrityDifferences: [] };
      const summary = { schemaVersion: 1, runId: id, taskId: 'T1', outcome: i === 1 ? 'build-failure' : 'passed', exitCode: i === 1 ? 11 : 0, integrity: { originalInputUnchanged: true, archivedSubmissionUnchanged: true, suiteManifestsVerified: true }, phases: { build: { status: i === 1 ? 'failed' : 'passed' }, public: { status: i === 1 ? 'not-run' : 'passed' }, reserved: { status: i === 1 ? 'not-run' : 'passed' } }, publicTests: i === 1 ? null : counts(3), reservedTests: i === 1 ? null : counts(2) };
      writeJson(path.join(runDir, 'record.json'), record);
      writeJson(path.join(runDir, 'summary.json'), summary);
      writeJson(path.join(runDir, 'stream.jsonl'), { type: 'result', usage: i === 2 ? { input_tokens: 2, cache_creation_input_tokens: 3, output_tokens: 4 } : raw });
    }
    const read = (id, file) => JSON.parse(fs.readFileSync(path.join(dir, id, file), 'utf8'));
    const sourceHashes = ids.flatMap((id) => ['record.json', 'summary.json', 'stream.jsonl'].map((file) => hash(path.join(dir, id, file))));
    let rows = selectOfficialAttempts(plan, audit, checkFileEvidence, (id) => read(id, 'record.json'));
    rows = attachOfficialUsage(rows, (id) => path.join(dir, id));
    rows = attachOfficialEvaluation(rows, (id) => read(id, 'summary.json'));
    rows = computeOfficialPassRates(rows, () => ({ public: 3, reserved: 2 }));
    rows = computeOfficialCompletion(rows);
    assert.deepEqual(rows.slice(0, 3).map((row) => row.correctCompletion), [true, false, null]);
    assert.equal(rows[0].record.runId, 'replacement');
    assert.equal(rows[2].usageComplete, false);
    const usage = summarizeOfficialUsage(rows);
    const completion = summarizeOfficialCompletion(rows);
    assert.deepEqual([usage.byTaskArchitecture[0].planned, usage.byTaskArchitecture[0].available, usage.byTaskArchitecture[0].availableSum, usage.byTaskArchitecture[0].completeSum], [3, 2, 18, null]);
    assert.deepEqual([completion.byTaskArchitecture[0].validRuns, completion.byTaskArchitecture[0].available, completion.byTaskArchitecture[0].correct, completion.byTaskArchitecture[0].rate], [3, 2, 1, 0.5]);
    assert.deepEqual([completion.byArchitecture[0].planned, completion.byArchitecture[0].available, completion.byArchitecture[0].missing], [18, 2, 16]);
    assert.deepEqual(ids.flatMap((id) => ['record.json', 'summary.json', 'stream.jsonl'].map((file) => hash(path.join(dir, id, file)))), sourceHashes);
    fs.writeFileSync(evidencePath, 'altered evidence');
    assert.throws(() => selectOfficialAttempts(plan, audit, checkFileEvidence, (id) => read(id, 'record.json')), /evidence mismatch/);
  } finally {
    for (const id of ids) {
      for (const file of ['record.json', 'summary.json', 'stream.jsonl']) {
        const p = path.join(dir, id, file);
        if (fs.existsSync(p)) fs.unlinkSync(p);
      }
      const runDir = path.join(dir, id);
      if (fs.existsSync(runDir)) fs.rmdirSync(runDir);
    }
    for (const file of ['evidence.txt', 'approval.txt']) {
      const p = path.join(dir, file);
      if (fs.existsSync(p)) fs.unlinkSync(p);
    }
    fs.rmdirSync(dir);
  }
});
