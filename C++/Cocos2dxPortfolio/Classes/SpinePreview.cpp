#include "SpinePreview.h"
#include "LegacySpinePose.h"
#include "spine/spine.h"
#include "spine/extension.h"
#include "renderer/backend/ProgramState.h"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <sstream>

USING_NS_CC;

// Spine's C runtime delegates file and texture loading to the host application.
extern "C" {
void _spAtlasPage_createTexture(spAtlasPage* page, const char* path) {
    auto texture = Director::getInstance()->getTextureCache()->addImage(path);
    if (!texture)
        return;
    texture->retain();
    page->rendererObject = texture;
    page->width = texture->getPixelsWide();
    page->height = texture->getPixelsHigh();
    Texture2D::TexParams sampler;
    sampler.minFilter = page->minFilter == SP_ATLAS_NEAREST ? backend::SamplerFilter::NEAREST
                                                            : backend::SamplerFilter::LINEAR;
    sampler.magFilter = page->magFilter == SP_ATLAS_NEAREST ? backend::SamplerFilter::NEAREST
                                                            : backend::SamplerFilter::LINEAR;
    auto wrap = [](spAtlasWrap mode) {
        return mode == SP_ATLAS_REPEAT           ? backend::SamplerAddressMode::REPEAT
               : mode == SP_ATLAS_MIRROREDREPEAT ? backend::SamplerAddressMode::MIRROR_REPEAT
                                                 : backend::SamplerAddressMode::CLAMP_TO_EDGE;
    };
    sampler.sAddressMode = wrap(page->uWrap);
    sampler.tAddressMode = wrap(page->vWrap);
    texture->setTexParameters(sampler);
    // Retained by the atlas until the preview is destroyed; do not accumulate atlas pages in cache.
    Director::getInstance()->getTextureCache()->removeTextureForKey(path);
}
void _spAtlasPage_disposeTexture(spAtlasPage* page) {
    if (page->rendererObject)
        static_cast<Texture2D*>(page->rendererObject)->release();
}
char* _spUtil_readFile(const char* path, int* length) {
    auto data = FileUtils::getInstance()->getDataFromFile(path);
    if (data.isNull() || data.getSize() > std::numeric_limits<int>::max())
        return nullptr;
    *length = static_cast<int>(data.getSize());
    auto result = MALLOC(char, *length + 1);
    if (!result)
        return nullptr;
    std::memcpy(result, data.getBytes(), *length);
    result[*length] = '\0';
    return result;
}
}

namespace {
struct Batch {
    TrianglesCommand command;
    std::vector<V3F_C4B_T2F> vertices;
    std::vector<unsigned short> indices;
    Texture2D* texture = nullptr;
    BlendFunc blend = BlendFunc::ALPHA_PREMULTIPLIED;
    Color4B baseColor = Color4B::WHITE;
    bool premultiplied = false;
    backend::ProgramState* program = nullptr;

    Batch() {
        program = new backend::ProgramState(
            backend::Program::getBuiltinProgram(backend::ProgramType::POSITION_TEXTURE_COLOR));
        auto layout = program->getVertexLayout();
        layout->setAttribute(backend::ATTRIBUTE_NAME_POSITION,
                             program->getAttributeLocation(backend::Attribute::POSITION),
                             backend::VertexFormat::FLOAT3, offsetof(V3F_C4B_T2F, vertices), false);
        layout->setAttribute(backend::ATTRIBUTE_NAME_TEXCOORD,
                             program->getAttributeLocation(backend::Attribute::TEXCOORD),
                             backend::VertexFormat::FLOAT2, offsetof(V3F_C4B_T2F, texCoords), false);
        layout->setAttribute(backend::ATTRIBUTE_NAME_COLOR,
                             program->getAttributeLocation(backend::Attribute::COLOR),
                             backend::VertexFormat::UBYTE4, offsetof(V3F_C4B_T2F, colors), true);
        layout->setLayout(sizeof(V3F_C4B_T2F));
        command.getPipelineDescriptor().programState = program;
    }
    ~Batch() {
        program->release();
    }
};

unsigned char channel(float value) {
    return static_cast<unsigned char>(std::max(0.f, std::min(1.f, value)) * 255.f);
}
} // namespace

