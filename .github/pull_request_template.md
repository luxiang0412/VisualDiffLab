## Summary

<!-- What changed and why? Link the task/report. -->

## Scope

- [ ] No unapproved scope creep; deferred items recorded in `docs/decisions/DEFERRED.md`
- [ ] Dependency changes recorded (purpose, license, maintenance, version, alternatives)

## Verification

- [ ] `dotnet build VisualDiffLab.sln -c Release -p:Platform=x64`
- [ ] App builds: `dotnet build src/VisualDiffLab.App -c Release -p:Platform=x64 -r win-x64`
- [ ] Unit/integration tests pass
- [ ] UI automation passes (or NOT RUN reason below)
- [ ] 4K performance re-measured if the pipeline/renderer changed
- [ ] Screenshots updated (light/dark where relevant)

## Evidence

<!-- Test output summary, screenshot paths, report updates. -->

## NOT RUN / known limitations

<!-- Be explicit; do not hide skipped checks. -->
