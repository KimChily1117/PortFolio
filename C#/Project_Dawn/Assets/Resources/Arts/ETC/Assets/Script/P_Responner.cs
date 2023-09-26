using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class P_Responner : MonoBehaviour
{
    public GameObject objPlayer; // 원본 
    public GameObject prefabPlayer; // 복사할 오브젝트
                                   
    public float ResponTime;
    public bool Reserv;
    IEnumerator ProcTime()
    {
        Reserv = true;
        yield return new WaitForSeconds(ResponTime);
        ResponObject();
        Reserv = false;
    }
    void ResponObject()
    {
        // 새로운 오브젝트를 생성한뒤에 
        objPlayer = Instantiate(prefabPlayer);



        //생성된 오브젝트를 부활 위치로 옮겨준다
        objPlayer.transform.position = this.transform.position;
    }
    private void Awake()
    {
        ResponObject(); //시작할때 객체를 만든다.
    }
    // Update is called once per frame
    void Update()
    {
        if (objPlayer == null && Reserv == false)
        {
            StartCoroutine(ProcTime());
        }
    }
}
