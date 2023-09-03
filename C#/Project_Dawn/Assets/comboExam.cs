using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class comboExam : MonoBehaviour
{
    [Header("Inputs")] 
    
    public KeyCode firstcode; 
    public KeyCode Secondcode; 
    public KeyCode Thirdscode; 
    public KeyCode Fourthcode; 

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}



[System.Serializable] //클래스에 serializeable을 붙이는 이유?에 대해서 공부해서 여기다가 적어보기( ~ 23.08.01)

public class Attack
{
    public string name;
    // 애니메이션 클립에 대한 이름 정보
    public float length;
    // 애니메이션 재생 시간에 대한 길이 정보
}






