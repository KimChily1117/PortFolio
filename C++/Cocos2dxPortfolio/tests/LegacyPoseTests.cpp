#include "LegacySpinePose.h"
#include "spine/extension.h"
#include <cmath>
#include <cstdlib>
#include <iostream>
extern "C" {
void _spAtlasPage_createTexture(spAtlasPage*, const char*) {}
void _spAtlasPage_disposeTexture(spAtlasPage*) {}
char* _spUtil_readFile(const char* path, int* length) { return _spReadFile(path, length); }
}
void check(bool value, const char* why) { if (!value) { std::cerr << why; std::exit(1); } }
bool near(float a, float b) { return std::abs(a - b) < .001f; }
int main() {
    const char* json = R"({"skeleton":{"hash":"fixture","spine":"3.6.53","e7v21x":true},
      "bones":[{"name":"root","rotation":30,"scaleX":2,"scaleY":3},
      {"name":"child","parent":"root","rotation":60,"x":5,"scaleX":0.5,"scaleY":2,"transform":"noScale"}],
      "animations":{"test":{"_e7_flips":[{"bone":"root","axis":"x","frames":[0,0,1,1]}]}}})";
    auto reader = spSkeletonJson_create(nullptr);
    auto data = spSkeletonJson_readSkeletonData(reader, json);
    check(data != nullptr, "test skeleton parses");
    auto s = spSkeleton_create(data); LegacySpinePose pose; std::string error;
    check(pose.load(json, s, error), "legacy configuration loads");
    pose.update(s, "test", .5f);
    auto child = s->bones[1];
    check(near(child->a, 0) && near(child->b, -2) && near(child->c, .5f), "scalar rotation and noScale inheritance");
    check(near(child->worldX, 8.660254f) && near(child->worldY, 5), "child position uses parent matrix");
    pose.update(s, "test", 1.5f);
    check(near(child->b, 2) && near(child->c, .5f) && near(child->worldX, -8.660254f), "world-axis flip propagates across noScale");
    pose.update(s, "test", 0);
    check(near(child->b, -2), "backward seek resets flip state");
    spSkeleton_dispose(s); spSkeletonData_dispose(data); spSkeletonJson_dispose(reader);
    std::cout << "PASS: legacy scalar inheritance, stepped flips, backward seek\n";
}