struct SpinePreview::Impl {
    spAtlas* atlas = nullptr;
    spSkeletonData* data = nullptr;
    spAtlasAttachmentLoader* loader = nullptr;
    spSkeleton* skeleton = nullptr;
    spAnimationStateData* stateData = nullptr;
    spAnimationState* state = nullptr;
    spSkeletonClipping* clipper = nullptr;
    std::vector<std::string> animations;
    std::vector<std::string> skins;
    std::vector<std::unique_ptr<Batch>> batches;
    Rect bounds;
    bool paused = false;
    size_t activeBatches = 0;
    size_t vertexCount = 0;
    double vertexSignature = 0;
    LegacySpinePose pose;
    std::string animation;
    float animationTime = 0;
    bool loop = true;
    void updatePose() {
        float time = animationTime;
        auto a = spSkeletonData_findAnimation(data, animation.c_str());
        if (a && a->duration > 0)
            time = loop ? std::fmod(time, a->duration) : std::min(time, a->duration);
        pose.update(skeleton, animation, time);
    }
    ~Impl() {
        batches.clear();
        if (state)
            spAnimationState_dispose(state);
        if (stateData)
            spAnimationStateData_dispose(stateData);
        if (skeleton)
            spSkeleton_dispose(skeleton);
        if (data)
            spSkeletonData_dispose(data);
        if (loader)
            spAttachmentLoader_dispose(&loader->super);
        if (clipper)
            spSkeletonClipping_dispose(clipper);
        if (atlas)
            spAtlas_dispose(atlas);
    }
};

SpinePreview::SpinePreview() : impl_(new Impl) {}
SpinePreview::~SpinePreview() = default;

SpinePreview* SpinePreview::create(const std::string& jsonPath, const std::string& atlasPath,
                                   std::string& error) {
    auto node = new SpinePreview;
    if (!node->init() || !node->load(jsonPath, atlasPath, error)) {
        delete node;
        return nullptr;
    }
    node->autorelease();
    return node;
}

bool SpinePreview::load(const std::string& jsonPath, const std::string& atlasPath, std::string& error) {
    auto files = FileUtils::getInstance();
    if (!files->isFileExist(jsonPath) || !files->isFileExist(atlasPath)) {
        error = "Skeleton JSON 또는 atlas 파일이 없습니다.";
        return false;
    }
    impl_->atlas = spAtlas_createFromFile(atlasPath.c_str(), nullptr);
    if (!impl_->atlas || !impl_->atlas->pages || !impl_->atlas->regions) {
        error = "Atlas를 읽을 수 없습니다.";
        return false;
    }
    for (auto page = impl_->atlas->pages; page; page = page->next)
        if (!page->rendererObject) {
            error = std::string("Atlas 이미지 로드 실패: ") + page->name;
            return false;
        }
    impl_->loader = spAtlasAttachmentLoader_create(impl_->atlas);
    auto json = spSkeletonJson_createWithLoader(&impl_->loader->super);
    impl_->data = spSkeletonJson_readSkeletonDataFile(json, jsonPath.c_str());
    if (!impl_->data)
        error = json->error ? json->error : "Skeleton JSON을 읽을 수 없습니다.";
    spSkeletonJson_dispose(json);
    if (!impl_->data)
        return false;
    for (int i = 0; i < impl_->data->slotsCount; ++i)
        if (impl_->data->slots[i]->darkColor) {
            error = "이 캐릭터의 two-color tint는 현재 렌더러에서 지원되지 않습니다.";
            return false;
        }
    impl_->skeleton = spSkeleton_create(impl_->data);
    if (!impl_->pose.load(files->getStringFromFile(jsonPath), impl_->skeleton, error))
        return false;
    impl_->stateData = spAnimationStateData_create(impl_->data);
    impl_->state = spAnimationState_create(impl_->stateData);
    impl_->clipper = spSkeletonClipping_create();
    for (int i = 0; i < impl_->data->animationsCount; ++i)
        impl_->animations.emplace_back(impl_->data->animations[i]->name);
    for (int i = 0; i < impl_->data->skinsCount; ++i)
        impl_->skins.emplace_back(impl_->data->skins[i]->name);
    impl_->updatePose();
    rebuildGeometry();
    scheduleUpdate();
    return true;
}

