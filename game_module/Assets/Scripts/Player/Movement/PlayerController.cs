using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Walk,
        Die
    }

    #region variables
    [SerializeField] private float _speed = 10f;
    Vector3 _destPos;

    public PlayerState _state;
    float wait_run_ratio = 0;   


    #endregion 


    



    // Start is called before the first frame update
    void Start()
    {
        //GameManager.Input.KeyAction -= OnKeyBoard;
        //GameManager.Input.KeyAction += OnKeyBoard;

        GameManager.Input.MouseAction -= OnMouseClicked;
        GameManager.Input.MouseAction += OnMouseClicked;
        _state = PlayerState.Idle;
    }


    private void Update()
    {

        switch (_state)
        {
            case PlayerState.Idle:
                ProcIdle();
                break;
            case PlayerState.Walk:
                ProcWalk();
                break;
            case PlayerState.Die:
                ProcDie();
                break;
           
        }        
    }

    private void OnMouseClicked(Define.MouseEvent evt)
    {
        if (_state == PlayerState.Die)
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Debug.DrawRay(Camera.main.transform.position, ray.direction * 100.0f, Color.red,
            1.0f);

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, LayerMask.GetMask("Floor")))
        {
            _destPos = hit.point;
            _state = PlayerState.Walk;
        }

        Debug.Log($"OnMouseClicked!");
    }
    private void ProcIdle()
    {
        // Animation
        Animator anim = GetComponent<Animator>();
        wait_run_ratio = Mathf.Lerp(wait_run_ratio, 0, 10.0f * Time.deltaTime);
        anim.SetFloat("wait_run_ratio", wait_run_ratio);

        anim.Play("WAIT_RUN");
        //
    }

    private void ProcWalk()
    {
        if (_state == PlayerState.Walk)
        {
            Vector3 dir = _destPos - this.transform.position; // 방향백터 추출;
            if (dir.magnitude < 0.00001f)
            {
                _state = PlayerState.Idle;
            }
            else
            {
                float movedist = Mathf.Clamp(_speed * Time.deltaTime, 0, dir.magnitude);
                transform.position += dir.normalized * movedist;

                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10 * Time.deltaTime);
                //transform.LookAt(_destPos);
            }

            Animator anim = GetComponent<Animator>();

            wait_run_ratio = Mathf.Lerp(wait_run_ratio, 1, 10.0f * Time.deltaTime);

            anim.SetFloat("wait_run_ratio", wait_run_ratio);
            anim.Play("WAIT_RUN");

        }
    }  
    private void ProcDie()
    {
        throw new NotImplementedException();
    }


    void OnKeyBoard() // Not Use
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation,
                Quaternion.LookRotation(Vector3.forward)
                , 0.2f);
            transform.position += Vector3.forward * Time.deltaTime * _speed;
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation,
               Quaternion.LookRotation(Vector3.back)
               , 0.2f);
            transform.position += Vector3.back * Time.deltaTime * _speed;

        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation,
               Quaternion.LookRotation(Vector3.left)
               , 0.2f);
            transform.position += Vector3.left * Time.deltaTime * _speed;

        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation,
               Quaternion.LookRotation(Vector3.right)
               , 0.2f);
            transform.position += Vector3.right * Time.deltaTime * _speed;
        }
    }
}
