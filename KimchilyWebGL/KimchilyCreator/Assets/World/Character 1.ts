import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { Debug, Time, WaitForSeconds } from 'UnityEngine';

export default class Character extends KimchilyScriptBehaviour {
    public speed: number = 140;

    // 몇 초 동안 회전할지
    public rotateSeconds: number = 2.0;

    // 회전 후 몇 초 기다릴지
    public waitSeconds: number = 1.0;

    Start(): void {
        Debug.Log('Character started');

        this.StartCoroutine(this.RotateRoutine());
    }

    Update(): void {
    }

    private *RotateRoutine(): Generator<any, void, unknown> {
        while (true) {

            // 1. 일정 시간 동안 회전
            let elapsed: number = 0;

            while (elapsed < this.rotateSeconds) {
                this.transform.Rotate(
                    0,
                    this.speed * Time.deltaTime,
                    0
                );

                elapsed += Time.deltaTime;

                // Unity의 yield return null;
                // 다음 프레임까지 대기
                yield null;
            }

            Debug.Log('Rotate finished');

            // 2. 일정 시간 정지
            yield new WaitForSeconds(this.waitSeconds);

            Debug.Log('Rotate restart');

            // while(true)이므로 다시 위로 돌아가서 회전
        }
    }
}