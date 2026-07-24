using System;
using System.Collections.Generic;
using UnityEngine;

#region Pool

class Pool
{
    private readonly Stack<Poolable> _poolStack = new Stack<Poolable>();
    private Transform _root;

    public string Key { get; private set; }
    public GameObject Original { get; private set; }

    public void Init(string key, GameObject original, Transform root, int count = 5)
    {
        Key = key;
        Original = original;
        _root = new GameObject { name = $"{key}_Pool" }.transform;
        _root.SetParent(root);

        for (int i = 0; i < count; i++)
            Push(Create());
    }

    public Poolable Pop()
    {
        Poolable poolable = _poolStack.Count > 0 ? _poolStack.Pop() : Create();
        poolable.IsUsing = true;
        poolable.transform.SetParent(null);
        poolable.gameObject.SetActive(true);
        return poolable;
    }

    public void Push(Poolable poolable)
    {
        if (poolable == null)
            return;

        poolable.IsUsing = false;
        poolable.transform.SetParent(_root);
        poolable.gameObject.SetActive(false);
        _poolStack.Push(poolable);
    }

    private Poolable Create()
    {
        GameObject go = UnityEngine.Object.Instantiate(Original);
        go.name = Original.name;
        Poolable poolable = go.GetComponent<Poolable>();
        if (poolable == null)
            poolable = go.AddComponent<Poolable>();

        poolable.PoolKey = Key;
        return poolable;
    }
}

#endregion Pool

public class PoolManager
{
    private readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();
    private readonly HashSet<string> _missingPrefabKeys = new HashSet<string>();
    private Transform _root;

    public void Init()
    {
        if (_root == null)
        {
            _root = new GameObject { name = "@Pool_Root" }.transform;
            UnityEngine.Object.DontDestroyOnLoad(_root.gameObject);
        }
    }

    public GameObject Pop(string key, int preloadCount = 3)
    {
        Init();

        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (_pools.TryGetValue(key, out Pool pool) == false)
        {
            if (CreatePool(key, preloadCount) == false)
                return null;

            pool = _pools[key];
        }

        return pool.Pop().gameObject;
    }

    public GameObject Pop(string key, Func<GameObject> createOriginal, int preloadCount = 3)
    {
        Init();

        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (_pools.TryGetValue(key, out Pool pool) == false)
        {
            if (CreateRuntimePool(key, createOriginal, preloadCount) == false)
                return null;

            pool = _pools[key];
        }

        return pool.Pop().gameObject;
    }

    public void Push(GameObject go)
    {
        if (go == null)
            return;

        Poolable poolable = go.GetComponent<Poolable>();
        if (poolable == null || string.IsNullOrWhiteSpace(poolable.PoolKey))
        {
            UnityEngine.Object.Destroy(go);
            return;
        }

        if (_pools.TryGetValue(poolable.PoolKey, out Pool pool) == false)
        {
            UnityEngine.Object.Destroy(go);
            return;
        }

        pool.Push(poolable);
    }

    public bool CreatePool(string key, int count = 3)
    {
        Init();

        if (_pools.ContainsKey(key))
            return true;

        if (_missingPrefabKeys.Contains(key))
            return false;

        GameObject prefab = Resources.Load<GameObject>($"Prefabs/{key}");
        if (prefab == null)
        {
            _missingPrefabKeys.Add(key);
            Debug.LogWarning($"[POOL] Prefab not found. Key={key}, Path=Resources/Prefabs/{key}");
            return false;
        }

        Pool pool = new Pool();
        pool.Init(key, prefab, _root, Mathf.Max(0, count));
        _pools.Add(key, pool);
        return true;
    }

    public bool CreateRuntimePool(string key, Func<GameObject> createOriginal, int count = 3)
    {
        Init();

        if (_pools.ContainsKey(key))
            return true;

        if (createOriginal == null)
            return false;

        GameObject original = createOriginal();
        if (original == null)
            return false;

        original.name = key;
        original.SetActive(false);
        original.transform.SetParent(_root);

        Poolable poolable = original.GetComponent<Poolable>();
        if (poolable == null)
            poolable = original.AddComponent<Poolable>();

        poolable.PoolKey = key;
        poolable.IsUsing = false;

        Pool pool = new Pool();
        pool.Init(key, original, _root, Mathf.Max(0, count));
        _pools.Add(key, pool);
        return true;
    }
}