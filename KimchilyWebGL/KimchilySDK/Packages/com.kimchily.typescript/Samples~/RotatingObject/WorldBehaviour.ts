import { Coroutine, Event, KimchilyScriptBehaviour } from "Kimchily.Script";
import { Debug, GameObject, Space, Time, Vector3, WaitForSeconds } from "UnityEngine";

/** Assign beacon and speed in the Kimchily TypeScript Behaviour Inspector. */
export default class WorldBehaviour extends KimchilyScriptBehaviour {
    public beacon: GameObject | null = null;
    public speed: number = 45;
    public amplitude: number = 0.25;
    public label: string = "Kimchily TypeScript";
    public animate: boolean = true;
    public offset: Vector3 = Vector3.zero;

    private readonly objects = new Map<string, GameObject>();
    private readonly blinked = new Event<[boolean]>();
    private routine: Coroutine | null = null;
    private origin: Vector3 = Vector3.zero;
    private elapsed = 0;

    OnEnable(): void {
        this.origin = this.transform.localPosition;
        this.objects.set("owner", this.gameObject);
        if (this.beacon !== null) this.objects.set("beacon", this.beacon);
        this.blinked.AddListener(this.onBlink);
        this.routine = this.StartCoroutine(this.blink());
    }

    Start(): void {
        Debug.Log(`${this.label}: ${this.gameObject.name}, references=${this.objects.size}`);
    }

    Update(deltaTime: number): void {
        if (!this.animate) return;
        this.elapsed += deltaTime;
        this.transform.Rotate(0, this.speed * Time.deltaTime, 0, Space.Self);
        this.transform.localPosition = this.origin.add(this.offset).add(
            new Vector3(0, Math.sin(this.elapsed) * this.amplitude, 0));
    }

    OnDisable(): void {
        if (this.routine !== null) this.StopCoroutine(this.routine);
        this.routine = null;
        this.blinked.RemoveListener(this.onBlink);
        this.transform.localPosition = this.origin;
        if (this.beacon !== null) this.beacon.SetActive(true);
    }

    OnDestroy(): void {
        this.StopAllCoroutines();
        this.objects.clear();
    }

    private readonly onBlink = (visible: boolean): void => {
        const beacon = this.objects.get("beacon");
        if (beacon !== undefined) beacon.SetActive(visible);
    };

    private *blink(): Generator<WaitForSeconds, void, unknown> {
        while (true) {
            this.blinked.Invoke(false);
            yield new WaitForSeconds(0.5);
            this.blinked.Invoke(true);
            yield new WaitForSeconds(0.5);
        }
    }
}
