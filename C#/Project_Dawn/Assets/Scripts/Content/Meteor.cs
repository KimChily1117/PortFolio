using Character;
using Google.Protobuf.Protocol;
using UnityEngine;

public class Meteor : MonoBehaviour
{
    [SerializeField] private Transform _visual;
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _startHeight = 6.5f;
    [SerializeField] private float _fallSpeed = 2.6777f;
    [SerializeField] private float _meteorHalfWidth = 1.1f;
    [SerializeField] private float _meteorHalfDepth = 0.4f;

    private BoxCollider2D _boxCollider2D;
    private Transform _generatedVisualRoot;
    private bool _hasImpacted;
    private bool _hitSent;

    private void OnEnable()
    {
        _boxCollider2D = GetComponent<BoxCollider2D>();
        if (_boxCollider2D != null)
            _boxCollider2D.enabled = false;

        _visual = ResolveVisualTransform();
        ResetMeteorState();
    }

    public void InitializeGroundImpact(Vector3 groundCenter)
    {
        transform.position = groundCenter;
        SetVisualStartPosition();
        Debug.Log($"[METEOR][SPAWN] GroundCenter={FormatVector2(transform.position)}, VisualStartHeight={_startHeight:0.00}");
    }

    private void Update()
    {
        if (_hasImpacted)
            return;

        if (_visual == null)
        {
            Debug.LogWarning($"[METEOR][HIT_SKIP] Reason=NoVisual, Meteor={name}");
            ImpactOnce();
            return;
        }

        _visual.localPosition += Vector3.down * Time.deltaTime * _fallSpeed;

        if (_visual.localPosition.y <= 0f)
            ImpactOnce();
    }

    private void ImpactOnce()
    {
        if (_hasImpacted)
        {
            Debug.Log($"[METEOR][HIT_SKIP] Reason=DuplicateImpact, Center={FormatVector2(transform.position)}");
            return;
        }

        _hasImpacted = true;

        if (_visual != null)
            _visual.localPosition = Vector3.zero;

        Vector3 center = transform.position;
        Debug.Log($"[METEOR][IMPACT] Center={FormatVector2(center)}");

        SpawnExplosion(center);
        TrySendHitCandidate(center);
        Destroy(gameObject);
    }

    private void TrySendHitCandidate(Vector3 meteorCenter)
    {
        if (GameManager.ObjectManager == null || GameManager.ObjectManager.MyPlayer == null)
        {
            Debug.Log("[METEOR][HIT_SKIP] Reason=NoMyPlayer");
            return;
        }

        BaseCharacter myPlayer = GameManager.ObjectManager.MyPlayer;
        Vector3 playerAnchor = ResolvePlayerAnchor(myPlayer, out string anchorSource);

        float dx = Mathf.Abs(playerAnchor.x - meteorCenter.x);
        float dy = Mathf.Abs(playerAnchor.y - meteorCenter.y);
        bool hit = dx <= _meteorHalfWidth && dy <= _meteorHalfDepth;

        Debug.Log($"[METEOR][AABB_CHECK] Center={FormatVector2(meteorCenter)}, PlayerAnchor={FormatVector2(playerAnchor)}, AnchorSource={anchorSource}, Dx={dx:0.00}, Dy={dy:0.00}, HalfWidth={_meteorHalfWidth:0.00}, HalfDepth={_meteorHalfDepth:0.00}, Hit={hit}");

        if (!hit)
        {
            Debug.Log("[METEOR][HIT_SKIP] Reason=OutsideAABB");
            return;
        }

        if (_hitSent)
        {
            Debug.Log("[METEOR][HIT_SKIP] Reason=DuplicateHit");
            return;
        }

        C_Collision cCollision = new C_Collision
        {
            Playerinfo = new ObjectInfo
            {
                ObjectId = myPlayer.Id,
                Damage = _damage
            }
        };

        GameManager.Network.Send(cCollision);
        _hitSent = true;

        Debug.Log($"[METEOR][HIT_SEND] Player={myPlayer.ObjInfo?.Name}, ObjectId={myPlayer.Id}, Damage={_damage:0.##}");
    }

    private Vector3 ResolvePlayerAnchor(BaseCharacter myPlayer, out string source)
    {
        if (myPlayer._shadowObject != null)
        {
            source = "Shadow";
            return myPlayer._shadowObject.transform.position;
        }

        source = "TransformFallback";
        return myPlayer.transform.position;
    }

    private void SpawnExplosion(Vector3 position)
    {
        GameObject explosionEffect = GameManager.Resources.Instantiate("Effect/ExplosionEffect");
        explosionEffect.transform.position = position;
        explosionEffect.GetComponent<Animator>().Play("bakal_skill_explosion_effect");
        GameManager.Sound.Play("Sounds/mon/bakal/bakal_dragon_3phase_meteor_exp_01");
        Destroy(explosionEffect, 1.5f);
    }

    private void ResetMeteorState()
    {
        _hasImpacted = false;
        _hitSent = false;
        SetVisualStartPosition();
    }

    private void SetVisualStartPosition()
    {
        if (_visual == null)
            return;

        _visual.localPosition = new Vector3(0f, _startHeight, _visual.localPosition.z);
    }

    private Transform ResolveVisualTransform()
    {
        if (_visual != null && _visual != transform)
            return _visual;

        Transform existingRoot = transform.Find("MeteorVisualRoot");
        if (existingRoot != null)
            return existingRoot;

        if (transform.childCount == 0)
            return null;

        GameObject visualRootObject = new GameObject("MeteorVisualRoot");
        _generatedVisualRoot = visualRootObject.transform;
        _generatedVisualRoot.SetParent(transform, false);
        _generatedVisualRoot.localPosition = Vector3.zero;
        _generatedVisualRoot.localRotation = Quaternion.identity;
        _generatedVisualRoot.localScale = Vector3.one;

        Transform[] children = new Transform[transform.childCount - 1];
        int index = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == _generatedVisualRoot)
                continue;

            children[index++] = child;
        }

        for (int i = 0; i < index; i++)
        {
            if (children[i] != null)
                children[i].SetParent(_generatedVisualRoot, true);
        }

        return _generatedVisualRoot;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Meteor damage is resolved once at ground impact by AABB against MyPlayer's combat anchor.
    }

    private string FormatVector2(Vector3 value)
    {
        return $"({value.x:0.00},{value.y:0.00})";
    }
}


