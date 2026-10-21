using System.Collections.Generic;
using UnityEngine;
using ProjectDawn.Network;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        [System.Serializable] sealed class HitArt {public BkEffect[] effects,hud; public BkEffect font;}
        BkEffect originalDamageFont;
        void LoadHitArt()
        {
            var asset=Resources.Load<TextAsset>("HitFeedback/manifest");if(asset==null)return;
            var doc=JsonUtility.FromJson<HitArt>(asset.text);originalDamageFont=doc.font;
            if(doc.hud!=null)foreach(var h in doc.hud)comboArt[h.id.Substring(8)]=GetSheet(h.resource,h.width,h.height,h.frames);
            foreach(var effect in doc.effects) {
                fx[effect.id]=effect;GetSheet(effect.resource,effect.width,effect.height,effect.frames);
            }
            if(originalDamageFont!=null)GetSheet(originalDamageFont.resource,originalDamageFont.width,originalDamageFont.height,originalDamageFont.frames);
        }
        readonly PersonalHitFeedback personalHit = new PersonalHitFeedback();
        sealed class ContactFlash {
            public Vector2 Position; public int Room, Facing, Variant; public float At;
            public PersonalHitFeedback.Profile Profile;
        }
        readonly List<ContactFlash> contacts = new List<ContactFlash>();
        readonly Dictionary<int,float> hitSoundAt = new Dictionary<int,float>();
        readonly AudioSource[] contactVoices = new AudioSource[3];
        int nextContactVoice, feedbackRoom = -1;
        float contactShakeAt = -1, contactShakePower, contactShakeFacing = 1, duckUntil;

        void ResetHitFeedback()
        {
            ResetCombatHud();
            personalHit.Reset();contacts.Clear();hitSoundAt.Clear();cues.Clear();skillTracks.Clear();
            feedbackRoom=-1;contactShakeAt=-1;contactShakePower=0;duckUntil=0;
            foreach(var voice in contactVoices)if(voice!=null)voice.Stop();
        }
        void PersonalContact(CombatEvent e)
        {
            PersonalHitFeedback.Profile profile;
            if(!personalHit.Accept(e,net.LocalPlayerId,Time.unscaledTime,out profile))return;
            contacts.Add(new ContactFlash {Position=e.Position,Room=e.RoomId,Facing=e.Facing,Variant=(e.Combo+e.HitIndex)%3,At=Time.unscaledTime,Profile=profile});
            if(contacts.Count>16)contacts.RemoveAt(0);
            if(Time.unscaledTime-contactShakeAt>.12f || profile.Shake>=contactShakePower) {
                contactShakeAt=Time.unscaledTime;contactShakePower=profile.Shake;contactShakeFacing=e.Facing;
            }
            duckUntil=Time.unscaledTime+.18f;
            if(flowMode || skillGallery)Debug.Log("[PERSONAL_HIT] local="+net.LocalPlayerId+" source="+e.SourceId+" skill="+e.Skill+" action="+e.SourceAction+" hit="+e.HitIndex+" profile="+profile.Name+" hold="+personalHit.LastHold.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" key="+e.FeedbackKey);
        }
        PlayerState PersonalVisualPose(PlayerState actor)
        {
            if(actor.Id!=net.LocalPlayerId)return actor;
            if(feedbackRoom!=actor.RoomId) {
                feedbackRoom=actor.RoomId;personalHit.CancelHold();contacts.Clear();contactShakePower=0;
            }
            // Never hold the visible jump, hurt/death pose or a new action to sell an old hit.
            if(actor.JumpHeight>.001f || actor.StunTime>0 || actor.Hp<=0 || actor.ActionDuration<=0) {
                personalHit.CancelHold();return actor;
            }
            var visual=actor.Clone();
            visual.ActionElapsed=personalHit.VisualAge(actor.ActionSequence,actor.ActionElapsed,Time.unscaledTime);
            return visual;
        }
        float PersonalEffectAge(int actor,int sequence,float age)
        {return actor==net.LocalPlayerId?personalHit.VisualAge(sequence,age,Time.unscaledTime):age;}
        Vector2 ContactShake()
        {
            float age=Time.unscaledTime-contactShakeAt;
            if(contactShakeAt<0 || age>.2f)return Vector2.zero;
            float envelope=Mathf.Pow(1-age/.2f,2);
            // One directional kick with damped recovery, not constant high-frequency camera noise.
            return new Vector2(-contactShakeFacing*Mathf.Cos(age*48),Mathf.Sin(age*38)*.3f)*contactShakePower*envelope;
        }
        void DrawPersonalContacts()
        {
            float now=Time.unscaledTime;contacts.RemoveAll(c=>now-c.At>c.Profile.Life);
            foreach(var c in contacts) {
                if(c.Room!=ViewedRoom)continue;
                float t=(now-c.At)/c.Profile.Life,alpha=(1-t)*(1-t);
                Vector2 at=ScreenPoint(c.Position)+new Vector2(-c.Facing*16,-67);
                if(fx.ContainsKey("contact_unionhit")) {
                    float sizeScale=.65f+Mathf.Min(1.6f,c.Profile.Scale)*.38f;
                    var tint=new Color(1,1,1,Mathf.Clamp01((1-t)*2));
                    DrawEffect("contact_unionhit",now-c.At,at,sizeScale,1,tint,false,c.Profile.Life);
                    string core=c.Profile.Name=="bloodsword" || c.Profile.Name=="overkill-impact"?"knocklarge":"slashlarge"+(c.Variant+1);
                    DrawEffect("contact_"+core,now-c.At,at,sizeScale*1.12f,1,tint,false,c.Profile.Life*.8f);
                    continue;
                }
                float size=(30+32*t)*c.Profile.Scale;
                TintTexture(disc,new Rect(at.x-size,at.y-size*.55f,size*2,size*1.1f),new Color(1,.08f,.025f,alpha*.5f));
                TintTexture(ring,new Rect(at.x-size,at.y-size*.65f,size*2,size*1.3f),new Color(1,.35f,.15f,alpha*.85f));
                // A localized contact glint leaves floor warnings and other players readable.
                // Compose in logical HUD coordinates; RotateAroundPivot displaces the glint
                // when the outer 1280x800 canvas is scaled to a smaller game window.
                var matrix=GUI.matrix;
                GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(at.x,at.y,0),Quaternion.Euler(0,0,c.Facing*-25),Vector3.one)
                    *Matrix4x4.Translate(new Vector3(-at.x,-at.y,0));
                Fill(new Rect(at.x-size,at.y-2,size*2,4),new Color(1,.9f,.7f,alpha));
                Fill(new Rect(at.x-2,at.y-size*.36f,4,size*.72f),new Color(1,.8f,.55f,alpha));
                GUI.matrix=matrix;
            }
        }
        bool DrawOriginalDamage(CombatEvent e,Vector2 point)
        {
            if(originalDamageFont==null)return false;
            var sheet=GetSheet(originalDamageFont.resource,originalDamageFont.width,originalDamageFont.height,originalDamageFont.frames);
            if(sheet.Texture==null)return false;
            bool personal=e.SourceId==net.LocalPlayerId && e.TargetId<0;
            int first=e.TargetId>=0?40:0;
            string number=Mathf.Max(0,Mathf.RoundToInt(e.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            float pop=1+.32f*Mathf.Clamp01(1-e.Age/.12f);
            float scale=(personal?(e.Value>=500?2.8f:2.45f):e.TargetId>=0?1.8f:1.4f)*pop;
            float width=0;
            foreach(char digit in number) {
                int row;if(!sheet.ByFrame.TryGetValue(first+digit-'0',out row))return false;
                width+=(sheet.Rows[row+3]-1)*scale;
            }
            float lane=((e.SourceAction+e.HitIndex)%3-1)*22;
            float x=point.x-width*.5f+lane, y=point.y-161-e.Age*56;
            var tint=new Color(1,1,1,Mathf.Clamp01((1-e.Age)/.35f));
            foreach(char digit in number) {
                int row=sheet.ByFrame[first+digit-'0'];
                DrawRow(sheet,row,new Vector2(x,y),0,0,scale,tint);
                x+=(sheet.Rows[row+3]-1)*scale;
            }
            return true;
        }
        bool PlayPersonalContactSound(CombatEvent e,string weapon)
        {
            var profile=PersonalHitFeedback.Select(e);
            string name=(weapon??"sqrswd")+"a_hit_";
            if(e.Skill==(int)SkillId.BloodSword)name="blood_sword_exp_";
            if(e.Skill==(int)SkillId.ExtremeOverkill && e.HitIndex==0)name="extreme_overkill_blood_fin";
            name=Pick(name);if(name==null)return false;
            AudioClip clip;
            if(!clips.TryGetValue(name,out clip)){clip=Resources.Load<AudioClip>("Sound/"+name);clips[name]=clip;}
            if(clip==null)return false;
            int index=nextContactVoice++%contactVoices.Length;
            var voice=contactVoices[index];
            if(voice==null){voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.priority=16;contactVoices[index]=voice;}
            voice.Stop();voice.clip=clip;voice.volume=profile.Volume*.8f;
            voice.pitch=profile.Hold>=.08f?.92f:1f;voice.Play();
            return true;
        }
    }
}
