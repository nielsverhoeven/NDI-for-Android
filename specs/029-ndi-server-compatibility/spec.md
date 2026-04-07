# Feature Specification: NDI Discovery Server Compatibility

**Feature Branch**: `[029-ndi-server-compatibility]`  
**Created**: 2026-04-07  
**Status**: Draft  
**Input**: User description: "when I'm at home, running the latest version of an NDI discovery service I do get results and can start viewing streams. However, when I am in the venue I want to use it in where we are running a somewhat older version of the NDI discovery server no sources are showing up. We need to investigate the compatibility with various NDI Discover Server versions."

## Clarifications

### Session 2026-04-07

- Q: Which discovery server versions are in scope for compatibility validation and support? → A: The latest known-good version, the failing venue version, and every older version the team can obtain for testing.
- Q: What status set should the final compatibility matrix use? → A: Compatible, limited, incompatible, and blocked; unknown is only a temporary diagnostic until enough evidence exists.
- Q: Where should compatibility results be surfaced in the delivered feature? → A: Use existing in-app diagnostics surfaces plus recorded validation artifacts; no new dedicated compatibility UI.
- Q: How should the spec classify a server version as limited versus incompatible? → A: Limited means discovery-only support; incompatible means stream start fails even once in an otherwise ready environment.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Reliable Venue Discovery (Priority: P1)

As an operator using the app at the venue, I want sources from supported older
NDI Discovery Server versions to appear and remain usable so I can discover and
start viewing streams in the same way I can at home on the latest server.

**Why this priority**: If supported older server versions cannot surface
sources, the primary venue use case fails even though the app works in the home
environment.

**Independent Test**: Configure the app against a supported older discovery
server version with at least one available source, run discovery, and confirm
that at least one source can be selected and opened for viewing without a
manual workaround.

**Acceptance Scenarios**:

1. **Given** the app is pointed at a supported older NDI Discovery Server
   version with available sources, **When** the user refreshes discovery,
   **Then** the source list shows the available sources and the user can open a
   stream.
2. **Given** the app is configured with multiple discovery servers that are not
   all on the same version, **When** discovery completes, **Then** sources from
   compatible servers remain usable and any incompatible server does not cause
   the overall result to be silently reported as fully successful.

---

### User Story 2 - Version Validation Matrix (Priority: P2)

As a maintainer, I want each targeted NDI Discovery Server version to have a
recorded compatibility result so the team knows which versions are supported,
limited, incompatible, or blocked.

**Why this priority**: Compatibility work is not complete unless the team can
name the versions that are known to work and separate them from versions that
need remediation or are out of scope. In scope means the latest known-good
baseline, the failing venue version, and every older server version the team
can obtain for direct testing.

**Independent Test**: Run the defined compatibility validation flow against the
target server versions and verify that each version receives a recorded result
with evidence and impact notes.

**Acceptance Scenarios**:

1. **Given** a defined set of target discovery server versions, **When** a
   compatibility validation cycle completes, **Then** each version is recorded
  with a status of compatible, limited, incompatible, or blocked and includes
  supporting evidence.
2. **Given** one tested version behaves differently from the latest known-good
   baseline, **When** the result is recorded, **Then** the recorded outcome
  describes whether discovery-only support is available or whether stream
  start failure makes the version incompatible.

---

### User Story 3 - Actionable Compatibility Diagnostics (Priority: P3)

As an operator or tester, I want an explicit compatibility diagnosis when a
server version is unsupported, unknown, or only partially compatible so I can
separate version issues from ordinary network or configuration problems.

**Why this priority**: Once compatibility varies by server version, silent empty
results are operationally expensive and push users toward guesswork.

**Independent Test**: Connect the app to an incompatible, unknown, or blocked
server-version scenario and confirm the result identifies the category of the
failure plus the next recommended action.

**Acceptance Scenarios**:

1. **Given** the app is connected to a discovery server version that is outside
   the supported compatibility range, **When** discovery is attempted,
   **Then** the result states that the version is unsupported or limited rather
   than presenting the issue as a generic empty discovery success.
2. **Given** a validation run cannot complete because the environment is not
   ready, **When** the result is recorded, **Then** it is marked blocked and
   includes the specific unblock step needed to retry.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- No new visual flow is required. This feature can succeed through discovery
  behavior changes, validation artifacts, and reuse of existing diagnostics
  surfaces, with no dedicated compatibility screen required. If implementation
  changes any existing visible diagnostics surface, emulator-run Playwright
  coverage for that affected flow and existing Playwright regression execution
  become mandatory.

### Test Environment & Preconditions *(mandatory)*

- Required runtime dependencies:
  - an Android device or emulator capable of running the current debug build;
  - access to the latest known-good NDI Discovery Server environment used as the
    baseline;
  - access to the venue server version or an equivalent older server build that
    reproduces the venue behavior;
  - access to every additional older server version the team can obtain for
    direct compatibility validation;
  - at least one available NDI source in each compatibility test environment.
