//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class Behimoth_Worp : MonoBehaviour
//{  
    
//    public Vector2 StartingPoint;
//    public Vector3 Dist_Camera;
//    public bool Enter_Dungeon;
//    public bool In_Gate;

//    //private void OnTriggerEnter2D(Collider2D collision)
//    //{
       
//    //    if (collision.gameObject.tag == "Player")
//    //    {
//    //        collision.transform.position = StartingPoint;

//    //        Debug.Log("In");
//    //    }
//    //}
//    private void OnCollisionEnter2D(Collision2D collision)
//    {
//        if (collision.gameObject.tag == "Player")
//        {
//            collision.transform.position = StartingPoint;

//            Enter_Dungeon = true;

//            In_Gate = false;

//            Debug.Log("In");
//        }
//    }

//    // Start is called before the first frame update
//    void Start()
//    {
//        Enter_Dungeon = false;        
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        In_Gate = true;
//    }
//}
