#!/usr/bin/env node
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');

const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'mcp-confine-probe-'));
const resolvedTemp = path.resolve(tempRoot);
if (!resolvedTemp.toLowerCase().startsWith(path.resolve(os.tmpdir()).toLowerCase() + path.sep)) throw new Error('Unsafe temporary path');
const agent = path.join(tempRoot, 'agent-root');
const workspace = path.join(agent, 'workspace');
const tests = path.join(agent, 'agent-tests');
const worker = path.join(tempRoot, 'worker');
const outside = path.join(tempRoot, 'outside-sentinel.txt');
const audit = path.join(tempRoot, 'audit.jsonl');

try {
  for (const directory of [workspace, tests, worker]) fs.mkdirSync(directory, { recursive: true });
  const source = path.join(__dirname, '..', '..', 'benchmark', 'BenchmarkTarget');
  for (const name of ['package.json', 'tsconfig.json']) fs.copyFileSync(path.join(source, name), path.join(workspace, name));
  fs.cpSync(path.join(source, 'src'), path.join(workspace, 'src'), { recursive: true });
  const marker = `SENTINEL_${crypto.randomBytes(16).toString('hex')}`;
  fs.writeFileSync(outside, marker, { flag: 'wx' });
  const code = `import { it, expect } from 'vitest'; import { readFileSync } from 'node:fs'; it('reads synthetic external sentinel', () => { expect(readFileSync(${JSON.stringify(outside)}, 'utf8')).toBe(${JSON.stringify(marker)}); });`;
  const request = (id, method, params) => ({ jsonrpc: '2.0', id, method, params });
  const requests = [
    request(1, 'initialize', {}),
    request(2, 'tools/call', { name: 'read_file', arguments: { path: outside } }),
    request(3, 'tools/call', { name: 'write_file', arguments: { path: 'agent-tests/tests/agent/sentinel.test.ts', content: code } }),
    request(4, 'tools/call', { name: 'run_agent_tests', arguments: {} }),
  ];
  const toolchain = 'C:/AgenticAIToolchain/node-v24.17.0-lock-586b704e';
  const env = {
    ...process.env,
    MCP_WRITABLE_DIRECTORIES: 'workspace;agent-tests',
    MCP_AGENT_TEST_NODE: path.join(toolchain, 'node', 'node.exe'),
    MCP_AGENT_TEST_VITEST: path.join(toolchain, 'packages', 'node_modules', 'vitest', 'vitest.mjs'),
    MCP_AGENT_TEST_WORKER_ROOT: worker,
    MCP_AGENT_TEST_TIMEOUT_SECONDS: '30',
    MCP_COMMAND_AUDIT_PATH: audit,
  };
  const server = 'C:/AgenticAIHarness/aee8d1162bb496895990a00095cc14c2651ff752/AgentWorkspaceMcpServer/AgentWorkspaceMcpServer.exe';
  const result = spawnSync(server, [agent], { input: requests.map((item) => JSON.stringify(item)).join('\n') + '\n', encoding: 'utf8', env, timeout: 45000, maxBuffer: 1048576 });
  if (result.error || result.status !== 0) throw new Error(`Server failed (exit=${result.status}): ${result.error || result.stderr}`);
  const responses = result.stdout.trim().split(/\r?\n/).map(JSON.parse);
  const find = (id) => responses.find((response) => response.id === id);
  const direct = find(2);
  const write = find(3);
  const run = find(4);
  const outcome = run?.result?.structuredContent;
  if (!direct || !write || !outcome) throw new Error('Missing expected MCP responses');
  const externalReadObserved = outcome.Status === 'completed' && outcome.ExitCode === 0 && outcome.Tests?.Passed === 1;
  console.log(JSON.stringify({
    serverExit: result.status,
    directReadRejected: Boolean(direct.error || direct.result?.isError),
    agentTestWriteSucceeded: Boolean(write.result && !write.result.isError),
    agentTestStatus: outcome.Status,
    agentTestExitCode: outcome.ExitCode,
    agentTestsPassed: outcome.Tests?.Passed,
    externalReadObserved,
    isolationAcceptance: externalReadObserved ? 'failed' : 'inconclusive',
    sentinelUnchanged: fs.readFileSync(outside, 'utf8') === marker,
  }));
} finally {
  // Only this newly created temporary probe directory is removed.
  fs.rmSync(resolvedTemp, { recursive: true, force: true });
}