const std::vector<std::string>& SpinePreview::animations() const {
    return impl_->animations;
}
const std::vector<std::string>& SpinePreview::skins() const {
    return impl_->skins;
}
void SpinePreview::setAnimationPaused(bool paused) {
    impl_->paused = paused;
}
Rect SpinePreview::localBounds() const {
    return impl_->bounds;
}

void SpinePreview::setSkin(size_t skin) {
    if (skin >= impl_->skins.size())
        return;
    spSkeleton_setSkinByName(impl_->skeleton, impl_->skins[skin].c_str());
    spSkeleton_setSlotsToSetupPose(impl_->skeleton);
    spAnimationState_apply(impl_->state, impl_->skeleton);
    impl_->updatePose();
    rebuildGeometry();
}

void SpinePreview::play(size_t animation) {
    if (animation >= impl_->animations.size())
        return;
    playAnimation(impl_->animations[animation], true);
}

bool SpinePreview::playAnimation(const std::string& animation, bool loop) {
    if (!spSkeletonData_findAnimation(impl_->data, animation.c_str()))
        return false;
    impl_->animation = animation;
    impl_->animationTime = 0;
    impl_->loop = loop;
    spAnimationState_clearTracks(impl_->state);
    spSkeleton_setToSetupPose(impl_->skeleton);
    spAnimationState_setAnimationByName(impl_->state, 0, animation.c_str(), loop);
    spAnimationState_apply(impl_->state, impl_->skeleton);
    impl_->updatePose();
    rebuildGeometry();
    return true;
}

void SpinePreview::seekAnimation(float seconds) {
    auto entry = spAnimationState_getCurrent(impl_->state, 0);
    if (!entry)
        return;
    impl_->animationTime = std::max(0.f, seconds);
    entry->trackTime = impl_->animationTime;
    spSkeleton_setToSetupPose(impl_->skeleton);
    spAnimationState_apply(impl_->state, impl_->skeleton);
    impl_->updatePose();
    rebuildGeometry();
}

float SpinePreview::animationDuration(const std::string& animation) const {
    auto a = spSkeletonData_findAnimation(impl_->data, animation.c_str());
    return a ? a->duration : 0.f;
}

bool SpinePreview::bonePosition(const std::string& name, Vec2& position) const {
    auto bone = spSkeleton_findBone(impl_->skeleton, name.c_str());
    if (!bone)
        return false;
    position.set(bone->worldX, bone->worldY);
    return true;
}

void SpinePreview::update(float delta) {
    if (impl_->paused)
        return;
    impl_->animationTime += delta;
    spSkeleton_update(impl_->skeleton, delta);
    spAnimationState_update(impl_->state, delta);
    spAnimationState_apply(impl_->state, impl_->skeleton);
    impl_->updatePose();
    rebuildGeometry();
}

