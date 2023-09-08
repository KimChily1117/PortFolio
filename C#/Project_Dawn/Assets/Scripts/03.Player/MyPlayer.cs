using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using Unity.VisualScripting;

public class MyPlayer : BaseCharacter
{
   
    public Define.PlayerState _state = Define.PlayerState.NONE;
    private float _speed = 1.0f;

    // * ������ ���ؼ� ���� ����

    public float jumpHeight = 2f;
    // ������
    public float jumpDuration = 1.0f;
    // ���� �� �ð��� ����

    private bool isJumping = false;
    private float jumpTimer = 0.0f;
    public Vector2 initialPosition;
    // ������ ������ ������ ��ġ ����

    // **  Input Buffer **

    InputBuffer _inputBuffer;

    public int currentAtkCount;
    private float comboTimeWindow = 0.2f; // �޺� ���� ��ȿ �ð� (�� ����)
    private float lastComboTime = 0f;   // ������ �޺� ���� �ð�


    [Header("Player HitBox")]
    public BoxCollider2D _HitBox;


    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // ��� ���� ���⼭ ���� ���� �ٲ��ش�.
    }

    private void Start()
    {
        Debug.Log($"Start Test");

        // _inputBuffer = new InputBuffer();


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

        _HitBox = Util.FindChild<BoxCollider2D>(this.gameObject, "Hitbox");

        _HitBox.gameObject.SetActive(false);

    }

    protected override void Update()
    {

        base.Update();

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

        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"down");
            _moveDir = Vector2.down;
            GameManager.Input.SetInputKeyCode(KeyCode.DownArrow);

        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"left");
            _moveDir = Vector2.left;
            GameManager.Input.SetInputKeyCode(KeyCode.LeftArrow);

        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            _state = Define.PlayerState.WALK;
            Debug.Log($"right");
            _moveDir = Vector2.right;
            GameManager.Input.SetInputKeyCode(KeyCode.RightArrow);
        }

    }


    private void OnKeyAction()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (Time.time - lastComboTime > comboTimeWindow)
            {
                lastComboTime = Time.time;
                if (currentAtkCount == 0)
                {
                    // ù ��° ����
                    _state = Define.PlayerState.ATK1;
                    _animator.SetTrigger("Attack1");
                    _HitBox.gameObject.SetActive(true);

                    currentAtkCount = 1;

                }
                else if (currentAtkCount == 1)
                {
                    _state = Define.PlayerState.ATK2;
                    _HitBox.gameObject.SetActive(true);

                    _animator.SetTrigger("Attack2");
                    // �� ��° ����
                    currentAtkCount = 2;
                }
                else if (currentAtkCount == 2)
                {
                    // �� ��° ���� (3�� ���� ����)
                    _state = Define.PlayerState.ATK3;
                    _HitBox.gameObject.SetActive(true);

                    _animator.SetTrigger("Attack3");
                    currentAtkCount = 0;

                }
            }
            // ���� Idle���� ��ŸŰ�� ������ �� or ��Ÿ ��� ���¿��� ��ŸŰ�� ������ ��.
            // Atk 1Ÿ ��Ÿ�� ������.

            if (_state == Define.PlayerState.JUMP)
                // To-do : ���� ���¿��� ���� ��Ÿ ��� �־����
                return;
            if (_state == Define.PlayerState.RUN)
                // To-do : �뽬 ���¿��� �뽬 ��Ÿ ��� �־����
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
            if (_state.ToString().Contains("ATK"))
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
        _HitBox.gameObject.SetActive(false);

    }

    public void ProcWalkPlayer()
    {
        Debug.Log($"walk State");
        _animator.SetBool("isWalk", true);
        _HitBox.gameObject.SetActive(false);


        if (_moveDir.x < 0)
            transform.localScale = new Vector3(-1, 1, 1);
        else if (_moveDir.x > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);

        }

        transform.Translate(_moveDir * Time.deltaTime * _speed);
    }

    public void ProcRunPlayer()
    {
        _speed = 2.25f;

        _animator.SetBool("isRun", true);
        _HitBox.gameObject.SetActive(false);


        if (_moveDir.x < 0)
            transform.localScale = new Vector3(-1, 1, 1);
        else if (_moveDir.x > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);

        }

        transform.Translate(_moveDir * Time.deltaTime * _speed);
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

            // To-do ����ٰ� Ű���� �Է½� x������ �����̴� code �߰��ʿ�
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
        _animator.SetBool("isAtkIdle", true);
    }

    #endregion


}
