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
        get { return PositionInfo.State; }
        set 
        {

            Debug.Log($"Posinfo ?? : {value}");
            if (_positionInfo.State == PlayerState.Moving || _positionInfo.State == PlayerState.Run)
            {
                if (value == PlayerState.Jump)
                {
                    _positionInfo.State = value;
                    ProcJumpPlayer(true);
                    return;
                }
            }
            base._state = value;

            //_updated = true;
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

    public override void ProcJumpPlayer(bool isMoving = false)
    {
        //base.ProcJumpPlayer();


        if(isMoving)
        {
            CellPos += GetVecFromDir(_positionInfo.MoveDir) * _speed * Time.deltaTime;
        }
        else
        {
            CellPos += GetVecFromDir(MoveDir.None) * _speed * Time.deltaTime;
        }


        _shadowObject.transform.position = CellPos;
        initialPosition = CellPos;

        jumpTimer += Time.deltaTime;
        //Debug.Log($"Jump Timer : {jumpTimer}");


        if (jumpTimer <= jumpDuration)
        {
            isJumping = true;

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
            PositionInfo.State = PlayerState.Idle;
        }


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
            PositionInfo.State = PlayerState.Atk;
            _animator.SetTrigger($"Attack1");

        }
        if (skillId == 3) //평2
        {
            Debug.Log($"Use Skill!!! 3");
            PositionInfo.State = PlayerState.Atk;
            _animator.SetTrigger($"Attack2");


        }
        if (skillId == 4) //평3
        {
            Debug.Log($"Use Skill!!! 4");

            _animator.SetTrigger($"Attack3");
        }

    }
    public override void TakeDamage(float Damage = 0)
    {
        base.TakeDamage(Damage);

        //if (HP <= 0)
        //{
        //    _animator.SetTrigger("Die");
        //    _shadowObject.SetActive(false);
        //    GameManager.Sound.Play("Effect/Swordman/sm_die");
        //}
    }

    public override void OnDead()
    {
        _animator.SetTrigger("Die");
        _shadowObject.SetActive(false);
        GameManager.Sound.Play("Effect/Swordman/sm_die");
        base.OnDead();
    }



    protected override void Update()
    {
        base.Update();

        transform.position = new Vector2(_positionInfo.PosX, _positionInfo.PosY);
    }


}
