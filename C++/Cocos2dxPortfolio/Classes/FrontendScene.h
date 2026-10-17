#pragma once
#include "cocos2d.h"
#include "core/GameModel.h"
#include <functional>
#include <vector>
#include <array>
class SpinePreview;
namespace cocos2d {
namespace ui {
class TextField;
}
} // namespace cocos2d
class FrontendScene : public cocos2d::Scene {
  public:
    CREATE_FUNC(FrontendScene);
    bool init() override;
    void onExit() override;
    std::string diagnostic() const;

  private:
    void showLobby();
    void clearPage();
    void showWorlds(int world);
    void showReady(int stage);
    void showHeroes(int page = 0);
    void showHero(size_t index);
    void showCutscene(const std::string& path, size_t hero);
    void showSkill(const std::string& skill, size_t hero);
    void loadReferenceCatalogs();
    void chrome(const std::string& title, std::function<void()> back = {});
    void gallery();
    SpinePreview* animatedHero(const std::string& path, const std::string& atlas, cocos2d::Rect box);
    void enterStage(int stage);
    void background();
    void defer(std::function<void()> action);
    aster::Profile profile_;
    bool saveBlocked_ = false;
    bool navigating_ = false;
    struct Hero {
        std::string id, name, path, atlas, gender, heroId;
    };
    struct Skill {
        std::string id, name, heroId, portrait, atlas;
    };
    struct Cut {
        std::string path, character, heroId;
    };
    std::vector<Hero> heroes_;
    std::vector<Skill> skills_;
    std::vector<Cut> cuts_;
    std::array<std::string, aster::HeroCount> party_;
    size_t featured_ = 0;
    int heroPage_ = 0;
    bool femaleOnly_ = true;
    std::string heroSearch_;
    std::string route_;
    cocos2d::ui::TextField* searchField_ = nullptr;
};
