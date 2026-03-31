# Feature Specification: GitHub Release Action

**Feature Branch**: `026-github-release-action`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "add a release action that packages the app into a shipment-ready package and publish it as a release in the github repository"

## Clarifications

### Session 2026-03-31

- Q: What is the required app package format for the release? → A: APK only — no Play Store publishing workflow exists in this repository; APK is directly installable by end users via sideloading.
- Q: What signing approach should be used for the release build? → A: Debug-signed initially; release keystore signing is deferred to a follow-up. The initial deliverable must be an aligned, installable APK.
- Q: Should the release pipeline require CI checks to pass before publishing? → A: Yes — the release workflow MUST depend on the existing preflight CI job passing; publishing must not proceed if the preflight gate fails.
- Q: What format should the git release tag use? → A: `v{versionName}` — e.g., `v0.12.3` — following standard convention for semantic versioning tags.
- Q: What is the source for auto-generated release notes? → A: GitHub's built-in auto-generated changelog mechanism (based on merged pull requests and commits since the previous release tag).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trigger Automated App Release (Priority: P1)

A maintainer merges approved changes into the main branch and wants a production-ready app package to be built and published as a versioned release in the repository — without any manual file handling or uploading.

**Why this priority**: This is the core value of the feature. Without an automated build and release publication, the entire workflow has no outcome. Every other story depends on a successful release being produced.

**Independent Test**: Can be fully tested by merging a commit to the main branch and verifying that a new release appears in the repository's Releases page with a downloadable app package attached.

**Acceptance Scenarios**:

1. **Given** a commit is merged to the main branch, **When** the release pipeline runs to completion, **Then** a new release entry is created in the GitHub repository with the correct version number.
2. **Given** the release pipeline starts, **When** the app is built in release configuration, **Then** the output package has code shrinking and resource optimization applied before it is attached to the release.
3. **Given** the release pipeline completes successfully, **When** a user navigates to the repository's Releases page, **Then** they can download the shipment-ready app package directly from the release entry.
4. **Given** a build step fails during the release pipeline, **When** the failure is detected, **Then** no release is published and the pipeline reports the failure clearly.

---

### User Story 2 - Manually Trigger a Release on Demand (Priority: P2)

A maintainer sometimes needs to cut a release from a specific state of the codebase on demand — for example, when a hotfix is ready or a milestone is reached — without waiting for the next automatic merge.

**Why this priority**: Automation on merge covers the common path; manual control is necessary for exceptional cases and gives maintainers confidence they are not locked into a push-only workflow.

**Independent Test**: Can be fully tested by manually triggering the release workflow from the repository's Actions tab and confirming a new release appears with the correct package attached.

**Acceptance Scenarios**:

1. **Given** no new commits have been merged, **When** a maintainer manually triggers the release workflow from the repository interface, **Then** a release is built and published using the current version of the codebase.
2. **Given** the manual trigger is initiated, **When** the pipeline completes, **Then** the resulting release is indistinguishable in quality and content from an automatically triggered release.

---

### User Story 3 - Release Includes Descriptive Release Notes (Priority: P3)

A maintainer and downstream consumers want each release to include a description of what changed, so they can understand what is in a given version without reading the commit log manually.

**Why this priority**: Release notes make each published version understandable to stakeholders and users. While the app package is the critical deliverable, notes make it actionable and communicable.

**Independent Test**: Can be fully tested by inspecting the body of a published release entry and confirming it contains a summary of changes since the previous release.

**Acceptance Scenarios**:

1. **Given** a release is published, **When** a user views the release entry, **Then** the release body contains a list of commits or pull requests merged since the previous release.
2. **Given** it is the first release ever published, **When** the release is created, **Then** the release notes describe the initial release state without referencing a non-existent prior release.

---

### Visual Change Quality Gate

No visual change. This feature adds an automated release pipeline and does not modify any in-app screens, layouts, or user-visible behavior within the Android application.

### Test Environment & Preconditions *(mandatory)*

- **Required**: Repository must have a valid signing configuration available as a repository secret, or the pipeline must produce a debug-signed package as an acceptable release artifact where signing is not configured.
- **Required**: The repository must have `GITHUB_TOKEN` permissions scoped to create releases and upload release assets.
- **Preflight check**: Before the release step executes, the pipeline MUST verify that the build completed successfully and produced an output package of non-zero size.
- **Blocked result**: If required secrets or permissions are absent, the pipeline MUST fail at the preflight step (before any release is created) and report which secret or permission is missing as the unblocking step.

