using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_EventHandler : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public Action<PointerEventData> ClickEventAction;
    public Action<PointerEventData> DragEventAction;

    public void OnDrag(PointerEventData eventData)
    {
        if (DragEventAction != null)
        {
            DragEventAction.Invoke(eventData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ClickEventAction != null) { ClickEventAction.Invoke(eventData); }
    }

}
