'use strict';

const LIMIT_STATUSES = new Set(['time-limit-exceeded', 'token-limit-exceeded', 'resource-limits-exceeded']);
const MEASURABLE_STATUSES = new Set(['completed', 'cli-failed']);

/** Derive B5 completion from validated evaluation, pass rates, official budgets and measured usage. */
function computeOfficialCompletion(rows) {
  return rows.map((row) => {
    const result = (functional, completion, issue) => ({ ...row, functionalSubmissionCorrect: functional, correctCompletion: completion, completionIssues: issue ? [issue] : [] });
    const summary = row.evaluationSummary;
    if (row.outcome !== 'valid' || !row.record || row.evaluationLinked !== true || !summary || !Array.isArray(row.passRateIssues) || row.passRateIssues.length !== 0 || typeof row.canonicalPassRate !== 'number') return result(null, null, 'Reliable evaluation and pass rates required');
    const functional = summary.outcome === 'passed' && row.canonicalPassRate === 1;
    if (!['passed', 'test-failures', 'build-failure'].includes(summary.outcome)) return result(null, null, 'Evaluation outcome unavailable');
    const record = row.record;
    if (record.timeLimitMs !== 240000 || record.tokenBudget !== 800000) return result(functional, null, 'Official budgets not confirmed');
    if (!Array.isArray(record.protectedIntegrityDifferences) || record.protectedIntegrityDifferences.length !== 0) return result(functional, null, 'Protected material integrity not confirmed');
    if (LIMIT_STATUSES.has(record.status)) return result(functional, false, null);
    if (!MEASURABLE_STATUSES.has(record.status)) return result(functional, null, 'Execution status requires review');
    if (!Number.isSafeInteger(record.agentTaskDurationMs) || record.agentTaskDurationMs < 0 || row.usageComplete !== true || !Number.isSafeInteger(row.totalTokens) || row.totalTokens < 0 || row.totalTokens !== record.tokensObserved) return result(functional, null, 'Duration or usage unavailable');
    if (record.agentTaskDurationMs > record.timeLimitMs || row.totalTokens > record.tokenBudget) return result(functional, null, 'Recorded limits and status are inconsistent');
    return result(functional, functional, null);
  });
}

module.exports = { computeOfficialCompletion };
