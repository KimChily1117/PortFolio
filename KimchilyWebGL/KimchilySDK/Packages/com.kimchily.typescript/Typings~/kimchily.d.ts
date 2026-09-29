/** 설치된 SDK의 제한된 API다. 임의 Unity/CLR/브라우저 객체는 노출하지 않는다. */
declare module "Kimchily.Network" {
    export type JsonValue = null | boolean | number | string | ReadonlyArray<JsonValue> | { readonly [key: string]: JsonValue };
    export type DeepReadonly<T> = T extends ReadonlyArray<infer U> ? ReadonlyArray<DeepReadonly<U>>
        : T extends object ? { readonly [K in keyof T]: DeepReadonly<T[K]> } : T;
    /** 검증된 최근 위치의 복사본이며 native Transform 참조가 아니다. */
    export interface PlayerPose {
        readonly sequence: number;
        readonly x: number;
        readonly y: number;
        readonly z: number;
        readonly yaw: number;
        readonly speed: number;
        readonly verticalVelocity: number;
        readonly grounded: boolean;
    }
    export interface RoomPlayer {
        readonly playerId: string;
        readonly name: string;
        readonly state: PlayerPose | null;
    }
    export interface GameSnapshot<T = unknown> {
        readonly scriptId: string;
        readonly scriptHash: string;
        readonly version: number;
        readonly state: DeepReadonly<T>;
    }
    export interface RoomSnapshot<T = unknown> {
        readonly connected: boolean;
        readonly selfId: string | null;
        readonly players: ReadonlyArray<RoomPlayer>;
        readonly game: GameSnapshot<T> | null;
    }
    /** C#는 연결·동기화만 담당한다. 게임 규칙은 서버에 등록된 TS가 판정한다. */
    export const Room: {
        /** 등록된 서버 스크립트와 정확한 SHA-256에 참여한다. 입장 중이면 요청을 기억한다. */
        useGame(scriptId: string, scriptHash: string): boolean;
        /** JSON 스냅샷의 동결된 복사본. T는 제작자의 타입이며 런타임 스키마 검증을 대신하지 않는다. */
        getState<T = unknown>(): RoomSnapshot<T>;
        /** UTF-8 1,024바이트 이하 JSON 값만 전송한다. true는 접수 여부이며 게임 성공 판정이 아니다. */
        sendAction(action: string, payload?: JsonValue): boolean;
    };
}

declare module "Kimchily.UI" {
    export interface HudAction {
        /** ASCII 영문/숫자/_/- 1..80자. 클릭하면 이 ID만 큐에 들어간다. */
        readonly id: string;
        readonly label: string;
        readonly enabled?: boolean;
    }
    export interface HudPanel {
        /** 최대 48자. 빈 문자열 허용. */
        readonly eyebrow: string;
        /** 최대 96자이며 비어 있을 수 없다. */
        readonly title: string;
        /** 최대 1,200자. 긴 본문은 실제 화면에서 말줄임 표시된다. */
        readonly body: string;
        /** 0..1, 기본값 0. */
        readonly progress?: number;
        /** #RRGGBB, 기본값 #72C6AE. */
        readonly accent?: string;
        readonly action?: HudAction | null;
    }
    /** 이 Behaviour가 소유하는 Unity TMP HUD. 비활성화·오류·재로드·파괴 때 화면과 입력을 정리한다. */
    export const Hud: {
        /** JSON 형태의 표시 데이터만 허용한다. callback, getter, Unity 참조를 전달하지 않는다. */
        showPanel(panel: HudPanel): void;
        /** 다음 Update에서 버튼 ID를 한 번 꺼낸다. 대기 입력이 없으면 null. */
        takeAction(): string | null;
        hide(): void;
    };
}

declare module "UnityEngine" {
    /** Rotation/translation coordinate system. */
    export enum Space { World = 0, Self = 1 }

