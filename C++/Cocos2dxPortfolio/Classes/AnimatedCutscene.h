#pragma once
#include "cocos2d.h"
#include "WebpAnimation.h"

class AnimatedCutscene : public cocos2d::Node {
  public:
    static AnimatedCutscene* create(const std::string& path, std::string& error);
    void setPaused(bool paused) { paused_ = paused; }
    void restart();
    float durationSeconds() const { return decoder_.durationMs() / 1000.f; }
    int frameCount() const { return decoder_.frameCount(); }
    std::string diagnostic() const;
    void update(float delta) override;
  private:
    bool load(const std::string& path, std::string& error);
    void upload();
    WebpAnimation decoder_;
    cocos2d::Sprite* sprite_ = nullptr;
    double elapsedMs_ = 0;
    bool paused_ = false;
};
