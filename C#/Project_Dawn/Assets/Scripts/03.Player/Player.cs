using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using Unity.VisualScripting;

public class Player : BaseCharacter
{
    [SerializeField] private Vector2 _vec;

    public Define.PlayerState _state = Define.PlayerState.NONE;

    private float _speed = 1.0f;
    
    
    
    // 점프를 위해서 따로 선언
    public float jumpHeight = 2f;
    public float jumpDuration = 1.0f;
    // 점프 할 시간을 정함
    private bool isJumping = false;
    private float jumpTimer = 0.0f;
    public Vector2 initialPosition;

    
    
    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // 장비에 따라 여기서 스텟 값을 바꿔준다.
    }

    private void Start()
    {
        Debug.Log($"Start Test");
        
        GameManager.Input.KeyDownAction -= OnKeyDownMoveAction;
        GameManager.Input.KeyDownAction += OnKeyDownMoveAction;


        GameManager.Input.KeyDownAction -= OnKeyAction;
        GameManager.Input.KeyDownAction += OnKeyAction;
        
        
        GameManager.Input.KeyUpAction -= OnKeyUpAction;
        GameManager.Input.KeyUpAction += OnKeyUpAction;
        
        

        GameManager.Input.DoubleKeyAction -= DoubleKeyAction;
        GameManager.Input.DoubleKeyAction += DoubleKeyAction;

        _state = Define.PlayerState.NONE;
        _moveDir = Vector2.zero;
        _SpriteRenderer = this.GetOrAddComponent<SpriteRenderer>();
        _animator = this.GetOrAddComponent<Animator>();
    }

    private void Update()
    {
        switch (_state)
        {
            case Define.PlayerState.NONE:
                break;
            case Define.PlayerState.IDLE:
                ProcIdlePlayer();
                break;
            case Define.PlayerState.WALK:
                ProcWalkPlayer();
                break;
            case Define.PlayerState.RUN:
                ProcRunPlayer();
                break;
            case Define.PlayerState.JUMP:
                ProcJumpPlayer();
                break;
            case Define.PlayerState.ATK1:
                break;
            case Define.PlayerState.ATK2:
                break;
            case Define.PlayerState.ATK3:
                break;
        }
    }

    public void OnKeyDownMoveAction()
    {

        if (Input.GetKey(KeyCode.UpArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"up");
            _moveDir = Vector2.up;
            GameManager.Input.SetInputKeyCode(KeyCode.UpArrow);
            transform.Translate(_moveDir * Time.deltaTime * _speed);

        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"down");
            _moveDir = Vector2.down;
            GameManager.Input.SetInputKeyCode(KeyCode.DownArrow);
            transform.Translate(_moveDir * Time.deltaTime * _speed);

        }
        
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"left");
            _moveDir = Vector2.left;
            GameManager.Input.SetInputKeyCode(KeyCode.LeftArrow);
            transform.Translate(_moveDir * Time.deltaTime * _speed);

            
        }
        
        if (Input.GetKey(KeyCode.RightArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"right");
            _moveDir = Vector2.right;
            GameManager.Input.SetInputKeyCode(KeyCode.RightArrow);
            transform.Translate(_moveDir * Time.deltaTime * _speed);
        }
        
    }


    private void OnKeyAction()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            // 공격 버튼
        }
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (_state == Define.PlayerState.JUMP) 
                return;
            
            initialPosition = transform.position;

            _state = Define.PlayerState.JUMP;
            
        }
    }




    public void OnKeyUpAction()
    {
        if (_state != Define.PlayerState.JUMP)
        {
            _state = Define.PlayerState.IDLE;
        }
    }

    public void DoubleKeyAction()
    {
        _state = Define.PlayerState.RUN;
    }
    

    #region ProcMethod
    
    public void ProcIdlePlayer()
    {
        _speed = 1.0f;

        _animator.SetBool("isWalk",false);
        _animator.SetBool("isRun",false);

    }
    
    public void ProcWalkPlayer()
    {
        Debug.Log($"walk State");
        _animator.SetBool("isWalk",true);
        if (_moveDir.x <= -1)
            _SpriteRenderer.flipX = true;
        else
        {
            _SpriteRenderer.flipX = false;
        }

    }
    
    public void ProcRunPlayer()
    {
        _speed = 2.25f;

        _animator.SetBool("isRun",true);
        transform.Translate(_moveDir * Time.deltaTime * _speed);
        if (_moveDir.x <= -1)
            _SpriteRenderer.flipX = true;
        else
        {
            _SpriteRenderer.flipX = false;
        }
    }

    public void ProcJumpPlayer()
    {

        jumpTimer += Time.deltaTime;
        //Debug.Log($"Jump Timer : {jumpTimer}");
        
        
        if (jumpTimer <= jumpDuration)
        {
            float jumpProgress = jumpTimer / jumpDuration;
            float yOffset = Mathf.Sin(jumpProgress * Mathf.PI) * jumpHeight;
            transform.position = initialPosition + new Vector2(0, yOffset);
            _animator.SetBool("isJump",true);
        }
        else
        {
            isJumping = false;
            jumpTimer = 0.0f;
            transform.position = initialPosition;
            _animator.SetBool("isJump",false);
            _state = Define.PlayerState.IDLE;

        }
        
        
    }
    
    
    
    #endregion
    
    
}
