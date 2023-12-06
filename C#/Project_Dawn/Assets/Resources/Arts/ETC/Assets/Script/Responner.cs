using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Responner : MonoBehaviour
{
    public GameObject objPlayer;
    public string prefabName;
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
        //Resources폴더로 부터 프리팹을 로드한다.
        
        GameObject prefab = Resources.Load(prefabName) as GameObject;
                
        //프리팹으로 오브젝트를 생성하고
        objPlayer = Instantiate(prefab);
        objPlayer.name = prefabName;
        //생성된 오브젝트를 부활 위치로 옮겨준다.
        objPlayer.transform.position = this.transform.position;
    }
    private void Start()
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
