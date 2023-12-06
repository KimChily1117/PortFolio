using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputBuffer
{
    // 입력 버퍼 관련 변수들
    private float inputBufferTime = 0.2f; // 입력 버퍼의 시간 윈도우 (초 단위)
    private float lastInputTime = 0f;     // 마지막 입력 시간

    // 현재 콤보 어택 상태를 나타내는 변수들
    private int currentComboIndex = 0;  // 현재 콤보 인덱스
    private float comboTimeWindow = 0.8f; // 콤보 어택 유효 시간 (초 단위)
    private float lastComboTime = 0f;   // 마지막 콤보 어택 시간


    

    public InputBuffer()
    {

        GameManager.Input.inputTypeAction = null;
        GameManager.Input.inputTypeAction += Attack;

    }



    /// <summary>
    /// InputBuffer코드에서 입력을 매프레임마다 처리하는 함수
    /// </summary>
    public void Tick()
    {
        
    }


    // imputManager에서 Input이 발생했을때. callback으로 지금 무슨 Type의 input이 발생했는지 넘겨준다.
    private void Attack(Define.InputType input)
    {

        // 특정 액션 실행
        Debug.Log("Action: " + input.ToString());

        // 콤보 어택 관련 변수 초기화
        if (Time.time - lastInputTime > inputBufferTime)
            currentComboIndex = 0;

        lastInputTime = Time.time;

    }





    //private void CheckComboAttack()
    //{
    //    // 콤보 어택 유효 시간을 초과한 경우 콤보 인덱스 초기화
    //    if (Time.time - lastComboTime > comboTimeWindow)
    //        currentComboIndex = 0;

    //    // 콤보 어택 체크
    //    if (currentComboIndex < 3)//comboInputs.Length
    //    {
    //        // 입력 배열의 순서대로 검사하여 콤보 어택이 발동되는지 확인
    //        if (Input.GetKeyDown(comboInputs[currentComboIndex]))
    //        {
    //            currentComboIndex++;

    //            // 마지막 콤보 어택 시간 업데이트
    //            lastComboTime = Time.time;

    //            // 모든 콤보 어택이 발동되었는지 체크
    //            if (currentComboIndex == comboInputs.Length)
    //            {
    //                // 콤보 어택 발동
    //                Debug.Log("Combo Attack!");
    //                // 이후에 추가적인 동작을 여기에 추가할 수 있습니다.
    //            }
    //        }
    //    }
    //}


}
