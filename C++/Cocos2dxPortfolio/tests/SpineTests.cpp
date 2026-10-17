#include <spine/spine.h>
#include <spine/extension.h>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <iostream>
#include <vector>
#include <fstream>
#include <sstream>
#include "LegacySpinePose.h"

extern "C" {
void _spAtlasPage_createTexture(spAtlasPage*, const char*) {}
void _spAtlasPage_disposeTexture(spAtlasPage*) {}
char* _spUtil_readFile(const char* path, int* length) { return _spReadFile(path, length); }
}
void require(bool ok, const char* why) {
    if (!ok) { std::cerr << "FAIL: " << why << '\n'; std::exit(1); }
}
std::vector<float> geometry(spSkeleton* skeleton) {
    std::vector<float> result;
    for (int i = 0; i < skeleton->slotsCount; ++i) {
        auto slot = skeleton->slots[i];
        auto a = slot->attachment;
        if (!a) continue;
        std::vector<float> points;
        if (a->type == SP_ATTACHMENT_REGION) {
            points.resize(8);
            spRegionAttachment_computeWorldVertices((spRegionAttachment*)a, slot->bone, points.data(), 0, 2);
        } else if (a->type == SP_ATTACHMENT_MESH) {
            auto mesh = (spMeshAttachment*)a;
            points.resize(mesh->super.worldVerticesLength);
            spVertexAttachment_computeWorldVertices(&mesh->super, slot, 0, (int)points.size(), points.data(), 0, 2);
        }
        for (float value : points) require(std::isfinite(value) && std::abs(value) < 1000000.f, "finite bounded world vertices");
        result.insert(result.end(), points.begin(), points.end());
    }
    return result;
}
int main(int argc, char** argv) {
    require(argc == 3, "expected JSON and atlas path");
    auto atlas = spAtlas_createFromFile(argv[2], nullptr);
    require(atlas != nullptr, "atlas loads");
    auto reader = spSkeletonJson_create(atlas);
    auto data = spSkeletonJson_readSkeletonDataFile(reader, argv[1]);
    if (!data) std::cerr << (reader->error ? reader->error : "unknown parser error") << '\n';
    require(data != nullptr, "skeleton parses in actual bundled runtime");
    require(data->animationsCount > 0 && data->bonesCount > 0, "has real bones and animations");
    auto skeleton = spSkeleton_create(data);
    std::ifstream source(argv[1]); std::stringstream contents; contents << source.rdbuf();
    LegacySpinePose pose; std::string error;
    require(pose.load(contents.str(), skeleton, error), error.c_str());
    auto stateData = spAnimationStateData_create(data);
    auto state = spAnimationState_create(stateData);
    bool moved = false;
    bool rendered = false;
    int samples = 0;
    for (int skin = 0; skin < data->skinsCount; ++skin) {
        spSkeleton_setSkin(skeleton, data->skins[skin]);
        for (int animation = 0; animation < data->animationsCount; ++animation) {
            spSkeleton_setToSetupPose(skeleton);
            spAnimationState_clearTracks(state);
            spAnimationState_setAnimation(state, 0, data->animations[animation], 1);
            spAnimationState_apply(state, skeleton);
            pose.update(skeleton, data->animations[animation]->name, 0);
            const auto start = geometry(skeleton);
            rendered = rendered || !start.empty();
            const float step = std::max(.01f, data->animations[animation]->duration / 17.f);
            for (int frame = 0; frame < 20; ++frame) {
                spSkeleton_update(skeleton, step);
                spAnimationState_update(state, step);
                spAnimationState_apply(state, skeleton);
                float duration = data->animations[animation]->duration;
                pose.update(skeleton, data->animations[animation]->name,
                            duration > 0 ? std::fmod((frame + 1) * step, duration) : 0);
                auto current = geometry(skeleton);
                rendered = rendered || !current.empty();
                moved = moved || current != start;
                ++samples;
            }
        }
    }
    require(rendered, "at least one sampled pose has renderable attachments");
    require(moved, "animation changes real world vertices");
    std::cout << "PASS: " << data->bonesCount << " bones, " << data->animationsCount << " animations, "
              << data->skinsCount << " skins, " << samples << " sampled animated poses\n";
    spAnimationState_dispose(state);
    spAnimationStateData_dispose(stateData);
    spSkeleton_dispose(skeleton);
    spSkeletonData_dispose(data);
    spSkeletonJson_dispose(reader);
    spAtlas_dispose(atlas);
}
