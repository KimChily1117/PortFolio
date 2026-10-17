#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <webp/demux.h>

// Retains compressed bytes and only the decoder's two compositing canvases.
class WebpAnimation {
  public:
    ~WebpAnimation();
    WebpAnimation() = default;
    WebpAnimation(const WebpAnimation&) = delete;
    WebpAnimation& operator=(const WebpAnimation&) = delete;
    bool load(std::vector<uint8_t> bytes, std::string& error);
    bool seek(int milliseconds, std::string& error);
    const uint8_t* pixels() const { return pixels_; }
    int width() const { return width_; }
    int height() const { return height_; }
    int frameCount() const { return frameCount_; }
    int frameIndex() const { return frameIndex_; }
    int durationMs() const { return durationMs_; }
    int frameEndMs() const { return frameEndMs_; }
  private:
    std::vector<uint8_t> bytes_;
    WebPAnimDecoder* decoder_ = nullptr;
    uint8_t* pixels_ = nullptr;
    int width_ = 0, height_ = 0, frameCount_ = 0, durationMs_ = 0;
    int frameIndex_ = -1, frameEndMs_ = 0, previousPosition_ = -1;
};
