#pragma once
#include "cocos2d.h"
#include <memory>
#include <string>
#include <vector>

// Uses the shipped Spine C animation runtime with a Cocos2d-x 4 renderer.
class SpinePreview : public cocos2d::Node {
  public:
    static SpinePreview* create(const std::string& jsonPath, const std::string& atlasPath,
                                std::string& error);
    ~SpinePreview() override;
    void update(float delta) override;
    void draw(cocos2d::Renderer*, const cocos2d::Mat4&, uint32_t flags) override;
    const std::vector<std::string>& animations() const;
    const std::vector<std::string>& skins() const;
    void play(size_t animation);
    bool playAnimation(const std::string& animation, bool loop = false);
    void seekAnimation(float seconds);
    float animationDuration(const std::string& animation) const;
    bool bonePosition(const std::string& bone, cocos2d::Vec2& position) const;
    void setSkin(size_t skin);
    void setAnimationPaused(bool paused);
    cocos2d::Rect localBounds() const;
    std::string diagnostic() const;

  private:
    SpinePreview();
    bool load(const std::string& jsonPath, const std::string& atlasPath, std::string& error);
    void rebuildGeometry();
    struct Impl;
    std::unique_ptr<Impl> impl_;
};
