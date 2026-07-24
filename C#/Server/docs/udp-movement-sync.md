# UDP Movement Sync

## Purpose

This document summarizes the UDP movement sync work for Project Dawn.

The goal is to move frequent movement updates from TCP to UDP while keeping the existing login, room, and broadcast architecture stable. UDP movement is converted back into the existing server-side movement flow, so the project can keep using `GameRoom.HandleMove` and `S_Move` broadcast without introducing UDP broadcast, reliable UDP, or a new gameplay replication layer yet.

## Existing TCP Movement Flow

Before UDP movement, Unity sent movement through TCP:

1. `MyPlayer` updates local `PositionInfo`.
2. `MyPlayer.CheckUpdatedFlag()` creates `C_Move`.
3. `GameManager.Network.Send(movePacket)` sends TCP `C_Move`.
4. The server receives `C_Move`.
5. The server calls `GameRoom.HandleMove`.
6. The server broadcasts `S_Move`.
7. Other Unity clients receive `S_Move`.
8. `PacketHandler.S_MoveHandler()` applies the received `PositionInfo` to `OtherPlayer`.

This TCP path remains available as an explicit compatibility/test mode. It is not an automatic failover path: when UDP mode is selected and UDP registration is not complete, Unity skips the movement datagram and does not silently resend the same movement through TCP.

## UDP Listener

A UDP listener was added on the server side.

The listener receives raw UDP datagrams and forwards them to UDP packet handling logic. Proto-based UDP packets are the only supported UDP gameplay path now. The old string-based fallback path has been removed.

## UDP Token And Login

The server issues a UDP token after successful TCP login.

The token is delivered to Unity through:

```text
S_Login.UdpToken
```

Unity stores this token and uses it to register the UDP endpoint. This keeps UDP endpoint registration tied to an authenticated TCP session instead of trusting arbitrary UDP senders.

## UDP Hello Registration

The first registration flow used a string payload:

```text
UDP_HELLO:{token}
```

Server behavior:

1. Receive `UDP_HELLO:{token}`.
2. Look up the `ClientSession` associated with that token.
3. Bind the sender UDP `EndPoint` to the session.
4. Store/update `ClientSession.UdpEndPoint`.
5. Update the last UDP-seen timestamp.

After registration, future UDP movement packets from that endpoint can be mapped back to the correct TCP session and player.

The current Proto registration flow uses:

```text
C_UdpHello
 -> token validation
 -> ClientSession.UdpEndPoint mapping
 -> S_UdpHello
```

`S_UdpHello` is now sent back to the Unity client as a Proto UDP datagram.

Success:

```text
S_UdpHello.Ok = true
S_UdpHello.Message = "UDP registration success."
```

Invalid token or rejected endpoint:

```text
S_UdpHello.Ok = false
S_UdpHello.Message = "UDP registration rejected."
```

Unity parses `S_UdpHello` and logs the registration result, so UDP endpoint registration success/failure is now visible on the client.

## Current Runtime Transport - 2026-07-20

The Unity field is initialized conservatively with `UseUdpMovement=false`, but `LoginScene.Initialize()` calls `SetUseUdpMovementForTest(true)` before normal gameplay. The effective runtime path in the current client build is therefore:

```text
TCP S_Login(UdpToken)
 -> UDP C_UdpHello
 -> UDP S_UdpHello(Ok=true)
 -> UDP C_UdpMove(PositionInfo, Sequence)
 -> server validation
 -> GameRoom.HandleMove
 -> TCP S_Move to relevant observers
```

Only the frequent client-to-server movement update is on UDP. Login, room transfer, combat events, and the server-to-client `S_Move` replication path remain on TCP.

Important behavior:

- UDP movement starts only after `S_UdpHello.Ok=true`.
- `UdpGameClient.TrySendMove()` returns `false` before registration and does not advance the send-interval state.
- There is no per-packet or registration-failure automatic TCP fallback.
- Setting `UseUdpMovement=false` explicitly selects the legacy TCP `C_Move` compatibility/test path.

## String-Based UDP_MOVE Test

The first movement test used a string payload:

```text
UDP_MOVE:x:y:z:state:moveDir
```

Early issue:

