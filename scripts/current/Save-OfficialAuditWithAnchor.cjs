#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { saveSnapshot } = require('./Save-OfficialAudit.cjs');

const PLAN_HASH = '441753b52b77cde505af0dd1bb8541c1300288794751a8bb5732e74a731e4573';
const hash = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex');
const name = (prefix, index, extension) => `${prefix}-${String(index).padStart(6, '0')}.${extension}`;

/** Append an audit revision and a separate exclusive anchor; failures preserve all evidence for review. */
function saveWithAnchor(plan, historyDir, anchorDir, candidate, trustedPreviousHash) {
  const history = fs.realpathSync(historyDir);
  const anchors = fs.realpathSync(anchorDir);
  if (!fs.statSync(history).isDirectory() || !fs.statSync(anchors).isDirectory()) throw new Error('Both directories must exist');
  const relative = path.relative(history, anchors);
  const reverse = path.relative(anchors, history);
  if (!relative || (!relative.startsWith(`..${path.sep}`) && relative !== '..' && !path.isAbsolute(relative)) || (!reverse.startsWith(`..${path.sep}`) && reverse !== '..' && !path.isAbsolute(reverse))) throw new Error('Audit and anchor directories must be separate');
  const snapshots = fs.readdirSync(history).filter((file) => /^audit-\d{6}\.json$/.test(file)).sort();
  const anchorFiles = fs.readdirSync(anchors).filter((file) => /^anchor-\d{6}\.txt$/.test(file)).sort();
  if (snapshots.length !== anchorFiles.length) throw new Error('Audit and anchor counts differ; review required');
  let latest = 'none';
  for (let i = 0; i < snapshots.length; i++) {
    if (snapshots[i] !== name('audit', i + 1, 'json') || anchorFiles[i] !== name('anchor', i + 1, 'txt')) throw new Error('Audit or anchor history has a gap');
    const anchorFile = path.join(anchors, anchorFiles[i]);
    if (!fs.lstatSync(anchorFile).isFile()) throw new Error('Anchor must be a regular file');
    const value = fs.readFileSync(anchorFile, 'utf8').trim();
    if (!/^[a-f0-9]{64}$/.test(value) || value !== hash(fs.readFileSync(path.join(history, snapshots[i])))) throw new Error('Audit and anchor mismatch');
    latest = value;
  }
  if (latest !== trustedPreviousHash) throw new Error('Trusted previous hash mismatch');
  const snapshot = saveSnapshot(plan, history, candidate, latest);
  const anchorPath = path.join(anchors, name('anchor', snapshot.revision, 'txt'));
  try { fs.writeFileSync(anchorPath, `${snapshot.sha256}\n`, { flag: 'wx' }); }
  catch (error) { throw new Error(`Snapshot ${snapshot.path} saved but anchor failed; stop for review: ${error.message}`); }
  return { ...snapshot, anchorPath };
}

if (require.main === module) {
  try {
    if (process.argv.length !== 7) throw new Error('Usage: node Save-OfficialAuditWithAnchor.cjs <plan.json> <audit-dir> <anchor-dir> <candidate.json> <trusted-previous-sha256|none>');
    const [, , planPath, history, anchors, candidatePath, previousHash] = process.argv;
    const bytes = fs.readFileSync(planPath);
    if (hash(bytes) !== PLAN_HASH) throw new Error('Official plan hash mismatch');
    console.log(JSON.stringify(saveWithAnchor(JSON.parse(bytes), history, anchors, JSON.parse(fs.readFileSync(candidatePath, 'utf8')), previousHash)));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}

module.exports = { saveWithAnchor };