    /** JavaScript value type. Reading a Transform vector returns a copy; assign it back to move the object. */
    export class Vector3 {
        constructor(x?: number, y?: number, z?: number);
        x: number;
        y: number;
        z: number;
        readonly magnitude: number;
        readonly sqrMagnitude: number;
        readonly normalized: Vector3;
        static readonly zero: Vector3;
        static readonly one: Vector3;
        static readonly up: Vector3;
        static readonly right: Vector3;
        static readonly forward: Vector3;
        /** Returns an independent copy. */
        clone(): Vector3;
        /** Returns a new vector; does not mutate this value. */
        add(other: Vector3): Vector3;
        subtract(other: Vector3): Vector3;
        multiply(scalar: number): Vector3;
        /** Normalizes this vector in place. Zero remains zero. */
        Normalize(): void;
        static Distance(a: Vector3, b: Vector3): number;
        static Dot(a: Vector3, b: Vector3): number;
        static Cross(a: Vector3, b: Vector3): Vector3;
        /** Linearly interpolates with t clamped to [0, 1]. */
        static Lerp(a: Vector3, b: Vector3, t: number): Vector3;
    }

    /** A scene reference assigned by the Inspector or provided by this behaviour. Cannot create native objects. */
    export class GameObject {
        private constructor();
        private readonly __gameObjectReference: never;
        name: string;
        readonly activeSelf: boolean;
        readonly transform: Transform;
        SetActive(value: boolean): void;
    }

    /** A scene Transform reference. Vector getters return copies, matching Unity value-type semantics. */
    export class Transform {
        private constructor();
        private readonly __transformReference: never;
        readonly gameObject: GameObject;
        position: Vector3;
        localPosition: Vector3;
        localScale: Vector3;
        eulerAngles: Vector3;
        /** Adds an offset in the chosen coordinate system; defaults to Space.Self. */
        Translate(x: number, y: number, z: number, relativeTo?: Space): void;
        /** Adds Euler rotation in degrees; defaults to Space.Self. */
        Rotate(x: number, y: number, z: number, relativeTo?: Space): void;
    }

    export class Time {
        private constructor();
        /** Scaled seconds since the previous Unity frame. */
        static readonly deltaTime: number;
    }

    export class Debug {
        private constructor();
        static Log(message: unknown): void;
        static LogWarning(message: unknown): void;
        static LogError(message: unknown): void;
    }

    /** Coroutine instruction using scaled Unity time. A finite, nonnegative duration is required. */
    export class WaitForSeconds {
        constructor(seconds: number);
        readonly seconds: number;
        private readonly __kimchilyWait: "seconds";
    }
}

declare module "Kimchily.Script" {
    import { GameObject, Transform, WaitForSeconds } from "UnityEngine";

    /** An opaque handle belonging to the behaviour that started it. */
    export class Coroutine {
        private constructor();
        private readonly __coroutineHandle: never;
    }
    export type CoroutineYield = WaitForSeconds | null | undefined;

    /** Base class for the default-exported world script. The runtime creates each instance. */
    export abstract class KimchilyScriptBehaviour {
        protected constructor();
        readonly gameObject: GameObject;
        readonly transform: Transform;
        /** Runs a synchronous generator. Yield null/undefined for the next frame, or WaitForSeconds. */
        StartCoroutine(iterator: Iterator<CoroutineYield, void, unknown>): Coroutine;
        StopCoroutine(coroutine: Coroutine): void;
        /** Stops only this behaviour's coroutines. Disabling/destroying also cancels owned routines. */
        StopAllCoroutines(): void;
        Awake?(): void;
        OnEnable?(): void;
        Start?(): void;
        Update?(deltaTime: number): void;
        OnDisable?(): void;
        OnDestroy?(): void;
    }

    /** Synchronous, local script event. This does not send network messages or subscribe to Unity events. */
    export class Event<TArgs extends unknown[] = []> {
        /** Capacity is 1..256 (default 64). Registering the same function twice adds it only once. */
        constructor(maxListeners?: number);
        readonly ListenerCount: number;
        AddListener(listener: (...args: TArgs) => void): void;
        RemoveListener(listener: (...args: TArgs) => void): void;
        /** Uses a listener snapshot; edits affect the next invocation. Listener exceptions propagate. */
        Invoke(...args: TArgs): void;
    }
}
