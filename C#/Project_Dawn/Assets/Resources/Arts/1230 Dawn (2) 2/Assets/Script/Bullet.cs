using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float fRange; // 범위 
    public Vector3 vStart;


    void Start()
    {
        vStart = this.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 vCurPos = this.transform.position;
        //float fCurDist = Vector3.Distance(vStart, vCurPos);
        Vector3 vCurDist = vCurPos - vStart;
        float fCurDist = vCurDist.magnitude;
        Debug.DrawLine(vStart, vCurPos);//씬화면에 선그리기

               


        if (fCurDist >= fRange) // 사정거리를 지정해놓고 일정 거리를 넘어가면 오브젝트(총알)을 삭제한다. 
        {
            //GetComponent<Rigidbody2D>().velocity = Vector3.zero;//리지드바디에 힘을 없애기
            Destroy(this.gameObject);
        }
    }



    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Monster")
        {
            GameObject objPlayer = GameManager.GetInstance().responnerPlayer.objPlayer;
            Player target = collision.gameObject.GetComponent<Player>();

            if (objPlayer != null)
            {
                Player me = objPlayer.GetComponent<Player>();
                if (me != null && target != null)
                {
                    me.Attack(target);                    
                }
            }
        }
    }

}