### Edge Cases

- What happens when a release with the same version tag already exists? The pipeline MUST not overwrite an existing release; it should fail with a clear message identifying the duplicate tag.
- What happens if the app build produces no output package? No release is created; the pipeline fails at the artifact verification step before the publish step runs.
- What happens if the version number has not changed since the last release? The pipeline MUST either fail with an informative message or produce a release with a unique distinguishing identifier (e.g., commit hash suffix) to avoid ambiguity.
- What happens when the pipeline is triggered on a branch other than main via manual trigger? The pipeline MUST clearly label the resulting release as non-production (e.g., a pre-release flag) so it is not confused with an official release.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST automatically initiate the release pipeline whenever a commit is merged to the main branch.
- **FR-002**: The system MUST support a manual trigger for the release pipeline, invokable by a maintainer with write access to the repository.
- **FR-003**: The system MUST build the app in its release configuration with code shrinking and resource optimization enabled before packaging.
- **FR-004**: The system MUST produce a release APK (not AAB) as the shipment-ready package; the APK must be aligned and installable via sideloading. AAB output is out of scope for this feature.
- **FR-005**: The system MUST verify that the built APK exists and is non-empty before proceeding to publish a release.
- **FR-006**: The system MUST create a versioned release entry in the GitHub repository, tagging it as `v{versionName}` (e.g., `v0.12.3`) using the version name recorded in `version.properties` at build time.
- **FR-007**: The system MUST attach the release APK to the release entry as the sole downloadable asset.
- **FR-008**: The system MUST not publish a release if any required build, test, or preflight step has failed.
- **FR-009**: The system MUST require that the existing CI preflight job passes before the release publish step runs; the release workflow MUST declare a dependency on the preflight outcome.
- **FR-010**: The system MUST generate release notes using GitHub's built-in auto-generated changelog, sourced from pull requests and commits merged since the previous release tag.
- **FR-011**: The system MUST prevent creation of a release that duplicates an existing release tag, failing explicitly with an informative error.
- **FR-012**: The system MUST report each pipeline failure with enough detail to distinguish between a build code failure and an environment or permission blocker.
- **FR-013**: Releases triggered manually from a non-main branch MUST be marked as pre-releases and not as production releases.

### Key Entities

- **Release**: A versioned, immutable snapshot of the app published to the repository's Releases page. Attributes: version name, version code, associated commit SHA, release notes, attached package asset, timestamp, and pre-release flag.
- **Release APK**: The shipment-ready Android Package (APK) attached to a release. Must be built in release configuration with all optimizations applied, aligned, and debug-signed (release keystore signing is deferred). Must pass size and integrity verification before attachment. AAB format is out of scope.
- **Release Trigger**: The event that initiates the release pipeline. Can be an automatic merge event on the main branch or a manual invocation by an authorized maintainer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A release is published within 15 minutes of the triggering event (merge or manual trigger) on 95% of successful runs.
- **SC-002**: Every release entry contains exactly one downloadable release APK asset and a non-empty auto-generated release notes body.
- **SC-003**: Zero duplicate release tags are created across 100 consecutive release executions.
- **SC-004**: The pipeline correctly reports a failure (without publishing) in 100% of cases where a build or preflight step exits with an error.
- **SC-005**: Maintainers report that the manual release trigger works reliably on first attempt without additional configuration steps.

## Assumptions

- The existing `version.properties` versioning system (currently producing version `0.12.3`, code `266`) is the authoritative source of the release version tag.
- The release package format is APK (not AAB). Distribution is via direct sideloading; Play Store publishing is out of scope.
- The initial deliverable uses debug signing. Release keystore signing (with a stored repository secret) is a follow-up and does not block this feature.
- The release workflow depends on the existing CI `preflight` job; the release will not publish if preflight fails.
- The release git tag takes the form `v{versionName}` (e.g., `v0.12.3`) sourced from `version.properties`.
- Release notes are auto-generated by GitHub from merged pull requests and commits since the previous release tag. No manually maintained changelog file is required.
- The CI environment already has the necessary tools and permissions to build the app, as evidenced by the existing `android-ci.yml` workflow.
