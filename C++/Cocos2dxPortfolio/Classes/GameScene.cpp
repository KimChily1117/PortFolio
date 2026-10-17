#include "GameScene.h"
#include "FrontendScene.h"
#include "Presentation.h"
#include "SpinePreview.h"
#include "SkillPresentation.h"
#include "BattleHud.h"
#include "ReferenceAudio.h"
#include "BattleStageBackground.h"
#include <algorithm>
#include <ctime>
#include <cmath>
#include <sstream>
USING_NS_CC;
namespace {
const Color3B Ink(236, 235, 229), Muted(136, 151, 171), Teal(125, 215, 204), Gold(231, 196, 129),
    Pink(214, 158, 200);
Color4F color(int r, int g, int b, int a = 255) {
    return Color4F(r / 255.f, g / 255.f, b / 255.f, a / 255.f);
}
std::string number(std::int64_t value) {
    return StringUtils::format("%lld", static_cast<long long>(value));
}
void panel(Node* parent, Rect rect, Color4F fill, Color4F border = Color4F(0, 0, 0, 0)) {
    auto d = DrawNode::create();
    parent->addChild(d);
    d->drawSolidRect(rect.origin, rect.origin + Vec2(rect.size.width, rect.size.height), fill);
    if (border.a > 0)
        d->drawRect(rect.origin, rect.origin + Vec2(rect.size.width, rect.size.height), border);
}
} // namespace
Label* GameScene::label(Node* parent, const std::string& text, float size, float x, float y, Color3B color,
                        bool left) {
    auto l = Label::createWithTTF(text, "fonts/NotoSansKR.otf", size);
    if (!l)
        l = Label::createWithSystemFont(text, "Arial", size);
    l->setPosition(x, y);
    l->setTextColor(Color4B(color));
    if (left)
        l->setAnchorPoint(Vec2(0, .5f));
    parent->addChild(l);
    return l;
}
Node* GameScene::button(Node* parent, const std::string& title, const Rect& rect, Color4F fill,
                        std::function<void()> callback, Label** text) {
    auto n = Node::create();
    n->setPosition(rect.origin);
    n->setContentSize(rect.size);
    parent->addChild(n);
    panel(n, Rect(0, 0, rect.size.width, rect.size.height), fill, color(90, 113, 129, 90));
    auto l = label(n, title, 14, rect.size.width / 2, rect.size.height / 2, Ink);
    if (text)
        *text = l;
    auto listener = EventListenerTouchOneByOne::create();
    listener->setSwallowTouches(true);
    listener->onTouchBegan = [n](Touch* t, Event*) {
        if (!n->isVisible())
            return false;
        bool inside =
            Rect(Vec2::ZERO, n->getContentSize()).containsPoint(n->convertToNodeSpace(t->getLocation()));
        if (inside)
            n->setOpacity(190);
        return inside;
    };
    listener->onTouchEnded = [n, callback](Touch* t, Event*) {
        n->setOpacity(255);
        if (Rect(Vec2::ZERO, n->getContentSize()).containsPoint(n->convertToNodeSpace(t->getLocation()))) {
            ReferenceAudio::playUi();
            callback();
        }
    };
    listener->onTouchCancelled = [n](Touch*, Event*) { n->setOpacity(255); };
    n->setCascadeOpacityEnabled(true);
    _eventDispatcher->addEventListenerWithSceneGraphPriority(listener, n);
    return n;
}
void GameScene::loadReferenceParty() {
    definitions_ = aster::defaultHeroes();
    referenceSkills_ = SkillPresentation::initialSkillIds();
    const auto& catalog = SkillPresentation::catalog();
    for (int i = 0; i < aster::HeroCount; ++i) {
        const auto key = "reference-skill-slot-" + std::to_string(i);
        auto id = UserDefault::getInstance()->getStringForKey(key.c_str(), referenceSkills_[i]);
        auto item = std::find_if(catalog.begin(), catalog.end(),
                                 [&](const auto& c) { return c.id == id && c.playable; });
        if (item == catalog.end())
            item = std::find_if(catalog.begin(), catalog.end(),
                                [&](const auto& c) { return c.id == referenceSkills_[i] && c.playable; });
        if (item == catalog.end()) {
            referenceSkills_[i].clear();
            continue;
        }
        referenceSkills_[i] = item->id;
        heroIds_[i] = item->heroId;
        auto defaults = aster::defaultHeroes();
        const int source = item->heroId == "c1017"   ? 0
                           : item->heroId == "c1048" ? 1
                           : item->heroId == "c1028" ? 2
                                                     : 3;
        definitions_[i] = defaults[source];
        definitions_[i].name = item->name;
    }
}
bool GameScene::init() {
    if (!Scene::init())
        return false;
    loadReferenceParty();
    cutscenesEnabled_ = UserDefault::getInstance()->getBoolForKey("cutscenes", true);
    store_.reset(new aster::SaveStore(FileUtils::getInstance()->getWritablePath() + "aster-profile-v1.json"));
    std::string error;
    store_->load(profile_, error);
    saveBlocked_ = !error.empty();
    buildInterface();
    beginBattle();
    scheduleUpdate();
    ReferenceAudio::playMusic("battle");
    if (saveBlocked_)
        toast("저장 파일을 읽지 못했습니다. 기존 파일을 보존합니다.");
    auto keys = EventListenerKeyboard::create();
    keys->onKeyPressed = [this](EventKeyboard::KeyCode key, Event*) {
        if (key == EventKeyboard::KeyCode::KEY_ESCAPE)
            showMenu();
        else if (key == EventKeyboard::KeyCode::KEY_1)
            chooseSkill(0);
        else if (key == EventKeyboard::KeyCode::KEY_2)
            chooseSkill(1);
        else if (key == EventKeyboard::KeyCode::KEY_3)
            chooseSkill(2);
        else if (key == EventKeyboard::KeyCode::KEY_SPACE && skillPresentation_) {
            paused_ = !paused_;
            skillPresentation_->setPaused(paused_);
            ReferenceAudio::setPaused(paused_);
        }
    };
    _eventDispatcher->addEventListenerWithSceneGraphPriority(keys, this);
    return true;
}
void GameScene::buildLandscape() {
    battleStage_ = Node::create();
    addChild(battleStage_);
    battleBackground_ = BattleStageBackground::create(profile_.stage);
    if (!battleBackground_)
        battleBackground_ = LayerColor::create(Color4B(24, 33, 40, 255));
    battleStage_->addChild(battleBackground_, -2);
    opponents_ = Node::create();
    battleStage_->addChild(opponents_);
    homes_ = {Vec2(430, 280), Vec2(300, 345), Vec2(300, 210), Vec2(190, 280),
              Vec2(850, 280), Vec2(980, 345), Vec2(980, 210), Vec2(1090, 280)};
    const std::array<std::string, 4> enemies{
        {"ashmage", "chaos_guardian_m1", "ogre_king", "chaos_guardian_m2"}};
    const auto& catalog = SkillPresentation::catalog();
    for (int i = 0; i < aster::UnitCount; ++i) {
        std::string json, atlas;
        if (i < aster::HeroCount) {
            auto item = std::find_if(catalog.begin(), catalog.end(),
                                     [&](const auto& c) { return c.id == referenceSkills_[i]; });
            if (item != catalog.end()) {
                json = item->modelJson;
                atlas = item->modelAtlas;
            }
        } else {
            auto base = "reference/epicseven/spine/model/" + enemies[i - aster::HeroCount] + "/" +
                        enemies[i - aster::HeroCount];
            json = base + ".cocos.json";
            atlas = base + ".atlas";
        }
        std::string error;
        auto rig = SpinePreview::create(json, atlas, error);
        rigs_[i] = rig;
        auto parent = i < aster::HeroCount ? battleStage_ : opponents_;
        auto shadow = DrawNode::create();
        shadows_[i] = shadow;
        shadow->drawSolidCircle(Vec2::ZERO, 37, 0, 32, color(8, 18, 12, 95));
        shadow->setPosition(homes_[i]);
        shadow->setScaleY(.22f);
        parent->addChild(shadow, -1);
        if (rig) {
            if (!rig->playAnimation("b_idle", true) && !rig->playAnimation("idle", true))
                rig->play(0);
            const auto bounds = rig->localBounds();
            const float scale = 185.f / std::max(1.f, bounds.size.height);
            const float sx = i < aster::HeroCount ? scale : -scale;
            rig->setScaleX(sx);
            rig->setScaleY(scale);
            rigHomes_[i] = homes_[i] - Vec2(bounds.getMidX() * sx, bounds.getMinY() * scale);
            rig->setPosition(rigHomes_[i]);
            parent->addChild(rig, 600 - int(homes_[i].y));
        } else {
            CCLOG("Battle rig unavailable: %s", error.c_str());
            label(parent, unitName(i), 18, homes_[i].x, homes_[i].y + 60, Ink);
        }
        bars_[i] = DrawNode::create();
        parent->addChild(bars_[i], 500);
    }
    auto target = EventListenerTouchOneByOne::create();
    target->setSwallowTouches(false);
    target->onTouchBegan = [this](Touch* touch, Event*) {
        if (cutscene_ || action_ || modal_ || paused_ || turnDelay_ > 0 || !battle_.awaitingPlayer())
            return false;
        const auto point = touch->getLocation();
        int closest = -1;
        float distance = 10000;
        for (int i = 0; i < aster::UnitCount; ++i)
            if (battle_.validTarget(selectedSkill_, i)) {
                float d = point.distance(homes_[i] + Vec2(0, 80));
                if (d < 95 && d < distance) {
                    closest = i;
                    distance = d;
                }
            }
        if (closest < 0)
            return false;
        selectedTarget_ = closest;
        return true;
    };
    target->onTouchEnded = [this](Touch*, Event*) { chooseTarget(selectedTarget_); };
    _eventDispatcher->addEventListenerWithSceneGraphPriority(target, battleStage_);
}
void GameScene::buildInterface() {
    addChild(LayerColor::create(Color4B(20, 25, 30, 255)), -10);
    buildLandscape();
    BattleHud::Callbacks callbacks;
    callbacks.onSkill = [this](int skill) { chooseSkill(skill); };
    callbacks.onAuto = [this] {
        ReferenceAudio::playUi();
        setAutoBattle(!battle_.autoSkills());
    };
    callbacks.onSpeed = [this] {
        ReferenceAudio::playUi();
        speed_ = speed_ == 1 ? 2 : 1;
        refresh();
    };
    callbacks.onMenu = [this] {
        ReferenceAudio::playUi();
        showMenu();
    };
    hud_ = BattleHud::create(callbacks);
    addChild(hud_, 10);
    actionEffects_ = DrawNode::create();
    battleStage_->addChild(actionEffects_, 1500);
    actionTitle_ = label(this, "", 22, 640, 620, Gold);
    actionTitle_->setLocalZOrder(20);
    toast_ = label(this, "", 17, 640, 148, Gold);
    toast_->setLocalZOrder(25);
    for (auto& l : floatPool_) {
        l = label(this, "", 26, 0, 0, Gold);
        l->setVisible(false);
        l->setLocalZOrder(30);
    }
}
void GameScene::beginBattle() {
    battle_.begin(definitions_, profile_);
    settled_ = false;
    turnDelay_ = .35f;
    pendingEvents_.clear();
    skillEvents_.clear();
    action_.reset();
    reactionTime_.fill(-1.f);
    selectedSkill_ = 0;
    selectedTarget_ = battle_.defaultTarget(0);
    for (int i = 0; i < aster::UnitCount; ++i) {
        displayedHp_[i] = battle_.units()[i].hp;
        if (auto rig = rigs_[i]) {
            rig->stopAllActions();
            rig->setPosition(rigHomes_[i]);
            rig->setOpacity(255);
            rig->setVisible(true);
            rig->setAnimationPaused(false);
        }
    }
    refresh();
}
std::string GameScene::unitName(int unit) const {
    const std::array<std::string, 4> enemies{{"재의 마법사", "혼돈의 수호병", "오우거", "혼돈의 돌격병"}};
    return unit >= 0 && unit < aster::HeroCount                  ? definitions_[unit].name
           : unit >= aster::HeroCount && unit < aster::UnitCount ? enemies[unit - aster::HeroCount]
                                                                 : "";
}
void GameScene::refresh() {
    const int active = action_ ? action_->actor : battle_.activeActor();
    hud_->setStage(std::string(presentation::worldName((battle_.stage() - 1) / 5)) + "  " +
                   presentation::stageName(battle_.stage()));
    hud_->setControls(battle_.autoSkills(), speed_);
    hud_->setInputEnabled(!cutscene_ && !modal_ && !paused_);
    hud_->setVisible(!cutscene_);
    if (active >= 0) {
        const auto& u = battle_.units()[active];
        hud_->setActiveHero({unitName(active),
                             active < aster::HeroCount ? BattleHud::facePath(heroIds_[active]) : "",
                             displayedHp_[active], u.maxHp});
        std::array<BattleHud::SkillView, 3> skills{};
        for (int s = 0; s < 3; ++s)
            skills[s] = {"S" + std::to_string(s + 1),
                         active < aster::HeroCount ? BattleHud::skillIconPath(heroIds_[active], s) : "",
                         u.cooldowns[s],
                         !action_ && battle_.awaitingPlayer() && turnDelay_ <= 0 && battle_.canUseSkill(s)};
        hud_->setSkills(skills);
    }
    std::vector<BattleHud::TurnView> order;
    auto turnOrder = battle_.turnOrder();
    if (action_) {
        turnOrder.erase(std::remove(turnOrder.begin(), turnOrder.end(), active), turnOrder.end());
        turnOrder.insert(turnOrder.begin(), active);
    }
    for (int id : turnOrder) {
        const auto& u = battle_.units()[id];
        order.push_back({id, unitName(id), id < aster::HeroCount ? BattleHud::facePath(heroIds_[id]) : "",
                         action_ && id == active ? 100.f : float(u.gauge / 10), u.enemy, id == active});
    }
    hud_->setTurnOrder(order);
    hud_->setStatus(action_                                     ? unitName(active) + " · " + action_->phase
                    : battle_.phase() != aster::Phase::Fighting ? "전투 종료"
                    : cutscene_                                 ? "스킬 연출"
                    : battle_.awaitingPlayer()
                        ? unitName(active) + "  ·  S" + std::to_string(selectedSkill_ + 1) +
                              "  ·  대상을 선택하세요"
                        : "행동 진행 중");
    bool marker = selectedTarget_ >= 0 && battle_.awaitingPlayer() && !cutscene_ && !action_ &&
                  battle_.validTarget(selectedSkill_, selectedTarget_);
    hud_->setTargetMarker(marker ? homes_[selectedTarget_] + Vec2(0, 215) : Vec2::ZERO, marker,
                          marker ? (selectedTarget_ < aster::HeroCount
                                        ? "+"
                                        : std::to_string(selectedTarget_ - aster::HeroCount + 1))
                                 : "");
    for (int i = 0; i < aster::UnitCount; ++i) {
        const auto& u = battle_.units()[i];
        auto bar = bars_[i];
        bar->clear();
        const Vec2 offset = rigs_[i] ? rigs_[i]->getPosition() - rigHomes_[i] : Vec2::ZERO;
        const Vec2 at = homes_[i] + offset + Vec2(-45, 193);
        shadows_[i]->setPosition(homes_[i] + offset);
        shadows_[i]->setVisible(displayedHp_[i] > 0 || reactionTime_[i] >= 0);
        if (displayedHp_[i] > 0) {
            bar->drawSolidRect(at, at + Vec2(90, 7), color(15, 16, 18, 220));
            bar->drawSolidRect(at + Vec2(1, 1), at + Vec2(88.f * displayedHp_[i] / u.maxHp + 1, 6),
                               i < aster::HeroCount ? color(116, 201, 87) : color(203, 68, 65));
            if (u.shield > 0)
                bar->drawSolidRect(at + Vec2(0, 9),
                                   at + Vec2(90.f * std::min(u.maxHp, u.shield) / u.maxHp, 12),
                                   color(106, 180, 240));
        }
        if (rigs_[i] && !cutscene_)
            rigs_[i]->setVisible(displayedHp_[i] > 0 || reactionTime_[i] >= 0);
    }
}
void GameScene::setAutoBattle(bool enabled) {
    battle_.setAutoSkills(enabled);
    refresh();
}
void GameScene::chooseSkill(int skill) {
    if (cutscene_ || action_ || modal_ || paused_ || turnDelay_ > 0 || !battle_.awaitingPlayer() ||
        !battle_.canUseSkill(skill))
        return;
    ReferenceAudio::playUi("skill");
    selectedSkill_ = skill;
    selectedTarget_ = battle_.defaultTarget(skill);
    const auto mode = battle_.targetMode(skill);
    if (mode == aster::TargetMode::AllAllies || mode == aster::TargetMode::AllEnemies)
        chooseTarget(selectedTarget_);
    else {
        toast(mode == aster::TargetMode::Ally ? "회복할 아군을 선택하세요" : "공격할 적을 선택하세요");
        refresh();
    }
}
void GameScene::chooseTarget(int target) {
    if (cutscene_ || action_ || modal_ || paused_ || turnDelay_ > 0 || !battle_.awaitingPlayer())
        return;
    auto events = battle_.useSkill(selectedSkill_, target);
    if (events.empty())
        return;
    queueEvents(events);
}
void GameScene::queueEvents(const std::vector<aster::BattleEvent>& events) {
    pendingEvents_.insert(pendingEvents_.end(), events.begin(), events.end());
    turnDelay_ = 1.15f;
    selectedSkill_ = 0;
    selectedTarget_ = battle_.defaultTarget(0);
    drainPresentationEvents();
    refresh();
}
void GameScene::update(float dt) {
    const float delta = std::min(dt, .25f);
    if ((toastTimer_ -= delta) <= 0)
        toast_->setString("");
    if (paused_ || modal_ || cutscene_)
        return;
    if (action_) {
        updateAction(delta * speed_);
        refresh();
        return;
    }
    turnDelay_ = std::max(0.f, turnDelay_ - delta * speed_);
    if (battle_.phase() != aster::Phase::Fighting) {
        if (!modal_)
            showResult();
        return;
    }
    if (turnDelay_ <= 0 && !battle_.awaitingPlayer())
        queueEvents(battle_.tick());
    refresh();
}
void GameScene::floatText(const std::string& text, Vec2 position, Color3B c) {
    auto l = floatPool_[floatIndex_++ % floatPool_.size()];
    l->stopAllActions();
    l->setString(text);
    l->setPosition(position);
    l->setTextColor(Color4B(c));
    l->setVisible(true);
    l->setOpacity(255);
    l->runAction(
        Sequence::create(Spawn::create(MoveBy::create(.8f, Vec2(0, 45)), FadeOut::create(.8f), nullptr),
                         Hide::create(), nullptr));
}
void GameScene::restoreIdle(int unit) {
    if (auto rig = rigs_[unit]) {
        rig->setPosition(rigHomes_[unit]);
        rig->setColor(Color3B::WHITE);
        rig->setOpacity(255);
        rig->setLocalZOrder(600 - int(homes_[unit].y));
        rig->setAnimationPaused(false);
        if (!rig->playAnimation("b_idle", true) && !rig->playAnimation("idle", true))
            rig->play(0);
    }
}
void GameScene::startAction(std::vector<aster::BattleEvent> events) {
    action_.reset(new ActionMotion);
    auto& a = *action_;
    a.events = std::move(events);
    const auto& event = a.events.front();
    a.actor = event.actor;
    a.target = event.target;
    a.enemy = a.actor >= aster::HeroCount;
    a.supportive = event.type == aster::EventType::Heal || event.type == aster::EventType::Shield;
    const bool clarissa = !a.enemy && heroIds_[a.actor] == "c1028";
    a.melee = !a.supportive && (clarissa || a.actor > aster::HeroCount);
    a.windup = a.enemy ? .3f : .16f;
    a.approach = a.melee ? (clarissa ? .7f : .34f) : .12f;
    a.returnDuration = a.melee ? (clarissa ? .7333f : .34f) : .18f;
    a.animation = "skill" + std::to_string(event.skillIndex + 1);
    auto actor = rigs_[a.actor];
    if (actor) {
        if (actor->animationDuration(a.animation) <= 0) {
            // Some shipped monsters have only skill1, even for a stronger action.
            a.animation = actor->animationDuration("skill1") > 0 ? "skill1" : "b_idle";
        }
        a.attackDuration = std::max(.5f, actor->animationDuration(a.animation));
        actor->setAnimationPaused(true);
        if (!actor->playAnimation("b_idle_ready", false))
            actor->playAnimation("idle", true);
        actor->setLocalZOrder(1000);
    } else
        a.attackDuration = .8f;
    // The hit/fire anchors below exist in the shipped animation events.
    // Other models have no damage event: the 55% anchor is a local timing choice.
    float hit = a.attackDuration * .55f;
    if (!a.enemy && heroIds_[a.actor] == "c1017" && event.skillIndex < 2)
        hit = event.skillIndex == 0 ? .2666f : 1.1f;
    if (clarissa && event.skillIndex < 2)
        hit = event.skillIndex == 0 ? .5f : .7666f;
    a.hit = a.windup + a.approach + std::clamp(hit, .05f, a.attackDuration);
    const float direction = a.enemy ? -1.f : 1.f;
    a.offset =
        a.melee ? homes_[a.target] - homes_[a.actor] - Vec2(direction * 125, 0) : Vec2(direction * 24, 0);
    a.phase = "공격 준비";
    actionTitle_->setString(unitName(a.actor) +
                            (a.supportive            ? " · 회복"
                             : a.enemy               ? " · 공격"
                             : event.skillIndex == 0 ? " · 기본 공격"
                                                     : " · 스킬") +
                            "  →  " + unitName(a.target));
    actionTitle_->setTextColor(Color4B(a.enemy ? Color3B(255, 125, 105) : Gold));
    ++actionCount_;
    if (a.enemy)
        ++enemyActionCount_;
    CCLOG("ACTION start actor=%d target=%d animation=%s duration=%.3f hit=%.3f enemy=%d melee=%d", a.actor,
          a.target, a.animation.c_str(), a.attackDuration, a.hit, a.enemy, a.melee);
    updateAction(0);
}
void GameScene::updateAction(float dt) {
    if (!action_)
        return;
    auto& a = *action_;
    a.elapsed += dt;
    const float attackStart = a.windup + a.approach;
    const float returnStart = attackStart + a.attackDuration;
    const float end = returnStart + a.returnDuration;
    const float direction = a.enemy ? -1.f : 1.f;
    auto actor = rigs_[a.actor];
    auto smooth = [](float p) {
        p = std::clamp(p, 0.f, 1.f);
        return p * p * (3 - 2 * p);
    };
    Vec2 offset;
    if (a.elapsed < a.windup) {
        a.phase = "공격 준비";
        offset = Vec2(-direction * 12 * smooth(a.elapsed / a.windup), 0);
        if (actor)
            actor->seekAnimation(a.elapsed);
    } else if (a.elapsed < attackStart) {
        a.phase = a.melee ? "접근" : "시전 준비";
        const float p = (a.elapsed - a.windup) / a.approach;
        offset = Vec2(-direction * 12, 0).lerp(a.offset, smooth(p));
        if (a.melee)
            offset.y += std::sin(p * 3.14159265f) * (a.enemy ? 12.f : 32.f);
        if (actor) {
            const std::string move = !a.enemy && heroIds_[a.actor] == "c1028" ? "jump_forward" : "run";
            if (actor->animationDuration(move) > 0) {
                actor->playAnimation(move, false);
                actor->seekAnimation(p * actor->animationDuration(move));
            }
        }
    } else if (a.elapsed < returnStart) {
        a.phase = a.events.front().skillIndex == 0 ? "기본 공격" : "스킬 시전";
        offset = a.offset;
        if (!a.attackStarted) {
            a.attackStarted = true;
            if (actor)
                actor->playAnimation(a.animation, false);
            if (!a.enemy)
                ReferenceAudio::playSkill(heroIds_[a.actor], a.events.front().skillIndex + 1);
        }
        if (actor)
            actor->seekAnimation(a.elapsed - attackStart);
    } else {
        a.phase = "복귀";
        const float p = (a.elapsed - returnStart) / a.returnDuration;
        offset = a.offset * (1 - smooth(p));
        if (a.melee)
            offset.y += std::sin(std::min(1.f, p) * 3.14159265f) * 16.f;
        if (actor) {
            if (!a.returning) {
                if (!actor->playAnimation("jump_back", false) && !actor->playAnimation("b_idle", true))
                    actor->playAnimation("idle", true);
            }
            actor->seekAnimation(a.elapsed - returnStart);
        }
        a.returning = true;
    }
    if (actor)
        actor->setPosition(rigHomes_[a.actor] + offset);
    const bool impactNow = !a.impactApplied && a.elapsed >= a.hit;
    if (impactNow) {
        a.impactApplied = true;
        ++actionImpactCount_;
        if (!a.supportive)
            ReferenceAudio::playAttack();
        for (const auto& event : a.events)
            present(event);
        CCLOG("ACTION impact actor=%d target=%d elapsed=%.3f", a.actor, a.target, a.elapsed);
    }
    for (int i = 0; i < aster::UnitCount; ++i) {
        if (reactionTime_[i] < 0 || !rigs_[i])
            continue;
        float& time = reactionTime_[i];
        // A new reaction starts on this frame, not at the beginning of its dt.
        // This also preserves a visible hit pose on a delayed 2x-speed frame.
        if (!impactNow)
            time += dt;
        const float duration = std::max(.4f, rigs_[i]->animationDuration("attacked"));
        rigs_[i]->seekAnimation(std::min(time, duration));
        const float push = std::sin(std::min(1.f, time / .35f) * 3.14159265f) * (a.enemy ? 32.f : 20.f);
        rigs_[i]->setPosition(rigHomes_[i] + Vec2(direction * push, 0));
        const float flash = std::max(0.f, 1 - time / .2f);
        rigs_[i]->setColor(
            Color3B(255, static_cast<GLubyte>(255 - 150 * flash), static_cast<GLubyte>(255 - 150 * flash)));
        if (displayedHp_[i] <= 0)
            rigs_[i]->setOpacity(static_cast<GLubyte>(255 * std::max(0.f, 1 - time / duration)));
        if (time >= duration) {
            time = -1;
            restoreIdle(i);
        }
    }
    const float impactAge = a.elapsed - a.hit;
    if (a.impactApplied && !a.supportive && impactAge < .22f) {
        const float strength = (a.enemy ? 7.f : 4.f) * (1 - impactAge / .22f);
        battleStage_->setPosition(std::sin(impactAge * 85) * strength,
                                  std::cos(impactAge * 65) * strength * .4f);
    } else
        battleStage_->setPosition(Vec2::ZERO);
    drawActionEffects();
    if (a.elapsed >= end)
        finishAction();
}
void GameScene::drawActionEffects() {
    actionEffects_->clear();
    if (!action_)
        return;
    const auto& a = *action_;
    const Color4F tint = a.supportive                   ? color(122, 255, 190)
                         : a.enemy                      ? color(255, 95, 70)
                         : heroIds_[a.actor] == "c1048" ? color(255, 165, 65)
                                                        : color(125, 220, 255);
    if (!a.impactApplied) {
        const float pulse = .5f + .5f * std::sin(a.elapsed * 10);
        actionEffects_->drawCircle(homes_[a.target], 48 + pulse * 5, 0, 40, false, 1, .32f, tint);
        if (a.enemy) {
            const Vec2 at = homes_[a.target] + Vec2(0, 225);
            actionEffects_->drawTriangle(at + Vec2(-8, 13), at + Vec2(8, 13), at, tint);
        }
        const float flightStart = std::max(a.windup + a.approach, a.hit - .3f);
        if (!a.melee && !a.supportive && a.elapsed >= flightStart) {
            const float p =
                std::clamp((a.elapsed - flightStart) / std::max(.01f, a.hit - flightStart), 0.f, 1.f);
            const Vec2 from = homes_[a.actor] + a.offset + Vec2(0, 110);
            const Vec2 to = homes_[a.target] + Vec2(0, 105);
            const Vec2 at = from.lerp(to, p);
            const Vec2 tail = from.lerp(to, std::max(0.f, p - .16f));
            actionEffects_->drawSegment(tail, at, 6, tint);
            actionEffects_->drawSolidCircle(at, 11, 0, 20, Color4F(1, 1, 1, 1));
        }
    } else {
        const float age = a.elapsed - a.hit;
        if (age < .36f) {
            Color4F glow = tint;
            glow.a = 1 - age / .36f;
            for (const auto& event : a.events) {
                const Vec2 at = homes_[event.target] + Vec2(0, 105);
                const float radius = 18 + age * 175;
                actionEffects_->drawCircle(at, radius, 0, 32, false, glow);
                if (a.supportive) {
                    actionEffects_->drawSegment(at + Vec2(-15, 0), at + Vec2(15, 0), 4, glow);
                    actionEffects_->drawSegment(at + Vec2(0, -15), at + Vec2(0, 15), 4, glow);
                } else {
                    for (int j = 0; j < 6; ++j) {
                        const float angle = j * 1.04719755f + .4f;
                        const Vec2 ray(std::cos(angle), std::sin(angle));
                        actionEffects_->drawSegment(at + ray * radius * .45f, at + ray * radius, 3, glow);
                    }
                    actionEffects_->drawSegment(at + Vec2(-35, -48), at + Vec2(35, 48), 4 * glow.a, glow);
                }
            }
        }
    }
}
void GameScene::finishAction() {
    if (!action_)
        return;
    const int actor = action_->actor;
    if (action_->events.front().skillIndex == 2)
        ReferenceAudio::stopEffects(); // No S3 cue may outlive the fallback/disabled presentation.
    for (int i = 0; i < aster::UnitCount; ++i)
        if (i == actor || reactionTime_[i] >= 0)
            restoreIdle(i);
    reactionTime_.fill(-1.f);
    action_.reset();
    actionEffects_->clear();
    actionTitle_->setString("");
    battleStage_->setPosition(Vec2::ZERO);
    turnDelay_ = .2f;
    CCLOG("ACTION complete actor=%d presentations=%d impacts=%d", actor, actionCount_, actionImpactCount_);
    drainPresentationEvents();
    refresh();
}
void GameScene::setBattlePaused(bool paused) {
    for (int i = 0; i < aster::UnitCount; ++i)
        if (rigs_[i])
            rigs_[i]->setAnimationPaused(paused || cutscene_ || (action_ && action_->actor == i) ||
                                         reactionTime_[i] >= 0);
    for (auto text : floatPool_) {
        if (paused)
            getActionManager()->pauseTarget(text);
        else
            getActionManager()->resumeTarget(text);
    }
    ReferenceAudio::setPaused(paused);
}
void GameScene::present(const aster::BattleEvent& e) {
    if (e.type == aster::EventType::Victory || e.type == aster::EventType::Defeat) {
        if (aster::settleBattle(profile_, battle_, settled_))
            persist();
        showResult();
        return;
    }
    if (e.actor < 0 || e.target < 0)
        return;
    const bool heal = e.type == aster::EventType::Heal, shield = e.type == aster::EventType::Shield;
    if (!shield)
        displayedHp_[e.target] = std::clamp(displayedHp_[e.target] + (heal ? e.amount : -e.amount), 0,
                                            battle_.units()[e.target].maxHp);
    floatText((shield       ? "BARRIER "
               : heal       ? "+"
               : e.critical ? "CRIT "
                            : "") +
                  number(e.amount),
              homes_[e.target] + Vec2(0, 115),
              shield       ? Color3B(117, 190, 255)
              : heal       ? Teal
              : e.critical ? Gold
                           : Ink);
    if (!heal && !shield && !cutscene_ && rigs_[e.target]) {
        reactionTime_[e.target] = 0;
        rigs_[e.target]->playAnimation("attacked", false);
        rigs_[e.target]->setAnimationPaused(true);
    }
}
void GameScene::drainPresentationEvents() {
    while (!pendingEvents_.empty() && !cutscene_ && !action_) {
        const auto event = pendingEvents_.front();
        if (event.skillIndex == 2 && event.actor >= 0 && event.actor < aster::HeroCount &&
            cutscenesEnabled_ && !referenceSkills_[event.actor].empty()) {
            skillEvents_.clear();
            do {
                skillEvents_.push_back(pendingEvents_.front());
                pendingEvents_.pop_front();
            } while (!pendingEvents_.empty() && pendingEvents_.front().actor == event.actor &&
                     pendingEvents_.front().skillIndex == 2);
            skillImpactApplied_ = false;
            showCutscene(event.actor);
            if (cutscene_)
                return;
            startAction(std::move(skillEvents_));
            skillEvents_.clear();
        } else {
            if (event.actor >= 0 && event.target >= 0) {
                std::vector<aster::BattleEvent> group;
                do {
                    group.push_back(pendingEvents_.front());
                    pendingEvents_.pop_front();
                } while (!pendingEvents_.empty() && pendingEvents_.front().actor == event.actor &&
                         pendingEvents_.front().skillIndex == event.skillIndex);
                startAction(std::move(group));
            } else {
                pendingEvents_.pop_front();
                present(event);
            }
        }
    }
}
void GameScene::applySkillImpact() {
    if (skillImpactApplied_)
        return;
    skillImpactApplied_ = true;
    ++impactCount_;
    for (const auto& event : skillEvents_)
        present(event);
    refresh();
}
void GameScene::showCutscene(int hero) {
    std::string error;
    SkillPresentation::Callbacks callbacks;
    callbacks.onHit = [this] { applySkillImpact(); };
    callbacks.onComplete = [this](bool) { finishCutscene(); };
    callbacks.onDiagnostic = [](const std::string& d) { CCLOG("S3: %s", d.c_str()); };
    auto player = SkillPresentation::create(referenceSkills_[hero], callbacks, error);
    if (!player) {
        toast("S3 불러오기 실패: " + error);
        return;
    }
    const int target = skillEvents_.empty() ? aster::HeroCount : skillEvents_.front().target;
    player->bindBattleStage(battleStage_, battleBackground_, opponents_,
                            rigs_[hero] ? rigs_[hero]->getPosition() : homes_[hero],
                            homes_[std::clamp(target, 0, aster::UnitCount - 1)]);
    skillPresentation_ = player;
    cutscene_ = player;
    ++presentationCount_;
    ReferenceAudio::playSkill(heroIds_[hero], 3);
    for (auto rig : rigs_)
        if (rig)
            rig->setAnimationPaused(true);
    if (rigs_[hero])
        rigs_[hero]->setVisible(false);
    addChild(player, 200);
}
void GameScene::skipSkillPresentation() {
    if (skillPresentation_) {
        ReferenceAudio::stopEffects();
        ReferenceAudio::setPaused(false);
        paused_ = false;
        skillPresentation_->skip();
    }
}
void GameScene::finishCutscene() {
    // Completion can also come directly from the player's Skip/Escape while paused.
    paused_ = false;
    ReferenceAudio::stopEffects();
    ReferenceAudio::setPaused(false);
    applySkillImpact();
    scheduleOnce(
        [this](float) {
            auto previous = cutscene_;
            cutscene_ = nullptr;
            skillPresentation_ = nullptr;
            if (previous && previous->getParent())
                previous->removeFromParent();
            skillEvents_.clear();
            turnDelay_ = .3f;
            ReferenceAudio::stopEffects();
            for (auto rig : rigs_)
                if (rig) {
                    rig->setAnimationPaused(paused_);
                    if (!rig->playAnimation("b_idle", true) && !rig->playAnimation("idle", true))
                        rig->play(0);
                }
            drainPresentationEvents();
            refresh();
        },
        0, "finish-reference-skill");
}
void GameScene::toast(const std::string& text) {
    toast_->setString(text);
    toastTimer_ = 3;
}
void GameScene::showMenu() {
    if (cutscene_)
        return;
    if (modal_)
        return;
    setBattlePaused(true);
    modal_ = Node::create();
    addChild(modal_, 150);
    auto shade = LayerColor::create(Color4B(3, 9, 17, 205));
    modal_->addChild(shade);
    auto blocker = EventListenerTouchOneByOne::create();
    blocker->setSwallowTouches(true);
    blocker->onTouchBegan = [](Touch*, Event*) { return true; };
    _eventDispatcher->addEventListenerWithSceneGraphPriority(blocker, shade);
    panel(modal_, Rect(400, 130, 480, 410), color(25, 27, 31), color(140, 116, 73));
    label(modal_, "전투 메뉴", 28, 640, 490, Gold);
    button(modal_, "계속하기", Rect(440, 390, 400, 50), color(40, 62, 52), [this] {
        modal_->removeFromParent();
        modal_ = nullptr;
        setBattlePaused(false);
        refresh();
    });
    button(modal_, cutscenesEnabled_ ? "스킬 연출 ON" : "스킬 연출 OFF", Rect(440, 320, 400, 50),
           color(38, 43, 49), [this] {
               cutscenesEnabled_ = !cutscenesEnabled_;
               UserDefault::getInstance()->setBoolForKey("cutscenes", cutscenesEnabled_);
               UserDefault::getInstance()->flush();
               modal_->removeFromParent();
               modal_ = nullptr;
               showMenu();
           });
    button(modal_, "로비로 돌아가기", Rect(440, 240, 400, 50), color(62, 48, 40), [this] {
        persist();
        scheduleOnce(
            [](float) {
                Director::getInstance()->replaceScene(TransitionFade::create(.3f, FrontendScene::create()));
            },
            0, "return-lobby");
    });
    button(modal_, ReferenceAudio::isEnabled() ? "사운드 ON" : "사운드 OFF", Rect(440, 155, 400, 50),
           color(38, 43, 49), [this] {
               ReferenceAudio::setEnabled(!ReferenceAudio::isEnabled());
               if (ReferenceAudio::isEnabled())
                   ReferenceAudio::playMusic("battle");
               modal_->removeFromParent();
               modal_ = nullptr;
               showMenu();
           });
    refresh();
}
void GameScene::showResult() {
    if (modal_ || cutscene_ || action_)
        return;
    modal_ = Node::create();
    addChild(modal_, 150);
    auto shade = LayerColor::create(Color4B(3, 9, 17, 210));
    modal_->addChild(shade);
    auto blocker = EventListenerTouchOneByOne::create();
    blocker->setSwallowTouches(true);
    blocker->onTouchBegan = [](Touch*, Event*) { return true; };
    _eventDispatcher->addEventListenerWithSceneGraphPriority(blocker, shade);
    bool won = battle_.phase() == aster::Phase::Victory;
    panel(modal_, Rect(340, 180, 600, 360), color(25, 27, 31), color(140, 116, 73));
    label(modal_, won ? "VICTORY" : "DEFEAT", 42, 640, 475, Gold);
    label(modal_,
          won ? "스테이지 클리어  ·  +" + number(aster::stageReward(battle_.stage())) + " G"
              : "다른 스킬과 대상을 선택해 다시 도전하세요",
          20, 640, 398, Ink);
    button(modal_, won ? "다음 스테이지" : "다시 도전", Rect(380, 275, 520, 52), color(43, 70, 55), [this] {
        persist();
        scheduleOnce(
            [](float) {
                Director::getInstance()->replaceScene(TransitionFade::create(.25f, GameScene::create()));
            },
            0, "next-battle");
    });
    button(modal_, "로비로", Rect(380, 208, 520, 48), color(51, 46, 42), [this] {
        persist();
        scheduleOnce(
            [](float) {
                Director::getInstance()->replaceScene(TransitionFade::create(.3f, FrontendScene::create()));
            },
            0, "result-lobby");
    });
    refresh();
}
std::string GameScene::presentationDiagnostic() const {
    std::ostringstream out;
    out << "presentations=" << presentationCount_ << " impacts=" << impactCount_
        << " queued=" << pendingEvents_.size() << " turns=" << battle_.ticks()
        << " active=" << battle_.activeActor() << " manual=" << battle_.awaitingPlayer()
        << " phase=" << int(battle_.phase()) << " result=" << (modal_ != nullptr) << " paused=" << paused_
        << "\n";
    if (battleStage_)
        out << "stage=" << battleStage_->getPositionX() << "," << battleStage_->getPositionY() << ","
            << battleStage_->getScaleX() << "," << battleStage_->getScaleY()
            << " background=" << battleBackground_->isVisible() << " opponents=" << opponents_->getPositionX()
            << "," << opponents_->getPositionY() << "\n";
    if (hud_)
        out << hud_->diagnostic() << "\n";
    out << "actions=" << actionCount_ << " actionImpacts=" << actionImpactCount_
        << " enemyActions=" << enemyActionCount_ << " presenting=" << (action_ != nullptr) << "\n";
    if (action_)
        out << "action actor=" << action_->actor << " target=" << action_->target
            << " animation=" << action_->animation << " time=" << action_->elapsed << " hit=" << action_->hit
            << " applied=" << action_->impactApplied << " phase=" << action_->phase << "\n";
    out << ReferenceAudio::diagnostic() << "\n";
    for (int i = 0; i < aster::UnitCount; ++i)
        out << "unit" << i << " hp=" << displayedHp_[i] << " modelHp=" << battle_.units()[i].hp
            << " rig=" << (rigs_[i] != nullptr)
            << " offset=" << (rigs_[i] ? rigs_[i]->getPositionX() - rigHomes_[i].x : 0) << ","
            << (rigs_[i] ? rigs_[i]->getPositionY() - rigHomes_[i].y : 0) << "\n";
    if (skillPresentation_)
        out << skillPresentation_->diagnostic();
    return out.str();
}
void GameScene::persist() {
    if (saveBlocked_)
        return;
    std::string error;
    if (!store_->save(profile_, error))
        toast("저장 실패: " + error);
}
void GameScene::suspend() {
    persist();
}
void GameScene::resumeGame() {
    setBattlePaused(paused_ || modal_);
    refresh();
}
void GameScene::onExit() {
    persist();
    ReferenceAudio::stopEffects();
    ReferenceAudio::setPaused(false);
    Scene::onExit();
}
