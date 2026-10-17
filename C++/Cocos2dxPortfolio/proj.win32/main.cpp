#include "AppDelegate.h"
#include <windows.h>
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    AppDelegate application;
    return cocos2d::Application::getInstance()->run();
}
