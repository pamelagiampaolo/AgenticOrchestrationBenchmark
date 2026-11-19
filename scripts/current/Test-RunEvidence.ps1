<#
.SYNOPSIS
Checks read-only linkage of a run record, evaluator summary and archived submission.
.PARAMETER ResultsDirectory
Trusted directory containing prepared-run.json and one run record.
.PARAMETER EvaluationDirectory
Trusted evaluator directory containing input and evidence subdirectories.
.OUTPUTS
JSON with linked identities and SHA-256 references. Exits nonzero on mismatch.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$EvaluationDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

try {
    Import-Module 'C:\repos\AgenticOrchestrationEvaluator\evaluator\EvaluationInput.psm1' -Force
    $preparedPath = Join-Path $ResultsDirectory 'prepared-run.json'
    $recordPaths = @('session-record.json', 'run-record.json') | ForEach-Object { Join-Path $ResultsDirectory $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    if (@($recordPaths).Count -ne 1) { throw 'Expected exactly one run record' }
    $recordPath = @($recordPaths)[0]
    $metadataPath = Join-Path $EvaluationDirectory 'input\metadata.json'
    $manifestPath = Join-Path $EvaluationDirectory 'input\submission-manifest.json'
    $summaryPath = Join-Path $EvaluationDirectory 'evidence\evaluation-summary.json'
    $prepared = Get-Content -LiteralPath $preparedPath -Raw -Encoding utf8 | ConvertFrom-Json
    $record = Get-Content -LiteralPath $recordPath -Raw -Encoding utf8 | ConvertFrom-Json
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8 | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding utf8 | ConvertFrom-Json
    if ($prepared.schemaVersion -ne 1 -or $record.schemaVersion -ne 2 -or $metadata.schemaVersion -ne 1 -or $manifest.schemaVersion -ne 1 -or $summary.schemaVersion -ne 1) { throw 'Unexpected schema version' }
    if ($record.runId -ne $prepared.runId -or $metadata.runId -ne $record.runId -or $summary.runId -ne $record.runId -or $prepared.taskId -ne $record.taskId -or $metadata.taskId -ne $record.taskId -or $summary.taskId -ne $record.taskId -or $prepared.sourceCommit -ne $record.sourceCommit) { throw 'Run identity mismatch' }
    $source = [IO.Path]::GetFullPath((Join-Path $prepared.agentRoot $prepared.workspaceDirectory))
    if (-not [string]::Equals($source, [IO.Path]::GetFullPath($metadata.submissionSource), [StringComparison]::OrdinalIgnoreCase)) { throw 'Submission source mismatch' }
    if ($metadata.treeSha256 -notmatch '^[a-f0-9]{64}$' -or $metadata.treeSha256 -cne $manifest.treeSha256) { throw 'Submission tree identity mismatch' }
    $archive = Join-Path $EvaluationDirectory 'input\submission'
    $archiveManifest = New-SubmissionManifestObject -Root $archive
    if ($archiveManifest.treeSha256 -cne $manifest.treeSha256 -or $archiveManifest.fileCount -ne $manifest.fileCount) { throw 'Archived submission differs from manifest' }
    $sourceDifferences = @(Compare-SubmissionToManifest -Root $source -Manifest $manifest -Scope 'source')
    if ($sourceDifferences.Count -ne 0) { throw 'Current source differs from submission manifest' }
    $references = @($preparedPath, $recordPath, $metadataPath, $manifestPath, $summaryPath) | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetFullPath($_); sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant() }
    }
    [ordered]@{ schemaVersion = 1; runId = $record.runId; taskId = $record.taskId; runKind = $record.runKind; submissionTreeSha256 = $manifest.treeSha256; submissionFiles = $manifest.fileCount; files = @($references) } | ConvertTo-Json -Depth 5
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
