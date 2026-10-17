#include "AnimatedCutscene.h"
#include <cmath>
#include <sstream>
USING_NS_CC;

AnimatedCutscene* AnimatedCutscene::create(const std::string& path, std::string& error) {
    auto node = new AnimatedCutscene;
    if (!node->init() || !node->load(path, error)) { delete node; return nullptr; }
    node->autorelease();
    return node;
}

bool AnimatedCutscene::load(const std::string& path, std::string& error) {
    auto data = FileUtils::getInstance()->getDataFromFile(path);
    if (data.isNull()) { error = "Cutscene file could not be read"; return false; }
    std::vector<uint8_t> bytes(data.getBytes(), data.getBytes() + data.getSize());
    if (!decoder_.load(std::move(bytes), error)) return false;
    auto texture = new Texture2D;
    const Size size(static_cast<float>(decoder_.width()), static_cast<float>(decoder_.height()));
    if (!texture->initWithData(decoder_.pixels(), decoder_.width() * decoder_.height() * 4,
                              backend::PixelFormat::RGBA8888, decoder_.width(), decoder_.height(), size)) {
        texture->release();
        error = "Cutscene texture allocation failed";
        return false;
    }
    texture->setAntiAliasTexParameters();
    sprite_ = Sprite::createWithTexture(texture);
    texture->release();
    if (!sprite_) { error = "Cutscene sprite creation failed"; return false; }
    sprite_->setBlendFunc(BlendFunc::ALPHA_NON_PREMULTIPLIED);
    sprite_->setOpacityModifyRGB(false);
    sprite_->setAnchorPoint(Vec2::ZERO);
    addChild(sprite_);
    setContentSize(size);
    scheduleUpdate();
    return true;
}

void AnimatedCutscene::upload() {
    // Cocos4's upload API is not const-correct; it only reads the pixel buffer.
    sprite_->getTexture()->updateWithData(const_cast<uint8_t*>(decoder_.pixels()), 0, 0, decoder_.width(), decoder_.height());
}

void AnimatedCutscene::restart() {
    elapsedMs_ = 0;
    std::string error;
    if (decoder_.seek(0, error)) upload();
    else { CCLOG("Cutscene: %s", error.c_str()); paused_ = true; }
}

void AnimatedCutscene::update(float delta) {
    if (paused_ || delta <= 0) return;
    const int oldFrame = decoder_.frameIndex();
    elapsedMs_ = std::fmod(elapsedMs_ + delta * 1000.0, static_cast<double>(decoder_.durationMs()));
    std::string error;
    if (!decoder_.seek(static_cast<int>(elapsedMs_), error)) {
        CCLOG("Cutscene: %s", error.c_str());
        paused_ = true;
        return;
    }
    if (oldFrame != decoder_.frameIndex()) upload();
}

std::string AnimatedCutscene::diagnostic() const {
    std::ostringstream result;
    result << "WEBP frames=" << decoder_.frameCount() << " frame=" << decoder_.frameIndex()
           << " time_ms=" << elapsedMs_ << " duration_ms=" << decoder_.durationMs()
           << " size=" << decoder_.width() << "x" << decoder_.height();
    return result.str();
}
