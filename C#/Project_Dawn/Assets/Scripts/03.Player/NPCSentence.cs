using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCSentence : MonoBehaviour
{
    [TextArea]
    public string[] sentences;
    public Transform chatTransform;
    public GameObject chatBox;

    [SerializeField]
    private UI_DialougeSystem dialougeSystem;

    // Start is called before the first frame update
    void Start()
    {
        dialougeSystem = Util.FindChild<UI_DialougeSystem>(this.gameObject);
        TalkNPC();

    }



    public void TalkNPC()
    {
        dialougeSystem.gameObject.SetActive(true);
        dialougeSystem.Ondialogue(sentences,this);
    }

    #region MouseEvent
    private void OnMouseDown()
    {
        Debug.Log($"Mouse DOwn!!! : {this.gameObject.name}");
        //TalkNPC();
    }

    private void OnMouseEnter()
    {
        Debug.Log($"Mouse On!!! : {this.gameObject.name}");

    }
    #endregion MouseEvent
}