- `UdpHelloTestClient` was sending its own GameObject transform.
- In small test windows this produced values such as `(960, 540)`, which looked like screen/UI coordinates rather than player world coordinates.

Fix:

- UDP movement now sends an explicit target player transform or the actual player movement position.
- The helper object's transform is no longer used as the movement source.
- `UdpHelloTestClient` is now treated as a legacy manual test component; scene transitions must not let it own or close the shared UDP client.

## Reusing Existing Move Flow

UDP movement does not directly broadcast UDP packets.

The server converts UDP movement data into the existing movement path:

```text
C_UdpMove(PositionInfo, Sequence)
 -> validated C_Move adapter
 -> GameRoom.HandleMove
 -> S_Move Broadcast
 -> Unity S_MoveHandler
 -> OtherPlayer.PositionInfo
```

This keeps existing room logic and client-side `S_Move` handling intact.

## Explicit TCP Compatibility Mode

Unity keeps a movement transport toggle:

```csharp
GameManager.Network.UseUdpMovement
```

Behavior:

- `false`: send normal TCP `C_Move`
- `true`: send UDP movement instead of TCP movement

The current login scene selects `true`, so normal client builds send movement over UDP. `false` is retained for controlled regression tests and emergency configuration only; it does not describe the normal runtime path and it is not selected automatically when UDP registration fails.

## UDP_MOVE Send Rate Limit

UDP movement initially sent too frequently.

Current send policy:

- Send UDP movement at a controlled interval.
- Skip packets if position changes are too small.
- Send immediately when `PlayerState` or `MoveDir` changes.

This reduces UDP traffic while preserving animation-relevant state transitions.

## Proto-Based UDP Migration

String UDP packets were migrated to Proto-based UDP datagrams.

Added messages:

```proto
message C_UdpHello {
  string token = 1;
}

message S_UdpHello {
  bool ok = 1;
  string message = 2;
}

message C_UdpMove {
  PositionInfo posInfo = 1;
  uint32 sequence = 2;
}
```

Added message IDs:

```proto
C_UDP_HELLO
S_UDP_HELLO
C_UDP_MOVE
```

`C_UdpMove` reuses existing `PositionInfo`, so UDP movement carries the same movement state, direction, and position fields used by TCP `C_Move`. It also includes a `uint32 sequence` field for server-side replay and duplicate packet rejection.

`S_UdpHello` response handling is implemented and verified for both success and invalid-token rejection.

## UDP Datagram Format

Current UDP datagram format:

```text
[ushort packetId][protobuf payload]
```

Layout:

- First 2 bytes: `ushort packetId`
- Remaining bytes: protobuf payload for that packet

Examples:

```text
[MsgId.CUdpHello][C_UdpHello bytes]
[MsgId.SUdpHello][S_UdpHello bytes]
[MsgId.CUdpMove][C_UdpMove bytes]
```

The server reads the first 2 bytes, resolves the packet ID, then parses the remaining bytes with the matching protobuf parser.


## UDP Security Hardening v1

The first server-side UDP security pass keeps the existing sender identity model:

```text
RemoteEndPoint -> ClientSession -> MyPlayer
```

`C_UdpMove` still does not carry `PlayerId`. The server continues to derive the player from the registered UDP endpoint and the existing authenticated TCP session.

Implemented server protections:

- UDP token values are no longer printed in full server logs. The token issue log records only `TokenLength` and expiry time.
- A successful `C_UdpHello` registration consumes the token by clearing `ClientSession.UdpToken` and `UdpTokenExpiresAt`.
- Repeated `C_UdpHello` from the already registered endpoint is treated as an idempotent registration and receives another successful `S_UdpHello` ACK.
- A different endpoint cannot replace an already registered session endpoint.
- One UDP endpoint cannot be registered to more than one `ClientSession`.
- TCP disconnect clears UDP token, token expiry, endpoint, last-seen time, sequence state, rate-limit counters, and movement validation state.
- Registered UDP movement uses `C_UdpMove.sequence` and drops old or duplicate sequence values before queueing room work.
- Sequence comparison uses unsigned wrap-around ordering:

```csharp
incoming != last && (int)(incoming - last) > 0
```

