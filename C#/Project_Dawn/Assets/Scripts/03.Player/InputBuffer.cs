using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputBuffer
{
    // �Է� ���� ���� ������
    private float inputBufferTime = 0.2f; // �Է� ������ �ð� ������ (�� ����)
    private float lastInputTime = 0f;     // ������ �Է� �ð�

    // ���� �޺� ���� ���¸� ��Ÿ���� ������
    private int currentComboIndex = 0;  // ���� �޺� �ε���
    private float comboTimeWindow = 0.8f; // �޺� ���� ��ȿ �ð� (�� ����)
    private float lastComboTime = 0f;   // ������ �޺� ���� �ð�


    

    public InputBuffer()
    {

        GameManager.Input.inputTypeAction = null;
        GameManager.Input.inputTypeAction += Attack;

    }



    /// <summary>
    /// InputBuffer�ڵ忡�� �Է��� �������Ӹ��� ó���ϴ� �Լ�
    /// </summary>
    public void Tick()
    {
        
    }


    // imputManager���� Input�� �߻�������. callback���� ���� ���� Type�� input�� �߻��ߴ��� �Ѱ��ش�.
    private void Attack(Define.InputType input)
    {

        // Ư�� �׼� ����
        Debug.Log("Action: " + input.ToString());

        // �޺� ���� ���� ���� �ʱ�ȭ
        if (Time.time - lastInputTime > inputBufferTime)
            currentComboIndex = 0;

        lastInputTime = Time.time;

    }





    //private void CheckComboAttack()
    //{
    //    // �޺� ���� ��ȿ �ð��� �ʰ��� ��� �޺� �ε��� �ʱ�ȭ
    //    if (Time.time - lastComboTime > comboTimeWindow)
    //        currentComboIndex = 0;

    //    // �޺� ���� üũ
    //    if (currentComboIndex < 3)//comboInputs.Length
    //    {
    //        // �Է� �迭�� ������� �˻��Ͽ� �޺� ������ �ߵ��Ǵ��� Ȯ��
    //        if (Input.GetKeyDown(comboInputs[currentComboIndex]))
    //        {
    //            currentComboIndex++;

    //            // ������ �޺� ���� �ð� ������Ʈ
    //            lastComboTime = Time.time;

    //            // ��� �޺� ������ �ߵ��Ǿ����� üũ
    //            if (currentComboIndex == comboInputs.Length)
    //            {
    //                // �޺� ���� �ߵ�
    //                Debug.Log("Combo Attack!");
    //                // ���Ŀ� �߰����� ������ ���⿡ �߰��� �� �ֽ��ϴ�.
    //            }
    //        }
    //    }
    //}


}
