#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const FIELDS = [
  ['input_tokens', 'inputTokens'],
  ['cache_read_input_tokens', 'cacheReadInputTokens'],
  ['cache_creation_input_tokens', 'cacheCreationInputTokens'],
  ['output_tokens', 'outputTokens'],
];

/** Check raw result usage and the stored authoritative totals without changing run files. */
function checkRun(directory, record) {
  if (!record || record.schemaVersion !== 2 || typeof record.runId !== 'string') throw new Error('Invalid run record');
  const issues = [];
  const isSingle = record.architecture === 'single-agent';
  const isHierarchy = ['hierarchical-static', 'hierarchical-adaptive'].includes(record.architecture);
  if (!isSingle && !isHierarchy) throw new Error('Unknown architecture');
  const agents = isSingle ? [{ name: 'single-agent', data: record, stream: path.join(directory, 'stream.jsonl') }]
    : (Array.isArray(record.agents) ? record.agents : []).map((agent) => {
      if (!agent || typeof agent.directory !== 'string' || path.isAbsolute(agent.directory)) throw new Error('Invalid agent directory');
      const agentPath = path.resolve(directory, agent.directory);
      const relative = path.relative(path.resolve(directory), agentPath);
      if (!relative || relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) throw new Error('Agent directory escapes run results');
      return { name: agent.agentId, data: agent, stream: path.join(agentPath, 'stream.jsonl') };
    });
  if (agents.length === 0) throw new Error('No agent records');
  const sums = Object.fromEntries(FIELDS.map(([, stored]) => [stored, 0]));
  for (const agent of agents) {
    let lines;
    try { lines = fs.readFileSync(agent.stream, 'utf8').split(/\r?\n/).filter(Boolean).map((line) => JSON.parse(line)); }
    catch { issues.push(`${agent.name}: stream unreadable or invalid JSON`); continue; }
    const results = lines.filter((line) => line && line.type === 'result');
    if (results.length !== 1) { issues.push(`${agent.name}: expected one result event, found ${results.length}`); continue; }
    const raw = results[0].usage;
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) { issues.push(`${agent.name}: raw usage unavailable`); continue; }
    const missing = FIELDS.filter(([key]) => !Number.isSafeInteger(raw[key]) || raw[key] < 0);
    if (missing.length) { issues.push(`${agent.name}: absent or invalid fields: ${missing.map(([key]) => key).join(', ')}`); continue; }
    const stored = agent.data.authoritativeUsage;
    if (!stored || agent.data.tokenMeasurementComplete !== true) { issues.push(`${agent.name}: authoritative record incomplete`); continue; }
    for (const [rawKey, storedKey] of FIELDS) {
      if (stored[storedKey] !== raw[rawKey]) issues.push(`${agent.name}: ${storedKey} differs from raw usage`);
      sums[storedKey] += raw[rawKey];
      if (!Number.isSafeInteger(sums[storedKey])) issues.push(`${agent.name}: token sum exceeds safe integer range`);
    }
    const input = raw.input_tokens + raw.cache_read_input_tokens + raw.cache_creation_input_tokens;
    const total = input + raw.output_tokens;
    if (!Number.isSafeInteger(total) || stored.totalInputTokens !== input || stored.totalTokens !== total) issues.push(`${agent.name}: authoritative totals inconsistent`);
  }
  const overall = isSingle ? record.authoritativeUsage : record.totalUsage;
  if (record.tokenMeasurementComplete !== true || !overall) issues.push('run: authoritative record incomplete');
  if (overall && agents.length && issues.length === 0) {
    for (const [, key] of FIELDS) if (overall[key] !== sums[key]) issues.push(`run: ${key} differs from agent sum`);
    const totalInput = sums.inputTokens + sums.cacheReadInputTokens + sums.cacheCreationInputTokens;
    const total = totalInput + sums.outputTokens;
    if (!Number.isSafeInteger(total) || overall.totalInputTokens !== totalInput || overall.totalTokens !== total || record.tokensObserved !== total) issues.push('run: totals inconsistent');
  }
  return { runId: record.runId, architecture: record.architecture, agentsChecked: agents.length, usageComplete: issues.length === 0, issues };
}

if (require.main === module) {
  try {
    if (process.argv.length !== 3) throw new Error('Usage: node Check-RunUsage.cjs <run-results-directory>');
    const directory = path.resolve(process.argv[2]);
    const files = ['session-record.json', 'run-record.json'].filter((name) => fs.existsSync(path.join(directory, name)));
    if (files.length !== 1) throw new Error('Expected exactly one run record');
    const result = checkRun(directory, JSON.parse(fs.readFileSync(path.join(directory, files[0]), 'utf8')));
    console.log(JSON.stringify(result));
    if (!result.usageComplete) process.exitCode = 1;
  } catch (error) { console.error(error.message); process.exitCode = 2; }
}

module.exports = { checkRun };
