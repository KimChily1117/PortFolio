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
        contactFilter.useTriggers = true;
    }

    public void OnUpdate()
    {
        //Debug.Log($"Combat Update");
        if (_player._state == PlayerState.Atk && Input.anyKeyDown)
        {
            inLineCollider.OverlapCollider(contactFilter, cols);

            foreach (Collider2D col in cols)
            {
                Debug.Log(col.transform.name);

 
                if (Mathf.Abs(col.transform.position.y) -
                    Mathf.Abs(_player._shadowObject.transform.position.y) < 0.02f)
                {
                    if (col.transform.TryGetComponent(out BaseCharacter baseCharacter))
                    {
                        GiveDamage(baseCharacter);
                        //Co_spritechange = StartCoroutine(hiteffect(spriteRenderer));                        
                    }
                }
            }
    
        }
    }


    private void GiveDamage(BaseCharacter collisionChar)
    {
        C_Collision c_Collision = new C_Collision();
        c_Collision.Playerinfo = collisionChar.ObjInfo;


        GameManager.Network.Send(c_Collision);

        // �켱 ���⼭ �ǰ� ������ �����ְ� ����
        // �������� �ٽ� takeDamge Packet�� �����־ �װ����� ó���� �Ѵ�.

    }


}
