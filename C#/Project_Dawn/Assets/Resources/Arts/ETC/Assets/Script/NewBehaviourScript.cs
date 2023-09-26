using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public float speed;
    public float z;
    public float x;
    public float y;
    public float zSpeed;
    Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        z = 0;
        zSpeed = 0;
        x = transform.position.x;
        y = transform.position.y;
     
    }

    // Update is called once per frame
    void Update()
    {
        
        if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
        {
            Vector3 vv = new Vector3(Input.GetAxisRaw("Horizontal") *speed, Input.GetAxisRaw("Vertical") * speed, 0);
            if (z > 0)
            {
                x += vv.x * 0.5f;
                y += vv.y * 0.5f;
            }
            else
            {
                x += vv.x ;
                y += vv.y ;
            }
        }

        if (Input.GetKey(KeyCode.Space))
        {
            if (z == 0)
            {
                zSpeed = 0.2f;
            }
        }

        if (z != 0 || zSpeed > 0)
        {
            zSpeed -= 0.008f;
            z += zSpeed;
            if (z < 0)
            {
                z = 0;
                zSpeed = 0;
            }
        }
        transform.position = (new Vector3(x, y + z, 0));
    }
}
