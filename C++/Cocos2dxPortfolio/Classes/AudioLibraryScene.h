#pragma once
#include "cocos2d.h"
#include <functional>
#include <string>
#include <vector>
namespace cocos2d { namespace ui { class TextField; } }

class AudioLibraryScene : public cocos2d::Scene {
public:
    static AudioLibraryScene* create(const std::function<void()>& onBack);
    void onExit() override;
private:
    bool load(const std::function<void()>&);
    void filter();
    void rows();
    void preview(size_t index);
    void later(const std::function<void()>&);
    std::function<void()> onBack_;
    cocos2d::ui::TextField* search_=nullptr;
    cocos2d::Node* rows_=nullptr;
    cocos2d::Label* summary_=nullptr;
    cocos2d::Label* status_=nullptr;
    std::vector<size_t> filtered_;
    std::string category_;
    int page_=0,previewId_=-1;
};
