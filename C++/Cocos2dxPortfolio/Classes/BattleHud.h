#pragma once
#include "cocos2d.h"
#include <array>
#include <functional>
#include <string>
#include <vector>

// Presentation-only view. The battle model owns CR, targeting, cooldowns and turns.
class BattleHud : public cocos2d::Node {
  public:
    struct Callbacks {
        std::function<void(int)> onSkill; // zero-based S1/S2/S3
        std::function<void()> onAuto, onSpeed, onMenu;
    };
    struct HeroView {
        std::string name, facePath;
        int hp = 0, maxHp = 1;
    };
    struct SkillView {
        std::string label, iconPath;
        int cooldown = 0;
        bool enabled = false;
    };
    struct TurnView {
        int unitId = -1;
        std::string name, facePath;
        float readiness = 0; // 0..100 percentage, not a normalized fraction
        bool enemy = false, active = false;
    };
    static BattleHud* create(const Callbacks& callbacks);
    static std::string facePath(const std::string& heroId);
    static std::string skillIconPath(const std::string& heroId, int skillIndex);
    void setStage(const std::string& stage);
    void setActiveHero(const HeroView& hero);
    void setSkills(const std::array<SkillView, 3>& skills);
    void setTurnOrder(const std::vector<TurnView>& order);
    void setControls(bool autoOn, int speed);
    void setStatus(const std::string& status);
    void setInputEnabled(bool enabled);
    void setTargetMarker(cocos2d::Vec2 position, bool visible, const std::string& text = "");
    std::string diagnostic() const;

  private:
    bool build(const Callbacks& callbacks);
    void refreshEnabled();
    Callbacks callbacks_;
    cocos2d::Label *stage_ = nullptr, *name_ = nullptr, *hp_ = nullptr, *status_ = nullptr;
    cocos2d::Label *auto_ = nullptr, *speed_ = nullptr, *targetNumber_ = nullptr;
    cocos2d::Sprite *face_ = nullptr, *hpFill_ = nullptr, *target_ = nullptr;
    std::array<cocos2d::Node*, 3> skillButtons_{};
    std::array<cocos2d::Sprite*, 3> skillIcons_{};
    std::array<cocos2d::Label*, 3> cooldowns_{}, skillNames_{};
    std::array<std::string, 3> skillPaths_{};
    std::array<bool, 3> enabled_{};
    struct OrderRow {
        cocos2d::Node* root;
        cocos2d::Sprite* face;
        cocos2d::DrawNode* border;
        cocos2d::Label* percent;
        cocos2d::Label* fallback;
        std::string path;
    };
    std::vector<OrderRow> order_;
    std::string facePath_, stageName_;
    bool inputEnabled_ = true;
    int activeUnit_ = -1;
};
