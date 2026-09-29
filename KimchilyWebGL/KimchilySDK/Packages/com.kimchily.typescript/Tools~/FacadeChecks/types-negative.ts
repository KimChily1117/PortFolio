import { Coroutine, Event, KimchilyScriptBehaviour } from "Kimchily.Script";
import { GameObject, Vector3, WaitForSeconds } from "UnityEngine";
import { Room } from "Kimchily.Network";
import { Hud } from "Kimchily.UI";

export default class InvalidUsage extends KimchilyScriptBehaviour {
    Start(): void {
        // @ts-expect-error The old native fixed-preset API has been removed.
        Room.enableGame("upload-arbitrary-server-code");
        // @ts-expect-error Room identity and transport are host-owned.
        Room.connect("ws://somewhere.invalid/ws");
        // @ts-expect-error No raw network message capability is exposed.
        Room.send({ type: "state", x: 99 });
        // @ts-expect-error Snapshots cannot overwrite server-owned state.
        Room.getState<{ phase: string }>().game!.state.phase = "complete";
        // @ts-expect-error Player snapshots are deeply readonly.
        Room.getState().players[0].state!.x = 99;
        // @ts-expect-error Room snapshots cannot add identities.
        Room.getState().players.push({ playerId: "fake", name: "fake", state: null });
        // @ts-expect-error Registered script identity requires an exact hash.
        Room.useGame("portal");
        // @ts-expect-error JSON payloads cannot contain callbacks.
        Room.sendAction("start", { callback: () => {} });
        // @ts-expect-error HUD takes display data, not JavaScript callbacks.
        Hud.showPanel({ eyebrow: "", title: "Ready", body: "", action: { id: "start", label: "Start", callback: () => {} } });
        // @ts-expect-error Progress is a number.
        Hud.showPanel({ eyebrow: "", title: "Ready", body: "", progress: "full" });
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
