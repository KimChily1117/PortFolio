/**
 * Chili Island의 권위 있는 게임 규칙. 이 파일은 Unity가 아닌 서버의 제한된 JS VM에서 실행된다.
 * C# 서버는 방/통신/위치 검증/시간을 제공하고, 발판과 성공 여부는 아래 TypeScript만 결정한다.
 * 이 값을 바꾸고 compile-script.ps1을 실행하면 C#을 고치지 않고 새로운 규칙 해시를 배포할 수 있다.
 */
export const config = {
    holdSeconds: 3,
    poseFreshMilliseconds: 1200,
    heightTolerance: 1.5,
    pads: [
        { id: "star", x: -3, y: 0, z: 2, radius: 1.1 },
        { id: "moon", x: 3, y: 0, z: 2, radius: 1.1 },
        { id: "sun", x: -3, y: 0, z: 6, radius: 1.1 },
        { id: "leaf", x: 3, y: 0, z: 6, radius: 1.1 }
    ]
} as const;

interface Pose { x: number; y: number; z: number; grounded: boolean; }
interface Participant { playerId: string; name: string; state: Pose | null; receivedAtMs: number; }
interface Input {
    kind: "watch" | "command" | "tick";
    action?: string;
    payload?: unknown;
    nowMs: number;
    selfId?: string;
    players: Participant[];
}
interface Pad { id: string; x: number; y: number; z: number; radius: number; active: boolean; playerId: string | null; }
interface State {
    phase: "waiting" | "playing" | "holding" | "complete";
    round: number;
    requiredPlayers: number;
    holdSeconds: number;
    remainingMs: number;
    pads: Pad[];
    // 서버 시각을 저장한다. 외부에 전달되지만 클라이언트가 이를 덮어쓸 API는 없다.
    _holdingSince: number | null;
}

/** 방이 처음 구독될 때 한 번 호출한다. 시작 버튼을 누르기 전까지는 대기 상태이다. */
export function create(): State {
    return {
        phase: "waiting", round: 0, requiredPlayers: 1,
        holdSeconds: config.holdSeconds, remainingMs: config.holdSeconds * 1000,
        pads: config.pads.map(pad => ({ ...pad, active: false, playerId: null })),
        _holdingSince: null
    };
}

/**
 * 매번 JSON으로 복사한 입력/이전 상태를 받는 reducer이다. Date.now나 클라이언트 시계 대신 nowMs를 쓴다.
 * null은 변경 없음이다. 변경 상태의 version 부여와 모든 참가자에게 전달하는 작업은 C# 호스트가 맡는다.
 */
export function reduce(previous: State, input: Input): State | null {
    const state: State = { ...previous, pads: previous.pads.map(pad => ({ ...pad })) };
    const requiredNow = Math.max(1, Math.min(config.pads.length, input.players.length));
    const fullCharge = config.holdSeconds * 1000;

    // 클라이언트는 요청만 한다. 완료 명령/필요 인원/발판 좌표를 payload로 보내도 규칙을 바꾸지 못한다.
    if (input.kind === "command") {
        if (input.action === "start") {
            if (state.phase === "waiting") {
                state.phase = "playing";
                state.round++;
                // 플레이 도중 입장·퇴장에 따라 난도가 바뀌지 않도록 시작 시 필요한 인원을 고정한다.
                state.requiredPlayers = requiredNow;
                state.remainingMs = fullCharge;
                state._holdingSince = null;
            }
        } else if (input.action === "reset") {
            state.phase = "waiting";
            state.remainingMs = fullCharge;
            state._holdingSince = null;
        } else {
            throw new Error("Unknown portal command");
        }
    }

    // 승리 후 발판에서 내려가거나 늦게 입장해도 포털은 열린 상태로 유지된다. 명시적인 reset만 해제한다.
    if (state.phase === "complete") return null;
    if (state.phase === "waiting") state.requiredPlayers = requiredNow;

    const used = new Set<string>();
    state.pads = config.pads.map((pad, index) => {
        const active = index < state.requiredPlayers;
        let playerId: string | null = null;
        if (active) {
            // 서버가 playerId 순서로 정렬해 준 참가자를 순회하므로 동시에 진입해도 점유 선택이 일정하다.
            for (const player of input.players) {
                const pose = player.state;
                const age = input.nowMs - player.receivedAtMs;
                if (!pose || !pose.grounded || used.has(player.playerId)
                    || age < 0 || age > config.poseFreshMilliseconds) continue;
                const dx = pose.x - pad.x;
                const dz = pose.z - pad.z;
                // XZ 원 내부 + 높이 허용 범위 + 접지 상태를 모두 만족해야 점유한다.
                if (dx * dx + dz * dz > pad.radius * pad.radius
                    || Math.abs(pose.y - pad.y) > config.heightTolerance) continue;
                playerId = player.playerId;
                used.add(playerId); // 같은 사람이 겹친 발판 여러 개를 동시에 채우지 못한다.
                break;
            }
        }
        return { ...pad, active, playerId };
    });

    state.remainingMs = fullCharge;
    if (state.phase !== "waiting") {
        const allOccupied = state.pads.filter(pad => pad.active && pad.playerId !== null).length === state.requiredPlayers;
        if (allOccupied) {
            state._holdingSince ??= input.nowMs;
            const remaining = Math.max(0, fullCharge - (input.nowMs - state._holdingSince));
            // UI용 시간을 100ms 단위로 올림하면 pose 패킷마다 불필요한 스냅샷을 보내지 않는다.
            state.remainingMs = Math.ceil(remaining / 100) * 100;
            state.phase = remaining === 0 ? "complete" : "holding";
        } else {
            state._holdingSince = null;
            state.phase = "playing";
        }
    }

    return JSON.stringify(state) === JSON.stringify(previous) ? null : state;
}
