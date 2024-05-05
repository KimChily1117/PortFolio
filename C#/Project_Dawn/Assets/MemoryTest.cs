using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MemoryTest : MonoBehaviour
{
    public Image m_Image;
    // Start is called before the first frame update
    void Start()
    {
        m_Image = this.GetComponent<Image>();
        m_Image.sprite = Resources.Load<Sprite>("chily");

        StartCoroutine(AutoDestroy());
        
    }

    IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        m_Image = null;
        
    }
}
