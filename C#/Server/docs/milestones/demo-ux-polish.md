# Demo UX Polish

## Goal

Improve visible demo flow without changing the server architecture.

## Candidate Scope

- WebLauncher layout/status polish
- Party/matching UI copy polish
- reward toast styling and item icon display
- nameplate readability polish
- portal retry UX polish
- demo checklist docs
- small reliability affordances for running multiple local clients

## Constraints

- No Protocol changes unless explicitly selected.
- No DB schema change.
- No Redis dependency.
- No server architecture change.
- Keep polish scoped to demo presentation and operator clarity.

## Acceptance Criteria

- Demo operator can see server, room, online player, and queue state quickly.
- Visible Unity client flow is easier to explain.
- Reward and party/match states are clearer during demos.
- Existing `fill-visible` and `fill-dummy` commands remain valid.

## Risks

- Less backend depth than persistence, Redis, API, or combat milestones.
- UI work can expand into prefab/layout cleanup if not bounded.

## Related Docs

- [Monitoring API](../monitoring-api.md)
- [WebLauncher README](../../WebLauncher/README.md)
- [DummyClient Regression and Demo Guide](../dummyclient-regression.md)
