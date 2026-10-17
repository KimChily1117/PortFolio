#include "WebpAnimation.h"
#include <limits>

WebpAnimation::~WebpAnimation() { WebPAnimDecoderDelete(decoder_); }

bool WebpAnimation::load(std::vector<uint8_t> bytes, std::string& error) {
    WebPAnimDecoderDelete(decoder_);
    decoder_ = nullptr;
    pixels_ = nullptr;
    frameIndex_ = -1;
    frameEndMs_ = durationMs_ = 0;
    previousPosition_ = -1;
    bytes_ = std::move(bytes);
    WebPData source{bytes_.data(), bytes_.size()};
    auto demux = WebPDemux(&source);
    if (!demux) { error = "WebP container could not be read"; return false; }
    width_ = static_cast<int>(WebPDemuxGetI(demux, WEBP_FF_CANVAS_WIDTH));
    height_ = static_cast<int>(WebPDemuxGetI(demux, WEBP_FF_CANVAS_HEIGHT));
    frameCount_ = static_cast<int>(WebPDemuxGetI(demux, WEBP_FF_FRAME_COUNT));
    bool valid = width_ > 0 && height_ > 0 && width_ <= 4096 && height_ <= 4096 &&
                 frameCount_ > 0 && frameCount_ <= 100000;
    WebPIterator frame{};
    if (valid && WebPDemuxGetFrame(demux, 1, &frame)) {
        do {
            if (frame.duration < 0 || frame.duration > std::numeric_limits<int>::max() - durationMs_) {
                valid = false;
                break;
            }
            durationMs_ += frame.duration;
        } while (WebPDemuxNextFrame(&frame));
        WebPDemuxReleaseIterator(&frame);
    } else valid = false;
    WebPDemuxDelete(demux);
    if (!valid || durationMs_ <= 0) { error = "Invalid animation dimensions or timing"; return false; }
    WebPAnimDecoderOptions options;
    if (!WebPAnimDecoderOptionsInit(&options)) { error = "WebP decoder ABI mismatch"; return false; }
    options.color_mode = MODE_RGBA;
    options.use_threads = 1;
    decoder_ = WebPAnimDecoderNew(&source, &options);
    if (!decoder_) { error = "WebP animation decoder could not be created"; return false; }
    return seek(0, error);
}

bool WebpAnimation::seek(int milliseconds, std::string& error) {
    if (!decoder_ || milliseconds < 0) { error = "Invalid playback position"; return false; }
    const int position = milliseconds % durationMs_;
    if (position < previousPosition_) {
        WebPAnimDecoderReset(decoder_);
        frameEndMs_ = 0;
        frameIndex_ = -1;
    }
    while (frameIndex_ < 0 || position >= frameEndMs_) {
        if (!WebPAnimDecoderHasMoreFrames(decoder_) ||
            !WebPAnimDecoderGetNext(decoder_, &pixels_, &frameEndMs_)) {
            error = "WebP frame decoding failed";
            return false;
        }
        ++frameIndex_;
    }
    previousPosition_ = position;
    return true;
}
