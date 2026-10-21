using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public static class DawnAssetChecks
{
    [Serializable] class Clip { public string name;public float[] times;public string[] resources,names; }
    [Serializable] class Document {public Clip[] clips;}
    public static void Run()
    {
        var hud=Resources.Load<GameObject>("DawnOriginal/HUD");
        if(hud==null || hud.GetComponentsInChildren<Image>().Length<5)throw new Exception("Original Dawn HUD images missing");
        var doc=JsonUtility.FromJson<Document>(Resources.Load<TextAsset>("DawnOriginal/meteor-clips").text);int frames=0;
        foreach(var clip in doc.clips)for(int i=0;i<clip.resources.Length;i++)
        {
            var sprites=Resources.LoadAll<Sprite>(clip.resources[i]);
            if(sprites.Length==0 || !string.IsNullOrEmpty(clip.names[i]) && !sprites.Any(s=>s.name==clip.names[i]))
                throw new Exception("Dawn meteor frame not found: "+clip.name+" / "+i+" / "+clip.names[i]);
            frames++;
        }
        foreach(var sound in new[]{"bakal_dragon_meteor_exp_01","bakal_dragon_meteor_cast"})
            if(Resources.Load<AudioClip>("Sound/"+sound)==null)throw new Exception("Meteor sound missing: "+sound);
        if(Resources.Load<Font>("Fonts/DNFForgedBlade-Medium")==null)throw new Exception("DNF Blade font missing");
        foreach(var name in new[]{"manifest","slashlarge1","slashlarge2","slashlarge3","knocklarge","unionhit","damagefont","dungeon_rank","dungeon_score","combo_bonus_num","newcombo_bonus"})
            if(Resources.Load<TextAsset>("HitFeedback/"+name)==null)throw new Exception("Original contact resource missing: "+name);
        Debug.Log("[DAWN_ORIGINAL_ASSETS_VALIDATED] HUD prefab + meteor frames="+frames);
        foreach(var name in new[]{"manifest","bottomFrame","hpOrb","mpOrb","bossWhite","partyHp","potionHp"})
            if(Resources.Load<TextAsset>("OriginalCombatHud/"+name)==null)throw new Exception("Original HUD resource missing: "+name);
        foreach(var name in new[]{"manifest","ground_magic","target_mark","room_portal","ground_flame","impact_wave","actor_shadow","mark_gauge"})
            if(Resources.Load<TextAsset>("OriginalWorldEffects/"+name)==null)throw new Exception("Original world resource missing: "+name);
        foreach(var name in new[]{"OriginalWorldEffects/bakal_side_room","OriginalWorldEffects/seria_room_exact","DawnOriginal/FlowDependencies/c95d4737a68c56047936e6a7cd332dc0"})
            if(Resources.Load<Texture2D>(name)==null)throw new Exception("Original scene background missing: "+name);
    }
}
