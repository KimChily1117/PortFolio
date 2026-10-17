#include "ReferenceAudio.h"
#include "core/SkillTimeline.h"
#include "cocos2d.h"
#include "audio/include/AudioEngine.h"
#include "json/document.h"
#include <algorithm>
#include <sstream>
#include <unordered_map>
#include <unordered_set>

USING_NS_CC;
namespace {
struct Clip {std::string id,name,path;float volume=.7f;};
struct Playing {std::string path,name;bool music=false;};
struct PlannedClip {Clip clip;float offset=0;};
struct AudioState {
    bool loaded=false,attempted=false,ready=false,enabled=true,paused=false;
    int music=-1;
    Clip musicClip;
    float musicPosition=0,musicRetry=0;
    bool recoverMusic=false,seekMusic=false;
    unsigned musicRecoveries=0;
    std::string context,last="not initialized";
    std::unordered_map<std::string,std::vector<Clip>> bindings;
    std::unordered_map<std::string,size_t> next;
    std::unordered_map<std::string,std::vector<PlannedClip>> skillPlans;
    aster::SkillTimeline phaseClock;
    std::vector<Clip> queuedPhases;
    size_t phasesPlayed=0;
    std::unordered_map<int,Playing> active;
    std::unordered_set<std::string> expired;
    std::vector<ReferenceAudio::Track> tracks;
    std::unordered_map<std::string,size_t> trackIndex;
};
AudioState& state(){static AudioState value;return value;}
std::string text(const rapidjson::Value&v,const char*key){return v.IsObject()&&v.HasMember(key)&&v[key].IsString()?v[key].GetString():"";}
float number(const rapidjson::Value&v,const char*key,float fallback=0){return v.IsObject()&&v.HasMember(key)&&v[key].IsNumber()?float(v[key].GetDouble()):fallback;}
void collect(){
    auto&s=state();if(!s.ready)return;
    for(auto it=s.active.begin();it!=s.active.end();){
        if(AudioEngine::getState(it->first)==AudioEngine::AudioState::ERROR){
            s.last="Audio playback unavailable: "+it->second.name;s.expired.insert(it->second.path);
            if(s.music==it->first){s.music=-1;s.recoverMusic=!s.context.empty();}it=s.active.erase(it);
        }else ++it;
    }
    // Finish callbacks run inside AudioEngine's update. Release caches later,
    // after its iteration, and only when no voice still uses the same path.
    for(auto it=s.expired.begin();it!=s.expired.end();){
        bool used=false;for(const auto&pair:s.active)if(pair.second.path==*it){used=true;break;}
        if(used){++it;continue;}
        auto path=*it;it=s.expired.erase(it);AudioEngine::uncache(path);
    }
}
int play(const Clip&clip,bool loop,bool music,float gain=-1){
    auto&s=state();if(!ReferenceAudio::initialize()||!s.enabled)return AudioEngine::INVALID_AUDIO_ID;
    if(!FileUtils::getInstance()->isFileExist(clip.path)){s.last="Missing staged audio: "+clip.id;return AudioEngine::INVALID_AUDIO_ID;}
    auto id=AudioEngine::play2d(clip.path,loop,std::max(0.f,std::min(1.f,gain>=0?gain:clip.volume)));
    if(id==AudioEngine::INVALID_AUDIO_ID){s.last="AudioEngine rejected: "+clip.name;return id;}
    s.active[id]={clip.path,clip.name,music};s.expired.erase(clip.path);s.last="playing "+clip.name;
    AudioEngine::setFinishCallback(id,[](int finished,const std::string&){
        auto&s=state();auto found=s.active.find(finished);
        if(found!=s.active.end()){s.expired.insert(found->second.path);s.active.erase(found);}
        if(s.music==finished){
            // Looping music cannot naturally finish. The Windows streaming
            // backend can report AL_STOPPED after an underrun; recover on a
            // later scheduler tick, outside AudioEngine's player iteration.
            s.music=-1;s.recoverMusic=!s.context.empty();
            s.last="Looping music ended unexpectedly; recovery queued";
        }
    });
    if(s.paused)AudioEngine::pause(id);
    return id;
}
int binding(const std::string&key,bool loop=false,bool music=false){
    ReferenceAudio::initialize();auto&s=state();auto found=s.bindings.find(key);
    if(found==s.bindings.end()||found->second.empty()){s.last="No verified sample binding: "+key;return AudioEngine::INVALID_AUDIO_ID;}
    auto&list=found->second;auto&next=s.next[key];const auto&clip=list[next++%list.size()];return play(clip,loop,music);
}
void advancePhases(float delta){
    auto&s=state();if(!s.ready||!s.enabled)return;
    if(s.paused){
        // Decoding can complete after pause(id) was called while INITIALIZING.
        // Keep newly ready voices paused as well as the already playing ones.
        for(const auto&p:s.active)
            if(AudioEngine::getState(p.first)==AudioEngine::AudioState::PLAYING)
                AudioEngine::pause(p.first);
        return;
    }
    if(s.music>=0&&AudioEngine::getState(s.music)==AudioEngine::AudioState::PLAYING){
        if(s.seekMusic){
            auto duration=AudioEngine::getDuration(s.music);
            if(s.musicPosition>0&&duration>s.musicPosition)AudioEngine::setCurrentTime(s.music,s.musicPosition);
            s.seekMusic=false;
        }else s.musicPosition=std::max(0.f,AudioEngine::getCurrentTime(s.music));
    }
    if(s.recoverMusic){
        s.musicRetry-=std::max(0.f,delta);
        if(s.musicRetry<=0){
            s.music=play(s.musicClip,true,true);s.musicRetry=1;
            if(s.music>=0){s.recoverMusic=false;s.seekMusic=true;++s.musicRecoveries;}
        }
    }
    for(auto index:s.phaseClock.advance(delta))if(index<s.queuedPhases.size()){play(s.queuedPhases[index],false,false);++s.phasesPlayed;}
}
}

