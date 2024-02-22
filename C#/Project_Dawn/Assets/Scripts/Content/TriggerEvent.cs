using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    private Action<Collider2D> _enterAction;
    private Action<Collider2D> _stayAction;
    private Action<Collider2D> _exitAction;


    public void AddTriggerEnterEvent(Action<Collider2D> enterAction) 
    { 
        _enterAction -= enterAction;
        _enterAction += enterAction;
    
    }
    public void AddTriggerStayEvent(Action<Collider2D> stayAction) 
    { 
        _stayAction -= stayAction;
        _stayAction += stayAction;
    }
    public void AddTriggerExitEvent(Action<Collider2D> exitAction)
    {
        _exitAction -= exitAction;
        _exitAction += exitAction;
    }
    public void AddTriggerEvent(Action<Collider2D> enterAction = null, Action<Collider2D> stayAction = null, Action<Collider2D> exitAction = null)
    {
        _enterAction = enterAction;
        _stayAction = stayAction;
        _exitAction = exitAction;
    }

    public void RemoveTriggerEnterEvent(Action<Collider2D> exitAction) 
    { 
        _exitAction -= exitAction;
    }
    public void RemoveTriggerStayEvent(Action<Collider2D> exitAction) 
    {
        _exitAction -= exitAction; 
    }
    public void RemoveTriggerExitEvent(Action<Collider2D> exitAction) 
    {
        _exitAction -= exitAction;
    }
    public void ClearTriggerEvent()
    {
        _enterAction = null;
        _stayAction = null;
        _exitAction = null;
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        _enterAction?.Invoke(collision);
    }

    //private void OnTriggerEnter(Collider other)
    //{
    ////    if (IsTriggerNoneAction(other))
    ////        return;

    //    _enterAction?.Invoke(other);
    //}
    //private void OnTriggerStay(Collider other)
    //{
    //    //if (IsTriggerNoneAction(other))
    //    //    return;

    //    _stayAction?.Invoke(other);
    //}
    //private void OnTriggerExit(Collider other)
    //{       
    //    _exitAction?.Invoke(other);
    //}
}



