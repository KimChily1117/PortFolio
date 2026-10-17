#include "SkillPresentation.h"
#include "SpinePreview.h"
#include "AnimatedCutscene.h"
#include "core/SkillTimeline.h"
#include "json/document.h"
#include "renderer/backend/Device.h"
#include "renderer/backend/ProgramState.h"
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <sstream>
#include <unordered_map>

USING_NS_CC;
namespace {
using J=rapidjson::Value;
const J& member(const J& v,const char* k){static const J empty;return v.IsObject()&&v.HasMember(k)?v[k]:empty;}
std::string str(const J&v,const char*k,const std::string& fallback=""){const auto&a=member(v,k);return a.IsString()?a.GetString():fallback;}
float num(const J&v,const char*k,float fallback=0){const auto&a=member(v,k);return a.IsNumber()?float(a.GetDouble()):a.IsString()?std::strtof(a.GetString(),nullptr):fallback;}
bool flag(const J&v,const char*k,bool fallback=false){const auto&a=member(v,k);return a.IsBool()?a.GetBool():fallback;}
std::vector<float> csv(std::string text){std::replace(text.begin(),text.end(),',',' ');std::istringstream stream(text);std::vector<float> result;float x;while(stream>>x)result.push_back(x);return result;}
Vec2 scalePair(const J&v){auto values=csv(str(v,"scale"));if(!values.empty())return Vec2(values[0],values.size()>1?values[1]:values[0]);float s=num(v,"scale",1);return Vec2(s,s);}
Color4B color(const J&v){auto c=csv(str(v,"color","0,0,0,255"));return c.size()==4?Color4B(GLubyte(c[0]),GLubyte(c[1]),GLubyte(c[2]),GLubyte(c[3])):Color4B::BLACK;}
float sample(const J& frames,float time,const char* axis,float fallback=0){
    if(!frames.IsArray()||frames.Empty())return fallback;
    const J* a=&frames[0];const J* b=a;
    for(const auto& frame:frames.GetArray()){if(num(frame,"time")<=time)a=&frame;else {b=&frame;break;}b=a;}
    float value=num(*a,axis,fallback);float span=num(*b,"time")-num(*a,"time");
    if(span<=0||str(*a,"curve")=="stepped")return value;
    float t=std::max(0.f,std::min(1.f,(time-num(*a,"time"))/span));
    // Source camera samples are retained; Bezier easing is not inferred here.
    return value+(num(*b,axis,fallback)-value)*t;
}
}

struct SkillPresentation::Impl {
    struct BoundNode {
        Node* node=nullptr;
        Vec3 position,rotation,scale;
        bool visible=true;
        ~BoundNode(){restore();}
        void bind(Node* value){
            restore();node=value;if(!node)return;node->retain();
            position=node->getPosition3D();rotation=node->getRotation3D();
            scale=Vec3(node->getScaleX(),node->getScaleY(),node->getScaleZ());visible=node->isVisible();
        }
        void restore(){
            if(!node)return;
            node->setPosition3D(position);node->setRotation3D(rotation);
            node->setScaleX(scale.x);node->setScaleY(scale.y);node->setScaleZ(scale.z);node->setVisible(visible);
            auto old=node;node=nullptr;old->release();
        }
    } stage,background,opponents;
    rapidjson::Document data;
    aster::SkillTimeline clock;
    Callbacks callbacks;
    SkillChoice choice;
    Node *world=nullptr,*camera=nullptr,*actors=nullptr,*effects=nullptr,*screen=nullptr;
    Node* opponentOffset=nullptr;
    SpinePreview* actor=nullptr;
    Map<std::string,Node*> named; // Retains finite-lived layers until their STOP cue.
    std::unordered_map<Node*,std::vector<Node*>> groupLayers;
    std::unordered_map<std::string,float> fadeOut;
    Vec2 actorHome,targetHome;
    Size size;
    float units=1;
    bool finished=false;
    std::string current;
    const J* shake=nullptr;
    float shakeStart=0,shakeEnd=0;
    std::vector<SpinePreview*> managedSpines;
    void note(const std::string&s){current=s;if(callbacks.onDiagnostic)callbacks.onDiagnostic(s);}
    void restoreStage(){opponents.restore();background.restore();stage.restore();}
    void syncStage(){
        if(stage.node){
            const auto offset=world->getPosition()+camera->getPosition();
            stage.node->setPosition3D(stage.position+Vec3(offset.x,offset.y,0));
            stage.node->setScaleX(stage.scale.x*world->getScaleX()*camera->getScaleX());
            stage.node->setScaleY(stage.scale.y*world->getScaleY()*camera->getScaleY());
            stage.node->setRotation3D(stage.rotation+Vec3(0,0,world->getRotation()+camera->getRotation()));
        }
        if(opponents.node&&opponentOffset){auto offset=opponentOffset->getPosition();opponents.node->setPosition3D(opponents.position+Vec3(offset.x,offset.y,0));}
    }
};

