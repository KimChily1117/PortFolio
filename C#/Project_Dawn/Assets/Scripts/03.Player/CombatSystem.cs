using Character;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.Protobuf.Protocol;


public class CombatSystem : MonoBehaviour 
{
    public bool canatteck = false;
    public Collider2D inLineCollider;
    public LayerMask enemyLayer;
    public MyPlayer _player;


    private ContactFilter2D contactFilter;
    public List<Collider2D> cols = new List<Collider2D>();
    
    public void Init(MyPlayer player)
    {
        _player = player;

        enemyLayer = 1 << LayerMask.NameToLayer("Enemy");  // Player ���̾ �浹 üũ��
        contactFilter.SetLayerMask(enemyLayer);



    }

    // Update is called once per frame
    public void OnUpdate()
    {
        Debug.Log($"Combat Update");
        if (_player._state == PlayerState.Atk)
        {
            inLineCollider.OverlapCollider(contactFilter, cols);
            
            foreach(Collider2D col in cols)
            {
                Debug.Log(col.transform.name);

                if(Mathf.Abs(col.transform.position.y) - 
                    Mathf.Abs(_player._shadowObject.transform.position.y) < 0.02f)
                {
                    if (col.TryGetComponent(out SpriteRenderer spriteRenderer))
                    {
                        spriteRenderer.color = Color.red;
                    }
                }
            }

        }
    }
}
