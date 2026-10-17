#include "AppDelegate.h"
#include <jni.h>
#include <memory>
namespace {
std::unique_ptr<AppDelegate> application;
}
void cocos_android_app_init(JNIEnv*) {
    application.reset(new AppDelegate());
}
