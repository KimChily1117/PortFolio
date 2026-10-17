#pragma once
#include "cocos2d.h"
#include <string>
#include <vector>

// Original atlas art, assembled for this offline battle viewport. No idle loop.
class BattleStageBackground : public cocos2d::Node {
  public:
    static BattleStageBackground* create(int stage = 1);
    void setCameraOffset(cocos2d::Vec2 offset);
    const std::string& sourceTheme() const {
        return theme_;
    }

  private:
    bool build(int stage);
    void layer(const std::string& asset, cocos2d::Vec2 center, float width, float factor, bool flip = false);
    struct Plane {
        cocos2d::Sprite* sprite;
        cocos2d::Vec2 base;
        float parallax;
    };
    std::vector<Plane> planes_;
    std::string theme_;
};
