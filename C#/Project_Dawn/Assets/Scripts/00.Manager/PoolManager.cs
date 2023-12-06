using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region Pool

class Pool
{
    private Stack<Poolable> _poolStack = new Stack<Poolable>();
    public GameObject Original { get; private set; }

    private Stack<Poolable> _poolStacks = new Stack<Poolable>();

    public void Init(GameObject _original, int count = 5)
    {
        Original = _original;
        
        


    }
    
    private void Push(Poolable poolable)
    {
        
        
        
        
    }

    private void Pop(Poolable poolable)
    {
        
        
        
        
    }

   
}

#endregion Pool





public class PoolManager
{
    private Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();
    private Transform _root;


    public void Init()
    {
        if (_root == null)
        {
            _root = new GameObject { name = "@Pool_Root" }.transform;
            Object.DontDestroyOnLoad(_root);
        }
    }
}