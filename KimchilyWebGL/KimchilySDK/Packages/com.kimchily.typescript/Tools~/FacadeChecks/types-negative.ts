import { Coroutine, Event, KimchilyScriptBehaviour } from "Kimchily.Script";
import { GameObject, Vector3, WaitForSeconds } from "UnityEngine";
// @ts-expect-error Networking is not part of this SDK facade.
import { Room } from "Kimchily.Script";

export default class InvalidUsage extends KimchilyScriptBehaviour {
    Start(): void {
        // @ts-expect-error Scene references cannot be directly constructed.
        new GameObject();
        // @ts-expect-error Handles can only come from StartCoroutine.
        new Coroutine();
        // @ts-expect-error A function is not a generator iterator.
        this.StartCoroutine(() => {});
        // @ts-expect-error A numeric duration is required.
        new WaitForSeconds("one");
        // @ts-expect-error Owner references are readonly.
        this.gameObject = this.gameObject;
        // @ts-expect-error Position requires a Vector3 value.
        this.transform.position = [1, 2, 3];
        // @ts-expect-error Arbitrary Unity APIs are not promised by the facade.
        this.gameObject.GetComponent("Camera");
        // @ts-expect-error Only Space.World or Space.Self is supported.
        this.transform.Rotate(0, 1, 0, "world");
        // @ts-expect-error Vector values are numeric.
        new Vector3("one", 2, 3);
        const event = new Event<[number]>();
        // @ts-expect-error The event payload is typed.
        event.Invoke("wrong");
        // @ts-expect-error The listener payload is typed.
        event.AddListener((value: string) => {});
        // @ts-expect-error C# event syntax is not TypeScript subscription syntax.
        event += () => {};
    }
}
