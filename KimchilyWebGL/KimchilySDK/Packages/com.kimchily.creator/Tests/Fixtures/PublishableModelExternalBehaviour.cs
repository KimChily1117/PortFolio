using UnityEngine;

namespace Kimchily.Creator.Editor.Tests
{
    [ExecuteAlways]
    public sealed class PublishableModelExternalBehaviour : MonoBehaviour
    {
        public static int EnableCount;
        void OnEnable() { EnableCount++; }
    }
}
