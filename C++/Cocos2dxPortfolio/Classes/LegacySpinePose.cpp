#include "LegacySpinePose.h"
#include "spine/extension.h"
#include "json/document.h"
#include <cmath>

bool LegacySpinePose::load(const std::string& json, spSkeleton* s, std::string& error) {
    rapidjson::Document d;
    d.Parse(json.c_str());
    enabled_ = !d.HasParseError() && d.IsObject() && d.HasMember("skeleton") &&
        d["skeleton"].IsObject() && d["skeleton"].HasMember("e7v21x") &&
        d["skeleton"]["e7v21x"].IsBool() && d["skeleton"]["e7v21x"].GetBool();
    flips_.clear();
    if (!enabled_) return true;
    if (s->ikConstraintsCount || s->transformConstraintsCount || s->pathConstraintsCount) {
        error = "Legacy 2.1 constraints require a separate compatibility implementation.";
        return false;
    }
    worlds_.resize(s->bonesCount);
    if (!d.HasMember("animations") || !d["animations"].IsObject()) return true;
    for (auto a = d["animations"].MemberBegin(); a != d["animations"].MemberEnd(); ++a) {
        if (!a->value.IsObject() || !a->value.HasMember("_e7_flips")) continue;
        const auto& records = a->value["_e7_flips"];
        if (!records.IsArray()) continue;
        for (const auto& r : records.GetArray()) {
            if (!r.IsObject() || !r.HasMember("bone") || !r["bone"].IsString() ||
                !r.HasMember("frames") || !r["frames"].IsArray()) continue;
            int bone = spSkeleton_findBoneIndex(s, r["bone"].GetString());
            if (bone < 0) continue;
            Flip f{bone, !r.HasMember("axis") || std::string(r["axis"].GetString()) == "x", {}};
            for (const auto& v : r["frames"].GetArray()) if (v.IsNumber()) f.frames.push_back(v.GetFloat());
            if (f.frames.size() % 2 == 0) flips_[a->name.GetString()].push_back(std::move(f));
        }
    }
    return true;
}

void LegacySpinePose::update(spSkeleton* s, const std::string& animation, float time) {
    if (!enabled_) { spSkeleton_updateWorldTransform(s); return; }
    for (auto& w : worlds_) w = World{};
    auto records = flips_.find(animation);
    if (records != flips_.end()) for (const auto& f : records->second) {
        bool value = false;
        for (size_t k = 0; k + 1 < f.frames.size() && f.frames[k] <= time; k += 2)
            value = f.frames[k + 1] != 0;
        if (f.x) worlds_[f.bone].fx = value; else worlds_[f.bone].fy = value;
    }
    for (int i = 0; i < s->bonesCount; ++i) {
        auto b = s->bones[i];
        auto& w = worlds_[i];
        auto p = b->parent;
        w.rotation = b->rotation; w.sx = b->scaleX; w.sy = b->scaleY;
        if (p) {
            const auto& pw = worlds_[p->data->index];
            auto mode = b->data->transformMode;
            if (mode == SP_TRANSFORMMODE_NORMAL || mode == SP_TRANSFORMMODE_NOSCALE ||
                mode == SP_TRANSFORMMODE_NOSCALEORREFLECTION) w.rotation += pw.rotation;
            if (mode == SP_TRANSFORMMODE_NORMAL || mode == SP_TRANSFORMMODE_NOROTATIONORREFLECTION) {
                w.sx *= pw.sx; w.sy *= pw.sy;
            }
            w.fx = w.fx != pw.fx; w.fy = w.fy != pw.fy;
            CONST_CAST(float, b->worldX) = p->a * b->x + p->b * b->y + p->worldX;
            CONST_CAST(float, b->worldY) = p->c * b->x + p->d * b->y + p->worldY;
        } else {
            w.fx = w.fx != (s->flipX != 0); w.fy = w.fy != (s->flipY != 0);
            CONST_CAST(float, b->worldX) = (s->flipX ? -b->x : b->x) + s->x;
            CONST_CAST(float, b->worldY) = ((s->flipY != spBone_isYDown()) ? -b->y : b->y) + s->y;
        }
        const float angle = w.rotation * 0.017453292519943295f;
        const float cs = std::cos(angle), sn = std::sin(angle);
        const float fx = w.fx ? -1.f : 1.f, fy = w.fy != bool(spBone_isYDown()) ? -1.f : 1.f;
        CONST_CAST(float, b->a) = cs * w.sx * fx;
        CONST_CAST(float, b->b) = -sn * w.sy * fx;
        CONST_CAST(float, b->c) = sn * w.sx * fy;
        CONST_CAST(float, b->d) = cs * w.sy * fy;
    }
}
