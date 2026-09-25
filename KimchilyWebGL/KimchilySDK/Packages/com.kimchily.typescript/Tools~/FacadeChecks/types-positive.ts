import { Coroutine, Event, KimchilyScriptBehaviour } from "Kimchily.Script";
import { GameObject, Transform, Vector3, WaitForSeconds, Time, Debug, Space } from "UnityEngine";

export default class TypeContract extends KimchilyScriptBehaviour {
    public target: GameObject | null = null;
    public targetTransform: Transform | null = null;
    public offset: Vector3 = Vector3.one;
    private readonly objects = new Map<string, GameObject>();
    private readonly changed = new Event<[GameObject, number]>();
    private handle: Coroutine | null = null;
    OnEnable(): void {
        this.objects.set("owner", this.gameObject);
        this.changed.AddListener((object, count) => Debug.Log(`${object.name}: ${count}`));
        this.changed.Invoke(this.gameObject, this.objects.size);
        this.handle = this.StartCoroutine(this.routine());
    }
    Start(): void { this.gameObject.name = "TypeContract"; }
    Update(dt: number): void {
        this.transform.Rotate(0, Time.deltaTime * 45, 0, Space.Self);
        this.transform.Translate(0, dt, 0, Space.World);
        const next = this.transform.position.add(this.offset);
        this.transform.position = next;
        this.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(2, 2, 2), 0.5);
        this.targetTransform?.gameObject.SetActive(true);
    }
    OnDisable(): void { if (this.handle) this.StopCoroutine(this.handle); }
    OnDestroy(): void { this.StopAllCoroutines(); }
    private *routine(): Generator<WaitForSeconds | null | undefined, void, unknown> {
        yield new WaitForSeconds(0.5);
        yield null;
        yield undefined;
    }
}
