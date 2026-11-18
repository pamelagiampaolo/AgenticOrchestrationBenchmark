'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

test('simulated official workflow preserves independent anchors and detects rollback', { skip: !process.env.OFFICIAL_PLAN_PATH }, () => {
  const planPath = process.env.OFFICIAL_PLAN_PATH;
  const plan = JSON.parse(fs.readFileSync(planPath, 'utf8'));
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'official-workflow-'));
  const history = path.join(dir, 'history');
  const anchors = path.join(dir, 'anchors');
  fs.mkdirSync(history); fs.mkdirSync(anchors);
  const files = [];
  try {
    const create = (name, content) => { const p = path.join(dir, name); fs.writeFileSync(p, content, { flag: 'wx' }); files.push(p); return p; };
    const evidence = create('simulated-run-record.json', '{"kind":"simulated-run-record"}');
    const approval = create('simulated-approval.txt', 'simulated authorization');
    const reference = (p) => ({ path: p, sha256: crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex') });
    const slot = plan.slots[0];
    const original = { runId: 'fixture-original', attempt: 1, previousRunId: null, classification: 'INFRA_PROVIDER', decision: 'invalid', metricsAvailable: false, evidence: reference(evidence) };
    const replacement = { runId: 'fixture-replacement', attempt: 2, previousRunId: original.runId, classification: 'completed', decision: 'valid', metricsAvailable: true, evidence: reference(evidence), authorizationEvidence: reference(approval) };
    const candidate = (attempts, slotOutcome) => ({ schemaVersion: 1, seed: plan.seed, slots: [{ slotId: slot.slotId, orderPosition: slot.orderPosition, taskId: slot.taskId, architecture: slot.architecture, repetition: slot.repetition, attempts, slotOutcome }] });
    const run = (script, args) => spawnSync(process.execPath, [path.join(__dirname, '..', 'scripts', 'current', script), ...args], { encoding: 'utf8' });
    const save = (name, data, anchor) => {
      const p = create(name, JSON.stringify(data));
      const result = run('Save-OfficialAudit.cjs', [planPath, history, p, anchor]);
      assert.equal(result.status, 0, result.stderr);
      const value = JSON.parse(result.stdout);
      fs.writeFileSync(path.join(anchors, `anchor-${value.revision}.txt`), value.sha256, { flag: 'wx' });
      return value;
    };
    const first = save('candidate-1.json', candidate([original], 'incomplete'), 'none');
    const second = save('candidate-2.json', candidate([original, replacement], 'valid'), fs.readFileSync(path.join(anchors, 'anchor-1.txt'), 'utf8'));
    assert.equal(JSON.parse(fs.readFileSync(second.path)).previousSha256, first.sha256);
    const check = run('Test-OfficialAudit.cjs', [planPath, second.path]);
    assert.equal(check.status, 0, check.stderr);
    assert.throws(() => fs.writeFileSync(path.join(anchors, 'anchor-2.txt'), 'overwrite', { flag: 'wx' }), /EEXIST/);
    fs.unlinkSync(second.path);
    const replay = run('Save-OfficialAudit.cjs', [planPath, history, path.join(dir, 'candidate-2.json'), fs.readFileSync(path.join(anchors, 'anchor-2.txt'), 'utf8')]);
    assert.equal(replay.status, 1);
    assert.match(replay.stderr, /trusted anchor/);
  } finally {
    for (const p of files) fs.unlinkSync(p);
    for (const p of fs.readdirSync(history)) fs.unlinkSync(path.join(history, p));
    for (const p of fs.readdirSync(anchors)) fs.unlinkSync(path.join(anchors, p));
    fs.rmdirSync(history); fs.rmdirSync(anchors); fs.rmdirSync(dir);
  }
});
