# Combat Authority v2

## Goal

Continue moving combat toward server-authoritative behavior while keeping the scope focused on current basic attacks first.

## Current Foundation

Implemented now:

- `C_Skill` records active runtime `SkillCastState`
- accepted basic attack SkillIds are `2`, `3`, and `4`
- `C_Collision` is treated as a hit candidate
- active cast validation
- duplicate-hit prevention per cast
- target alive/existence checks
- range validation
- facing/front validation
- line/depth validation
- player/enemy combat anchor validation
- enemy death/reward guard

## First Scope

Candidate v2 work:

- server cooldown validation for SkillIds `2`, `3`, `4`
- server action/cast lock validation
- structured rejected-skill logs
- structured rejected-hit metrics
- skill-specific damage/range/active-window tuning
- reward hardening tied only to validated damage/death

## Implementation Notes

Recommended constraints:

- start with basic attacks only
- keep skill transport on TCP
- avoid UDP skill work
- avoid Protocol changes unless explicitly needed
- do not add DashAttack or JumpAttack ids until animation/controller policy is decided
- keep DummyClient collision simulation as a regression aid, not as the source of truth

## Acceptance Criteria

- Valid basic attack flow still damages enemies.
- Duplicate collision for the same cast is rejected.
- Collision without active cast is rejected.
- Skill cooldown violation is rejected server-side.
- Movement/attack lock behavior is logged clearly.
- Existing reward/drop flow still works after validated kills.

## Risks

- Server validation windows can conflict with Unity animation timing.
- Scope can expand into full combat rewrite if not constrained.
- Client-side hit candidate logs and server validation logs must remain comparable.

## Related Docs

- [Development Notes](../dev-notes.md)
- [DummyClient Regression and Demo Guide](../dummyclient-regression.md)
## v2 Implementation Decision

- Basic attack SkillIds `2`, `3`, and `4` use server-side `CooldownMs=1200`, `ActiveWindowMs=1200`, and `ActionLockMs=50`.
- `C_Skill` is rejected before recording a new cast when the same skill cooldown is still active.
- `C_Skill` is rejected before recording a new cast when any previous cast remains inside its action-lock window; hit validation still uses the longer active window.
- Movement packets are not blocked by the action lock in this milestone; movement/attack interaction is limited to skill rejection logs and counters.
- Rejected skill and hit reasons are counted in memory through `CombatRejectMetrics` for later Monitoring API reuse.

## Completion Status

Status: Completed on 2026-07-06.

Implemented and verified:

- Server cooldown validation for SkillIds `2`, `3`, and `4` with `CooldownMs=1200`.
- Server action/cast lock validation with a shorter 50ms action-lock window, separated from the 1200ms hit validation window to preserve Unity basic-attack combo sync.
- Structured rejection logs for `CooldownActive` and `ActionLocked`.
- `CombatRejectMetrics` in-memory counters for existing hit reject reasons and new skill cast reject reasons.
- Regression checks confirmed normal attack intervals still damage enemies and fast attack intervals produce server-side rejects.
- Protocol files were not changed.

Monitoring reuse:

- Combat reject counters are now exposed through `GET /api/events/recent` as `rejectReasonCounts`.
- Recent skill rejects appear as `SkillCastRejected` events.



