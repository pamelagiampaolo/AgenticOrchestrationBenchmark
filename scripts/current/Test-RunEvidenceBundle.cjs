#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { checkFileEvidence } = require('./Test-OfficialAudit.cjs');

/** Check a P66 evidence document and its five referenced files against a trusted outer hash. */
function verifyBundle(bundlePath, expectedHash, expectedRunId, expectedTaskId) {
  if (!/^[a-f0-9]{64}$/.test(expectedHash) || !checkFileEvidence({ path: bundlePath, sha256: expectedHash })) throw new Error('Evidence document hash mismatch');
  const bundle = JSON.parse(fs.readFileSync(bundlePath, 'utf8').replace(/^\uFEFF/, ''));
  if (bundle.schemaVersion !== 1 || bundle.runId !== expectedRunId || bundle.taskId !== expectedTaskId || !['pilot', 'official'].includes(bundle.runKind) || !/^[a-f0-9]{64}$/.test(bundle.submissionTreeSha256 || '') || !Number.isSafeInteger(bundle.submissionFiles) || bundle.submissionFiles <= 0 || !Array.isArray(bundle.files) || bundle.files.length !== 5) throw new Error('Invalid evidence identity or structure');
  const files = new Map();
  for (const reference of bundle.files) {
    if (!reference || typeof reference.path !== 'string' || !path.isAbsolute(reference.path) || !/^[a-f0-9]{64}$/.test(reference.sha256 || '') || !checkFileEvidence(reference)) throw new Error('Referenced file hash mismatch or unreadable');
    const name = path.basename(reference.path);
    if (files.has(name)) throw new Error('Duplicate evidence file');
    files.set(name, JSON.parse(fs.readFileSync(reference.path, 'utf8').replace(/^\uFEFF/, '')));
  }
  const prepared = files.get('prepared-run.json');
  const record = files.get('run-record.json') || files.get('session-record.json');
  const metadata = files.get('metadata.json');
  const manifest = files.get('submission-manifest.json');
  const summary = files.get('evaluation-summary.json');
  if (!prepared || !record || !metadata || !manifest || !summary || files.has('run-record.json') && files.has('session-record.json')) throw new Error('Missing required evidence file');
  if ([prepared, record, metadata, summary].some((value) => value.runId !== expectedRunId || value.taskId !== expectedTaskId) || record.runKind !== bundle.runKind || prepared.sourceCommit !== record.sourceCommit || metadata.treeSha256 !== bundle.submissionTreeSha256 || manifest.treeSha256 !== bundle.submissionTreeSha256 || manifest.fileCount !== bundle.submissionFiles) throw new Error('Evidence contents disagree');
  return { runId: expectedRunId, taskId: expectedTaskId, runKind: bundle.runKind, referencesChecked: files.size, submissionTreeSha256: bundle.submissionTreeSha256 };
}

if (require.main === module) {
  try {
    if (process.argv.length !== 6) throw new Error('Usage: node Test-RunEvidenceBundle.cjs <absolute-bundle.json> <expected-sha256> <runId> <taskId>');
    console.log(JSON.stringify(verifyBundle(process.argv[2], process.argv[3], process.argv[4], process.argv[5])));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}

module.exports = { verifyBundle };