void SpinePreview::rebuildGeometry() {
    auto& p = *impl_;
    p.activeBatches = 0;
    p.vertexCount = 0;
    p.vertexSignature = 0;
    float minX = std::numeric_limits<float>::max(), minY = minX, maxX = -minX, maxY = -minX;
    std::vector<float> world;
    unsigned short quadIndices[] = {0, 1, 2, 2, 3, 0};
    spSkeletonClipping_clipEnd2(p.clipper);
    for (int i = 0; i < p.skeleton->slotsCount; ++i) {
        auto slot = p.skeleton->drawOrder[i];
        auto attachment = slot->attachment;
        if (!attachment) {
            spSkeletonClipping_clipEnd(p.clipper, slot);
            continue;
        }
        if (attachment->type == SP_ATTACHMENT_CLIPPING) {
            spSkeletonClipping_clipStart(p.clipper, slot,
                                         reinterpret_cast<spClippingAttachment*>(attachment));
            spSkeletonClipping_clipEnd(p.clipper, slot);
            continue;
        }
        float* uv = nullptr;
        unsigned short* indices = nullptr;
        int indicesCount = 0;
        spAtlasRegion* region = nullptr;
        spColor color;
        if (attachment->type == SP_ATTACHMENT_REGION) {
            auto a = reinterpret_cast<spRegionAttachment*>(attachment);
            world.resize(8);
            spRegionAttachment_computeWorldVertices(a, slot->bone, world.data(), 0, 2);
            uv = a->uvs;
            indices = quadIndices;
            indicesCount = 6;
            region = static_cast<spAtlasRegion*>(a->rendererObject);
            color = a->color;
        } else if (attachment->type == SP_ATTACHMENT_MESH || attachment->type == SP_ATTACHMENT_LINKED_MESH) {
            auto a = reinterpret_cast<spMeshAttachment*>(attachment);
            world.resize(a->super.worldVerticesLength);
            spVertexAttachment_computeWorldVertices(&a->super, slot, 0, a->super.worldVerticesLength,
                                                    world.data(), 0, 2);
            uv = a->uvs;
            indices = a->triangles;
            indicesCount = a->trianglesCount;
            region = static_cast<spAtlasRegion*>(a->rendererObject);
            color = a->color;
        } else {
            // Paths, points, and bounding boxes are non-rendering Spine attachments.
            spSkeletonClipping_clipEnd(p.clipper, slot);
            continue;
        }
        if (!region || !region->page || !region->page->rendererObject || world.empty() || !indicesCount) {
            spSkeletonClipping_clipEnd(p.clipper, slot);
            continue;
        }
        const float alpha = p.skeleton->color.a * slot->color.a * color.a;
        if (alpha <= 0.f) {
            spSkeletonClipping_clipEnd(p.clipper, slot);
            continue;
        }
        float* positions = world.data();
        int count = static_cast<int>(world.size() / 2);
        if (spSkeletonClipping_isClipping(p.clipper)) {
            spSkeletonClipping_clipTriangles(p.clipper, positions, static_cast<int>(world.size()), indices,
                                             indicesCount, uv, 2);
            positions = p.clipper->clippedVertices->items;
            count = p.clipper->clippedVertices->size / 2;
            uv = p.clipper->clippedUVs->items;
            indices = p.clipper->clippedTriangles->items;
            indicesCount = p.clipper->clippedTriangles->size;
        }
        if (count > 0 && indicesCount > 0) {
            if (p.activeBatches == p.batches.size())
                p.batches.emplace_back(new Batch);
            auto& batch = *p.batches[p.activeBatches++];
            batch.texture = static_cast<Texture2D*>(region->page->rendererObject);
            batch.vertices.resize(count);
            batch.indices.assign(indices, indices + indicesCount);
            bool pma = batch.texture->hasPremultipliedAlpha();
            const float rgbAlpha = pma ? alpha : 1.f;
            Color4B tint(channel(p.skeleton->color.r * slot->color.r * color.r * rgbAlpha),
                         channel(p.skeleton->color.g * slot->color.g * color.g * rgbAlpha),
                         channel(p.skeleton->color.b * slot->color.b * color.b * rgbAlpha), channel(alpha));
            batch.baseColor = tint;
            batch.premultiplied = pma;
            using BF = backend::BlendFactor;
            batch.blend = pma ? BlendFunc::ALPHA_PREMULTIPLIED : BlendFunc::ALPHA_NON_PREMULTIPLIED;
            if (slot->data->blendMode == SP_BLEND_MODE_ADDITIVE)
                batch.blend = {pma ? BF::ONE : BF::SRC_ALPHA, BF::ONE};
            else if (slot->data->blendMode == SP_BLEND_MODE_MULTIPLY)
                batch.blend = {BF::DST_COLOR, BF::ONE_MINUS_SRC_ALPHA};
            else if (slot->data->blendMode == SP_BLEND_MODE_SCREEN)
                batch.blend = {BF::ONE, BF::ONE_MINUS_SRC_COLOR};
            for (int v = 0; v < count; ++v) {
                const float x = positions[v * 2], y = positions[v * 2 + 1];
                batch.vertices[v].vertices = Vec3(x, y, 0);
                batch.vertices[v].texCoords = Tex2F(uv[v * 2], uv[v * 2 + 1]);
                batch.vertices[v].colors = tint;
                minX = std::min(minX, x);
                minY = std::min(minY, y);
                maxX = std::max(maxX, x);
                maxY = std::max(maxY, y);
                p.vertexSignature += (v + 1) * (x + y * .6180339887);
            }
            p.vertexCount += count;
        }
        spSkeletonClipping_clipEnd(p.clipper, slot);
    }
    spSkeletonClipping_clipEnd2(p.clipper);
    p.bounds = p.vertexCount ? Rect(minX, minY, maxX - minX, maxY - minY) : Rect::ZERO;
}

