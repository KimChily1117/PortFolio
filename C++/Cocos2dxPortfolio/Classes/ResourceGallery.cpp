#include "ResourceGallery.h"
#include "FrontendScene.h"
#include "Presentation.h"
#include "SpinePreview.h"
#include "AnimatedCutscene.h"
#include "audio/include/AudioEngine.h"
#include "json/document.h"
#include "ui/UITextField.h"
#include <cstdlib>
#include <cctype>

USING_NS_CC;
using namespace presentation;
namespace {
const Color3B Ink(235, 235, 227), Muted(144, 161, 179), Teal(125, 215, 204), Gold(231, 196, 129);

bool localAssetPath(const std::string& path) {
    const std::string prefix = "reference/epicseven/";
    return path.compare(0, prefix.size(), prefix) == 0 &&
           path.find("..") == std::string::npos && path.find('\\') == std::string::npos &&
           path.find(':') == std::string::npos;
}

void fitLabel(Label* label, float width) {
    if (label->getContentSize().width > width)
        label->setScale(width / label->getContentSize().width);
}
std::string searchable(std::string value) {
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    });
    return value;
}
} // namespace

bool ResourceGallery::init() {
    if (!Scene::init())
        return false;
    loadCatalog();
    std::string initialFilter = std::any_of(items_.begin(), items_.end(), [](const Item& item) {
        return item.type == "spine";
    }) ? "spine" : "image";
    if (std::any_of(items_.begin(), items_.end(), [this](const Item& item) {
        return matchesFilter(item, "female");
    })) initialFilter = "female";
    const char* requested = std::getenv("ASTER_GALLERY_ITEM");
    if (requested)
        for (const auto& item : items_)
            if (item.name == requested) {
                if (!matchesFilter(item, initialFilter)) initialFilter = item.type;
                break;
            }
    filter_ = initialFilter;
    for (size_t i = 0; i < items_.size(); ++i)
        if (matchesFilter(items_[i], filter_)) {
            if (requested && items_[i].name == requested) selection_ = filtered_.size();
            else if (!requested && filter_ == "spine" && items_[i].name == "c1001")
                selection_ = filtered_.size();
            filtered_.push_back(i);
        }
    showItem();
    schedule(CC_SCHEDULE_SELECTOR(ResourceGallery::updateAudio), .1f);
    return true;
}

void ResourceGallery::loadCatalog() {
    auto files = FileUtils::getInstance();
    const char* catalogs[] = {"reference/epicseven/catalog.json", "reference/epicseven/cutscene-catalog.json"};
    for (const auto path : catalogs) {
        if (!files->isFileExist(path)) continue;
        const auto json = files->getStringFromFile(path);
        rapidjson::Document document;
        document.Parse(json.c_str());
        if (document.HasParseError() || !document.IsObject() || !document.HasMember("items") ||
            !document["items"].IsArray()) {
            catalogError_ = "일부 리소스 목록을 읽을 수 없습니다.";
            continue;
        }
        for (const auto& value : document["items"].GetArray()) {
            if (!value.IsObject()) continue;
            auto stringField = [&value](const char* key) -> std::string {
                return value.HasMember(key) && value[key].IsString() ? value[key].GetString() : "";
            };
            Item item{stringField("type"), stringField("path"), stringField("name"), stringField("category"),
                      stringField("atlas"), stringField("display_name"), stringField("gender"), stringField("hero_id")};
            if ((item.type != "image" && item.type != "audio" && item.type != "spine" && item.type != "cutscene") ||
                !localAssetPath(item.path) || (item.type == "spine" && !localAssetPath(item.atlas))) continue;
            if (std::any_of(items_.begin(), items_.end(), [&item](const Item& existing) {
                return existing.type == item.type && existing.path == item.path;
            })) continue;
            if (item.name.empty()) item.name = item.path.substr(item.path.find_last_of('/') + 1);
            if (item.category.empty()) item.category = item.type;
            items_.push_back(std::move(item));
        }
    }
    if (items_.empty() && catalogError_.empty()) catalogError_ = "아직 가져온 리소스가 없습니다.";
}

void ResourceGallery::defer(std::function<void()> action) {
    scheduleOnce([action](float) { action(); }, 0, "gallery_navigation");
}

