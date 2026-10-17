#pragma once
#include "cocos2d.h"
#include <functional>
#include <string>
#include <vector>

class SpinePreview;
class AnimatedCutscene;
namespace cocos2d { namespace ui { class TextField; } }

class ResourceGallery : public cocos2d::Scene {
  public:
    CREATE_FUNC(ResourceGallery);
    bool init() override;
    void onExit() override;
    SpinePreview* spinePreview() const { return spine_; }
    AnimatedCutscene* cutscenePreview() const { return cutscene_; }

  private:
    struct Item {
        std::string type, path, name, category, atlas, displayName, gender, heroId;
    };
    void loadCatalog();
    void selectFilter(const std::string& type);
    void showItem();
    void navigate(int offset);
    void toggleAudio();
    void stopAudio();
    void updateAudio(float);
    void defer(std::function<void()> action);
    void showSpine(const Item& item);
    void showCutscene(const Item& item);
    void changeAnimation();
    void changeSkin();
    void fitSpine();
    void openPicker(bool animations);
    void refreshPicker();
    void closePicker();
    void chooseAnimation(size_t index);
    bool matchesFilter(const Item& item, const std::string& filter) const;

    std::vector<Item> items_;
    std::vector<size_t> filtered_;
    std::string filter_ = "image";
    std::string catalogError_;
    size_t selection_ = 0;
    int audioId_ = -1;
    cocos2d::Label* audioButtonLabel_ = nullptr;
    cocos2d::Label* audioStatusLabel_ = nullptr;
    SpinePreview* spine_ = nullptr;
    AnimatedCutscene* cutscene_ = nullptr;
    cocos2d::Label* animationLabel_ = nullptr;
    cocos2d::Label* skinLabel_ = nullptr;
    cocos2d::Label* pauseLabel_ = nullptr;
    size_t animation_ = 0, skin_ = 0;
    bool animationPaused_ = false;
    float zoom_ = 1.f, spineBaseScale_ = 1.f;
    cocos2d::Vec2 spineCenter_;
    cocos2d::Node* picker_ = nullptr;
    cocos2d::Node* pickerList_ = nullptr;
    cocos2d::ui::TextField* pickerSearch_ = nullptr;
    bool pickingAnimations_ = false;
    size_t pickerPage_ = 0;
    std::string pickerQuery_;
};
