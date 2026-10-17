#pragma once
#include "cocos2d.h"
#include <functional>
#include <algorithm>
#include <string>

namespace presentation {
inline cocos2d::Color4F rgba(int r, int g, int b, int a = 255) {
    return cocos2d::Color4F(r / 255.f, g / 255.f, b / 255.f, a / 255.f);
}
inline std::string stageName(int stage) {
    return std::to_string((stage - 1) / 5 + 1) + "-" + std::to_string((stage - 1) % 5 + 1);
}
inline const char* worldName(int world) {
    static const char* names[] = {"잊힌 천문대", "안개의 정원", "달빛 도서관", "유리 사막",   "침묵의 성채",
                                  "별무리 협곡", "푸른 심연",   "황혼의 탑",   "새벽의 성역", "마지막 별"};
    return names[std::max(0, std::min(9, world))];
}
inline void panel(cocos2d::Node* parent, cocos2d::Rect r, cocos2d::Color4F fill,
                  cocos2d::Color4F border = rgba(66, 86, 99)) {
    auto d = cocos2d::DrawNode::create();
    parent->addChild(d);
    d->drawSolidRect(r.origin, r.origin + cocos2d::Vec2(r.size.width, r.size.height), fill);
    d->drawRect(r.origin, r.origin + cocos2d::Vec2(r.size.width, r.size.height), border);
}
inline cocos2d::Label* text(cocos2d::Node* parent, const std::string& str, float size, cocos2d::Vec2 pos,
                            cocos2d::Color3B color, bool left = false) {
    auto l = cocos2d::Label::createWithTTF(str, "fonts/NotoSansKR.otf", size);
    l->setPosition(pos);
    l->setTextColor(cocos2d::Color4B(color));
    if (left)
        l->setAnchorPoint(cocos2d::Vec2(0, .5f));
    parent->addChild(l);
    return l;
}
inline cocos2d::Node* button(cocos2d::Node* parent, const std::string& title, cocos2d::Rect r,
                             cocos2d::Color4F fill, std::function<void()> callback, float font = 17) {
    auto n = cocos2d::Node::create();
    n->setPosition(r.origin);
    n->setContentSize(r.size);
    parent->addChild(n);
    panel(n, cocos2d::Rect(cocos2d::Vec2::ZERO, r.size), fill);
    text(n, title, font, cocos2d::Vec2(r.size.width / 2, r.size.height / 2), cocos2d::Color3B(235, 235, 227));
    auto listener = cocos2d::EventListenerTouchOneByOne::create();
    listener->setSwallowTouches(true);
    listener->onTouchBegan = [n](cocos2d::Touch* t, cocos2d::Event*) {
        return n->isVisible() && cocos2d::Rect(cocos2d::Vec2::ZERO, n->getContentSize())
                                     .containsPoint(n->convertToNodeSpace(t->getLocation()));
    };
    listener->onTouchEnded = [n, callback](cocos2d::Touch* t, cocos2d::Event*) {
        if (cocos2d::Rect(cocos2d::Vec2::ZERO, n->getContentSize())
                .containsPoint(n->convertToNodeSpace(t->getLocation())))
            callback();
    };
    cocos2d::Director::getInstance()->getEventDispatcher()->addEventListenerWithSceneGraphPriority(listener,
                                                                                                   n);
    return n;
}
inline cocos2d::Sprite* portrait(cocos2d::Node* parent, int index, cocos2d::Vec2 position, float height) {
    auto s = cocos2d::Sprite::create("art/characters.png",
                                     cocos2d::Rect((index % 3) * 512, (index / 3) * 512, 512, 512));
    s->setPosition(position);
    s->setScale(height / 512);
    parent->addChild(s);
    return s;
}
class HeroAnimator {
  public:
    static cocos2d::SpriteFrame* frame(int hero, int column) {
        auto texture =
            cocos2d::Director::getInstance()->getTextureCache()->addImage("art/heroes-animation.png");
        float w = texture->getContentSize().width / 4.f, h = texture->getContentSize().height / 3.f;
        return cocos2d::SpriteFrame::createWithTexture(texture, cocos2d::Rect(column * w, hero * h, w, h));
    }
    static cocos2d::Sprite* create(cocos2d::Node* parent, int hero, cocos2d::Vec2 pos, float height) {
        auto s = cocos2d::Sprite::createWithSpriteFrame(frame(hero, 0));
        s->setPosition(pos);
        s->setScale(height / s->getContentSize().height);
        parent->addChild(s);
        idle(s, hero);
        return s;
    }
    static void idle(cocos2d::Sprite* sprite, int hero) {
        sprite->stopActionByTag(110);
        cocos2d::Vector<cocos2d::SpriteFrame*> frames;
        for (int col : {0, 1, 0, 1})
            frames.pushBack(frame(hero, col));
        auto action = cocos2d::RepeatForever::create(
            cocos2d::Animate::create(cocos2d::Animation::createWithSpriteFrames(frames, .34f)));
        action->setTag(110);
        sprite->runAction(action);
    }
    static void attack(cocos2d::Sprite* sprite, int hero, float speed) {
        sprite->stopActionByTag(110);
        cocos2d::Vector<cocos2d::SpriteFrame*> frames;
        for (int col : {1, 2, 2, 3, 0})
            frames.pushBack(frame(hero, col));
        auto action = cocos2d::Sequence::create(
            cocos2d::Animate::create(cocos2d::Animation::createWithSpriteFrames(frames, .09f / speed)),
            cocos2d::CallFunc::create([sprite, hero] { idle(sprite, hero); }), nullptr);
        action->setTag(110);
        sprite->runAction(action);
    }
};
} // namespace presentation