void ResourceGallery::selectFilter(const std::string& type) {
    filter_ = type;
    filtered_.clear();
    for (size_t i = 0; i < items_.size(); ++i)
        if (matchesFilter(items_[i], type))
            filtered_.push_back(i);
    selection_ = 0;
    showItem();
}

bool ResourceGallery::matchesFilter(const Item& item, const std::string& filter) const {
    if (filter == "female")
        return item.gender == "female" && matchesFilter(item, "portrait");
    if (filter == "portrait")
        return item.type == "spine" &&
               (item.path.find("/portrait/") != std::string::npos || item.category == "초상화");
    if (filter == "battle")
        return item.type == "spine" &&
               (item.path.find("/model/") != std::string::npos || item.category == "전투 모델");
    return item.type == filter;
}

void ResourceGallery::showItem() {
    closePicker();
    stopAudio();
    audioButtonLabel_ = nullptr;
    audioStatusLabel_ = nullptr;
    spine_ = nullptr;
    cutscene_ = nullptr;
    animationLabel_ = nullptr;
    skinLabel_ = nullptr;
    pauseLabel_ = nullptr;
    removeAllChildren();
    auto background = LayerGradient::create(Color4B(26, 44, 61, 255), Color4B(10, 19, 32, 255));
    background->setContentSize(Size(1280, 720));
    addChild(background);
    text(this, "RESOURCE GALLERY", 13, Vec2(35, 683), Teal, true);
    text(this, "에픽세븐 리소스", 29, Vec2(35, 645), Ink, true);
    const char* filterIds[] = {"female", "portrait", "battle", "cutscene", "spine", "image", "audio"};
    const char* filterTitles[] = {"여성 초상화", "초상화", "전투", "스킬 컷신", "캐릭터 전체", "이미지", "오디오"};
    for (int i = 0; i < 7; ++i) {
        const std::string id = filterIds[i];
        button(this, filterTitles[i], Rect(410 + i * 120, 630, 115, 48),
               filter_ == id ? rgba(37, 87, 84) : rgba(29, 43, 58),
               [this, id] { defer([this, id] { selectFilter(id); }); }, 14);
    }
    panel(this, Rect(35, 177, 1210, 424), rgba(14, 25, 37));
    button(this, "<  메인 메뉴", Rect(35, 37, 220, 51), rgba(27, 46, 58), [this] {
        defer([this] {
            stopAudio();
            Director::getInstance()->replaceScene(
                TransitionFade::create(.2f, FrontendScene::create(), Color3B(9, 18, 28)));
        });
    });
    text(this, "LOCAL RESOURCE PREVIEW", 11, Vec2(1245, 22), Muted)->setAnchorPoint(Vec2(1, .5f));
    button(this, "목록 · " + std::to_string(filtered_.size()) + "개", Rect(855, 37, 225, 51),
           rgba(37, 87, 84), [this] { defer([this] { openPicker(false); }); });
    if (filtered_.empty()) {
        text(this, catalogError_.empty() ? "이 분류에 표시할 리소스가 없습니다." : catalogError_,
             22, Vec2(640, 403), Ink);
        text(this, "가져온 캐릭터와 리소스는 이 화면에서 확인할 수 있습니다.",
             15, Vec2(640, 361), Muted);
        return;
    }

    const auto& item = items_[filtered_[selection_]];
    const auto title = item.displayName.empty() || item.displayName == item.name
                           ? item.name : item.displayName + " · " + item.name;
    fitLabel(text(this, title, 21, Vec2(35, 145), Ink, true), 800);
    fitLabel(text(this, item.category, 14, Vec2(35, 113), Gold, true), 800);
    text(this, std::to_string(selection_ + 1) + " / " + std::to_string(filtered_.size()),
         16, Vec2(1245, 142), Teal)->setAnchorPoint(Vec2(1, .5f));
    button(this, "<  이전", Rect(425, 37, 180, 51), rgba(29, 43, 58),
           [this] { defer([this] { navigate(-1); }); });
    button(this, "다음  >", Rect(625, 37, 180, 51), rgba(29, 43, 58),
           [this] { defer([this] { navigate(1); }); });

    if (!FileUtils::getInstance()->isFileExist(item.path)) {
        text(this, "리소스 파일을 찾을 수 없습니다.", 22, Vec2(640, 400), Ink);
        return;
    }
    if (item.type == "spine") {
        showSpine(item);
    } else if (item.type == "cutscene") {
        showCutscene(item);
    } else if (item.type == "image") {
        auto sprite = Sprite::create(item.path);
        if (!sprite || sprite->getContentSize().width <= 0 || sprite->getContentSize().height <= 0) {
            text(this, "이 이미지 형식을 표시할 수 없습니다.", 22, Vec2(640, 400), Ink);
            return;
        }
        const auto size = sprite->getContentSize();
        sprite->setScale(std::min(1170.f / size.width, 384.f / size.height));
        sprite->setPosition(Vec2(640, 389));
        addChild(sprite);
        // The sprite retains its texture; avoid keeping every visited full-size image cached.
        Director::getInstance()->getTextureCache()->removeTextureForKey(item.path);
    } else {
        text(this, "AUDIO PREVIEW", 15, Vec2(640, 493), Teal);
        auto playButton = button(this, "", Rect(500, 349, 280, 68), rgba(37, 87, 84),
                                 [this] { toggleAudio(); });
        audioButtonLabel_ = text(playButton, "재생", 23, Vec2(140, 34), Ink);
        audioStatusLabel_ = text(this, "버튼을 눌러 소리를 들어보세요.", 15, Vec2(640, 300), Muted);
    }
}

