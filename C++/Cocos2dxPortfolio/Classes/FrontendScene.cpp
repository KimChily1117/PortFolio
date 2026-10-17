#include "FrontendScene.h"
#include "Presentation.h"
#include "GameScene.h"
#include "ResourceGallery.h"
#include "SaveStore.h"
#include "SpinePreview.h"
#include "AnimatedCutscene.h"
#include "SkillPresentation.h"
#include "ReferenceAudio.h"
#include "AudioLibraryScene.h"
#include "json/document.h"
#include "ui/UITextField.h"
#include <ctime>
#include <cctype>
#include <memory>
#include <cstdlib>
USING_NS_CC;
using namespace presentation;
namespace {
const Color3B Ink(246, 238, 217), Muted(191, 182, 165), Gold(236, 201, 126), Teal(152, 218, 201);
const std::string Art = "reference/epicseven/frontend/";
std::string field(const rapidjson::Value& v, const char* k) {
    return v.HasMember(k) && v[k].IsString() ? v[k].GetString() : "";
}
bool localPath(const std::string& s) {
    return s.find("reference/") == 0 && s.find("..") == std::string::npos &&
           s.find(':') == std::string::npos && s.find('\\') == std::string::npos;
}
std::string folded(std::string s) {
    for (char& c : s)
        if (static_cast<unsigned char>(c) < 128)
            c = static_cast<char>(std::tolower(c));
    return s;
}
Sprite* picture(Node* parent, const std::string& path, Rect box, bool fill = false) {
    if (!FileUtils::getInstance()->isFileExist(path))
        return nullptr;
    auto s = Sprite::create(path);
    if (!s || s->getContentSize().width <= 0 || s->getContentSize().height <= 0)
        return nullptr;
    auto sz = s->getContentSize();
    s->getTexture()->setAntiAliasTexParameters();
    s->setScale(fill ? std::max(box.size.width / sz.width, box.size.height / sz.height)
                     : std::min(box.size.width / sz.width, box.size.height / sz.height));
    s->setPosition(box.getMidX(), box.getMidY());
    parent->addChild(s);
    return s;
}
Node* action(Node* parent, const std::string& title, Rect box, std::function<void()> callback,
             bool primary = false, float font = 18) {
    auto n = button(
        parent, title, box, rgba(26, 24, 24, 235),
        [callback] {
            ReferenceAudio::playUi();
            callback();
        },
        font);
    if (FileUtils::getInstance()->isFileExist(Art + (primary ? "_btn_green.png" : "_btn_gold.png"))) {
        auto s = Sprite::create(Art + (primary ? "_btn_green.png" : "_btn_gold.png"));
        if (s) {
            // Hide the helper's drawn panel before inserting the original button image.
            n->getChildren().at(0)->setVisible(false);
            s->setPosition(box.size.width / 2, box.size.height / 2);
            s->setScaleX(box.size.width / s->getContentSize().width);
            s->setScaleY(box.size.height / s->getContentSize().height);
            n->addChild(s, -1);
        }
    }
    return n;
}
void fittedText(Node* parent, const std::string& value, float size, Vec2 pos, float width, Color3B color) {
    auto l = text(parent, value, size, pos, color, true);
    if (l->getContentSize().width > width)
        l->setScale(width / l->getContentSize().width);
}
} // namespace
bool FrontendScene::init() {
    if (!Scene::init())
        return false;
    aster::SaveStore store(FileUtils::getInstance()->getWritablePath() + "aster-profile-v1.json");
    std::string error;
    store.load(profile_, error);
    saveBlocked_ = !error.empty();
    if (!saveBlocked_ && !store.save(profile_, error))
        saveBlocked_ = true;
    ReferenceAudio::playMusic("lobby");
    loadReferenceCatalogs();
    const char* page = std::getenv("ASTER_FRONTEND_PAGE");
    std::string route = page ? page : "lobby";
    if (route == "worlds")
        showWorlds(0);
    else if (route == "ready")
        showReady(profile_.stage);
    else if (route == "heroes")
        showHeroes();
    else if (route == "hero" && !heroes_.empty())
        showHero(featured_);
    else
        showLobby();
    return true;
}
std::string FrontendScene::diagnostic() const {
    return "route=" + route_ + " portraits=" + std::to_string(heroes_.size()) +
           " supportedSkills=" + std::to_string(skills_.size()) + " party=" + party_[0] + "," + party_[1] +
           "," + party_[2] + "," + party_[3] + " stage=" + std::to_string(profile_.stage) + "\n" +
           ReferenceAudio::diagnostic();
}
void FrontendScene::defer(std::function<void()> fn) {
    if (navigating_)
        return;
    navigating_ = true;
    scheduleOnce(
        [this, fn](float) {
            navigating_ = false;
            fn();
        },
        0, "navigate");
}
void FrontendScene::clearPage() {
    if (searchField_)
        searchField_->didNotSelectSelf();
    searchField_ = nullptr;
    removeAllChildren();
}
void FrontendScene::onExit() {
    if (searchField_)
        searchField_->didNotSelectSelf();
    searchField_ = nullptr;
    unschedule("navigate");
    Scene::onExit();
}
void FrontendScene::loadReferenceCatalogs() {
    auto read = [](const std::string& path, const char* root,
                   std::function<void(const rapidjson::Value&)> consume) {
        auto fs = FileUtils::getInstance();
        if (!fs->isFileExist(path))
            return;
        rapidjson::Document doc;
        doc.Parse(fs->getStringFromFile(path).c_str());
        if (doc.HasParseError() || !doc.IsObject() || !doc.HasMember(root) || !doc[root].IsArray())
            return;
        for (const auto& row : doc[root].GetArray())
            if (row.IsObject())
                consume(row);
    };
    read("reference/epicseven/catalog.json", "items", [this](const rapidjson::Value& v) {
        auto path = field(v, "path"), atlas = field(v, "atlas");
        if (field(v, "type") != "spine" || path.find("/portrait/") == std::string::npos || !localPath(path) ||
            !localPath(atlas))
            return;
        Hero h{field(v, "name"),   field(v, "display_name"), path, atlas,
               field(v, "gender"), field(v, "hero_id")};
        if (h.name.empty())
            h.name = h.id;
        heroes_.push_back(h);
    });
    read("reference/epicseven/cutscene-catalog.json", "items", [this](const rapidjson::Value& v) {
        auto path = field(v, "path");
        if (localPath(path))
            cuts_.push_back({path, field(v, "character_id"), field(v, "hero_id")});
    });
    read("reference/skills/catalog.json", "skills", [this](const rapidjson::Value& v) {
        if (!v.HasMember("playable") || !v["playable"].IsBool() || !v["playable"].GetBool())
            return;
        auto path = field(v, "portraitJson"), atlas = field(v, "portraitAtlas");
        if (!localPath(path) || !localPath(atlas))
            return;
        skills_.push_back({field(v, "id"), field(v, "name"), field(v, "heroId"), path, atlas});
        const auto heroId = field(v, "heroId");
        if (std::none_of(heroes_.begin(), heroes_.end(),
                         [&heroId](const Hero& h) { return h.id == heroId; })) {
            auto name = field(v, "display_name");
            if (name.empty())
                name = field(v, "name");
            heroes_.push_back({heroId, name, path, atlas, field(v, "gender"), heroId});
        }
    });
    // Start with a supported S3 hero; keep the user's explicit lobby selection.
    auto saved = UserDefault::getInstance()->getStringForKey("reference-lobby-hero", "c1048");
    bool picked = false;
    for (size_t i = 0; i < heroes_.size(); ++i) {
        if (!picked && heroes_[i].gender == "female") {
            featured_ = i;
            picked = true;
        }
        if (heroes_[i].id == saved) {
            featured_ = i;
            break;
        }
    }
    for (int i = 0; i < aster::HeroCount; ++i) {
        std::string key = "reference-skill-slot-" + std::to_string(i);
        auto fallback = skills_.empty() ? "" : skills_[i % skills_.size()].id;
        party_[i] = UserDefault::getInstance()->getStringForKey(key.c_str(), fallback);
        if (std::none_of(skills_.begin(), skills_.end(),
                         [this, i](const Skill& s) { return s.id == party_[i]; }))
            party_[i] = fallback;
    }
}
void FrontendScene::background() {
    addChild(LayerColor::create(Color4B(29, 24, 21, 255), 1280, 720));
    picture(this, Art + "lobby_pub_mockup.png", Rect(0, 0, 1280, 720), true);
}
void FrontendScene::chrome(const std::string& title, std::function<void()> back) {
    panel(this, Rect(0, 650, 1280, 70), rgba(17, 18, 23, 238), rgba(118, 95, 61));
    panel(this, Rect(0, 0, 1280, 34), rgba(17, 18, 23, 240), rgba(62, 52, 41));
    if (back)
        action(
            this, "<", Rect(20, 662, 55, 44), [this, back] { defer(back); }, false, 23);
    text(this, title, 25, Vec2(back ? 94 : 30, 686), Ink, true);
    text(this, "G  " + std::to_string(profile_.gold), 19, Vec2(948, 685), Gold, true);
    text(this, "CLEAR  " + std::to_string(profile_.bestCleared) + " / 50", 16, Vec2(1110, 685), Muted, true);
    text(this,
         saveBlocked_ ? "저장 파일 확인 필요 · 현재 진행은 저장되지 않습니다"
                      : "OFFLINE  ·  로컬 모험 / 캐릭터 컬렉션",
         12, Vec2(25, 17), Muted, true);
    text(this, "에픽세븐 리소스 기반 재구현", 12, Vec2(1110, 17), Muted);
}
SpinePreview* FrontendScene::animatedHero(const std::string& path, const std::string& atlas, Rect box) {
    std::string error;
    auto node = SpinePreview::create(path, atlas, error);
    if (!node) {
        text(this, "캐릭터 리소스를 불러올 수 없습니다", 14, Vec2(box.getMidX(), box.getMidY()), Muted);
        return nullptr;
    }
    const auto& skins = node->skins();
    auto normal = std::find(skins.begin(), skins.end(), "normal");
    if (normal != skins.end())
        node->setSkin(static_cast<size_t>(normal - skins.begin()));
    const auto& anims = node->animations();
    auto idle = std::find(anims.begin(), anims.end(), "idle");
    if (idle != anims.end())
        node->play(static_cast<size_t>(idle - anims.begin()));
    auto b = node->localBounds();
    if (b.size.width <= 0 || b.size.height <= 0)
        return nullptr;
    float scale = std::min(box.size.width / b.size.width, box.size.height / b.size.height);
    node->setScale(scale);
    node->setPosition(box.getMidX() - b.getMidX() * scale, box.getMidY() - b.getMidY() * scale);
    addChild(node);
    return node;
}
void FrontendScene::gallery() {
    Director::getInstance()->replaceScene(
        TransitionFade::create(.2f, ResourceGallery::create(), Color3B(20, 19, 22)));
}
void FrontendScene::showLobby() {
    route_ = "lobby";
    clearPage();
    background();
    if (!heroes_.empty())
        animatedHero(heroes_[featured_].path, heroes_[featured_].atlas, Rect(205, 132, 610, 493));
    panel(this, Rect(30, 452, 227, 165), rgba(22, 21, 25, 218), rgba(145, 115, 67));
    text(this, "여정의 쉼터", 26, Vec2(48, 581), Gold, true);
    text(this, "다음 모험을 준비하세요", 15, Vec2(48, 540), Ink, true);
    text(this, "영웅 · 편성 · 모험", 14, Vec2(48, 509), Muted, true);
    text(this, "서버 연결 없는 로컬 플레이", 12, Vec2(48, 480), Muted, true);
    panel(this, Rect(903, 189, 347, 426), rgba(17, 20, 27, 235), rgba(145, 115, 67));
    text(this, "모 험", 31, Vec2(925, 573), Gold, true);
    int next = std::min(aster::MaxStage, profile_.bestCleared + 1);
    text(this, "CHAPTER  " + std::to_string((next - 1) / 5 + 1), 12, Vec2(925, 529), Muted, true);
    text(this, worldName((next - 1) / 5), 24, Vec2(925, 495), Ink, true);
    text(this, "다음 지역   " + stageName(next), 18, Vec2(925, 455), Teal, true);
    action(
        this, "모험 시작   >", Rect(922, 370, 310, 64),
        [this, next] { defer([this, next] { showWorlds((next - 1) / 5); }); }, true, 23);
    action(this, "최근 스테이지  " + stageName(profile_.stage), Rect(922, 300, 310, 54),
           [this] { defer([this] { showReady(profile_.stage); }); });
    text(this, "진행도와 전투 보상은 기기에 저장됩니다.", 12, Vec2(925, 269), Muted, true);
    text(this, "50개 지역 · 4인 편성", 14, Vec2(925, 232), Gold, true);
    if (!heroes_.empty()) {
        panel(this, Rect(308, 144, 438, 45), rgba(19, 20, 28, 202), rgba(102, 83, 56));
        fittedText(this, heroes_[featured_].name, 19, Vec2(326, 167), 400, Ink);
    }
    auto h = action(this, "영웅 컬렉션", Rect(30, 52, 250, 70), [this] { defer([this] { showHeroes(); }); });
    picture(h, Art + "main_hud_btn_hero.png", Rect(4, 5, 55, 60));
    auto g = action(this, "리소스 도감", Rect(295, 52, 250, 70), [this] { defer([this] { gallery(); }); });
    picture(g, Art + "main_hud_btn_archieve.png", Rect(4, 5, 55, 60));
    action(this, "팀 편성", Rect(560, 52, 250, 70),
           [this, next] { defer([this, next] { showReady(next); }); });
    auto m = action(
        this, "월드맵", Rect(903, 52, 347, 70),
        [this, next] { defer([this, next] { showWorlds((next - 1) / 5); }); }, true, 24);
    picture(m, Art + "main_hud_btn_battle.png", Rect(12, 4, 68, 64));
    action(this, "사운드 라이브러리", Rect(30, 380, 227, 48), [this] {
        defer([] {
            Director::getInstance()->replaceScene(AudioLibraryScene::create(
                [] { Director::getInstance()->replaceScene(FrontendScene::create()); }));
        });
    });
    chrome("에픽세븐  /  로컬 로비");
}
void FrontendScene::showWorlds(int world) {
    route_ = "worlds";
    clearPage();
    background();
    picture(this, Art + "worldmap_paper.png", Rect(0, 34, 1280, 616), true);
    panel(this, Rect(20, 117, 900, 456), rgba(30, 31, 30, 35), rgba(105, 78, 45));
    picture(this, Art + "region-" + std::to_string(world + 1) + ".png", Rect(100, 145, 690, 405));
    auto lines = DrawNode::create();
    addChild(lines);
    std::array<Vec2, 5> points{{{145, 245}, {305, 350}, {465, 278}, {625, 406}, {785, 335}}};
    for (int i = 0; i < 4; ++i)
        lines->drawSegment(points[i], points[i + 1], 2, rgba(65, 47, 31, 200));
    for (int i = 0; i < 5; ++i) {
        int stage = world * 5 + i + 1;
        bool open = aster::canEnterStage(profile_, stage);
        auto p = points[i];
        lines->drawSolidCircle(p, 31, 0, 40, open ? rgba(40, 83, 75) : rgba(65, 59, 52));
        lines->drawCircle(p, 32, 0, 40, false, rgba(237, 208, 144));
        auto n = button(
            this, open ? stageName(stage) : "잠김", Rect(p.x - 40, p.y - 32, 80, 64), rgba(0, 0, 0, 0),
            [this, stage, open] {
                if (open)
                    defer([this, stage] { showReady(stage); });
            },
            17);
        n->getChildren().at(0)->setVisible(false);
        text(this,
             stage <= profile_.bestCleared ? "★  CLEAR"
             : i == 4                      ? "BOSS"
                                           : "STAGE",
             12, Vec2(p.x, p.y - 51), Color3B(57, 44, 27));
    }
    panel(this, Rect(947, 119, 308, 458), rgba(24, 24, 29, 239), rgba(153, 125, 76));
    text(this, "CHAPTER " + std::to_string(world + 1), 13, Vec2(968, 548), Gold, true);
    fittedText(this, worldName(world), 24, Vec2(968, 507), 264, Ink);
    text(this, "지역을 선택해 출전을 준비하세요.", 13, Vec2(968, 463), Muted, true);
    text(this, "진행 조건", 17, Vec2(968, 402), Gold, true);
    text(this, "이전 지역 클리어 후 다음 지역 개방", 13, Vec2(968, 369), Ink, true);
    text(this, "획득 보상", 17, Vec2(968, 312), Gold, true);
    text(this, "골드 · 로컬 진행도", 16, Vec2(968, 278), Ink, true);
    text(this, "재도전 가능 / 입장 재화 소모 없음", 12, Vec2(968, 226), Muted, true);
    text(this, "지역 이름과 진행 규칙: ASTER", 12, Vec2(968, 171), Muted, true);
    for (int w = 0; w < 10; ++w)
        action(
            this, std::to_string(w + 1), Rect(25 + w * 123, 590, 113, 42),
            [this, w] { defer([this, w] { showWorlds(w); }); }, world == w, 17);
    text(this, "지도에서 열린 지역을 선택하세요", 18, Vec2(462, 79), Color3B(62, 43, 26));
    action(this, "로비", Rect(953, 49, 296, 50), [this] { defer([this] { showLobby(); }); });
    chrome("모험  /  월드맵", [this] { showLobby(); });
}
void FrontendScene::showReady(int stage) {
    route_ = "ready";
    clearPage();
    background();
    panel(this, Rect(0, 34, 1280, 616), rgba(16, 19, 25, 200), rgba(67, 53, 34));
    text(this, "TEAM 01", 14, Vec2(37, 615), Gold, true);
    text(this, "출전 영웅", 27, Vec2(37, 580), Ink, true);
    auto defs = aster::defaultHeroes();
    for (int i = 0; i < aster::HeroCount; ++i) {
        float x = 28 + i * 216;
        panel(this, Rect(x, 151, 202, 389), rgba(28, 29, 34, 220), rgba(122, 99, 66));
        auto found = std::find_if(skills_.begin(), skills_.end(),
                                  [this, i](const Skill& s) { return s.id == party_[i]; });
        if (found != skills_.end()) {
            animatedHero(found->portrait, found->atlas, Rect(x + 13, 260, 176, 263));
            fittedText(this, found->name, 20, Vec2(x + 15, 238), 172, Ink);
        } else {
            HeroAnimator::create(this, i, Vec2(x + 101, 381), 230);
            fittedText(this, defs[i].name, 20, Vec2(x + 15, 238), 172, Ink);
        }
        auto role = found == skills_.end() ? defs[i].role : found->heroId == "c1017" ? "HEALER" : "ATTACKER";
        text(this, "LV " + std::to_string(profile_.levels[i]) + "  /  " + role, 14, Vec2(x + 15, 203), Gold,
             true);
        action(
            this, "영웅 변경  >", Rect(x + 13, 162, 176, 29),
            [this, i, stage] {
                defer([this, i, stage] {
                    if (!skills_.empty()) {
                        auto it = std::find_if(skills_.begin(), skills_.end(),
                                               [this, i](const Skill& s) { return s.id == party_[i]; });
                        size_t next = it == skills_.end()
                                          ? 0
                                          : (static_cast<size_t>(it - skills_.begin()) + 1) % skills_.size();
                        for (int j = 0; j < aster::HeroCount; ++j)
                            if (j != i && party_[j] == skills_[next].id)
                                party_[j] = party_[i];
                        party_[i] = skills_[next].id;
                        for (int j = 0; j < aster::HeroCount; ++j)
                            UserDefault::getInstance()->setStringForKey(
                                ("reference-skill-slot-" + std::to_string(j)).c_str(), party_[j]);
                        UserDefault::getInstance()->flush();
                    }
                    showReady(stage);
                });
            },
            false, 14);
    }
    panel(this, Rect(904, 151, 348, 462), rgba(23, 23, 28, 240), rgba(151, 122, 71));
    text(this, "STAGE " + stageName(stage), 14, Vec2(927, 580), Gold, true);
    fittedText(this, worldName((stage - 1) / 5), 25, Vec2(927, 538), 300, Ink);
    text(this, stage % 5 == 0 ? "보스 전투" : "일반 전투", 20, Vec2(927, 490), Ink, true);
    text(this, "획득 골드  " + std::to_string(aster::stageReward(stage)), 18, Vec2(927, 433), Gold, true);
    text(this, "4인 팀 · 턴제 전투", 15, Vec2(927, 377), Muted, true);
    text(this, "스킬을 고른 뒤 공격 대상을 선택하세요.", 13, Vec2(927, 344), Muted, true);
    text(this, skills_.empty() ? "기본 ASTER 영웅으로 출전합니다." : "S3 재생을 지원하는 영웅만 편성됩니다.",
         12, Vec2(927, 275), Teal, true);
    text(this, "능력치와 승패 규칙은 로컬 프로토타입 기준", 12, Vec2(927, 244), Muted, true);
    action(
        this, "전투 시작  >", Rect(921, 168, 314, 58),
        [this, stage] { defer([this, stage] { enterStage(stage); }); }, true, 23);
    action(this, "<  지역 선택", Rect(28, 57, 267, 56),
           [this, stage] { defer([this, stage] { showWorlds((stage - 1) / 5); }); });
    action(this, "영웅 둘러보기", Rect(314, 57, 267, 56), [this] { defer([this] { showHeroes(); }); });
    chrome("모험  /  출전 준비", [this, stage] { showWorlds((stage - 1) / 5); });
}
void FrontendScene::showHeroes(int page) {
    route_ = "heroes";
    clearPage();
    background();
    panel(this, Rect(0, 34, 1280, 616), rgba(15, 18, 26, 228), rgba(62, 52, 41));
    if (!heroes_.empty())
        animatedHero(heroes_[featured_].path, heroes_[featured_].atlas, Rect(10, 94, 560, 534));
    panel(this, Rect(587, 115, 666, 456), rgba(21, 25, 33, 237), rgba(107, 89, 62));
    std::vector<size_t> filtered;
    for (size_t i = 0; i < heroes_.size(); ++i)
        if ((!femaleOnly_ || heroes_[i].gender == "female") &&
            (heroSearch_.empty() ||
             folded(heroes_[i].id + " " + heroes_[i].name).find(folded(heroSearch_)) != std::string::npos))
            filtered.push_back(i);
    int pages = std::max(1, static_cast<int>((filtered.size() + 7) / 8));
    heroPage_ = std::max(0, std::min(page, pages - 1));
    for (int row = 0; row < 8; ++row) {
        size_t n = heroPage_ * 8 + row;
        if (n >= filtered.size())
            break;
        size_t index = filtered[n];
        float y = 515 - row * 49;
        auto b = button(this, "", Rect(599, y, 640, 45), rgba(39, 42, 48),
                        [this, index] { defer([this, index] { showHero(index); }); });
        picture(b, Art + heroes_[index].id + "_face.png", Rect(4, 2, 42, 42));
        fittedText(b, heroes_[index].name, 17, Vec2(58, 24), 410, Ink);
        text(b, heroes_[index].id, 12, Vec2(531, 24), Muted);
        text(b, ">", 20, Vec2(615, 24), Gold);
    }
    if (filtered.empty())
        text(this, "일치하는 영웅이 없습니다", 18, Vec2(920, 340), Muted);
    auto search = ui::TextField::create("이름 또는 ID 검색", "fonts/NotoSansKR.otf", 17);
    searchField_ = search;
    search->setPlaceHolderColor(Color4B(Muted));
    search->setTextColor(Color4B(Ink));
    search->ignoreContentAdaptWithSize(false);
    search->setContentSize(Size(405, 44));
    search->setAnchorPoint(Vec2(0, .5f));
    search->setPosition(Vec2(602, 602));
    search->setString(heroSearch_);
    addChild(search);
    action(
        this, "검색", Rect(1100, 580, 143, 43),
        [this, search] {
            heroSearch_ = search->getString();
            search->didNotSelectSelf();
            defer([this] { showHeroes(0); });
        },
        true, 16);
    action(
        this, femaleOnly_ ? "여성 영웅  ✓" : "전체 영웅", Rect(28, 576, 224, 47),
        [this] {
            femaleOnly_ = !femaleOnly_;
            defer([this] { showHeroes(0); });
        },
        false, 16);
    action(this, "<", Rect(600, 58, 85, 43), [this] { defer([this] { showHeroes(heroPage_ - 1); }); });
    text(this,
         std::to_string(heroPage_ + 1) + " / " + std::to_string(pages) + "   ·   " +
             std::to_string(filtered.size()) + "명",
         16, Vec2(917, 80), Ink);
    action(this, ">", Rect(1155, 58, 85, 43), [this] { defer([this] { showHeroes(heroPage_ + 1); }); });
    text(this, "영웅을 선택하면 표정과 스킬 컷신을 볼 수 있습니다.", 13, Vec2(284, 68), Muted);
    chrome("영웅 컬렉션", [this] { showLobby(); });
}
void FrontendScene::showHero(size_t index) {
    route_ = "hero";
    if (index >= heroes_.size())
        return;
    clearPage();
    background();
    panel(this, Rect(0, 34, 1280, 616), rgba(14, 17, 25, 204), rgba(64, 51, 35));
    auto& h = heroes_[index];
    auto preview = animatedHero(h.path, h.atlas, Rect(38, 81, 645, 550));
    panel(this, Rect(730, 128, 516, 484), rgba(21, 24, 32, 237), rgba(147, 119, 74));
    text(this, "HERO COLLECTION", 12, Vec2(754, 583), Gold, true);
    fittedText(this, h.name, 29, Vec2(754, 538), 464, Ink);
    text(this, h.id, 15, Vec2(755, 493), Muted, true);
    text(this, "움직이는 초상화", 18, Vec2(755, 438), Teal, true);
    text(this, "캐릭터의 표정과 스킬 연출을 감상하세요.", 15, Vec2(755, 402), Muted, true);
    if (preview && !preview->skins().empty()) {
        auto label = text(this, "", 15, Vec2(987, 348), Gold);
        auto current = std::make_shared<size_t>(0);
        auto normal = std::find(preview->skins().begin(), preview->skins().end(), "normal");
        if (normal != preview->skins().end())
            *current = static_cast<size_t>(normal - preview->skins().begin());
        label->setString("표정: " + preview->skins()[*current]);
        action(
            this, "표정 변경", Rect(754, 284, 220, 45),
            [preview, current, label] {
                *current = (*current + 1) % preview->skins().size();
                preview->setSkin(*current);
                label->setString("표정: " + preview->skins()[*current]);
            },
            false, 16);
        auto paused = std::make_shared<bool>(false);
        action(
            this, "재생 / 정지", Rect(990, 284, 231, 45),
            [preview, paused] {
                *paused = !*paused;
                preview->setAnimationPaused(*paused);
            },
            false, 16);
    }
    std::vector<std::string> clips;
    for (const auto& c : cuts_)
        if (c.character == h.id || (!h.heroId.empty() && c.heroId == h.heroId))
            clips.push_back(c.path);
    if (!clips.empty()) {
        auto selected = std::make_shared<size_t>(0);
        auto label = text(this, "스킬 컷신 1 / " + std::to_string(clips.size()), 14, Vec2(986, 258), Muted);
        action(
            this, "컷신 재생", Rect(754, 199, 327, 45),
            [this, index, clips, selected] {
                auto path = clips[*selected];
                defer([this, index, path] { showCutscene(path, index); });
            },
            true, 17);
        action(
            this, ">", Rect(1095, 199, 125, 45),
            [selected, clips, label] {
                *selected = (*selected + 1) % clips.size();
                label->setString("스킬 컷신 " + std::to_string(*selected + 1) + " / " +
                                 std::to_string(clips.size()));
            },
            false, 18);
    } else
        text(this, "이 초상화에 연결된 컷신이 없습니다.", 14, Vec2(987, 222), Muted);
    text(this, "컷신은 스킬 연출의 영상 구간입니다.", 12, Vec2(987, 158), Muted);
    auto supported =
        std::find_if(skills_.begin(), skills_.end(), [&h](const Skill& s) { return s.heroId == h.id; });
    if (supported != skills_.end()) {
        const auto id = supported->id;
        action(
            this, "S3 연출 보기", Rect(40, 53, 642, 52),
            [this, index, id] { defer([this, index, id] { showSkill(id, index); }); }, true, 19);
    }
    action(
        this, "로비 영웅으로 설정", Rect(750, 57, 490, 52),
        [this, index] {
            featured_ = index;
            UserDefault::getInstance()->setStringForKey("reference-lobby-hero", heroes_[index].id);
            UserDefault::getInstance()->flush();
            defer([this] { showLobby(); });
        },
        true);
    chrome("영웅  /  상세", [this] { showHeroes(heroPage_); });
}
void FrontendScene::showCutscene(const std::string& path, size_t hero) {
    route_ = "cutscene";
    clearPage();
    addChild(LayerColor::create(Color4B(4, 5, 9, 255), 1280, 720));
    std::string error;
    auto clip = AnimatedCutscene::create(path, error);
    if (clip) {
        auto size = clip->getContentSize();
        float scale = std::min(1220.f / size.width, 550.f / size.height);
        clip->setScale(scale);
        clip->setPosition(640 - size.width * scale / 2, 365 - size.height * scale / 2);
        addChild(clip);
        action(
            this, "처음부터", Rect(505, 49, 270, 49),
            [clip] {
                clip->setPaused(false);
                clip->restart();
            },
            true, 17);
    } else
        text(this, "컷신을 불러올 수 없습니다: " + error, 15, Vec2(640, 360), Muted);
    chrome("스킬 컷신", [this, hero] { showHero(hero); });
}
void FrontendScene::showSkill(const std::string& skill, size_t hero) {
    route_ = "skill";
    clearPage();
    addChild(LayerColor::create(Color4B(4, 5, 9, 255), 1280, 720));
    SkillPresentation::Callbacks callbacks;
    callbacks.onComplete = [this, hero](bool) {
        ReferenceAudio::stopEffects();
        defer([this, hero] { showHero(hero); });
    };
    std::string error;
    auto preview = SkillPresentation::create(skill, callbacks, error);
    if (preview) {
        addChild(preview);
        ReferenceAudio::playSkill(skill.substr(0, skill.find('.')), 3);
    } else {
        text(this, "S3 연출을 불러올 수 없습니다: " + error, 15, Vec2(640, 360), Muted);
        chrome("스킬 연출", [this, hero] { showHero(hero); });
    }
}
void FrontendScene::enterStage(int stage) {
    if (!aster::selectStage(profile_, stage))
        return;
    aster::SaveStore store(FileUtils::getInstance()->getWritablePath() + "aster-profile-v1.json");
    std::string error;
    if (saveBlocked_ || !store.save(profile_, error)) {
        saveBlocked_ = true;
        text(this, "저장 파일을 확인해 주세요. 출전을 시작할 수 없습니다.", 15, Vec2(640, 130), Gold);
        return;
    }
    for (int i = 0; i < aster::HeroCount; ++i)
        UserDefault::getInstance()->setStringForKey(("reference-skill-slot-" + std::to_string(i)).c_str(),
                                                    party_[i]);
    UserDefault::getInstance()->flush();
    Director::getInstance()->replaceScene(
        TransitionFade::create(.3f, GameScene::create(), Color3B(10, 14, 20)));
}
