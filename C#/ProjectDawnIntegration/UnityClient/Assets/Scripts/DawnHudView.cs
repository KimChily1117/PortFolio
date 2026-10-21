using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ProjectDawn.Network;
namespace DnfRaidDemo
{
    // Original Project Dawn prefab: original images, globe animations and button layout.
    public sealed class DawnHudView:MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform hud;
        private Image hp,mp;
        private Image hpDamage;
        private readonly HealthTrail personalTrail=new HealthTrail(),bossTrail=new HealthTrail();
        private Sprite trailSprite;private Texture2D trailTexture;
        public void ResetHealth(){personalTrail.Reset();bossTrail.Reset();lastBossRatio=-1;}
        private RectTransform bossBar;
        private Image bossHp,bossDamage;
        private Sprite bossPortraitSprite;
        private float lastBossRatio=-1,damageHold;
        public void Initialize(Action inventory,Action skills,Texture2D arena,Texture2D vignette)
        {
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<GraphicRaycaster>();
            if(arena!=null)Background("Arena",arena,new Vector2(1280,720),new Vector2(0,40));
            if(vignette!=null)Background("Vignette",vignette,new Vector2(1280,800),Vector2.zero);
            var prefab=Resources.Load<GameObject>("DawnOriginal/HUD");
            if(prefab==null)throw new InvalidOperationException("Original Dawn HUD prefab missing");
            hud=Instantiate(prefab,transform,false).GetComponent<RectTransform>();hud.localScale=Vector3.one*.8f;
            hp=hud.GetComponentsInChildren<Image>().Single(i=>i.name=="Hp");
            mp=hud.GetComponentsInChildren<Image>().Single(i=>i.name=="Mp");
            hpDamage=Instantiate(hp,hp.transform.parent);hpDamage.name="HP white damage trail";
            foreach(var animator in hpDamage.GetComponents<Animator>())Destroy(animator);
            hpDamage.transform.SetSiblingIndex(hp.transform.GetSiblingIndex());
            trailTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            var pixels=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(31.5f-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))));
            trailTexture.SetPixels(pixels);trailTexture.Apply();trailSprite=Sprite.Create(trailTexture,new Rect(0,0,64,64),new Vector2(.5f,.5f));
            hpDamage.sprite=trailSprite;hpDamage.color=Color.white;hpDamage.type=Image.Type.Filled;hpDamage.fillMethod=Image.FillMethod.Vertical;hpDamage.fillOrigin=0;hpDamage.raycastTarget=false;
            foreach(var image in new[]{hp,mp}) {image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Vertical;image.fillOrigin=0;image.raycastTarget=false;}
            var rootImage=hud.GetComponent<Image>();rootImage.raycastTarget=false;
            foreach(var button in hud.GetComponentsInChildren<Button>())
            {var rect=button.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(72,72);rect.anchoredPosition=new Vector2(button.name=="BtnInventory"?-570:570,-12);
             var action=button.name=="BtnInventory"?inventory:skills;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>action());}
            if(FindObjectOfType<EventSystem>()==null)new GameObject("Dawn UI input",typeof(EventSystem),typeof(StandaloneInputModule));
            // Current IMGUI layer rebuilds the bottom HUD from original DNF pieces;
            // keep the Dawn canvas/background and boss prefab lifecycle intact.
            hud.gameObject.SetActive(false);
        }
        private void Background(string label,Texture2D texture,Vector2 size,Vector2 position)
        {
            var go=new GameObject(label,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(transform,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=size;rect.anchoredPosition=position;
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
        }
        public void Display(PlayerState player,bool visible)
        {
            canvas.enabled=visible;if(!visible)return;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);canvas.scaleFactor=scale;
            hud.anchoredPosition=new Vector2(0,(Screen.height/scale-800)*.5f);
            if(player!=null){hp.fillAmount=Mathf.Clamp01(player.Hp/player.MaxHp);hpDamage.fillAmount=personalTrail.Update(hp.fillAmount,Time.unscaledTime,Time.unscaledDeltaTime);mp.fillAmount=Mathf.Clamp01(player.Mp/player.MaxMp);}
        }
        public float BossHpFraction => bossHp==null?1:bossHp.fillAmount;
        public float BossDamageFraction => bossDamage==null?1:bossDamage.fillAmount;
        public void DisplayBoss(BossState boss,bool visible)
        {
            if(bossBar==null && visible){
                var original=Resources.Load<GameObject>("DawnOriginal/BossHpBar");
                if(original==null)throw new InvalidOperationException("Original Dawn boss HUD prefab missing");
                var imported=Instantiate(original,transform,false);
                bossBar=imported.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Empty_HpBar");
                bossBar.SetParent(transform,false);Destroy(imported);
                bossBar.anchorMin=bossBar.anchorMax=new Vector2(.5f,.5f);bossBar.pivot=new Vector2(0,1);
                bossBar.anchoredPosition=new Vector2(-620,322);bossBar.localScale=Vector3.one*.95f;
                bossHp=bossBar.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Hp");
                bossDamage=bossBar.GetComponentsInChildren<Image>(true).Single(i=>i.name=="BackHpBar");
                var portrait=bossBar.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Hp_image");
                var bossTexture=Resources.Load<Texture2D>("Art/boss1/0000");
                bossPortraitSprite=Sprite.Create(bossTexture,new Rect(235,bossTexture.height-192,65,80),new Vector2(.5f,.5f));
                portrait.sprite=bossPortraitSprite;portrait.preserveAspect=true;portrait.rectTransform.sizeDelta=new Vector2(38,48);
                foreach(var image in bossBar.GetComponentsInChildren<Image>(true))image.raycastTarget=false;
                // Retain the original frame; replace its ALERT placeholder children with the
                // actual extracted red/white monster HP strips (monster_hp.img frames 2 and 7).
                bossHp.sprite=Resources.Load<Sprite>("DawnOriginal/BossHealthFill");
                bossDamage.sprite=Resources.Load<Sprite>("DawnOriginal/BossDamageFill");
                if(bossHp.sprite==null||bossDamage.sprite==null)throw new InvalidOperationException("Original monster HP strips missing");
                foreach(var image in new[]{bossHp,bossDamage}){
                    foreach(Transform child in image.transform)child.gameObject.SetActive(false);
                    // No children remain to mask. Disabling the old mask also ensures its own
                    // filled graphic is visible instead of relying on the prefab mask setting.
                    var mask=image.GetComponent<Mask>();if(mask!=null)mask.enabled=false;
                    image.color=Color.white;image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillOrigin=0;
                }
            }
            // Preserve the existing prefab/trail data; the visible bar is composed above the
            // IMGUI battlefield in DrawOriginalBossHealth, otherwise the arena covers this canvas.
            if(bossBar==null)return;bossBar.gameObject.SetActive(false);if(!visible||boss==null)return;
            float ratio=Mathf.Clamp01(boss.Hp/Mathf.Max(1,boss.MaxHp));
            bossHp.fillAmount=ratio;
            bossDamage.fillAmount=bossTrail.Update(ratio,Time.unscaledTime,Time.unscaledDeltaTime);
            lastBossRatio=ratio;
        }
        private void OnDestroy(){if(bossPortraitSprite!=null)Destroy(bossPortraitSprite);if(trailSprite!=null)Destroy(trailSprite);if(trailTexture!=null)Destroy(trailTexture);}
    }
}