void ResourceGallery::showCutscene(const Item& item) {
    std::string error;
    cutscene_ = AnimatedCutscene::create(item.path, error);
    if (!cutscene_) {
        text(this, "스킬 컷신을 읽을 수 없습니다.", 22, Vec2(640, 420), Ink);
        fitLabel(text(this, error, 14, Vec2(640, 375), Muted), 1120);
        return;
    }
    const auto size = cutscene_->getContentSize();
    const float scale = size.width > 0 && size.height > 0 ? std::min(1170.f / size.width, 324.f / size.height) : 1.f;
    cutscene_->setAnchorPoint(Vec2(.5f, .5f));
    cutscene_->setScale(scale);
    cutscene_->setPosition(Vec2(640, 419));
    addChild(cutscene_);
    animationPaused_ = false;
    auto pauseButton = button(this, "", Rect(475, 191, 190, 45), rgba(37, 87, 84), [this] {
        if (!cutscene_) return;
        animationPaused_ = !animationPaused_;
        cutscene_->setPaused(animationPaused_);
        pauseLabel_->setString(animationPaused_ ? "재생" : "일시 정지");
    });
    pauseLabel_ = text(pauseButton, "일시 정지", 15, Vec2(95, 22), Ink);
    button(this, "처음부터", Rect(680, 191, 190, 45), rgba(29, 43, 58), [this] {
        if (!cutscene_) return;
        // Let restart retain its paused error state if decoding fails.
        cutscene_->setPaused(false);
        cutscene_->restart();
        animationPaused_ = false;
        pauseLabel_->setString("일시 정지");
    }, 15);
    text(this, std::to_string(cutscene_->frameCount()) + " frames · " +
                   StringUtils::format("%.1f초", cutscene_->durationSeconds()),
         14, Vec2(60, 213), Muted, true);
}

