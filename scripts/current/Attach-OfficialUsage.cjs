'use strict';

const { checkRun } = require('./Check-RunUsage.cjs');

/** Attach verified token usage to P54 selections; resolveDirectory must return the trusted run directory. */
function attachOfficialUsage(selections, resolveDirectory) {
  return selections.map((selection) => {
    if (selection.outcome !== 'valid' || !selection.record) {
      return { ...selection, usageComplete: false, totalTokens: null, usageIssues: ['No valid run record'] };
    }
    try {
      const check = checkRun(resolveDirectory(selection.record.runId), selection.record);
      if (!check.usageComplete) return { ...selection, usageComplete: false, totalTokens: null, usageIssues: check.issues };
      return { ...selection, usageComplete: true, totalTokens: selection.record.tokensObserved, usageIssues: [] };
    } catch (error) {
      return { ...selection, usageComplete: false, totalTokens: null, usageIssues: [`Usage check failed: ${error.message}`] };
    }
  });
}

module.exports = { attachOfficialUsage };
