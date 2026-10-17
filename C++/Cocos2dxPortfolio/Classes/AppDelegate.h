#pragma once
#include "cocos2d.h"
class AppDelegate : private cocos2d::Application {
  public:
    bool applicationDidFinishLaunching() override;
    void applicationDidEnterBackground() override;
    void applicationWillEnterForeground() override;
};
