#include "AudioLibraryScene.h"
#include "ReferenceAudio.h"
#include "ui/UITextField.h"
#include <algorithm>
#include <cctype>
#include <iomanip>
#include <sstream>

USING_NS_CC;
namespace {
Label* label(Node*parent,const std::string&text,float font,Vec2 position,const Color3B&color=Color3B(230,225,211)){
    auto result=Label::createWithTTF(text,"fonts/NotoSansKR.otf",font);if(!result)result=Label::createWithSystemFont(text,"Arial",font);
    result->setAnchorPoint(Vec2(0,.5f));result->setPosition(position);result->setTextColor(Color4B(color));parent->addChild(result);return result;
}
void button(Node*parent,const std::string&text,Rect frame,const std::function<void()>&callback){
    auto back=LayerColor::create(Color4B(49,54,66,255),frame.size.width,frame.size.height);back->setPosition(frame.origin);parent->addChild(back);
    auto textLabel=Label::createWithTTF(text,"fonts/NotoSansKR.otf",16);if(!textLabel)textLabel=Label::createWithSystemFont(text,"Arial",16);
    auto item=MenuItemLabel::create(textLabel,[callback](Ref*){callback();});auto menu=Menu::create(item,nullptr);menu->setPosition(frame.getMidX(),frame.getMidY());parent->addChild(menu);
}
std::string lower(std::string value){for(auto&c:value)c=char(std::tolower(static_cast<unsigned char>(c)));return value;}
}
AudioLibraryScene* AudioLibraryScene::create(const std::function<void()>&back){auto scene=new AudioLibraryScene;if(!scene->init()||!scene->load(back)){delete scene;return nullptr;}scene->autorelease();return scene;}
bool AudioLibraryScene::load(const std::function<void()>&back){
    onBack_=back;ReferenceAudio::stopMusic();ReferenceAudio::stopEffects();
    addChild(LayerColor::create(Color4B(19,23,32,255),1280,720));
    label(this,"사운드 라이브러리",29,Vec2(30,680));
    label(this,"원본 샘플명 검색 · 영웅 ID(c1037), 음원명 또는 뱅크명",15,Vec2(30,642),Color3B(171,179,195));
    button(this,"돌아가기",Rect(1115,657,135,42),[this]{search_->didNotSelectSelf();later([this]{ReferenceAudio::stopEffects();if(onBack_)onBack_();});});
    auto inputBack=LayerColor::create(Color4B(36,42,54,255),780,40);inputBack->setPosition(30,583);addChild(inputBack);
    search_=ui::TextField::create("샘플명 / 영웅 ID / bank", "fonts/NotoSansKR.otf",17);
    search_->ignoreContentAdaptWithSize(false);search_->setContentSize(Size(760,40));search_->setAnchorPoint(Vec2(0,.5f));search_->setPosition(Vec2(40,603));addChild(search_);
    button(this,"검색",Rect(825,583,100,40),[this]{search_->didNotSelectSelf();later([this]{filter();});});
    button(this,"초기화",Rect(935,583,100,40),[this]{search_->didNotSelectSelf();search_->setString("");category_.clear();later([this]{filter();});});
    button(this,"재생 중지",Rect(1045,583,205,40),[this]{ReferenceAudio::stopTrack(previewId_);previewId_=-1;});
    const std::vector<std::pair<std::string,std::string>> categories={{"전체",""},{"음악","music"},{"음성","voice"},{"효과음","effect"},{"UI","ui"},{"영상","cinema"}};
    for(size_t n=0;n<categories.size();++n){auto entry=categories[n];button(this,entry.first,Rect(30+float(n)*108,531,100,36),[this,entry]{category_=entry.second;later([this]{filter();});});}
    summary_=label(this,"",15,Vec2(700,549));rows_=Node::create();addChild(rows_);
    button(this,"이전",Rect(30,45,100,36),[this]{if(page_>0){--page_;later([this]{rows();});}});
    button(this,"다음",Rect(145,45,100,36),[this]{if(size_t((page_+1)*10)<filtered_.size()){++page_;later([this]{rows();});}});
    label(this,"미확인 이벤트 연결을 포함한 모든 추출 샘플을 개별로 재생합니다.",13,Vec2(270,63),Color3B(171,179,195));
    status_=label(this,"",11,Vec2(30,22),Color3B(151,165,184));status_->setDimensions(1220,28);status_->setOverflow(Label::Overflow::SHRINK);
    schedule([this](float){status_->setString(ReferenceAudio::diagnostic());},.25f,"audio-status");
    filter();return true;
}
void AudioLibraryScene::later(const std::function<void()>&work){scheduleOnce([work](float){work();},0,"audio-ui-refresh");}
void AudioLibraryScene::filter(){
    filtered_.clear();const auto query=lower(search_->getString());const auto& all=ReferenceAudio::catalog();
    for(size_t n=0;n<all.size();++n){const auto&t=all[n];if(!category_.empty()&&t.category!=category_)continue;
        if(query.empty()||lower(t.name+" "+t.id+" "+t.bank).find(query)!=std::string::npos)filtered_.push_back(n);
    }page_=0;rows();
}
void AudioLibraryScene::rows(){
    rows_->removeAllChildrenWithCleanup(true);const auto&all=ReferenceAudio::catalog();
    const size_t pages=std::max<size_t>(1,(filtered_.size()+9)/10);page_=std::min(page_,int(pages-1));
    summary_->setString(std::to_string(filtered_.size())+" / "+std::to_string(all.size())+"   "+std::to_string(page_+1)+" / "+std::to_string(pages)+"   "+(category_.empty()?"all":category_));
    for(size_t row=0;row<10;++row){size_t offset=size_t(page_)*10+row;if(offset>=filtered_.size())break;size_t index=filtered_[offset];const auto&t=all[index];float y=493-float(row)*43;
        auto back=LayerColor::create(row%2?Color4B(27,32,42,255):Color4B(31,36,47,255),1220,39);back->setPosition(30,y-19);rows_->addChild(back);
        auto name=label(rows_,t.name.empty()?t.id:t.name,14,Vec2(44,y+7));name->setDimensions(810,21);name->setOverflow(Label::Overflow::SHRINK);
        label(rows_,t.bank+"  |  "+t.id,10,Vec2(44,y-10),Color3B(155,165,181));
        std::ostringstream duration;duration<<std::fixed<<std::setprecision(2)<<t.duration<<" s";label(rows_,duration.str(),13,Vec2(890,y));
        button(rows_,"재생",Rect(1105,y-16,120,32),[this,index]{preview(index);});
    }
    if(filtered_.empty())label(rows_,"일치하는 샘플이 없습니다",20,Vec2(44,460));
}
void AudioLibraryScene::preview(size_t index){ReferenceAudio::stopTrack(previewId_);ReferenceAudio::setEnabled(true);previewId_=ReferenceAudio::playTrack(ReferenceAudio::catalog()[index].id);}
void AudioLibraryScene::onExit(){if(search_)search_->didNotSelectSelf();ReferenceAudio::stopTrack(previewId_);previewId_=-1;Scene::onExit();}