- Required preflight checks:
  - `pwsh ./scripts/verify-android-prereqs.ps1`
  - `./gradlew.bat --version`
  - confirm the Android target is visible in `adb devices`
  - confirm each target discovery server endpoint is reachable enough to begin
    compatibility testing.
- If a server version or venue environment cannot be reached, the validation
  result MUST be recorded as blocked with the missing dependency, the failed
  preflight evidence, and the exact retry condition.
- Existing automated tests are regression protection and must remain unchanged
  unless this feature directly changes the covered behavior; any such test
  update must include explicit requirement/contract traceability.

### Edge Cases

- The older discovery server is reachable but returns no sources because of a
  version mismatch rather than a network outage.
- Multiple configured discovery servers return mixed results, with one version
  compatible and another incompatible or unreachable.
- The server version cannot be read directly, so the system must classify the
  result as unknown compatibility instead of assuming support.
- Discovery succeeds but starting a stream fails on one server version, meaning
  discovery compatibility and stream-start compatibility must be reported
  separately.
- The venue issue is caused by network segmentation or firewall policy rather
  than server-version incompatibility and must not be misclassified.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a compatibility matrix that includes the
  latest known-good NDI Discovery Server version, the venue server version that
  currently fails, and every additional older server version the team can
  obtain for direct validation.
- **FR-002**: For every server version marked compatible in the matrix, the
  system MUST allow users to discover available sources and start viewing at
  least one stream without a manual workaround.
- **FR-003**: The system MUST distinguish discovery-server compatibility issues
  from endpoint unreachability, missing sources, and general network failures.
- **FR-004**: The system MUST record a per-version compatibility result with one
  of these outcomes: compatible, limited, incompatible, or blocked.
- **FR-004a**: A version MUST be classified as limited when source discovery is
  usable but the validated support level is restricted to discovery-only.
- **FR-004b**: A version MUST be classified as incompatible when stream start
  fails even once in an otherwise ready environment for that version.
- **FR-005**: Every recorded compatibility result MUST include evidence notes
  describing what was tested and which user-visible capability succeeded or
  failed.
- **FR-006**: When discovery involves multiple configured servers with mixed
  compatibility outcomes, the system MUST preserve usable sources from
  compatible servers and MUST report the non-compatible servers as partial or
  failed results rather than implying full success.
- **FR-007**: When a target server version is unsupported, unknown, or only
  partially compatible, the system MUST provide actionable diagnostic guidance
  that identifies the compatibility category and the next recommended step
  through existing in-app diagnostics surfaces and recorded validation
  artifacts.
- **FR-008**: If the target server version cannot be determined directly, the
  system MUST classify the result as unknown compatibility only until
  sufficient evidence exists to mark it compatible, limited, incompatible, or
  blocked in the final matrix.
- **FR-009**: Compatibility validation MUST preserve the current successful
  behavior against the latest known-good server baseline.
- **FR-010**: For environment-dependent validations, the system MUST run and
  record preflight checks before executing compatibility, end-to-end, or release
  gates.
- **FR-011**: Validation reporting MUST classify each failed or blocked gate as
  either a code failure, a compatibility failure, or an environment blocker with
  reproduction details.
- **FR-012**: Existing automated tests MUST be preserved as regression
  protection; they MAY be changed only when the requested feature directly
  changes the covered behavior or the test is separately proven invalid.
- **FR-013**: Any change to a pre-existing automated test MUST document the
  specific feature requirement or contract change that made the test update
  necessary.

### Key Entities *(include if feature involves data)*

- **Discovery Server Version Target**: A specific discovery server version or
  environment to be validated, including its role as baseline, venue target, or
  additional support candidate.
- **Compatibility Result**: The recorded outcome for a tested server version,
  including its final compatibility status of compatible, limited,
  incompatible, or blocked, plus tested capabilities and evidence notes.
- **Compatibility Diagnostic**: The user- or maintainer-facing explanation that
  distinguishes compatibility problems from other discovery failures and states
  the recommended next action.

## Assumptions

- The venue environment can be reproduced either on-site or through an
  equivalent older discovery server build available to the team.
- Additional older discovery server versions beyond the venue version will be
  included in the feature only if the team can obtain runnable test instances
  for them.
- The feature scope covers source discovery and initial stream-start behavior,
  which are the capabilities currently failing in the venue scenario.
- Existing discovery settings, overlay diagnostics, and validation artifacts are
  the intended surfaces for communicating compatibility outcomes, and no new
  dedicated compatibility screen is in scope.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every server version classified as compatible, operators can
  complete the discover-and-open-one-stream workflow successfully during the
  validation cycle.
- **SC-002**: The compatibility matrix contains an explicit recorded result for
  100% of in-scope server versions before implementation is considered complete.
- **SC-003**: In unsupported, unknown, or blocked scenarios, the team can
  determine the failure category and next step from the recorded result without
  requiring a second exploratory debugging session.
- **SC-004**: The latest known-good server baseline continues to achieve the
  same successful discovery and stream-start outcome throughout validation.
