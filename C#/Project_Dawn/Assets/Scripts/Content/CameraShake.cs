using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    #region Singleton
    static CameraShake s_instance;

    public static CameraShake Instance
    {
        get
        {
            return s_instance;
        }
    }

    #endregion Singleton

    [SerializeField]
    float m_force = 0f;

    [SerializeField]
    Vector3 m_offset = Vector3.zero;

    Quaternion m_originRotation;



    private void Start()
    {
        s_instance = this;
        m_originRotation = transform.rotation;
    }


    public IEnumerator Shake(float duration , float magnitude)
    {
        Vector3 originalPos = transform.localPosition;

        float elapsed = 0.0f;
        while (elapsed < duration) 
        {

            float x = Random.Range(-1f,1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = new Vector3(x, y, originalPos.z);

            elapsed += Time.deltaTime;

            yield return null;
        }

        transform.localPosition = originalPos;
    }
    

    public void RestCameraShake()
    {
        transform.rotation = m_originRotation;

    }

}