- Registered UDP movement is rate-limited per session: `60` accepted move attempts per `1000ms` window.
- Invalid token, unregistered endpoint, parse failure, and unknown datagram logs are sampled to avoid log flooding.
- UDP movement is validated before `room.Push(room.HandleMove, ...)` whenever possible.
- UDP movement speed validation uses the last accepted UDP position and timestamp:

```text
allowedDistance = MaxUdpMoveSpeed * clamp(deltaTime, 0.05s, 0.50s) + UdpMoveDistanceTolerance
```

Current constants:

```text
MaxUdpMoveSpeed = 12.0
UdpMoveDistanceTolerance = 1.25
FirstUdpMoveDistanceTolerance = 5.0
```

- If `MovementBoundsProvider` has a map for the current room type, UDP movement outside walkable bounds is dropped before room queueing.
- `GameRoom.HandleMove` now applies movement bounds by current `RoomType` instead of only Town, while preserving the existing Town MyRoom exemption.
- A Town player starts in private MyRoom state with `IsInPublicTownArea=false` and authoritative position `(0,0)`.
- The MyRoom -> public Lobby portal is a client teleport. If its destination satisfies `TownSpawnService.IsPublicLobbyPosition()` (`x >= 18` and inside the exported Town walkable map), the server accepts that one private-to-public transition without applying the normal per-frame speed-distance limit.
- After the transition, normal speed and Town bounds validation resume and `GameRoom.HandleMove` changes `IsInPublicTownArea` to `true`.

Build verification:

```text
dotnet build Server\Server.sln
```

Result: build succeeded with existing `netcoreapp3.1` EOL warnings and no errors.

Client migration status:

Unity now uses the server-generated `C_UdpMove` definition with `Sequence`. `UdpGameClient` resets its sequence for each UDP connection, waits for a successful `S_UdpHello` registration response, and sends movement with a monotonically increasing non-zero sequence. `MyPlayer` records its UDP send interval state only after the datagram send succeeds.

## Run/Moving State Toggle Fix

Problem:

During running, server logs sometimes alternated between:

```text
State=Moving
State=Run
State=Moving
State=Run
```

Cause:

- Keyboard movement could set `PlayerState.Moving`.
- Double-input run handling then set `PlayerState.Run`.
- Sudden direction changes could briefly send `Moving` during a run.

Implemented client send-side fix:

- Added a short run-state grace window in `MyPlayer`.
- If run was just detected, movement input keeps `Run` instead of being overwritten to `Moving`.
- The UDP send path normalizes `Moving` to `Run` during the grace window.

Implemented remote animation fix:

- `OtherPlayer.ProcWalkPlayer()` now clears `isRun` before setting `isWalk`.
- `OtherPlayer.ProcRunPlayer()` now clears `isWalk` before setting `isRun`.
- `OtherPlayer` has a short remote run-state grace window so a brief `Moving` packet right after `Run` does not immediately flicker the remote animation back to walk.

This has been verified with movement sync tests.

## Jump Coordinate And Animation Fix

Problem:

- Normal movement used the parent `transform.position` as the gameplay/world coordinate.
- Jump startup and jump visual code used `_Sprite.transform.position`, which is a child visual object.
- During jump movement, `CellPos` and `PositionInfo` could move while the parent `transform.position` stayed behind.
- After jump exit, normal movement could overwrite `CellPos` from the stale parent transform, causing visible coordinate drift.
- Remote `OtherPlayer` could also remain stuck in the jump animation if a non-jump `S_Move` arrived before the local remote jump timer exited.

Implemented fix:

- Parent `transform.position` is the gameplay/world coordinate.
- `CellPos` and `PositionInfo.PosX/PosY` are synchronized from the parent transform.
- Jump movement moves the parent transform, then updates `CellPos`.
- `_Sprite.transform.localPosition` is used only for the jump height visual offset.
- Jump exit resets the sprite local offset and syncs `CellPos` from the parent transform.
- `OtherPlayer` resets remote jump visual state when received movement state is no longer `Jump`.

This keeps jump movement enabled while preventing sprite-world coordinates from becoming network gameplay coordinates.

## UDP Movement v1 Verification

Verified:

