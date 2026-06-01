# Feature Specification: Fix Prereq Preflight

**Feature Branch**: `012-fix-prereq-preflight`  
**Created**: March 23, 2026  
**Status**: Draft  
**Input**: User description: "the build pipeline running on a pull request to main is failing because of missing prerrequisites. Make sure that the preflight action is optimized to make sure all prerequisites are installed before running the validation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Successful PR Build (Priority: P1)

As a developer, I want the CI pipeline to pass preflight checks on pull requests to main so that builds don't fail due to missing prerequisites.

**Why this priority**: This is the core issue causing build failures, highest impact.

**Independent Test**: Can be tested by creating a PR with code changes and verifying the preflight job passes without manual intervention.

**Acceptance Scenarios**:

1. **Given** a pull request is opened to main branch, **When** the CI preflight job runs, **Then** all Android prerequisites are verified and the job completes successfully
2. **Given** missing prerequisites on the CI runner, **When** preflight runs, **Then** the system automatically installs the missing components and continues validation

---

### User Story 2 - Automatic Prerequisite Installation (Priority: P2)

As a CI maintainer, I want the preflight action to automatically install missing prerequisites so that manual setup is not required.

**Why this priority**: Reduces maintenance overhead and ensures consistency.

**Independent Test**: Can be tested by running the verification script on a clean environment and observing automatic installations.

**Acceptance Scenarios**:

1. **Given** JDK is not installed, **When** preflight runs, **Then** JDK 21 is automatically downloaded and installed
2. **Given** Android SDK packages are missing, **When** preflight runs, **Then** required packages are installed via sdkmanager

---

### User Story 3 - Optimized Installation Time (Priority: P3)

As a developer, I want prerequisite installation to be fast so that CI feedback is timely.

**Why this priority**: Improves developer experience with quicker build times.

**Independent Test**: Can be measured by timing the preflight job duration.

**Acceptance Scenarios**:

1. **Given** all prerequisites are missing, **When** preflight installs them, **Then** total installation time is under 5 minutes

### Visual Change Quality Gate *(mandatory when UI changes are present)*

No visual change - this feature only affects CI infrastructure and build scripts.

### Edge Cases

- What happens when network connectivity fails during package downloads?
- How does system handle insufficient disk space for installations?
- What if the CI runner lacks permissions to install software?
- How to handle version conflicts with pre-installed tools?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST automatically detect missing Android prerequisites (JDK, Android SDK packages, CLI tools)
- **FR-002**: System MUST install missing prerequisites using appropriate package managers (sdkmanager, etc.)
- **FR-003**: System MUST verify all prerequisites are properly installed before proceeding with validation
- **FR-004**: System MUST handle installation failures gracefully with clear error messages
- **FR-005**: System MUST optimize installation order to minimize time (e.g., parallel downloads where possible)

### Non-Functional Requirements

- **NFR-001**: Installation process MUST complete within 5 minutes on standard CI runners
- **NFR-002**: System MUST not modify existing installations if they meet version requirements
- **NFR-003**: Error messages MUST be clear and actionable for troubleshooting

## Success Criteria

- 100% of pull request builds to main pass the preflight stage without manual intervention
- Average preflight job duration remains under 3 minutes
- Zero false positives in prerequisite detection
- System successfully installs all required components on clean CI runners

## Key Entities

- CI Runner Environment (Windows-latest on GitHub Actions)
- Android Prerequisites Verification Script (verify-android-prereqs.ps1)
- Android SDK Manager (sdkmanager)
- JDK Installation (Temurin 21)
- Gradle Wrapper

## Assumptions

- CI runners have internet access for downloading packages
- CI runners have sufficient permissions to install software
- GitHub Actions provides basic Windows environment with PowerShell
- Required versions (JDK 21, Android API 34, etc.) remain stable

## Dependencies

- GitHub Actions Windows runners
- Android SDK repository availability
- JDK distribution availability
- No conflicts with pre-installed tools on CI runners</content>
<parameter name="filePath">c:\github\NDI-for-Android\specs\012-fix-prereq-preflight\spec.md