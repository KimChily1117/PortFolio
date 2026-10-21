using System;
using DnfRaidDemo;
using UnityEngine;
public static class ChatEnterChecks {
 public static void Run() {
  int checks=0;
  Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("Chat Enter: "+name);checks++;};
  var keys=new ChatEnterInput();
  check(keys.Press(false,1),"Enter opens chat");
  check(!keys.Press(true,1),"Update/OnGUI duplicate does not submit");
  check(!keys.TakeSubmit(false,2),"opening Enter does not send");
  keys.Release();keys.Press(true,3);
  check(!keys.TakeSubmit(false,3),"wait for TextField content");
  check(keys.TakeSubmit(false,4),"second Enter submits");
  check(!keys.TakeSubmit(false,5),"submit exactly once");
  check(!keys.Press(false,6),"held Enter cannot reopen");
  keys.Release();check(keys.Press(false,7),"next press reopens");
  keys.Release();keys.Press(true,8);
  check(!keys.TakeSubmit(true,9),"IME composition defers");
  check(!keys.TakeSubmit(true,10),"IME wait preserves intent");
  check(!keys.TakeSubmit(false,11),"commit frame does not send stale draft");
  check(keys.TakeSubmit(false,12),"committed Korean sends without a third Enter");
  keys.Release();keys.Press(true,13);keys.Cancel();
  check(!keys.TakeSubmit(false,15),"Escape/context switch cancels pending send");
  Debug.Log("[CHAT_ENTER_VALIDATED] "+checks);
 }
}
