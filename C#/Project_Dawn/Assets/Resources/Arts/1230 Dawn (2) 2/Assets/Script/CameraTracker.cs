using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTracker : MonoBehaviour
{
    public bool In_Gate;
    public GameObject objTarget;
    public Vector3 vTargetPos;

    void Start()
    {
        if (objTarget)
        {
            vTargetPos.x += 1;
            this.transform.position = vTargetPos;
        }

    }   



    // Update is called once per frame
    void Update()
    {



        if (objTarget && In_Gate == false)
        {
            
            vTargetPos.x = objTarget.transform.position.x;
           
            vTargetPos.z = this.transform.position.z;

            this.transform.position = vTargetPos;

        }
        

    }
}
