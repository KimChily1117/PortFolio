using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


public class CoroutineQueue : MonoBehaviour
{
    //public Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();

    //bool isRunning;


    //public void Push(IEnumerator _routine)
    //{        
    //    coroutineQueue.Enqueue(_routine);

    //    if (isRunning == false)
    //    {
    //        isRunning = true;
    //    }

    //    if (isRunning)
    //        StartCoroutine(Flush());
    //}


    //private IEnumerator Flush()
    //{
    //    while (true)
    //    {
    //        IEnumerator routine = Pop();
    //        if (isRunning == false)
    //            yield break;                    

    //        yield return StartCoroutine(routine);
    //    }
    //}

    //private IEnumerator Pop()
    //{
    //    if(coroutineQueue.Count == 0)
    //    {
    //        isRunning = false;
    //        return null;
    //    }

    //    return coroutineQueue.Dequeue();
    //}
}