void ResourceGallery::showSpine(const Item& item) {
    std::string error;
    spine_ = SpinePreview::create(item.path, item.atlas, error);
    if (!spine_) {
        text(this, "캐릭터 애니메이션을 읽을 수 없습니다.", 22, Vec2(640, 420), Ink);
        fitLabel(text(this, error, 14, Vec2(640, 375), Muted), 1120);
        return;
    }
    auto clip = ClippingRectangleNode::create(Rect(55, 257, 1170, 324));
    addChild(clip);
    clip->addChild(spine_);
    animation_ = 0;
    const auto& animations = spine_->animations();
    for (size_t i = 0; i < animations.size(); ++i)
        if (animations[i] == "idle" || animations[i] == "Idle" || animations[i] == "b_idle") {
            animation_ = i;
            break;
        }
    skin_ = 0;
    const auto& skins = spine_->skins();
    for (size_t i = 0; i < skins.size(); ++i)
        if (skins[i] == "normal") {
            skin_ = i;
            break;
        }
    spine_->setSkin(skin_);
    spine_->play(animation_);
    const auto bounds = spine_->localBounds();
    if (bounds.size.width > 0 && bounds.size.height > 0) {
        spineBaseScale_ = std::min(1130.f / bounds.size.width, 300.f / bounds.size.height);
        spineCenter_ = bounds.origin + Vec2(bounds.size.width / 2, bounds.size.height / 2);
    } else {
        spineBaseScale_ = 1.f;
        spineCenter_ = Vec2::ZERO;
    }
    zoom_ = 1.f;
    animationPaused_ = false;
    fitSpine();
    button(this, "스킬·동작 목록 · " + std::to_string(animations.size()) + "개",
           Rect(900, 100, 345, 32), rgba(29, 43, 58),
           [this] { defer([this] { openPicker(true); }); }, 14);
    auto animationButton = button(this, "", Rect(55, 191, 310, 45), rgba(29, 43, 58),
                                  [this] { changeAnimation(); });
    animationLabel_ = text(animationButton,
        animations.empty() ? "애니메이션 없음" : "동작: " + animations[animation_] + "  >",
        15, Vec2(155, 22), Ink);
    fitLabel(animationLabel_, 290);
    auto skinButton = button(this, "", Rect(380, 191, 310, 45), rgba(29, 43, 58),
                             [this] { changeSkin(); });
    skinLabel_ = text(skinButton, skins.empty() ? "표정 없음" : "표정: " + skins[skin_] + "  >",
                      15, Vec2(155, 22), Ink);
    fitLabel(skinLabel_, 290);
    auto pauseButton = button(this, "", Rect(705, 191, 170, 45), rgba(37, 87, 84), [this] {
        if (!spine_) return;
        animationPaused_ = !animationPaused_;
        spine_->setAnimationPaused(animationPaused_);
        pauseLabel_->setString(animationPaused_ ? "재생" : "일시 정지");
    });
    pauseLabel_ = text(pauseButton, "일시 정지", 15, Vec2(85, 22), Ink);
    button(this, "축소 −", Rect(890, 191, 155, 45), rgba(29, 43, 58), [this] {
        zoom_ = std::max(.25f, zoom_ / 1.2f);
        fitSpine();
    });
    button(this, "확대 +", Rect(1060, 191, 165, 45), rgba(29, 43, 58), [this] {
        zoom_ = std::min(4.f, zoom_ * 1.2f);
        fitSpine();
    });
}

void ResourceGallery::fitSpine() {
    if (!spine_) return;
    const float scale = spineBaseScale_ * zoom_;
    spine_->setScale(scale);
    spine_->setPosition(Vec2(640, 419) - spineCenter_ * scale);
}

void ResourceGallery::changeAnimation() {
    if (!spine_ || spine_->animations().empty()) return;
    chooseAnimation((animation_ + 1) % spine_->animations().size());
}

void ResourceGallery::chooseAnimation(size_t index) {
    if (!spine_ || index >= spine_->animations().size()) return;
    animation_ = index;
    spine_->play(animation_);
    animationLabel_->setString("동작: " + spine_->animations()[animation_] + "  >");
    animationLabel_->setScale(1.f);
    fitLabel(animationLabel_, 290);
}

