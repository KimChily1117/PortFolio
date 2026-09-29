import { KimchilyScriptBehaviour } from "Kimchily.Script";
import { Room } from "Kimchily.Network";
import { Hud } from "Kimchily.UI";
import { GameObject, Time, Vector3 } from "UnityEngine";
import { SCRIPT_ID, SCRIPT_HASH } from "./PortalRuleIdentity";

/** 서버 TS가 만든 공개 상태의 계약. C# SDK는 발판/승리 같은 필드를 알지 못한다. */
interface PadState {
    id: string; x: number; y: number; z: number; radius: number;
    active: boolean; playerId: string | null;
}
interface PortalState {
    phase: "waiting" | "playing" | "holding" | "complete";
    round: number; requiredPlayers: number; holdSeconds: number; remainingMs: number;
    pads: ReadonlyArray<PadState>;
}

/**
 * 제작자가 작성하는 클라이언트 게임 스크립트.
 * 서버 PortalRules.ts는 정답을 계산하고, 이 파일은 그 정답을 어떻게 보여줄지 결정한다.
 * 문구·버튼·진행률·발판 빛·포털 연출 모두 TS에 있다.
 * C# HUD는 문자열과 숫자를 TMP/Canvas로 그리는 범용 도구다.
 */
export default class PortalGarden extends KimchilyScriptBehaviour {
    // Inspector에서 연결하는 씬 오브젝트. 새 C# MonoBehaviour를 만들 필요가 없다.
    public pad0!: GameObject;
    public pad1!: GameObject;
    public pad2!: GameObject;
    public pad3!: GameObject;
    public idle0!: GameObject;
    public idle1!: GameObject;
    public idle2!: GameObject;
    public idle3!: GameObject;
    public portalOpen!: GameObject;
    public portalClosed!: GameObject;
    public halo!: GameObject;

    // 클라이언트 연출용 값이다. 승리 시간은 서버 TS의 RULES에서 바꾼다.
    public pulseSpeed: number = 3;
    public celebrationScale: number = 1.08;
    private elapsed = 0;
    private poll = 0;
    private game: PortalState | null = null;
    private hudSignature = "";
    private bound = false;

    OnEnable(): void {
        // 생성된 해시로 이 콘텐츠가 기대하는 서버 규칙을 정확하게 지정한다.
        // 연결 전 호출이면 SDK가 기억했다가 joined 뒤 watch를 보낸다.
        this.bound = Room.useGame(SCRIPT_ID, SCRIPT_HASH);
        this.game = null;
        this.poll = 0;
        this.hudSignature = "";
    }

    Start(): void {
        this.portalOpen.SetActive(false);
        this.portalClosed.SetActive(true);
    }

    Update(): void {
        // 실행 중 다른 규칙으로 재바인딩하는 요청은 SDK가 거절한다. 이전 규칙 결과를 새 게임으로 오인하지 않는다.
        if (!this.bound) {
            Hud.showPanel({ eyebrow: "CHILI ISLAND", title: "규칙 버전이 달라요", body: "월드를 나간 뒤 새 게시 링크로 다시 입장해 주세요", action: null });
            return;
        }
        this.elapsed += Time.deltaTime;
        // Unity Button은 ID만 보관한다. 다음 Update에서 TS가 읽어 VM 재진입을 피한다.
        const action = Hud.takeAction();
        if (action === "start" || action === "reset") {
            // 승리를 보내는 것이 아니라 시작/재시작 요청만 보낸다.
            // true는 전송 접수이며 최종 결과는 서버 스냅샷으로 확인한다.
            Room.sendAction(action);
        }
        // getState는 이미 받은 상태를 읽는다. 추가 네트워크 요청을 만들지 않는다.
        this.poll -= Time.deltaTime;
        if (this.poll <= 0) {
            this.poll = .1;
            const room = Room.getState<PortalState>();
            const candidate = room.game?.scriptId === SCRIPT_ID && room.game.scriptHash === SCRIPT_HASH ? room.game.state : null;
            // 게임별 자료형 검사는 범용 C# 클라이언트 대신 제작자 TS에서 수행한다.
            this.game = candidate && this.isPortalState(candidate) ? candidate : null;
            this.drawHud(room.connected, room.players.length);
        }
        this.drawWorld();
    }

