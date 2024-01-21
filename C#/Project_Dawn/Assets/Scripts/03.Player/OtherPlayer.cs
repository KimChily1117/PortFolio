using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using Unity.VisualScripting;
using Google.Protobuf.Protocol;

public class OtherPlayer : BaseCharacter
{

    public override PlayerState _state 
    {
        get { return PosInfo.State; }
        set 
        {          

            Debug.Log($"Posinfo ?? : {value}");
            if (PosInfo.State == PlayerState.Moving || PosInfo.State == PlayerState.Run)
            {
                if (value == PlayerState.Jump)
                {
                    PosInfo.State = value;
                    ProcJumpPlayer();
                    return;
                }
            }
            base._state = value;
        }
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // 장비에 따라 여기서 스텟 값을 바꿔준다.
    }

   



    #region ProcMethod

    public override void ProcIdlePlayer()
    {
        base.ProcIdlePlayer();

        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        //_HitBox.gameObject.SetActive(false);

    }

    public override void ProcWalkPlayer()
    {
        
        base.ProcWalkPlayer();

        Debug.Log($"Other Player : Walk Proc");
        _animator.SetBool("isWalk", true);

        CellPos = transform.position;


        //_HitBox.gameObject.SetActive(false);

    }

    public override void ProcRunPlayer()
    {      

        base.ProcRunPlayer();
        _animator.SetBool("isRun", true);

        //_HitBox.gameObject.SetActive(false);
        
    }



    public override void ProcAtk()
    {
        base.ProcAtk();
        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        _animator.SetBool("isAtkIdle", true);
    }

    #endregion


    protected override void CheckUpdatedFlag()
    {

    }


    public void UseSkill(int skillId)
    {  
        if (skillId == 2) //평1
        {
            Debug.Log($"Use Skill!!! 2");
            PosInfo.State = PlayerState.Atk;
            _animator.SetTrigger($"Attack1");

        }
        if (skillId == 3) //평2
        {
            Debug.Log($"Use Skill!!! 3");
            PosInfo.State = PlayerState.Atk;
            _animator.SetTrigger($"Attack2");


        }
        if (skillId == 4) //평3
        {
            Debug.Log($"Use Skill!!! 4");

            _animator.SetTrigger($"Attack3");
        }

    }


    public override void ProcJumpPlayer()
    {
        //base.ProcJumpPlayer();

        CellPos += GetVecFromDir(PosInfo.MoveDir) * _speed * Time.deltaTime;
        _shadowObject.transform.position = CellPos;        
        initialPosition = CellPos;

        jumpTimer += Time.deltaTime;
        //Debug.Log($"Jump Timer : {jumpTimer}");


        if (jumpTimer <= jumpDuration)
        {
            float jumpProgress = jumpTimer / jumpDuration;
            float yOffset = Mathf.Sin(jumpProgress * Mathf.PI) * jumpHeight;
            _Sprite.transform.position = initialPosition + new Vector2(0, yOffset);
            _animator.SetBool("isJump", true);

        }
        else
        {
            isJumping = false;
            jumpTimer = 0.0f;
            _Sprite.transform.position = initialPosition;
            _animator.SetBool("isJump", false);
            PosInfo.State = PlayerState.Idle;
        }


    }

}
