using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JoystickController : DynamicJoystick , IEndDragHandler
{
    [SerializeField]
    Button AtkBtn;

    [SerializeField]
    Button JumpBtn;

    [SerializeField]

    MyPlayer myplayer;


    private void Awake()
    {
        AtkBtn.onClick.RemoveAllListeners();
        JumpBtn.onClick.RemoveAllListeners();
    }

    protected override void Start()
    {
        base.Start();
        myplayer = GameManager.ObjectManager.MyPlayer as MyPlayer;


        AtkBtn.onClick.AddListener(() => {
            GameManager.Input.TouchAttackAction.Invoke();
        });
        
        JumpBtn.onClick.AddListener(() => {
            GameManager.Input.TouchJumpAction.Invoke();
        });

    }

    private void Update()
    {
        //Debug.Log($"JoystickController : {Horizontal}, {Vertical}");

        GameManager.Input.TouchAction?.Invoke(Horizontal, Vertical);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        GameManager.Input.EndDragAction?.Invoke(eventData);
    }
}