bool ReferenceAudio::initialize(){
    auto&s=state();
    if(!s.loaded){
        s.enabled=UserDefault::getInstance()->getBoolForKey("reference-audio-enabled",true);
        auto source=FileUtils::getInstance()->getStringFromFile("reference/audio/bindings.json");rapidjson::Document document;document.Parse(source.c_str());
        if(document.HasParseError()||!document.IsObject()||!document.HasMember("bindings")||!document["bindings"].IsObject()){s.last="Audio bindings unavailable";return false;}
        s.loaded=true;
        for(auto it=document["bindings"].MemberBegin();it!=document["bindings"].MemberEnd();++it){
            if(!it->value.IsObject()||!it->value.HasMember("tracks")||!it->value["tracks"].IsArray())continue;
            auto&list=s.bindings[it->name.GetString()];
            for(const auto&v:it->value["tracks"].GetArray())list.push_back({text(v,"id"),text(v,"name"),text(v,"path"),number(v,"volume",.7f)});
        }
        if(document.HasMember("skillPlans")&&document["skillPlans"].IsObject())for(auto it=document["skillPlans"].MemberBegin();it!=document["skillPlans"].MemberEnd();++it){
            if(!it->value.IsObject()||!it->value.HasMember("phases")||!it->value["phases"].IsArray())continue;
            auto&plan=s.skillPlans[it->name.GetString()];
            for(const auto&v:it->value["phases"].GetArray())plan.push_back({{text(v,"id"),text(v,"name"),text(v,"path"),number(v,"volume",.65f)},number(v,"offset")});
        }
    }
    if(!s.attempted){
        s.attempted=true;s.ready=AudioEngine::lazyInit();
        s.last=s.ready?"Cocos AudioEngine ready (Ogg Vorbis)":"Audio output initialization failed";
        if(s.ready){
            Director::getInstance()->getScheduler()->schedule([](float){collect();},&s,1.f,false,"reference-audio-cache");
            Director::getInstance()->getScheduler()->schedule([](float delta){advancePhases(delta);},&s,0.f,false,"reference-audio-phases");
        }
    }
    return s.ready;
}
void ReferenceAudio::playMusic(const std::string&context){
    auto&s=state();initialize();
    if(s.context==context&&s.music!=AudioEngine::INVALID_AUDIO_ID&&AudioEngine::getState(s.music)!=AudioEngine::AudioState::ERROR)return;
    stopMusic();
    auto found=s.bindings.find("music."+context);
    if(found==s.bindings.end()||found->second.empty()){s.last="No verified music binding: "+context;return;}
    s.musicClip=found->second.front();s.music=play(s.musicClip,true,true);
    if(s.music!=AudioEngine::INVALID_AUDIO_ID)s.context=context;
}
void ReferenceAudio::playUi(const std::string&action){binding("ui."+action);}
void ReferenceAudio::playAttack(const std::string&hero){
    if(hero.empty()){binding("attack.default");return;}
    playSkill(hero,1);
}
void ReferenceAudio::playSkill(const std::string&hero,int skill){
    if(skill<1||skill>3)return;
    auto&s=state();if(!initialize()||!s.enabled)return;
    std::string prefix="hero."+hero+".skill"+std::to_string(skill);
    auto plan=s.skillPlans.find(hero);
    if(skill==3&&plan!=s.skillPlans.end()&&!plan->second.empty()){
        stopEffects();std::vector<aster::SkillCue> cues;
        for(const auto&phase:plan->second){cues.push_back({phase.offset,0,s.queuedPhases.size()});s.queuedPhases.push_back(phase.clip);}
        s.phaseClock.reset(std::move(cues),0);advancePhases(0);
    }else binding(prefix+".sfx");
    binding(prefix+".voice");
}
int ReferenceAudio::playTrack(const std::string&id,bool loop,float volume){
    catalog();auto&s=state();auto found=s.trackIndex.find(id);if(found==s.trackIndex.end()){s.last="Unknown audio track: "+id;return AudioEngine::INVALID_AUDIO_ID;}
    const auto&t=s.tracks[found->second];return play({t.id,t.name,t.path,volume},loop,false,volume);
}
void ReferenceAudio::stopTrack(int id){
    auto&s=state();auto found=s.active.find(id);if(found==s.active.end())return;
    const auto path=found->second.path;s.active.erase(found);s.expired.insert(path);
    if(s.music==id){s.music=-1;s.context.clear();s.recoverMusic=false;s.seekMusic=false;}
    if(s.ready)AudioEngine::stop(id);
}
void ReferenceAudio::stopMusic(){auto&s=state();if(s.music>=0)stopTrack(s.music);s.music=-1;s.context.clear();s.recoverMusic=false;s.seekMusic=false;s.musicPosition=0;s.musicRetry=0;}
void ReferenceAudio::stopEffects(){
    auto&s=state();s.phaseClock.stop();s.queuedPhases.clear();s.phasesPlayed=0;
    std::vector<int> ids;for(const auto&p:s.active)if(!p.second.music)ids.push_back(p.first);for(auto id:ids)stopTrack(id);
}
void ReferenceAudio::setPaused(bool paused){
    auto&s=state();s.paused=paused;if(!s.ready)return;
    for(const auto&p:s.active){if(paused)AudioEngine::pause(p.first);else AudioEngine::resume(p.first);}
}
void ReferenceAudio::setEnabled(bool enabled){
    auto&s=state();initialize();s.enabled=enabled;UserDefault::getInstance()->setBoolForKey("reference-audio-enabled",enabled);UserDefault::getInstance()->flush();
    if(!enabled){stopEffects();stopMusic();collect();}
}
bool ReferenceAudio::isEnabled(){initialize();return state().enabled;}
void ReferenceAudio::shutdown(){
    auto&s=state();stopEffects();stopMusic();
    if(s.ready){Director::getInstance()->getScheduler()->unschedule("reference-audio-cache",&s);Director::getInstance()->getScheduler()->unschedule("reference-audio-phases",&s);AudioEngine::end();}
    s.ready=false;s.attempted=false;s.expired.clear();s.active.clear();s.last="audio shut down";
}
const std::vector<ReferenceAudio::Track>& ReferenceAudio::catalog(){
    auto&s=state();if(!s.tracks.empty())return s.tracks;
    auto source=FileUtils::getInstance()->getStringFromFile("reference/audio/catalog.json");rapidjson::Document document;document.Parse(source.c_str());
    if(document.HasParseError()||!document.IsObject()||!document.HasMember("tracks")||!document["tracks"].IsArray())return s.tracks;
    for(const auto&v:document["tracks"].GetArray()){
        Track t;t.id=text(v,"id");t.name=text(v,"name");t.bank=text(v,"bank");t.path=text(v,"path");t.category=text(v,"category");t.duration=number(v,"duration");
        s.trackIndex[t.id]=s.tracks.size();s.tracks.push_back(std::move(t));
    }return s.tracks;
}
std::string ReferenceAudio::diagnostic(){
    auto&s=state();std::ostringstream out;out<<"audio="<<(s.ready?"ready":"unavailable")<<" enabled="<<s.enabled<<" paused="<<s.paused<<" active="<<s.active.size()<<" pending="<<(s.queuedPhases.size()-s.phasesPlayed)<<" phaseTime="<<s.phaseClock.elapsed()<<" music="<<s.context<<" musicRecoveries="<<s.musicRecoveries<<" musicRecoveryPending="<<s.recoverMusic<<" last="<<s.last;
    if(s.ready)for(const auto&p:s.active)out<<" ["<<p.first<<":"<<p.second.name<<" state="<<int(AudioEngine::getState(p.first))<<" time="<<AudioEngine::getCurrentTime(p.first)<<"]";
    return out.str();
}
