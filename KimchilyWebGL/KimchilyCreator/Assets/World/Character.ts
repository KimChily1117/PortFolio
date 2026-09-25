import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { Debug, GameObject, Time, Vector3, WaitForSeconds } from 'UnityEngine';
import { sampleHeight } from './Motion';

export default class Character extends KimchilyScriptBehaviour {
    public beacon!: GameObject;
    public speed: number = 140;
    public blinkSeconds: number = 0.5;

    private elapsed: number = 0;
    private counters: Map<string, number> = new Map<string, number>();

    Start(): void {
        this.counters.set('starts', 1);
        Debug.Log('Published TypeScript world started: revision 2');
        this.StartCoroutine(this.Blink());
    }

    Update(): void {
        this.elapsed += Time.deltaTime;
        this.transform.Rotate(0, this.speed * Time.deltaTime, 0);
        this.transform.position = new Vector3(0, sampleHeight(this.elapsed), 0);
    }

    private *Blink(): Generator<WaitForSeconds, void, unknown> {
        while (true) {
            this.beacon.SetActive(false);
            yield new WaitForSeconds(this.blinkSeconds);
            this.beacon.SetActive(true);
            yield new WaitForSeconds(this.blinkSeconds);
        }
    }

}
