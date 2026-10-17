#include "BattleHud.h"
#include "Presentation.h"
#include <algorithm>
#include <cmath>
#include <sstream>
USING_NS_CC;
using namespace presentation;
namespace {
const std::string Root = "reference/epicseven/battleui/";
const Color3B White(244, 239, 224), Gold(238, 210, 135), Muted(191, 197, 200);
Sprite* art(Node* parent, const std::string& path, Vec2 center, Size fit) {
    auto sprite = Sprite::create();
    parent->addChild(sprite);
    sprite->setPosition(center);
    if (FileUtils::getInstance()->isFileExist(path)) {
        sprite->setTexture(path);
        sprite->getTexture()->setAntiAliasTexParameters();
        auto size = sprite->getContentSize();
        sprite->setScale(std::min(fit.width / size.width, fit.height / size.height));
    }
    return sprite;
}
void texture(Sprite* sprite, const std::string& path, Size fit) {
    bool exists = !path.empty() && FileUtils::getInstance()->isFileExist(path);
    sprite->setVisible(exists);
    if (!exists)
        return;
    sprite->setTexture(path);
    sprite->getTexture()->setAntiAliasTexParameters();
    auto s = sprite->getContentSize();
    sprite->setScale(std::min(fit.width / s.width, fit.height / s.height));
}
Node* hit(Node* parent, Rect bounds, std::function<void()> callback) {
    auto node = Node::create();
    node->setPosition(bounds.origin);
    node->setContentSize(bounds.size);
    parent->addChild(node);
    auto listener = EventListenerTouchOneByOne::create();
    listener->setSwallowTouches(true);
    listener->onTouchBegan = [node](Touch* t, Event*) {
        return node->isVisible() && Rect(Vec2::ZERO, node->getContentSize())
                                        .containsPoint(node->convertToNodeSpace(t->getLocation()));
    };
    listener->onTouchEnded = [node, callback](Touch* t, Event*) {
        if (Rect(Vec2::ZERO, node->getContentSize())
                .containsPoint(node->convertToNodeSpace(t->getLocation())))
            callback();
    };
    Director::getInstance()->getEventDispatcher()->addEventListenerWithSceneGraphPriority(listener, node);
    return node;
}
void fitLabel(Label* label, const std::string& value, float width) {
    label->setString(value);
    label->setScale(1);
    if (label->getContentSize().width > width)
        label->setScale(width / label->getContentSize().width);
}
} // namespace
BattleHud* BattleHud::create(const Callbacks& callbacks) {
    auto node = new BattleHud;
    if (!node->init() || !node->build(callbacks)) {
        delete node;
        return nullptr;
    }
    node->autorelease();
    return node;
}
std::string BattleHud::facePath(const std::string& heroId) {
    return Root + "face/" + heroId + "_s.png";
}
std::string BattleHud::skillIconPath(const std::string& heroId, int skillIndex) {
    return Root + "skill/sk_" + heroId + "_" + std::to_string(std::clamp(skillIndex, 0, 2) + 1) + ".png";
}
bool BattleHud::build(const Callbacks& callbacks) {
    callbacks_ = callbacks;
    setContentSize(Size(1280, 720));
    auto bottom = LayerGradient::create(Color4B(5, 10, 14, 0), Color4B(5, 10, 14, 205));
    bottom->setContentSize(Size(1280, 145));
    addChild(bottom);
    auto top = LayerGradient::create(Color4B(5, 10, 14, 190), Color4B(5, 10, 14, 0));
    top->setContentSize(Size(1280, 95));
    top->setPositionY(625);
    addChild(top);
    stage_ = text(this, "", 20, Vec2(640, 684), White);
    stage_->enableShadow(Color4B::BLACK, Size(1, -1), 1);
    auto rail = art(this, Root + "img/_game_hud_timebg1.png", Vec2(31, 370), Size(15, 516));
    rail->setOpacity(175);
    text(this, "행동 순서", 12, Vec2(69, 641), White);
    for (int i = 0; i < 8; ++i) {
        auto row = Node::create();
        row->setPosition(69, 570 - i * 55);
        addChild(row);
        auto border = DrawNode::create();
        row->addChild(border);
        art(row, Root + "img/cm_hero_cirbg2.png", Vec2::ZERO, Size(48, 48));
        auto face = art(row, "", Vec2::ZERO, Size(44, 44));
        auto percent = text(row, "", 11, Vec2(42, -2), White);
        row->setVisible(false);
        auto fallback = text(row, "", 18, Vec2::ZERO, White);
        order_.push_back({row, face, border, percent, fallback, ""});
    }
    art(this, Root + "img/cm_hero_cirbg2.png", Vec2(91, 65), Size(102, 102));
    face_ = art(this, "", Vec2(91, 65), Size(98, 98));
    art(this, Root + "img/cm_hero_cirfrm2.png", Vec2(91, 65), Size(104, 104));
    name_ = text(this, "", 23, Vec2(159, 95), White, true);
    name_->enableShadow(Color4B::BLACK, Size(1, -1), 1);
    auto bar = Sprite::create(Root + "img/game_hud_bar_bg.png");
    if (bar) {
        bar->setPosition(159, 53);
        bar->setAnchorPoint(Vec2(0, .5f));
        bar->setScaleX(313 / bar->getContentSize().width);
        bar->setScaleY(16 / bar->getContentSize().height);
        addChild(bar);
    }
    hpFill_ = Sprite::create(Root + "img/game_hud_bar_hp.png");
    if (!hpFill_)
        hpFill_ = Sprite::create();
    hpFill_->setAnchorPoint(Vec2(0, .5f));
    hpFill_->setPosition(161, 53);
    addChild(hpFill_);
    hp_ = text(this, "", 14, Vec2(314, 28), White);
    status_ = text(this, "", 16, Vec2(711, 55), White);
    for (int i = 0; i < 3; ++i) {
        auto node = hit(this, Rect(980 + i * 99, 14, 86, 86), [this, i] {
            if (inputEnabled_ && enabled_[i] && callbacks_.onSkill)
                callbacks_.onSkill(i);
        });
        skillButtons_[i] = node;
        auto border = DrawNode::create();
        node->addChild(border);
        border->drawSolidCircle(Vec2(43, 43), 42, 0, 48, rgba(10, 17, 22, 200));
        border->drawCircle(Vec2(43, 43), 42, 0, 48, false, rgba(190, 179, 139));
        skillIcons_[i] = art(node, "", Vec2(43, 43), Size(81, 84));
        cooldowns_[i] = text(node, "", 31, Vec2(43, 43), White);
        cooldowns_[i]->enableOutline(Color4B(5, 7, 10, 255), 2);
        skillNames_[i] = text(this, "S" + std::to_string(i + 1), 12, Vec2(1023 + i * 99, 118), Gold);
    }
    const char* icons[] = {"cm_hud_top_btn_auto.png", "cm_hud_top_btn_speed.png", "cm_hud_top_btn_pause.png"};
    for (int i = 0; i < 3; ++i) {
        auto node = hit(this, Rect(1080 + i * 62, 635, 59, 66), [this, i] {
            if (!inputEnabled_)
                return;
            auto fn = i == 0 ? callbacks_.onAuto : i == 1 ? callbacks_.onSpeed : callbacks_.onMenu;
            if (fn)
                fn();
        });
        art(node, Root + "img/" + icons[i], Vec2(29.5f, 35), Size(59, 66));
        auto label = text(node, i == 0 ? "AUTO" : i == 1 ? "1×" : "메뉴", 11, Vec2(29.5f, 3), White);
        if (i == 0)
            auto_ = label;
        else if (i == 1)
            speed_ = label;
    }
    target_ = art(this, Root + "img/game_hud_target1.png", Vec2::ZERO, Size(40, 39));
    target_->setVisible(false);
    targetNumber_ = text(this, "", 18, Vec2::ZERO, White);
    targetNumber_->setVisible(false);
    refreshEnabled();
    return true;
}
void BattleHud::setStage(const std::string& stage) {
    stageName_ = stage;
    fitLabel(stage_, stage, 770);
}
void BattleHud::setActiveHero(const HeroView& hero) {
    fitLabel(name_, hero.name, 330);
    if (facePath_ != hero.facePath) {
        facePath_ = hero.facePath;
        texture(face_, facePath_, Size(98, 98));
    }
    int maxHp = std::max(1, hero.maxHp), hp = std::clamp(hero.hp, 0, maxHp);
    hp_->setString(std::to_string(hp) + " / " + std::to_string(maxHp));
    if (hpFill_->getTexture()) {
        hpFill_->setScaleX(309.f * hp / maxHp / hpFill_->getContentSize().width);
        hpFill_->setScaleY(9 / hpFill_->getContentSize().height);
    }
}
void BattleHud::setSkills(const std::array<SkillView, 3>& skills) {
    for (int i = 0; i < 3; ++i) {
        if (skillPaths_[i] != skills[i].iconPath) {
            skillPaths_[i] = skills[i].iconPath;
            texture(skillIcons_[i], skillPaths_[i], Size(81, 84));
        }
        enabled_[i] = skills[i].enabled && skills[i].cooldown <= 0;
        cooldowns_[i]->setString(skills[i].cooldown > 0 ? std::to_string(skills[i].cooldown) : "");
        fitLabel(skillNames_[i], skills[i].label.empty() ? "S" + std::to_string(i + 1) : skills[i].label, 95);
    }
    refreshEnabled();
}
void BattleHud::setTurnOrder(const std::vector<TurnView>& order) {
    activeUnit_ = -1;
    for (size_t i = 0; i < order_.size(); ++i) {
        auto& row = order_[i];
        row.root->setVisible(i < order.size());
        if (i >= order.size())
            continue;
        const auto& item = order[i];
        row.fallback->setString(item.facePath.empty() ? std::to_string(item.unitId - 3) : "");
        if (item.active)
            activeUnit_ = item.unitId;
        if (row.path != item.facePath) {
            row.path = item.facePath;
            texture(row.face, row.path, Size(44, 44));
        }
        row.border->clear();
        row.border->drawSolidCircle(Vec2::ZERO, item.active ? 28 : 25, 0, 40,
                                    item.active  ? rgba(245, 216, 122)
                                    : item.enemy ? rgba(190, 79, 68)
                                                 : rgba(91, 171, 202));
        row.percent->setString(std::to_string(static_cast<int>(std::clamp(item.readiness, 0.f, 100.f))) +
                               "%");
        row.percent->setTextColor(Color4B(item.active ? Gold : Muted));
    }
}
void BattleHud::setControls(bool autoOn, int speed) {
    auto_->setString(autoOn ? "AUTO ON" : "AUTO OFF");
    auto_->setTextColor(Color4B(autoOn ? Gold : White));
    speed_->setString(std::to_string(speed) + "×");
}
void BattleHud::setStatus(const std::string& status) {
    fitLabel(status_, status, 440);
}
void BattleHud::setInputEnabled(bool enabled) {
    inputEnabled_ = enabled;
    refreshEnabled();
}
void BattleHud::refreshEnabled() {
    for (int i = 0; i < 3; ++i) {
        bool enabled = inputEnabled_ && enabled_[i];
        if (skillIcons_[i])
            skillIcons_[i]->setColor(enabled ? Color3B::WHITE : Color3B(95, 101, 106));
        if (skillNames_[i])
            skillNames_[i]->setTextColor(Color4B(enabled ? Gold : Muted));
    }
}
void BattleHud::setTargetMarker(Vec2 position, bool visible, const std::string& value) {
    targetNumber_->setPosition(position);
    targetNumber_->setVisible(visible);
    targetNumber_->setString(value);
    target_->setPosition(position);
    target_->setVisible(visible);
}
std::string BattleHud::diagnostic() const {
    std::ostringstream out;
    out << "stage=" << stageName_ << " active=" << activeUnit_ << " input=" << inputEnabled_
        << " skills=" << enabled_[0] << enabled_[1] << enabled_[2];
    return out.str();
}