SkillPresentation::SkillPresentation():impl_(new Impl){}
SkillPresentation::~SkillPresentation()=default;

const std::vector<SkillPresentation::SkillChoice>& SkillPresentation::catalog(){
    static std::vector<SkillChoice> choices;
    if(!choices.empty())return choices;
    auto text=FileUtils::getInstance()->getStringFromFile("reference/skills/catalog.json");
    rapidjson::Document d;d.Parse(text.c_str());
    const auto& list=member(d,"skills");if(!list.IsArray())return choices;
    for(const auto& v:list.GetArray()){
        SkillChoice c;c.id=str(v,"id");c.name=str(v,"name");c.heroId=str(v,"heroId");
        c.modelJson=str(v,"modelJson");c.modelAtlas=str(v,"modelAtlas");c.portraitJson=str(v,"portraitJson");c.portraitAtlas=str(v,"portraitAtlas");
        c.cutPath=str(v,"cutPath");c.timelinePath=str(v,"timelinePath");c.playable=flag(v,"playable");c.duration=num(v,"duration");
        const auto& limits=member(v,"limitations");if(limits.IsArray())for(const auto& s:limits.GetArray())if(s.IsString())c.limitations.emplace_back(s.GetString());
        choices.push_back(std::move(c));
    }return choices;
}
std::array<std::string,4> SkillPresentation::initialSkillIds(){return {{"c1017.s3","c1048.s3","c1028.s3","c1037.s3"}};}
SkillPresentation* SkillPresentation::create(const std::string& id,const Callbacks& cb,std::string& error){
    auto p=new SkillPresentation;if(!p->init()||!p->load(id,cb,error)){delete p;return nullptr;}p->autorelease();return p;
}
bool SkillPresentation::load(const std::string&id,const Callbacks&cb,std::string&error){
    auto& i=*impl_;const auto& all=catalog();auto choice=std::find_if(all.begin(),all.end(),[&](const SkillChoice&c){return c.id==id;});
    if(choice==all.end()||!choice->playable){error="Skill source schedule unavailable: "+id;return false;}
    i.choice=*choice;i.callbacks=cb;
    auto text=FileUtils::getInstance()->getStringFromFile(choice->timelinePath);i.data.Parse(text.c_str());
    if(i.data.HasParseError()||!member(i.data,"events").IsArray()){error="Invalid skill timeline";return false;}
    i.size=Director::getInstance()->getVisibleSize();setContentSize(i.size);
    i.world=Node::create();i.camera=Node::create();i.actors=Node::create();i.effects=Node::create();i.screen=Node::create();
    i.opponentOffset=Node::create();addChild(i.opponentOffset);
    addChild(i.world);i.world->addChild(i.camera);i.camera->addChild(i.effects);i.camera->addChild(i.actors,0);addChild(i.screen,100);
    i.actor=SpinePreview::create(choice->modelJson,choice->modelAtlas,error);if(!i.actor)return false;
    i.actor->playAnimation("b_idle",true);i.actor->seekAnimation(0);
    auto bounds=i.actor->localBounds();i.units=std::min(0.6f,i.size.height*0.43f/std::max(1.f,bounds.size.height));
    i.actorHome=Vec2(i.size.width*.28f,i.size.height*.25f);i.targetHome=Vec2(i.size.width*.72f,i.size.height*.25f);
    i.actor->setPosition(i.actorHome);i.actor->setScale(i.units);i.actors->addChild(i.actor);i.actor->playAnimation("b_idle",true);
    i.actor->unscheduleUpdate();i.managedSpines.push_back(i.actor);
    std::vector<aster::SkillCue> cues;const auto& events=i.data["events"];
    for(rapidjson::SizeType n=0;n<events.Size();++n)cues.push_back({num(events[n],"time"),num(events[n],"duration"),n});
    i.clock.reset(std::move(cues),num(i.data,"duration"));
    auto skipLabel=Label::createWithTTF("건너뛰기 >>","fonts/NotoSansKR.otf",18);
    if(!skipLabel)skipLabel=Label::createWithSystemFont("Skip >>","Arial",18);
    auto item=MenuItemLabel::create(skipLabel,[this](Ref*){skip();});
    item->setContentSize(Size(140,48));skipLabel->setAnchorPoint(Vec2(.5f,.5f));skipLabel->setPosition(Vec2(70,24));
    auto menu=Menu::create(item,nullptr);menu->setPosition(i.size.width-90,i.size.height-35);addChild(menu,1000);
    auto touch=EventListenerTouchOneByOne::create();touch->setSwallowTouches(true);touch->onTouchBegan=[](Touch*,Event*){return true;};
    _eventDispatcher->addEventListenerWithSceneGraphPriority(touch,this);
    auto key=EventListenerKeyboard::create();key->onKeyReleased=[this](EventKeyboard::KeyCode code,Event*){if(code==EventKeyboard::KeyCode::KEY_ESCAPE)skip();};
    _eventDispatcher->addEventListenerWithSceneGraphPriority(key,this);
    // The caller must be able to bind the host stage before time-zero cues.
    scheduleUpdate();return true;
}

