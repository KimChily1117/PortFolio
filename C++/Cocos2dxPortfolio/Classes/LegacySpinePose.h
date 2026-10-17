#pragma once
#include "spine/spine.h"
#include <string>
#include <unordered_map>
#include <vector>

// Compatibility for converted E7 2.1 skeletons. Modern rigs use the stock runtime.
class LegacySpinePose {
  public:
    bool load(const std::string& json, spSkeleton* skeleton, std::string& error);
    void update(spSkeleton* skeleton, const std::string& animation, float time);
    bool enabled() const { return enabled_; }
  private:
    struct Flip { int bone; bool x; std::vector<float> frames; };
    struct World { float rotation = 0, sx = 1, sy = 1; bool fx = false, fy = false; };
    bool enabled_ = false;
    std::unordered_map<std::string, std::vector<Flip>> flips_;
    std::vector<World> worlds_;
};
