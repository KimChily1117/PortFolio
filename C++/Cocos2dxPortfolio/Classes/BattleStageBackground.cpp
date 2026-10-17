#include "BattleStageBackground.h"
#include <algorithm>
USING_NS_CC;
BattleStageBackground* BattleStageBackground::create(int stage) {
    auto node = new BattleStageBackground;
    if (!node->init() || !node->build(stage)) {
        delete node;
        return nullptr;
    }
    node->autorelease();
    return node;
}
void BattleStageBackground::layer(const std::string& asset, Vec2 center, float width, float factor,
                                  bool flip) {
    auto path = "reference/epicseven/battleui/backgrounds/" + asset + ".png";
    if (!FileUtils::getInstance()->isFileExist(path))
        return;
    auto sprite = Sprite::create(path);
    if (!sprite)
        return;
    sprite->getTexture()->setAntiAliasTexParameters();
    sprite->setPosition(center);
    sprite->setScale(width / sprite->getContentSize().width);
    sprite->setFlippedX(flip);
    addChild(sprite);
    planes_.push_back({sprite, center, factor});
}
bool BattleStageBackground::build(int stage) {
    setContentSize(Size(1280, 720));
    int theme = (std::max(1, stage) - 1) / 5 % 3;
    if (theme == 0) {
        theme_ = "forest";
        addChild(LayerColor::create(Color4B(97, 129, 104, 255), 1280, 720));
        layer("forest/forest2", Vec2(640, 449), 1400, .08f);
        layer("forest/forest4", Vec2(640, 346), 1400, .16f);
        layer("forest/forest12", Vec2(110, 452), 115, .32f);
        layer("forest/forest12", Vec2(1135, 452), 115, .32f, true);
        layer("forest/forest0", Vec2(640, 147), 1400, .5f);
        layer("forest/forest13_6", Vec2(130, 660), 420, .65f);
        layer("forest/forest13_6", Vec2(1150, 660), 420, .65f, true);
    } else if (theme == 1) {
        theme_ = "battle";
        addChild(LayerColor::create(Color4B(56, 90, 117, 255), 1280, 720));
        layer("battle/battle_19", Vec2(640, 641), 1400, .05f);
        layer("battle/battle_16", Vec2(640, 424), 1400, .18f);
        layer("battle/battle_11", Vec2(65, 411), 220, .32f);
        layer("battle/battle_12", Vec2(1215, 411), 220, .32f);
        layer("battle/battle_0", Vec2(640, 160), 1450, .5f);
    } else {
        theme_ = "nworld1";
        addChild(LayerColor::create(Color4B(48, 48, 62, 255), 1280, 720));
        layer("nworld1/nworld1_13", Vec2(640, 500), 1400, .08f);
        layer("nworld1/nworld1_5", Vec2(140, 402), 410, .25f);
        layer("nworld1/nworld1_6", Vec2(1130, 402), 430, .25f);
        layer("nworld1/nworld1_1", Vec2(640, 342), 1400, .36f);
        layer("nworld1/nworld1_0", Vec2(640, 155), 1450, .5f);
    }
    return !planes_.empty();
}
void BattleStageBackground::setCameraOffset(Vec2 offset) {
    for (auto& plane : planes_)
        plane.sprite->setPosition(plane.base + offset * plane.parallax);
}