void SkillPresentation::bindBattleStage(Node* stage,Node* background,Node* opponents,Vec2 actorHome,Vec2 targetHome){
    auto&i=*impl_;if(i.finished)return;
    i.restoreStage();i.stage.bind(stage);i.background.bind(background);i.opponents.bind(opponents);
    i.actorHome=actorHome;i.targetHome=targetHome;i.actor->setPosition(actorHome);
    i.opponentOffset->stopAllActions();i.opponentOffset->setPosition(Vec2::ZERO);i.syncStage();
}

void SkillPresentation::dispatch(size_t index){
    auto& i=*impl_;const auto&e=i.data["events"][rapidjson::SizeType(index)];const auto&p=member(e,"params");const auto type=str(e,"type");float span=num(e,"duration");
    i.note(type+" @ "+std::to_string(num(e,"time")));
    if(type=="ANI"){if(!i.actor->playAnimation(str(p,"ani"),flag(p,"loop")))i.note("Missing actor animation "+str(p,"ani"));else i.actor->seekAnimation(std::max(0.f,float(i.clock.elapsed())-num(e,"time")));return;}
    if(type=="IDLE"){i.actor->playAnimation("b_idle",true);return;}
    if(type=="FIRE"){if(i.callbacks.onHit)i.callbacks.onHit();return;}
    if(type=="CUTIN"){
        std::string error;auto cut=AnimatedCutscene::create(str(p,"path",i.choice.cutPath),error);if(!cut){i.note(error);return;}
        float s=std::min(i.size.width/cut->getContentSize().width,i.size.height/cut->getContentSize().height);
        cut->setScale(s);cut->setPosition((i.size.width-cut->getContentSize().width*s)*.5f,(i.size.height-cut->getContentSize().height*s)*.5f);
        i.screen->addChild(cut,500);cut->runAction(Sequence::create(DelayTime::create(cut->durationSeconds()),RemoveSelf::create(),nullptr));return;
    }
    if(type=="DARK"||type=="COLOR_FADE"){
        auto tint=color(p);tint.a=0;auto layer=LayerColor::create(tint,i.size.width,i.size.height);auto opacity=GLubyte(std::max(0.f,std::min(255.f,num(p,"opacity",1)*255)));
        bool foreground=num(p,"globalZ",-1)>0;auto holder=foreground?i.screen:i.world;holder->addChild(layer,foreground?100:-100);
        float in=num(p,"fadeIn")/1000.f,out=num(p,"fadeOut")/1000.f,hold=num(p,"duration")/1000.f;
        std::string id=str(p,"id");if(!id.empty()){i.named.insert(id,layer);i.fadeOut[id]=out;}
        if(hold<0)layer->runAction(FadeTo::create(in,opacity));
        else layer->runAction(Sequence::create(FadeTo::create(in,opacity),DelayTime::create(hold),FadeOut::create(out),RemoveSelf::create(),nullptr));return;
    }
    if(type=="STOP"||type=="RMCHILD"){
        auto found=i.named.find(str(p,"targetId"));if(found!=i.named.end()){
            auto node=found->second;node->stopAllActions();float fade=i.fadeOut[str(p,"targetId")];
            auto layers=i.groupLayers.find(node);if(layers!=i.groupLayers.end()){for(auto* layer:layers->second)layer->removeFromParentAndCleanup(true);i.groupLayers.erase(layers);}
            node->runAction(Sequence::create(FadeOut::create(fade),RemoveSelf::create(),nullptr));i.named.erase(found);
        }return;
    }
    if(type=="CAM"){
        const float zoom=std::max(.1f,num(p,"scale",1));Vec2 focus=i.actorHome;int f=int(num(p,"focus",1));
        if(f==20)focus=i.targetHome;else if(f==41)focus=Vec2(i.size.width*.5f,i.size.height*.35f);
        Vec2 target=Vec2(i.size.width*.5f,i.size.height*.35f)-focus*zoom+Vec2(num(p,"x")*i.size.width,num(p,"y")*i.size.height);
        if(f==41){target=Vec2::ZERO;}
        i.camera->stopActionByTag(4101);auto action=Spawn::create(MoveTo::create(span,target),ScaleTo::create(span,f==41?1:zoom),nullptr);action->setTag(4101);i.camera->runAction(action);return;
    }
    if(type=="MOVE"){
        Vec2 target=i.actorHome;int location=int(num(p,"location",0));if(location==3||location==4||location==1)target=i.targetHome;
        target+=Vec2(num(p,"x")*i.size.width,num(p,"y")*i.size.height);
        i.actor->stopActionByTag(4102);FiniteTimeAction* action=nullptr;
        if(int(num(p,"style"))==2&&span>0)action=JumpTo::create(span,target,std::max(20.f,num(p,"distance",200)*i.units*.4f),1);
        else action=MoveTo::create(span,target);action->setTag(4102);i.actor->runAction(action);return;
    }
    if(type=="LAYOUT"){
        if(int(num(p,"mode"))==1)i.actor->runAction(MoveTo::create(span,i.actorHome));
        else if(num(p,"self_x")||num(p,"self_y"))i.actor->runAction(MoveTo::create(span,i.actorHome+Vec2(num(p,"self_x")*i.size.width,num(p,"self_y")*i.size.height)));
        if(int(num(p,"mode"))==1||member(p,"away_x").IsNumber()||member(p,"away_y").IsNumber()){
            i.opponentOffset->stopAllActions();
            Vec2 away=int(num(p,"mode"))==1?Vec2::ZERO:Vec2(num(p,"away_x")*i.size.width,num(p,"away_y")*i.size.height);
            i.opponentOffset->runAction(MoveTo::create(span,away));
        }return;
    }
    if(type=="VISIBLE"){i.actor->setVisible(flag(p,"show"));return;}
    if(type=="BGSHOW"){if(i.background.node)i.background.node->setVisible(flag(p,"show",false));return;}
    if(type=="BGHATCH"){
        const std::string root="reference/skills/assets/hatch/";
        auto cache=Director::getInstance()->getTextureCache();
        Texture2D* textures[3]={cache->addImage(root+"bg_hatch_1.png"),cache->addImage(root+"bg_hatch_2.png"),cache->addImage(root+"bg_hatch_3.png")};
        if(!textures[0]||!textures[1]||!textures[2]){i.note("Missing original hatch textures");return;}
        Texture2D::TexParams sampler;sampler.sAddressMode=backend::SamplerAddressMode::REPEAT;sampler.tAddressMode=backend::SamplerAddressMode::REPEAT;
        for(auto* texture:textures)texture->setTexParameters(sampler);
        auto sprite=Sprite::createWithTexture(textures[0]);
        auto files=FileUtils::getInstance();
        auto program=backend::Device::getInstance()->newProgram(files->getStringFromFile(root+"sprite_hatch.cocos.vert"),files->getStringFromFile(root+"sprite_hatch.cocos.frag"));
        if(!program){i.note("Original hatch shader could not compile");return;}
        auto state=new backend::ProgramState(program);program->release();sprite->setProgramState(state);state->release();
        state->setTexture(state->getUniformLocation("u_Texture1"),1,textures[1]->getBackendTexture());
        state->setTexture(state->getUniformLocation("u_Texture2"),2,textures[2]->getBackendTexture());
        for(int n=0;n<3;++n){auto values=csv(str(p,("color"+std::to_string(n)).c_str(),"255,255,255,255"));float rgba[4]={1,1,1,1};for(size_t k=0;k<values.size()&&k<4;++k)rgba[k]=values[k]/255.f;state->setUniform(state->getUniformLocation("u_FragColor"+std::to_string(n)),rgba,sizeof(rgba));}
        const Vec3 speeds(num(p,"speed0"),num(p,"speed1"),num(p,"speed2"));
        sprite->schedule([state,speeds,elapsed=0.f](float delta)mutable{elapsed+=delta;float options[4]={elapsed*speeds.x,elapsed*speeds.y,elapsed*speeds.z,0};state->setUniform(state->getUniformLocation("u_Option"),options,sizeof(options));},"hatch-scroll");
        float options[4]={0,0,0,0};state->setUniform(state->getUniformLocation("u_Option"),options,sizeof(options));
        sprite->setPosition(i.size.width*(.5f+num(p,"x")),i.size.height*(.5f+num(p,"y")));
        sprite->setRotation(num(p,"rotate"));sprite->setScale(std::max(i.size.width/sprite->getContentSize().width,i.size.height/sprite->getContentSize().height)*num(p,"scale",1));
        sprite->setOpacity(0);i.world->addChild(sprite,-50);
        float fadeIn=num(p,"fadeIn")/1000.f,fadeOut=num(p,"fadeOut")/1000.f;
        sprite->runAction(Sequence::create(FadeIn::create(fadeIn),DelayTime::create(std::max(0.f,span-fadeIn-fadeOut)),FadeOut::create(fadeOut),RemoveSelf::create(),nullptr));return;
    }
    if(type=="SHAKE"){
        const auto& cameras=member(i.data,"cameras");auto source=str(p,"source");
        if(!source.empty()&&cameras.IsObject()&&cameras.HasMember(source.c_str())){
            i.shake=&cameras[source.c_str()];i.shakeStart=float(i.clock.elapsed());i.shakeEnd=span>0?i.shakeStart+span:float(i.clock.duration());
        }else i.note("Camera shake lacks recorded source; no random replacement");return;
    }
    if(type=="EFFECT"||type=="BGSTAGE"){
        auto effectName=str(p,"effect");const auto& bank=member(i.data,"effects");if(!bank.IsObject()||!bank.HasMember(effectName.c_str()))return;
        auto group=Node::create();Vec2 anchor=i.actor->getPosition();int location=int(num(p,"location",1));
        if(location==4||location==6||location==7)anchor=i.targetHome;
        if(location==10&&!str(p,"locationParam").empty()){
            Vec2 bone;if(i.actor->bonePosition(str(p,"locationParam"),bone))anchor=i.actor->getPosition()+bone*i.units;
        }
        group->setPosition(anchor+Vec2(num(p,"x")*i.size.width,num(p,"y")*i.size.height));auto s=scalePair(p);group->setScaleX(s.x*i.units);group->setScaleY(s.y*i.units);
        i.camera->addChild(group);auto id=str(p,"id");if(!id.empty())i.named.insert(id,group);
        const auto& effect=bank[effectName.c_str()];const auto& primitives=member(effect,"items");if(!primitives.IsArray())return;
        for(const auto& primitive:primitives.GetArray()){
            const J* item=&primitive;float delay=num(primitive,"delay");
            const auto& primitiveParams=member(primitive,"params");
            auto layer=Node::create();layer->setPosition(group->getPosition());layer->setScaleX(group->getScaleX());layer->setScaleY(group->getScaleY());
            // Sibling layers can straddle the actor; nesting all effects in one
            // background node would silently discard each primitive's ordering.
            i.camera->addChild(layer,int(num(primitiveParams,"z")+(num(p,"globalZ")+num(primitiveParams,"globalZ"))*1000));
            i.groupLayers[group].push_back(layer);
            group->runAction(Sequence::create(DelayTime::create(delay),CallFunc::create([this,layer,item](){
                auto& state=*impl_;if(state.finished)return;
                if(member(*item,"unsupported").IsString()){state.note(str(*item,"unsupported"));return;}
                const auto& params=member(*item,"params");Node* visual=nullptr;
                if(str(*item,"type")=="spine"){
                    std::string error;auto spine=SpinePreview::create(str(*item,"json"),str(*item,"atlas"),error);if(!spine){state.note(error);return;}
                    spine->playAnimation(str(*item,"animation"),false);visual=spine;
                }else if(str(*item,"type")=="particle"){
                    auto parent=Node::create();const auto& emitters=member(*item,"emitters");
                    if(emitters.IsArray())for(const auto& emitter:emitters.GetArray()){
                        const J* em=&emitter;parent->runAction(Sequence::create(DelayTime::create(num(emitter,"delay")),CallFunc::create([parent,em](){
                            auto map=FileUtils::getInstance()->getValueMapFromFile(str(*em,"path"));
                            auto particle=new ParticleSystemQuad;
                            if(particle->initWithDictionary(map)){
                                particle->autorelease();particle->setAutoRemoveOnFinish(false);particle->setEmissionRate(num(member(*em,"source"),"emissionRate",particle->getEmissionRate()));
                                parent->addChild(particle);
                            }else particle->release();
                        }),nullptr));
                    }visual=parent;
                }
                if(!visual)return;auto scale=scalePair(params);visual->setScaleX(scale.x);visual->setScaleY(scale.y);visual->setRotation(num(params,"rotate"));
                visual->setPosition(num(params,"x"),num(params,"y"));layer->addChild(visual);
                float life=num(*item,"duration");if(life>0)visual->runAction(Sequence::create(DelayTime::create(life),RemoveSelf::create(),nullptr));
            }),nullptr));
        }
        return;
    }
    if(type=="UNSUPPORTED")i.note("Unimplemented source action: "+str(p,"sourceAction"));
}

