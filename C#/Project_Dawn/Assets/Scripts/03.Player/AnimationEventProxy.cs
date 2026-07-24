using Character;
using UnityEngine;

public class AnimationEventProxy : MonoBehaviour
{
    private BaseCharacter _owner;
    private MyPlayer _myPlayer;

    private void Awake()
    {
        CacheOwner();
    }

    private void Start()
    {
        CacheOwner();
    }

    public void AnimEvent_BasicAttackHitFrame()
    {
        CacheOwner();

        if (_owner == null)
        {
            Debug.LogWarning($"[CLIENT][ANIM_EVENT_PROXY_FAILED] Reason=OwnerNotFound, GameObject={name}");
            return;
        }

        if (_myPlayer == null)
        {
            Debug.Log($"[CLIENT][ANIM_EVENT_PROXY_IGNORED] Reason=OwnerIsNotMyPlayer, GameObject={name}, Owner={_owner.name}, OwnerType={_owner.GetType().Name}");
            return;
        }

        Debug.Log($"[CLIENT][ANIM_EVENT_PROXY] Forward=AnimEvent_BasicAttackHitFrame, GameObject={name}, Player={_myPlayer.name}");
        _myPlayer.AnimEvent_BasicAttackHitFrame();
    }

    private void CacheOwner()
    {
        if (_owner == null)
            _owner = GetComponentInParent<BaseCharacter>();

        if (_myPlayer == null)
            _myPlayer = _owner as MyPlayer ?? GetComponentInParent<MyPlayer>();
    }
}