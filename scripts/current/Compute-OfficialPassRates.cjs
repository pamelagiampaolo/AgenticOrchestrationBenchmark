'use strict';

const COUNT_FIELDS = ['expected', 'executed', 'passed', 'failed', 'skipped', 'missing', 'unexpected'];

/** Compute B5 pass rates from a linked summary and trusted canonical suite sizes. */
function computeOfficialPassRates(rows, expectedForTask) {
  return rows.map((row) => {
    const unavailable = (reason) => ({ ...row, publicPassRate: null, reservedPassRate: null, canonicalPassRate: null, passRateIssues: [reason] });
    if (row.outcome !== 'valid' || row.evaluationLinked !== true || !row.evaluationSummary) return unavailable('No linked evaluation');
    try {
      const expected = expectedForTask(row.slot.taskId);
      if (!expected || !Number.isSafeInteger(expected.public) || expected.public <= 0 || !Number.isSafeInteger(expected.reserved) || expected.reserved <= 0 || !Number.isSafeInteger(expected.public + expected.reserved)) return unavailable('Canonical suite sizes unavailable');
      const summary = row.evaluationSummary;
      if (!summary.phases || !summary.phases.build) return unavailable('Build phase unavailable');
      if (summary.outcome === 'build-failure') {
        if (summary.phases.build.status !== 'failed') return unavailable('Inconsistent build failure');
        return { ...row, publicPassRate: 0, reservedPassRate: 0, canonicalPassRate: 0, passRateIssues: [] };
      }
      if (!['passed', 'test-failures'].includes(summary.outcome) || summary.phases.build.status !== 'passed') return unavailable('Evaluation outcome unavailable or inconsistent');
      const suite = (name) => {
        const counts = summary[`${name}Tests`];
        const phase = summary.phases[name];
        if (!counts || !phase || !['passed', 'failed'].includes(phase.status) || COUNT_FIELDS.some((key) => !Number.isSafeInteger(counts[key]) || counts[key] < 0)) return null;
        if (counts.expected !== expected[name] || counts.missing !== 0 || counts.unexpected !== 0 || counts.executed !== counts.passed + counts.failed || counts.executed + counts.skipped !== counts.expected || (phase.status === 'passed' && counts.failed !== 0)) return null;
        return counts;
      };
      const pub = suite('public');
      const res = suite('reserved');
      if (!pub || !res || (summary.outcome === 'passed' && (summary.phases.public.status !== 'passed' || summary.phases.reserved.status !== 'passed' || pub.skipped || res.skipped)) || (summary.outcome === 'test-failures' && ![summary.phases.public.status, summary.phases.reserved.status].includes('failed'))) return unavailable('Suite counts or phases unavailable or inconsistent');
      return { ...row, publicPassRate: pub.passed / expected.public, reservedPassRate: res.passed / expected.reserved, canonicalPassRate: (pub.passed + res.passed) / (expected.public + expected.reserved), passRateIssues: [] };
    } catch (error) {
      return unavailable(`Canonical sizes unavailable: ${error.message}`);
    }
  });
}

module.exports = { computeOfficialPassRates };