void SkillPresentation::update(float delta){
    auto&i=*impl_;if(i.finished)return;
    // Actor update precedes cue dispatch, so an ANI cue starts exactly at this
    // schedule boundary instead of immediately advancing by the preceding dt.
    for(auto* spine:i.managedSpines)spine->update(std::max(0.f,delta));
    for(auto index:i.clock.advance(delta)){dispatch(index);if(i.finished)return;}
    if(i.shake&&i.clock.elapsed()<=i.shakeEnd){
        const auto& animations=member(member(*i.shake,"skeleton"),"animations");
        if(animations.IsObject()&&animations.MemberCount()){
            const auto& bones=member(animations.MemberBegin()->value,"bones");
            if(bones.IsObject()&&bones.MemberCount()){
                const J* tracks=&bones.MemberBegin()->value;if(bones.HasMember("camera"))tracks=&bones["camera"];
                auto t=float(i.clock.elapsed())-i.shakeStart;const auto& keys=member(*tracks,"translate");
                i.world->setPosition(sample(keys,t,"x")*i.units,sample(keys,t,"y")*i.units);
            }
        }
    }else i.world->setPosition(Vec2::ZERO);
    i.syncStage();
    if(i.clock.complete())finish(false,false);
}
void SkillPresentation::finish(bool skipped,bool remove){
    auto&i=*impl_;if(i.finished)return;i.finished=true;i.clock.stop();unscheduleAllCallbacks();stopAllActions();
    i.restoreStage();
    removeAllChildrenWithCleanup(true);i.named.clear();i.groupLayers.clear();i.managedSpines.clear();auto callback=i.callbacks.onComplete;i.callbacks={};
    retain();if(callback)callback(skipped);if(remove&&getParent())removeFromParentAndCleanup(true);release();
}
void SkillPresentation::skip(){finish(true,false);}
void SkillPresentation::setPaused(bool paused){
    // Freeze visual clocks without disabling the Skip menu and keyboard listener.
    std::function<void(Node*)> visit=[&](Node*n){
        if(paused){n->getScheduler()->pauseTarget(n);n->getActionManager()->pauseTarget(n);}
        else{n->getScheduler()->resumeTarget(n);n->getActionManager()->resumeTarget(n);}
        for(auto*child:n->getChildren())visit(child);
    };visit(this);
}
void SkillPresentation::onExit(){if(!impl_->finished){impl_->callbacks={};finish(true,false);}Node::onExit();}
std::string SkillPresentation::diagnostic()const{std::ostringstream text;text<<impl_->choice.id<<" "<<impl_->clock.elapsed()<<"/"<<impl_->clock.duration()<<" "<<impl_->current;return text.str();}