- `S_UdpHello Ok=True` registration success
- invalid UDP token test with `S_UdpHello Ok=False`
- Proto `C_UdpHello` / `S_UdpHello` send and receive
- Proto `C_UdpMove` send
- monotonically increasing, non-zero `C_UdpMove.sequence` send and old/duplicate rejection
- server move enqueue through the existing `C_Move -> GameRoom.HandleMove` path
- remote client coordinate, `PlayerState`, `MoveDir`, and animation sync
- Run/Moving state correction
- jump movement coordinate sync and remote jump animation exit
- `UseUdpMovement=true` uses UDP `C_UdpMove`
- `UseUdpMovement=false` uses the existing TCP `C_Move` compatibility path
- UDP registration-pending movement is skipped instead of automatically falling back to TCP
- MyRoom `(0,0)` -> public Lobby teleport is accepted without repeated `Reason=Speed` drops

## String Fallback Status

String UDP fallback has been removed.

Removed:

- `ServerCore/UdpListener.cs`
  - `UDP_HELLO`
  - `UDP_HELLO:{token}`
  - `UDP_HELLO_ACK`
  - `UDP_HELLO_REJECT`
  - `UDP_MOVE:`
  - `Encoding.UTF8.GetString(...)` fallback after Proto datagram handling fails
- `Server/Udp/UdpGamePacketHandler.cs`
  - `UdpMovePrefix = "UDP_MOVE:"`
  - `HandleStringPayload(...)`
  - `TryParseStringMove(...)`
- `Program.cs`
  - `UdpPayloadHandler = _udpPacketHandler.HandleStringPayload`
- Unity `NetworkManager.cs`
  - `UDP_HELLO_ACK`
  - `UDP_HELLO_REJECT`
  - string fallback receive path using `Encoding.UTF8.GetString(...)`
  - raw string fallback logging

Current behavior:

- Proto UDP remains the primary path.
- Unknown UDP datagrams are ignored/logged instead of parsed as legacy strings.
- TCP movement compatibility mode is unchanged and still controlled explicitly by `UseUdpMovement=false`.

## Operations And Remaining Hardening

### UDP Move Log Cleanup - Completed

Per-move enqueue logging is disabled by default:

```text
[UDP] Move enqueued...
```

The server uses `DebugUdpMovementLog=false` for the detailed enqueue line. Validation/drop logs are sampled on the first occurrence and every 25th drop so failures remain diagnosable without logging every datagram.

### Legacy UDP Test Helper

`NetworkManager` now owns runtime UDP connect/register/move send after login.

`UdpHelloTestClient` should not:

- connect UDP independently during normal gameplay,
- close UDP on scene transition,
- act as the source of movement coordinates.

Once scene references are removed safely in Unity Editor, the helper script can be deleted.

### Server Validation

UDP movement now has a first-pass server validation layer:

- Endpoint ownership is enforced through `RemoteEndPoint -> ClientSession -> MyPlayer`.
- Movement before UDP registration is rejected.
- Old or duplicate `C_UdpMove.sequence` values are dropped.
- Registered UDP movement is rate-limited per session.
- Movement speed/range is checked against the last accepted UDP position and timestamp.
- Movement bounds are applied for any room type with loaded `MovementBoundsProvider` data.

Remaining validation work:

- Retune movement constants only if a new character speed, dash, or teleport mechanic exceeds the current envelope.
- Add stronger packet authenticity later, such as a session-bound MAC or challenge response, if the project needs protection beyond endpoint binding.
- Keep UDP skills out of scope until TCP skill validation is stable.

### SkillId 402

Next gameplay expansion:

- Add active skill `SkillId=402`.
- Name: client-defined active skill.
- Keep skill transport on TCP `C_Skill` / `S_Skill`.
- First implementation should focus on animation sync only.
- Do not add UDP skill yet.

### Action Lock

Needed before long skill animations:

- Prevent movement packets from immediately overriding skill animations.
- Decide whether movement is blocked during attack/skill or only animation state is locked.
- Add client-side action lock first.
- Add server-side validation later.

## Next Work Order

1. Keep movement-only UDP soak tests for multi-client Town and Bakal sessions.
2. Add explicit reconnect/re-registration handling if UDP endpoint rebinding becomes a product requirement.
3. Keep skills and other reliable gameplay events on TCP until their server-authoritative validation is stable.
4. Add stronger datagram authenticity only if endpoint binding is no longer sufficient for the deployment model.
