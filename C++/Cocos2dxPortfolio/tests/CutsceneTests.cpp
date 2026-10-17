#include "WebpAnimation.h"
#include <fstream>
#include <iostream>
#include <iterator>
#include <stdexcept>

void require(bool ok, const std::string& message) {
    if (!ok) throw std::runtime_error(message);
}
uint64_t signature(const WebpAnimation& animation) {
    uint64_t value = 1469598103934665603ULL;
    const auto size = static_cast<size_t>(animation.width()) * animation.height() * 4;
    for (size_t i = 0; i < size; ++i) value = (value ^ animation.pixels()[i]) * 1099511628211ULL;
    return value;
}
int main(int argc, char** argv) {
    try {
        require(argc == 2, "Expected animated WebP path");
        std::ifstream input(argv[1], std::ios::binary);
        require(input.good(), "File readable");
        std::vector<uint8_t> bytes(std::istreambuf_iterator<char>(input), {});
        WebpAnimation animation;
        std::string error;
        require(animation.load(std::move(bytes), error), error);
        require(animation.frameCount() > 1, "Multiple actual frames");
        const auto initial = signature(animation);
        int samples = 1;
        bool changed = false;
        while (animation.frameEndMs() < animation.durationMs()) {
            const int next = animation.frameEndMs();
            require(animation.seek(next, error), error);
            changed = changed || signature(animation) != initial;
            ++samples;
        }
        require(changed, "Animation pixels change");
        require(animation.seek(animation.durationMs(), error), error);
        require(signature(animation) == initial, "Loop resets exact first frame including disposal/blending");
        require(animation.seek(animation.durationMs() - 1, error), error);
        require(animation.seek(0, error), error);
        require(signature(animation) == initial, "Restart resets exact first frame");
        std::cout << "PASS " << samples << " frames " << animation.durationMs() << "ms "
                  << animation.width() << 'x' << animation.height() << '\n';
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