void ResourceGallery::openPicker(bool animations) {
    if (animations && !spine_) return;
    closePicker();
    pickingAnimations_ = animations;
    pickerPage_ = (animations ? animation_ : selection_) / 20;
    pickerQuery_.clear();
    picker_ = LayerColor::create(Color4B(4, 10, 18, 235), 1280, 720);
    addChild(picker_, 100);
    // Children handle their buttons first; the backdrop swallows all remaining touches.
    auto blocker = EventListenerTouchOneByOne::create();
    blocker->setSwallowTouches(true);
    blocker->onTouchBegan = [](Touch*, Event*) { return true; };
    _eventDispatcher->addEventListenerWithSceneGraphPriority(blocker, picker_);
    auto keyboard = EventListenerKeyboard::create();
    keyboard->onKeyReleased = [this](EventKeyboard::KeyCode key, Event*) {
        if (key == EventKeyboard::KeyCode::KEY_ESCAPE)
            defer([this] { closePicker(); });
    };
    _eventDispatcher->addEventListenerWithSceneGraphPriority(keyboard, picker_);
    panel(picker_, Rect(75, 65, 1130, 590), rgba(18, 32, 45));
    text(picker_, animations ? "스킬·동작 선택" : "리소스 선택", 25, Vec2(105, 620), Ink, true);
    button(picker_, "닫기  ×", Rect(1050, 602, 125, 38), rgba(29, 43, 58),
           [this] { defer([this] { closePicker(); }); }, 16);
    panel(picker_, Rect(105, 547, 895, 44), rgba(9, 20, 31), rgba(70, 95, 101));
    pickerSearch_ = ui::TextField::create(animations ? "동작 이름 검색 (예: skill)" : "이름 또는 ID 검색 (예: c1001)",
                                          "fonts/NotoSansKR.otf", 18);
    pickerSearch_->ignoreContentAdaptWithSize(false);
    pickerSearch_->setTextAreaSize(Size(865, 44));
    pickerSearch_->setTextHorizontalAlignment(TextHAlignment::LEFT);
    pickerSearch_->setTextVerticalAlignment(TextVAlignment::CENTER);
    pickerSearch_->setAnchorPoint(Vec2(0, .5f));
    pickerSearch_->setPosition(Vec2(120, 569));
    pickerSearch_->setTextColor(Color4B(Ink));
    pickerSearch_->setPlaceHolderColor(Muted);
    pickerSearch_->setMaxLengthEnabled(true);
    pickerSearch_->setMaxLength(80);
    pickerSearch_->setCursorEnabled(true);
    pickerSearch_->setTouchEnabled(true);
    picker_->addChild(pickerSearch_);
    pickerSearch_->addEventListener([this](Ref*, ui::TextField::EventType event) {
        if (!pickerSearch_ || (event != ui::TextField::EventType::INSERT_TEXT &&
                              event != ui::TextField::EventType::DELETE_BACKWARD)) return;
        pickerQuery_ = pickerSearch_->getString();
        pickerPage_ = 0;
        scheduleOnce([this](float) { refreshPicker(); }, 0, "gallery_picker_refresh");
    });
    button(picker_, "검색 지우기", Rect(1015, 547, 160, 44), rgba(29, 43, 58), [this] {
        if (!pickerSearch_) return;
        pickerSearch_->setString("");
        pickerQuery_.clear();
        pickerPage_ = 0;
        defer([this] { refreshPicker(); });
    }, 15);
    pickerList_ = Node::create();
    picker_->addChild(pickerList_);
    refreshPicker();
}

void ResourceGallery::refreshPicker() {
    if (!picker_ || !pickerList_) return;
    pickerList_->removeAllChildren();
    std::vector<size_t> matches;
    const auto query = searchable(pickerQuery_);
    if (pickingAnimations_) {
        if (!spine_) return;
        for (size_t i = 0; i < spine_->animations().size(); ++i)
            if (searchable(spine_->animations()[i]).find(query) != std::string::npos)
                matches.push_back(i);
    } else {
        for (size_t i = 0; i < filtered_.size(); ++i) {
            const auto& item = items_[filtered_[i]];
            if (searchable(item.displayName + " " + item.name + " " + item.heroId + " " +
                           item.category + " " + item.path).find(query) != std::string::npos)
                matches.push_back(i);
        }
    }
    constexpr size_t perPage = 20;
    const size_t pages = std::max<size_t>(1, (matches.size() + perPage - 1) / perPage);
    pickerPage_ = std::min(pickerPage_, pages - 1);
    for (size_t cell = 0; cell < perPage && pickerPage_ * perPage + cell < matches.size(); ++cell) {
        const size_t index = matches[pickerPage_ * perPage + cell];
        const float x = 105.f + static_cast<float>(cell % 4) * 272.f;
        const float y = 460.f - static_cast<float>(cell / 4) * 73.f;
        const bool selected = pickingAnimations_ ? index == animation_ : index == selection_;
        auto entry = button(pickerList_, "", Rect(x, y, 255, 61),
                            selected ? rgba(37, 87, 84) : rgba(27, 43, 57), [this, index] {
            defer([this, index] {
                const bool animation = pickingAnimations_;
                closePicker();
                if (animation) chooseAnimation(index);
                else { selection_ = index; showItem(); }
            });
        });
        std::string name, category;
        if (pickingAnimations_) {
            name = spine_->animations()[index];
            category = "애니메이션 " + std::to_string(index + 1);
        } else {
            const auto& item = items_[filtered_[index]];
            name = item.displayName.empty() ? item.name : item.displayName;
            category = item.displayName.empty() || item.displayName == item.name
                           ? item.category : item.name + " · " + item.category;
        }
        fitLabel(text(entry, name, 17, Vec2(12, 40), Ink, true), 231);
        fitLabel(text(entry, category, 12, Vec2(12, 17), selected ? Teal : Muted, true), 231);
    }
    if (matches.empty())
        text(pickerList_, "검색 결과가 없습니다.", 20, Vec2(640, 345), Muted);
    text(pickerList_, std::to_string(matches.size()) + "개  ·  " + std::to_string(pickerPage_ + 1) +
                         " / " + std::to_string(pages) + " 페이지", 15, Vec2(640, 107), Muted);
    button(pickerList_, "<  이전 페이지", Rect(105, 84, 230, 46), rgba(29, 43, 58), [this, pages] {
        defer([this, pages] { pickerPage_ = (pickerPage_ + pages - 1) % pages; refreshPicker(); });
    }, 16);
    button(pickerList_, "다음 페이지  >", Rect(945, 84, 230, 46), rgba(29, 43, 58), [this, pages] {
        defer([this, pages] { pickerPage_ = (pickerPage_ + 1) % pages; refreshPicker(); });
    }, 16);
}

