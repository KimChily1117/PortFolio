# Redis MatchQueueStore

## Goal

Add Redis-backed queue storage behind the existing `IMatchQueueStore` abstraction.

## Current Foundation

Current matching already separates queue data through:

- `MatchTicket`
- `IMatchQueueStore`
- `InMemoryMatchQueueStore`
- `MatchQueueKey`

The external matching flow should stay the same.

## First Scope

Store only pure ticket data:

- ticket id
- queue key
- player id/name
- session id for local process resolution
- enqueue time
- expiry time
- state

Keep GameServer responsible for:

- resolving live sessions
- sending packets
- creating parties
- starting RoomTransfer
- entering target rooms

## Candidate Redis Structures

```text
Sorted Set: match:queue:{queueKey}
Hash: match:ticket:{ticketId}
Set or Hash: match:party:{partyId}:members
TTL: ticket expiry and stale cleanup
```

## Acceptance Criteria

- In-memory store remains available.
- Redis store can be selected/configured explicitly.
- `fill-visible` still works.
- `fill-dummy --clients 8 --partySize 4` still creates two rooms.
- Redis does not store live `ClientSession`, `Player`, `GameRoom`, UDP movement, or combat state.
- Queue snapshot API can still show waiting tickets.

## Risks

- Atomic dequeue matters if multiple workers are introduced.
- Stale tickets and disconnect cleanup must be handled carefully.
- Redis should not own RoomTransfer execution.

## Related Docs

- [Matchmaking Flow](../matchmaking-flow.md)
- [Monitoring API](../monitoring-api.md)