    private isPortalState(value: PortalState): boolean {
        return ["waiting", "playing", "holding", "complete"].includes(value.phase)
            && Number.isFinite(value.holdSeconds) && value.holdSeconds > 0
            && Number.isFinite(value.remainingMs) && value.remainingMs >= 0
            && Number.isInteger(value.requiredPlayers) && value.requiredPlayers >= 1 && value.requiredPlayers <= 4
            && Array.isArray(value.pads) && value.pads.length === 4
            && value.pads.every(pad => !!pad && typeof pad.active === "boolean"
                && (pad.playerId === null || typeof pad.playerId === "string"));
    }

    private drawHud(connected: boolean, playerCount: number): void {
        const game = this.game;
        let eyebrow = "CHILI ISLAND · TYPESCRIPT";
        let title = "서버 규칙을 연결하는 중";
        let body = connected ? "현재 월드의 TS 규칙을 불러옵니다" : "닉네임과 채팅 서버 연결을 확인해 주세요";
        let progress = 0;
        let accent = "#8CFABA";
        // JSON 경계를 넘기므로 버튼이 없을 때 undefined 대신 명시적인 null을 사용한다.
        let action: { id: string; label: string; enabled: boolean } | null = null;
        // 안내 문구와 버튼 분기도 모두 제작자가 수정하는 TS 코드다.
        if (game) {
            const occupied = game.pads.filter(pad => pad.active && !!pad.playerId).length;
            if (game.phase === "waiting") {
                title = "모이면 시작! · " + game.requiredPlayers + "인 협동";
                body = "같은 QR로 입장한 뒤 시작을 눌러 주세요";
                action = { id: "start", label: "모두 준비됐어요 · 시작", enabled: connected };
            } else if (game.phase === "complete") {
                eyebrow = "CHILI ISLAND · CLEAR!";
                title = "포털이 열렸어요!";
                body = "모두 함께 빛나는 포털로 이동해 보세요";
                progress = 1;
                accent = "#FFD36A";
                action = { id: "reset", label: "한 번 더 플레이", enabled: connected };
            } else if (game.phase === "holding") {
                title = "그대로! " + Math.ceil(game.remainingMs / 1000) + "초";
                body = "발판에서 내려가면 에너지가 초기화돼요";
                // 서버의 남은 시간을 표시한다. 클라이언트 타이머로 승리를 판정하지 않는다.
                progress = Math.max(0, Math.min(1, 1 - game.remainingMs / (game.holdSeconds * 1000)));
            } else {
                title = "서로 다른 발판으로 · " + occupied + "/" + game.requiredPlayers;
                body = "빛나는 발판을 하나씩 맡아 " + game.holdSeconds + "초 지켜 주세요";
            }
            if (playerCount < game.requiredPlayers && game.phase !== "waiting" && game.phase !== "complete") {
                body = "친구가 나갔어요 · 인원을 다시 맞춰 주세요";
                action = { id: "reset", label: "인원 다시 맞추기", enabled: connected };
            }
        }
        const panel = { eyebrow, title, body, progress, accent, action };
        const signature = JSON.stringify(panel);
        if (signature !== this.hudSignature) {
            Hud.showPanel(panel);
            this.hudSignature = signature;
        }
    }

    private drawWorld(): void {
        const lights = [this.pad0, this.pad1, this.pad2, this.pad3];
        const idle = [this.idle0, this.idle1, this.idle2, this.idle3];
        const complete = this.game?.phase === "complete";
        for (let i = 0; i < lights.length; i++) {
            const pad = this.game?.pads[i];
            const required = !!pad?.active;
            const occupied = required && !!pad.playerId;
            // 필요한 빈 발판은 맥동하고, 점유한 발판은 안정된 빛을 보여준다.
            lights[i].SetActive(complete || required);
            const ringScale = complete || occupied ? 1 : .94 + Math.sin(this.elapsed * this.pulseSpeed) * .035;
            lights[i].transform.localScale = new Vector3(ringScale, 1, ringScale);
            idle[i].SetActive(!occupied && !complete);
        }
        this.portalOpen.SetActive(complete);
        this.portalClosed.SetActive(!complete);
        const scale = complete ? this.celebrationScale + Math.sin(this.elapsed * this.pulseSpeed) * .035 : 1;
        this.halo.transform.localScale = new Vector3(scale, scale, scale);
    }

    OnDisable(): void {
        // 이전 월드의 UI를 남기지 않는다. SDK도 수명 종료를 별도로 보장한다.
        Hud.hide();
        this.hudSignature = "";
    }
}
