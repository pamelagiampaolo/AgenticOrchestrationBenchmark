'use strict';

const COMPLETED_OUTCOMES = new Map([['passed', 0], ['test-failures', 10], ['build-failure', 11]]);
const INTEGRITY_FIELDS = ['originalInputUnchanged', 'archivedSubmissionUnchanged', 'suiteManifestsVerified'];

/** Attach a matching completed evaluation summary; the caller must supply trusted, verified summaries. */
function attachOfficialEvaluation(selections, readSummary) {
  return selections.map((selection) => {
    const unavailable = (issue) => ({ ...selection, evaluationLinked: false, evaluationSummary: null, evaluationIssues: [issue] });
    if (selection.outcome !== 'valid' || !selection.record) return unavailable('No valid run record');
    try {
      const summary = readSummary(selection.record.runId);
      if (!summary || summary.schemaVersion !== 1 || summary.runId !== selection.record.runId || summary.taskId !== selection.record.taskId) {
        return unavailable('Missing or inconsistent evaluation identity');
      }
      if (!COMPLETED_OUTCOMES.has(summary.outcome) || summary.exitCode !== COMPLETED_OUTCOMES.get(summary.outcome)) {
        return unavailable('Evaluation not completed or outcome inconsistent; review required');
      }
      if (!summary.integrity || INTEGRITY_FIELDS.some((key) => summary.integrity[key] !== true)) {
        return unavailable('Evaluation integrity not confirmed; review required');
      }
      return { ...selection, evaluationLinked: true, evaluationSummary: summary, evaluationIssues: [] };
    } catch (error) {
      return unavailable(`Evaluation read failed: ${error.message}`);
    }
  });
}

module.exports = { attachOfficialEvaluation };
