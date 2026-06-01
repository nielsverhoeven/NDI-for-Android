# Feature 010: Dual Emulator Infrastructure Setup

**Status**: 📋 Specification Complete & Clarified - Ready for Planning Phase

## 📌 Quick Summary

**What**: Establish a reliable, reproducible dual-emulator testing infrastructure  
**Why**: Feature 009 (latency measurement) and future multi-device e2e tests require stable emulator provisioning, inter-device networking, and artifact collection  
**Impact**: Enable 80%+ first-time test pass rate and remove manual emulator setup from developer workflows

## 🎯 Feature Scope

### User Stories (Prioritized)

1. **P1 🎯 Automated Emulator Provisioning** - Start emulators, verify health, install NDI SDK
2. **P1 🎯 Relay Server Infrastructure** - Bridge inter-device communication reliably  
3. **P2 Per-Suite Environment Reset** - Isolate test state between suite runs
4. **P2 Artifact Recovery & CI/CD Integration** - Collect logs, recordings, diagnostics for debugging
5. **P3 Diagnostic Dashboard** - Visualize infrastructure health and trends (post-MVP)

### Success Metrics

- Provisioning completes in <2 min (first boot) or <10 sec (already running)
- Relay maintains <100ms latency between emulators
- 95% of multi-device test runs succeed without manual retry
- Tests run consistently in local dev and CI/CD environments
- Feature 009 latency tests achieve 80%+ first-attempt pass rate

## 📂 Specification Artifacts

```
specs/010-dual-emulator-setup/
├── spec.md                      # Complete feature specification (5 stories, 14 FR, 8 SC)
├── checklists/
│   └── requirements.md          # Quality validation: 12/12 items ✓
├── contracts/                   # (Placeholder for design-phase contracts)
├── validation/                  # (Placeholder for validation evidence)
└── README.md                    # This file
```

## ✅ Specification Quality

**All validation items passing (12/12)**:
- ✓ No implementation details leaked
- ✓ User-focused value statements
- ✓ Testable, unambiguous requirements (14 FR)
- ✓ Measurable success criteria (8 SC) - all technology-agnostic
- ✓ Complete acceptance scenarios (5 stories × 3-4 scenarios each)
- ✓ Edge cases identified (6 major edge cases documented)
- ✓ Clear scope boundaries (infrastructure-only, out-of-scope clearly marked)
- ✓ Assumptions and dependencies documented
## ✅ Clarifications Integrated (Session 2026-03-23)

**3 high-impact questions answered & spec updated**:
1. ✓ **Relay Technology**: TCP Socket Forwarding (direct relay, minimal overhead) → Updated US2, FR-006, FR-007
2. ✓ **API Levels**: API 32-35 (Android 12-15, NDI SDK support + coverage balance) → Updated US1, FR-002, Assumptions
3. ✓ **Artifact Storage**: Host filesystem only (testing/e2e/artifacts/ + CI/CD job uploads) → Updated US4, FR-010, FR-011, FR-012
## 🚀 Next Steps

### Immediate (When Ready)

1. **Option A**: Run `/speckit.clarify` to validate any assumptions
   - Max 3 targeted clarification questions
   - Use if any ambiguity exists about emulator flavor, relay protocol, etc.

2. **Option B**: Run `/speckit.plan` to begin design phase
   - Generates design artifacts (plan.md, data-model.md)
   - Creates task breakdown (tasks.md)
   - Recommended if specification feels complete and actionable

### During Planning Phase

- Define emulator image versions and NDI SDK versions (version matrix)
- Specify relay server technology (simple TCP proxy vs. existing tool)
- Design provisioning script architecture (bash, PowerShell, Python)
- Create data-model for provisioning reports and artifact metadata

### During Implementation Phase

- Implement provisioning script with health checks
- Build relay server (or integrate existing tool)
- Write state reset helpers for Playwright pre-test hooks
- Implement artifact collection in test runner cleanup
- Add CI/CD integration (GitHub Actions, etc.)

## 📊 Dependencies

**Blocks**: Nothing (independent infrastructure feature)  
**Unblocked By**: Feature 009 (latency measurement) - will validate and use this infrastructure  
**Requires**: Android SDK pre-installed in CI/CD runners (assumed, not in scope)  
**Integrates With**: Existing Playwright e2e framework, ADB toolchain, NDI SDK bridge

## 👥 Key Stakeholders

- **Test Infrastructure Team**: Owns provisioning/relay/artifact collection
- **Mobile Developers**: Use local provisioning for multi-device testing
- **CI/CD Maintainers**: Deploy infrastructure to GitHub Actions runners
- **Feature Developers** (e.g., Feature 009 team): Consume provisioning API

## 📝 Key Design Decisions (From Spec)

1. **Scope**: Infrastructure-only (no app UI changes)
2. **Emulator Boot Timeout**: 90 seconds (reasonable default for modern CI runners)
3. **Relay Health Check Interval**: Automatic restart on failure (seamless recovery)
4. **Artifact Storage**: Preserve to persistent storage in CI/CD (vs. ephemeral)
5. **MVP Scope**: Focus on P1 provisioning + P1 relay (defer P3 dashboard)

## 🔗 Related Features

- **Feature 009**: Dual-Emulator NDI Latency Measurement (primary consumer of this infrastructure)
- **Feature 008**: Settings E2E Validation (existing regression gate that will be protected)
- **Future Features**: Any multi-device NDI scenarios will leverage this infrastructure

---

**Specification Created**: 2026-03-23  
**Author**: AI Specification Generator  
**Status**: ✅ Ready for Planning Phase
