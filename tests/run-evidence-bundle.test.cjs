'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const { verifyBundle } = require('../scripts/current/Test-RunEvidenceBundle.cjs');

test('checks real pilot evidence and detects a changed nested hash', () => {
  const runId = '20261118T172146Z-77c1a569';
  const ps = 'C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe';
  const script = path.resolve(__dirname, '../scripts/current/Test-RunEvidence.ps1');
  const result = spawnSync(ps, ['-NoProfile', '-File', script, '-ResultsDirectory', `C:\\AgenticAIResults\\${runId}`, '-EvaluationDirectory', `C:\\repos\\AgenticOrchestrationEvaluator\\runs\\${runId}`], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  const bundle = JSON.parse(result.stdout);
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'run-evidence-'));
  const file = path.join(dir, 'bundle.json');
  const sha = () => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
  try {
    fs.writeFileSync(file, JSON.stringify(bundle));
    assert.deepEqual(verifyBundle(file, sha(), runId, 'T1'), { runId, taskId: 'T1', runKind: 'pilot', referencesChecked: 5, submissionTreeSha256: bundle.submissionTreeSha256 });
    assert.throws(() => verifyBundle(file, '0'.repeat(64), runId, 'T1'), /document hash mismatch/);
    assert.throws(() => verifyBundle(file, sha(), runId, 'T6'), /identity/);
    bundle.files[0].sha256 = '0'.repeat(64);
    fs.writeFileSync(file, JSON.stringify(bundle));
    assert.throws(() => verifyBundle(file, sha(), runId, 'T1'), /Referenced file hash mismatch/);
  } finally {
    if (fs.existsSync(file)) fs.unlinkSync(file);
    fs.rmdirSync(dir);
  }
});