void SpinePreview::draw(Renderer* renderer, const Mat4& transform, uint32_t flags) {
    const auto& projection = Director::getInstance()->getMatrix(MATRIX_STACK_TYPE::MATRIX_STACK_PROJECTION);
    for (size_t i = 0; i < impl_->activeBatches; ++i) {
        auto& batch = *impl_->batches[i];
        // Node tint/opacity must also affect the custom Spine renderer. Derive
        // from the original batch color each draw so paused poses never compound tint.
        const auto c = getDisplayedColor();
        const float opacity = getDisplayedOpacity() / 255.f;
        const float rgb = batch.premultiplied ? opacity : 1.f;
        const auto& base = batch.baseColor;
        const Color4B tint(static_cast<GLubyte>(base.r * c.r / 255.f * rgb),
                           static_cast<GLubyte>(base.g * c.g / 255.f * rgb),
                           static_cast<GLubyte>(base.b * c.b / 255.f * rgb),
                           static_cast<GLubyte>(base.a * opacity));
        for (auto& vertex : batch.vertices)
            vertex.colors = tint;
        batch.program->setUniform(batch.program->getUniformLocation(backend::Uniform::MVP_MATRIX),
                                  projection.m, sizeof(projection.m));
        batch.program->setTexture(batch.program->getUniformLocation(backend::Uniform::TEXTURE), 0,
                                  batch.texture->getBackendTexture());
        TrianglesCommand::Triangles triangles(batch.vertices.data(), batch.indices.data(),
                                              static_cast<unsigned int>(batch.vertices.size()),
                                              static_cast<unsigned int>(batch.indices.size()));
        batch.command.init(getGlobalZOrder(), batch.texture, batch.blend, triangles, transform, flags);
        renderer->addCommand(&batch.command);
    }
}

std::string SpinePreview::diagnostic() const {
    std::ostringstream out;
    out << "animations=" << impl_->animations.size() << " batches=" << impl_->activeBatches
        << " animation=" << impl_->animation << " time=" << impl_->animationTime
        << " vertices=" << impl_->vertexCount << " signature=" << impl_->vertexSignature
        << " bounds=" << impl_->bounds.origin.x << "," << impl_->bounds.origin.y << ","
        << impl_->bounds.size.width << "," << impl_->bounds.size.height;
    return out.str();
}
