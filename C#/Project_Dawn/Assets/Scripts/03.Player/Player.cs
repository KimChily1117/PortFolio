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



    // * 점프를 위해서 따로 선언

    public float jumpHeight = 2f;
    // 점프력
    public float jumpDuration = 1.0f;
    // 점프 할 시간을 정함

    private bool isJumping = false;
    private float jumpTimer = 0.0f;
    public Vector2 initialPosition;
    // 점프를 시작한 최초의 위치 백터

    // **  Input Buffer **

    InputBuffer _inputBuffer;



    // Atk

    // 입력 버퍼 관련 변수들
    private float inputBufferTime = 0.2f; // 입력 버퍼의 시간 윈도우 (초 단위)
    private float lastInputTime = 0f;     // 마지막 입력 시간

    public int currentAtkCount;
    private float comboTimeWindow = 0.8f; // 콤보 어택 유효 시간 (초 단위)
    private float lastComboTime = 0f;   // 마지막 콤보 어택 시간



    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // 장비에 따라 여기서 스텟 값을 바꿔준다.
    }

    private void Start()
    {
        Debug.Log($"Start Test");

        _inputBuffer = new InputBuffer();


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
            case Define.PlayerState.ATKIDLE:
                ProcAtkIdle();
                break;
            case Define.PlayerState.ATK1:
                ProcAtkPlayer();
                break;
            case Define.PlayerState.ATK2:
                ProcAtkPlayer();
                break;
            case Define.PlayerState.ATK3:
                ProcAtkPlayer();
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

            GameManager.Input.SetInputKeyCode(KeyCode.X);
            //_state = Define.PlayerState.ATKIDLE;

            if (_state == Define.PlayerState.IDLE ||
                _state == Define.PlayerState.ATKIDLE)
            {
                // 콤보 어택 관련 변수 초기화
                if (Time.time - lastInputTime > inputBufferTime)
                    currentAtkCount = 0;

                lastInputTime = Time.time;

                _state = Define.PlayerState.ATK1;
            }

            // 최초 Idle에서 평타키를 눌렀을 때 or 평타 대기 상태에서 평타키를 눌렀을 떄.
            // Atk 1타 상타로 진입함.

            if (_state == Define.PlayerState.JUMP)
                // To-do : 점프 상태에서 점프 평타 모션 넣어야함
                return;
            if (_state == Define.PlayerState.RUN)
                // To-do : 대쉬 상태에서 대쉬 평타 모션 넣어야함
                return;
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
            if(_state.ToString().Contains("ATK"))
            {
                return;
            }
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

        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);

    }

    public void ProcWalkPlayer()
    {
        Debug.Log($"walk State");
        _animator.SetBool("isWalk", true);
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

        _animator.SetBool("isRun", true);
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

            // To-do 여기다가 키보드 입력시 x축으로 움직이는 code 추가필요

            _animator.SetBool("isJump", true);
        }
        else
        {
            isJumping = false;
            jumpTimer = 0.0f;
            transform.position = initialPosition;
            _animator.SetBool("isJump", false);
            _state = Define.PlayerState.IDLE;

        }


    }
        

    public void ProcAtkIdle()
    {
        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        _animator.SetBool("isAtkIdle",true);
        currentAtkCount = 0;
    }

    public void ProcAtkPlayer()
    {
        if (Time.time - lastComboTime > comboTimeWindow)
        {
            lastComboTime = Time.time;
            switch (currentAtkCount)
            {
                case 0:
                    Debug.Log($"Atk1");
                    currentAtkCount++;
                    _state = Define.PlayerState.ATK2;

                    break;
                case 1:
                    Debug.Log($"Atk2");
                    currentAtkCount++;
                    _state = Define.PlayerState.ATK3;

                    break;
                case 2:
                    Debug.Log($"Atk3");
                    currentAtkCount = 0;
                    _state = Define.PlayerState.ATKIDLE;
                    break;              
            }

        }
    }


    #endregion


}
