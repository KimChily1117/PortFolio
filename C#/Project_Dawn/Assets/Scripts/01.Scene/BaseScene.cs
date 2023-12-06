using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public abstract class BaseScene : MonoBehaviour
{
 
    
    // Start is called before the first frame update
    protected virtual void Start()
    {
         Initialize();
    }

    protected abstract void Initialize(); // 추상 클래스 , 함수로 선언하여서 각 Scene마다의 초기화 동작을 선언하도록 만들어놓음.
}
