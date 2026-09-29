import { KimchilyScriptBehaviour } from "Kimchily.Script";
import { Room } from "Kimchily.Network";
import { GameObject, Time, Vector3 } from "UnityEngine";

/** Presentation is authored in TS; pad occupancy and victory come from the C# server. */
export default class PortalGarden extends KimchilyScriptBehaviour {
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
    public pulseSpeed: number = 3;
    public celebrationScale: number = 1.08;
    private elapsed = 0;
    private poll = 0;
    private complete = false;

    Start(): void {
        Room.enableGame("chili-portal-v1");
        this.portalOpen.SetActive(false);
        this.portalClosed.SetActive(true);
    }
    Update(): void {
        this.elapsed += Time.deltaTime;
        this.poll -= Time.deltaTime;
        if (this.poll <= 0) {
            this.poll = .1;
            const game = Room.getState().game;
            const lights = [this.pad0, this.pad1, this.pad2, this.pad3];
            const idle = [this.idle0, this.idle1, this.idle2, this.idle3];
            this.complete = game !== null && game.phase === "complete";
            for (let i = 0; i < 4; i++) {
                const pad = game?.pads[i];
                const required = !!pad?.active;
                const occupied = required && !!pad.playerId;
                lights[i].SetActive(this.complete || required);
                const ringScale = this.complete || occupied ? 1 : .94 + Math.sin(this.elapsed * 3) * .035;
                lights[i].transform.localScale = new Vector3(ringScale, 1, ringScale);
                idle[i].SetActive(!occupied && !this.complete);
            }
            this.portalOpen.SetActive(this.complete);
            this.portalClosed.SetActive(!this.complete);
        }
        const scale = this.complete ? this.celebrationScale + Math.sin(this.elapsed * this.pulseSpeed) * .035 : 1;
        this.halo.transform.localScale = new Vector3(scale, scale, scale);
    }
}
