/** Supported Kimchily world APIs. These declarations describe the bundled facade, not the full Unity API. */
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
