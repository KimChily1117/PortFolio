using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_MobileController : UI_Scene
{
    private JoystickController _joystickController;
    protected RectTransform rectTransform { get; set; }
    protected override void Start()
    {
        base.Start();
        _joystickController = GetComponent<JoystickController>();


        rectTransform = GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(0, 0);

    }





}
