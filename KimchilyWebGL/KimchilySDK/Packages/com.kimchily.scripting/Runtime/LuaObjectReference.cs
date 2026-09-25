using System;
using UnityEngine;

namespace Kimchily.Scripting
{
    [Serializable]
    public sealed class LuaObjectReference
    {
        public string name;
        public GameObject target;

        public LuaObjectReference() { }
        public LuaObjectReference(string name, GameObject target)
        {
            this.name = name;
            this.target = target;
        }
    }
}
