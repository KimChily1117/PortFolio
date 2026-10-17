#pragma once
#include "cocos2d.h"
#include "core/GameModel.h"
#include "SaveStore.h"
#include <memory>
#include <functional>
#include <deque>
class SpinePreview;
class SkillPresentation;
class BattleHud;
class GameScene : public cocos2d::Scene {
  public:
    CREATE_FUNC(GameScene);
    bool init() override;
    void update(float dt) override;
    void onExit() override;
    void suspend();
    void resumeGame();
    std::string presentationDiagnostic() const;
    void skipSkillPresentation();
    void setAutoBattle(bool enabled);

  private:
    cocos2d::Label* label(cocos2d::Node*, const std::string&, float, float, float, cocos2d::Color3B,
                          bool left = false);
    cocos2d::Node* button(cocos2d::Node*, const std::string&, const cocos2d::Rect&, cocos2d::Color4F,
                          std::function<void()>, cocos2d::Label** text = nullptr);
    void loadReferenceParty();
    void buildInterface();
    void buildLandscape();
    void beginBattle();
    void refresh();
    void chooseSkill(int skill);
    void chooseTarget(int target);
    void queueEvents(const std::vector<aster::BattleEvent>& events);
    void present(const aster::BattleEvent& event);
    void startAction(std::vector<aster::BattleEvent> events);
    void updateAction(float dt);
    void finishAction();
    void restoreIdle(int unit);
    void setBattlePaused(bool paused);
    void drawActionEffects();
    void floatText(const std::string&, cocos2d::Vec2, cocos2d::Color3B);
    void toast(const std::string&);
    void showMenu();
    void persist();
    void showCutscene(int hero);
    void finishCutscene();
    void drainPresentationEvents();
    void applySkillImpact();
    void showResult();
    std::string unitName(int unit) const;
    aster::Profile profile_;
    aster::Battle battle_;
    std::array<aster::HeroDefinition, aster::HeroCount> definitions_;
    std::unique_ptr<aster::SaveStore> store_;
    std::array<SpinePreview*, aster::UnitCount> rigs_{};
    std::array<cocos2d::Vec2, aster::UnitCount> homes_{}, rigHomes_{};
    std::array<cocos2d::DrawNode*, aster::UnitCount> bars_{};
    std::array<cocos2d::DrawNode*, aster::UnitCount> shadows_{};
    std::array<std::string, aster::HeroCount> referenceSkills_{}, heroIds_{};
    std::array<int, aster::UnitCount> displayedHp_{};
    std::array<cocos2d::Label*, 24> floatPool_{};
    std::deque<aster::BattleEvent> pendingEvents_;
    std::vector<aster::BattleEvent> skillEvents_;
    struct ActionMotion {
        std::vector<aster::BattleEvent> events;
        std::string animation, phase;
        cocos2d::Vec2 offset;
        int actor = -1, target = -1;
        bool melee = false, enemy = false, supportive = false;
        bool attackStarted = false, impactApplied = false, returning = false;
        float elapsed = 0, windup = 0, approach = 0, attackDuration = 0;
        float hit = 0, returnDuration = 0;
    };
    std::unique_ptr<ActionMotion> action_;
    std::array<float, aster::UnitCount> reactionTime_{};
    cocos2d::DrawNode* actionEffects_ = nullptr;
    cocos2d::Label* actionTitle_ = nullptr;
    int actionCount_ = 0, actionImpactCount_ = 0, enemyActionCount_ = 0;
    SkillPresentation* skillPresentation_ = nullptr;
    BattleHud* hud_ = nullptr;
    cocos2d::Node *battleStage_ = nullptr, *battleBackground_ = nullptr, *opponents_ = nullptr,
                  *modal_ = nullptr, *cutscene_ = nullptr;
    cocos2d::Label* toast_ = nullptr;
    bool skillImpactApplied_ = false, cutscenesEnabled_ = true, paused_ = false, settled_ = false,
         saveBlocked_ = false;
    int presentationCount_ = 0, impactCount_ = 0, speed_ = 1, floatIndex_ = 0, selectedSkill_ = 0,
        selectedTarget_ = -1;
    float turnDelay_ = 0, toastTimer_ = 0;
};
