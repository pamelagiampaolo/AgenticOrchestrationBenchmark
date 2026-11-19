'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

test('CLI checks the frozen plan and supplied audit anchor without writing input files', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-report-cli-'));
  const planPath = 'C:\\AgenticAIResults\\official-plan-aee8d1162bb496895990a00095cc14c2651ff752.json';
  const plan = JSON.parse(fs.readFileSync(planPath, 'utf8'));
  const auditPath = path.join(dir, 'audit.json');
  const expectedPath = path.join(dir, 'expected.json');
  const badPlanPath = path.join(dir, 'bad-plan.json');
  const cli = path.resolve(__dirname, '../scripts/current/Report-OfficialResults.cjs');
  try {
    fs.writeFileSync(auditPath, JSON.stringify({ schemaVersion: 1, seed: plan.seed, slots: [] }));
    fs.writeFileSync(expectedPath, '{}');
    fs.writeFileSync(badPlanPath, '{}');
    const hash = (file) => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
    const anchor = hash(auditPath);
    const originalExpected = hash(expectedPath);
    const invoke = (planFile, suppliedAnchor) => spawnSync(process.execPath, [cli, planFile, auditPath, suppliedAnchor, dir, dir, expectedPath], { encoding: 'utf8' });
    const result = invoke(planPath, anchor);
    assert.equal(result.status, 0, result.stderr);
    const report = JSON.parse(result.stdout);
    assert.equal(report.inputs.auditSha256, anchor);
    assert.equal(report.inputs.expectedCountsSha256, originalExpected);
    assert.equal(report.completion.byTaskArchitecture.length, 18);
    assert.deepEqual([report.completion.byArchitecture[0].planned, report.completion.byArchitecture[0].available, report.completion.byArchitecture[0].rate], [18, 0, null]);
    assert.equal(report.issues.length, 0);
    const wrongAnchor = invoke(planPath, '0'.repeat(64));
    assert.equal(wrongAnchor.status, 1);
    assert.match(wrongAnchor.stderr, /Audit anchor mismatch/);
    const wrongPlan = invoke(badPlanPath, anchor);
    assert.equal(wrongPlan.status, 1);
    assert.match(wrongPlan.stderr, /Official plan hash mismatch/);
    assert.equal(hash(auditPath), anchor);
    assert.equal(hash(expectedPath), originalExpected);
  } finally {
    for (const file of [auditPath, expectedPath, badPlanPath]) if (fs.existsSync(file)) fs.unlinkSync(file);
    fs.rmdirSync(dir);
  }
});
