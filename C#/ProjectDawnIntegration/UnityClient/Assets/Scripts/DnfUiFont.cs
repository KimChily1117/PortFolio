using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace DnfRaidDemo {
 // Apply to instantiated legacy Dawn prefabs as well as IMGUI text.
 public sealed class DnfUiFont:MonoBehaviour {
  public Font Font; TMP_FontAsset tmp; float next;
  void LateUpdate(){
   if(Font==null||Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
   foreach(var text in FindObjectsOfType<Text>(true))if(text.font!=Font)text.font=Font;
   foreach(var text in FindObjectsOfType<TMP_Text>(true)){
    if(tmp==null){tmp=TMP_FontAsset.CreateFontAsset(Font);tmp.name="DNF Blade runtime";}
    if(text.font!=tmp)text.font=tmp;
   }
  }
  void OnDestroy(){if(tmp!=null){foreach(var texture in tmp.atlasTextures)if(texture!=null)Destroy(texture);Destroy(tmp.material);Destroy(tmp);}}
 }
}