void ResourceGallery::closePicker() {
    unschedule("gallery_picker_refresh");
    if (pickerSearch_)
        if (auto field = dynamic_cast<TextFieldTTF*>(pickerSearch_->getVirtualRenderer()))
            field->detachWithIME();
    pickerSearch_ = nullptr;
    pickerList_ = nullptr;
    if (picker_) picker_->removeFromParent();
    picker_ = nullptr;
}

void ResourceGallery::changeSkin() {
    if (!spine_ || spine_->skins().empty()) return;
    skin_ = (skin_ + 1) % spine_->skins().size();
    spine_->setSkin(skin_);
    skinLabel_->setString("표정: " + spine_->skins()[skin_] + "  >");
    skinLabel_->setScale(1.f);
    fitLabel(skinLabel_, 290);
}

void ResourceGallery::navigate(int offset) {
    if (filtered_.empty())
        return;
    selection_ = offset < 0 ? (selection_ == 0 ? filtered_.size() - 1 : selection_ - 1)
                            : (selection_ + 1) % filtered_.size();
    showItem();
}

void ResourceGallery::toggleAudio() {
    if (filtered_.empty() || !audioButtonLabel_ || !audioStatusLabel_)
        return;
    if (audioId_ != AudioEngine::INVALID_AUDIO_ID) {
        stopAudio();
        audioButtonLabel_->setString("재생");
        audioStatusLabel_->setString("재생을 멈췄습니다.");
        return;
    }
    audioId_ = AudioEngine::play2d(items_[filtered_[selection_]].path, false, .65f);
    if (audioId_ == AudioEngine::INVALID_AUDIO_ID) {
        audioStatusLabel_->setString("이 오디오 파일을 재생할 수 없습니다.");
        return;
    }
    audioButtonLabel_->setString("정지");
    audioStatusLabel_->setString("재생 중 · 다른 리소스로 이동하면 멈춥니다.");
}

void ResourceGallery::updateAudio(float) {
    if (audioId_ != AudioEngine::INVALID_AUDIO_ID &&
        AudioEngine::getState(audioId_) == AudioEngine::AudioState::ERROR) {
        audioId_ = AudioEngine::INVALID_AUDIO_ID;
        if (audioButtonLabel_)
            audioButtonLabel_->setString("다시 재생");
        if (audioStatusLabel_)
            audioStatusLabel_->setString("재생이 종료되었거나 지원되지 않는 파일입니다.");
    }
}

void ResourceGallery::stopAudio() {
    if (audioId_ != AudioEngine::INVALID_AUDIO_ID) {
        AudioEngine::stop(audioId_);
        audioId_ = AudioEngine::INVALID_AUDIO_ID;
    }
}

void ResourceGallery::onExit() {
    closePicker();
    stopAudio();
    Scene::onExit();
}
