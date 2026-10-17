#include "core/GameModel.h"
#include <algorithm>
#include <cmath>
#include <limits>
namespace aster {
std::array<HeroDefinition, HeroCount> defaultHeroes() {
    return {{{"Achates", "HEALER", "S3", 510, 58, 18, 102, SkillBehavior::HealParty},
             {"Aramintha", "MAGE", "S3", 405, 88, 10, 108, SkillBehavior::AreaAttack},
             {"Clarissa", "WARRIOR", "S3", 465, 80, 15, 111, SkillBehavior::AreaAttack},
             {"Dominiel", "MAGE", "S3", 420, 72, 12, 115, SkillBehavior::AreaAttack}}};
}
int upgradeCost(int level) {
    return level < 1 || level >= MaxLevel ? 0 : 60 + level * 25 + level * level * 3;
}
bool upgrade(Profile& p, int hero) {
    if (hero < 0 || hero >= HeroCount)
        return false;
    int cost = upgradeCost(p.levels[hero]);
    if (!cost || p.gold < cost)
        return false;
    p.gold -= cost;
    ++p.levels[hero];
    return true;
}
int stageReward(int stage) {
    return 75 + std::clamp(stage, 1, MaxStage) * 25;
}
bool settleBattle(Profile& p, const Battle& b, bool& settled) {
    if (settled || b.phase() != Phase::Victory)
        return false;
    settled = true;
    p.gold = std::min(GoldLimit, p.gold + stageReward(b.stage()));
    p.bestCleared = std::max(p.bestCleared, b.stage());
    p.stage = std::min(MaxStage, b.stage() + 1);
    p.victories = std::min(100000000, p.victories + 1);
    return true;
}
bool validProfile(const Profile& p) {
    if (p.gold < 0 || p.gold > GoldLimit || p.stage < 1 || p.stage > MaxStage || p.bestCleared < 0 ||
        p.bestCleared > MaxStage || p.stage > p.bestCleared + 1 || p.victories < 0 || p.victories > 100000000)
        return false;
    for (int level : p.levels)
        if (level < 1 || level > MaxLevel)
            return false;
    return true;
}
bool canEnterStage(const Profile& p, int stage) {
    return stage >= 1 && stage <= MaxStage && stage <= p.bestCleared + 1;
}
bool selectStage(Profile& p, int stage) {
    if (!canEnterStage(p, stage))
        return false;
    p.stage = stage;
    return true;
}
void Battle::begin(const std::array<HeroDefinition, HeroCount>& defs, const Profile& p) {
    phase_ = Phase::Fighting;
    turns_ = 0;
    active_ = -1;
    stage_ = std::clamp(p.stage, 1, MaxStage);
    rng_ = 0xA57E1234u + stage_;
    for (int i = 0; i < HeroCount; ++i)
        skillBehaviors_[i] = defs[i].skillBehavior;
    for (int i = 0; i < UnitCount; ++i) {
        auto& u = units_[i];
        u = Unit{};
        u.id = i;
        u.enemy = i >= HeroCount;
        if (!u.enemy) {
            int level = std::clamp(p.levels[i], 1, MaxLevel);
            const auto& d = defs[i];
            u.maxHp = std::clamp(d.hp, 1, 10000) + (level - 1) * 48;
            u.attack = std::clamp(d.attack, 1, 10000) + (level - 1) * 9;
            u.defense = std::clamp(d.defense, 0, 10000) + (level - 1) * 3;
            u.speed = std::clamp(d.speed, 1, 1000);
        } else {
            u.maxHp = 350 + stage_ * 55 + (i - HeroCount) * 25;
            u.attack = 30 + stage_ * 8;
            u.defense = 4 + stage_ * 3;
            u.speed = 80 + (i - HeroCount) * 7;
            if (stage_ % 5 == 0 && i == UnitCount - 1) {
                u.maxHp *= 2;
                u.attack += 12;
            }
        }
        u.hp = u.maxHp;
    }
    selectNextActor();
}
void Battle::selectNextActor() {
    double elapsed = std::numeric_limits<double>::max();
    active_ = -1;
    for (const auto& u : units_)
        if (u.alive()) {
            double time = std::max(0.0, 1000.0 - u.gauge) / u.speed;
            if (time < elapsed - 1e-9) {
                elapsed = time;
                active_ = u.id;
            }
        }
    if (active_ < 0)
        return;
    for (auto& u : units_)
        if (u.alive())
            u.gauge = std::min(1000.0, u.gauge + elapsed * u.speed);
    units_[active_].gauge = 1000;
    for (int& cooldown : units_[active_].cooldowns)
        cooldown = std::max(0, cooldown - 1);
}
int Battle::targetFor(bool enemy) const {
    int begin = enemy ? 0 : HeroCount;
    for (int i = begin; i < begin + HeroCount; ++i)
        if (units_[i].alive())
            return i;
    return -1;
}
TargetMode Battle::targetMode(int skill) const {
    if (active_ < 0 || active_ >= HeroCount)
        return TargetMode::Enemy;
    auto behavior = skillBehaviors_[active_];
    if (skill == 2) {
        if (behavior == SkillBehavior::HealParty || behavior == SkillBehavior::BarrierParty)
            return TargetMode::AllAllies;
        if (behavior == SkillBehavior::AreaAttack)
            return TargetMode::AllEnemies;
    }
    if (skill == 1 && behavior == SkillBehavior::HealParty)
        return TargetMode::Ally;
    return TargetMode::Enemy;
}
bool Battle::canUseSkill(int skill) const {
    return phase_ == Phase::Fighting && active_ >= 0 && active_ < HeroCount && skill >= 0 && skill < 3 &&
           units_[active_].alive() && units_[active_].cooldowns[skill] == 0;
}
bool Battle::validTarget(int skill, int target) const {
    if (!canUseSkill(skill) || target < 0 || target >= UnitCount || !units_[target].alive())
        return false;
    auto mode = targetMode(skill);
    bool ally = mode == TargetMode::Ally || mode == TargetMode::AllAllies;
    return units_[target].enemy != ally;
}
int Battle::defaultTarget(int skill) const {
    if (active_ < 0)
        return -1;
    auto mode = targetMode(skill);
    if (mode != TargetMode::Ally && mode != TargetMode::AllAllies)
        return targetFor(units_[active_].enemy);
    int lowest = -1;
    for (int i = 0; i < HeroCount; ++i)
        if (units_[i].alive() && (lowest < 0 || double(units_[i].hp) / units_[i].maxHp <
                                                    double(units_[lowest].hp) / units_[lowest].maxHp))
            lowest = i;
    return lowest;
}
void Battle::damage(int actor, int target, int multiplier, int skill, std::vector<BattleEvent>& events) {
    auto& a = units_[actor];
    auto& t = units_[target];
    rng_ ^= rng_ << 13;
    rng_ ^= rng_ >> 17;
    rng_ ^= rng_ << 5;
    bool crit = !a.enemy && rng_ % 100 < 18;
    int amount = std::max(1, a.attack * multiplier / 100 - t.defense);
    if (crit)
        amount = amount * 3 / 2;
    int blocked = std::min(t.shield, amount);
    t.shield -= blocked;
    amount = std::min(t.hp, amount - blocked);
    t.hp -= amount;
    events.push_back({skill == 0 ? EventType::Attack : EventType::Skill, actor, target, amount, crit, skill});
}
std::vector<BattleEvent> Battle::act(int skill, int target) {
    std::vector<BattleEvent> events;
    int actor = active_;
    auto& a = units_[actor];
    auto mode = targetMode(skill);
    if (!a.enemy && (mode == TargetMode::Ally || mode == TargetMode::AllAllies)) {
        bool barrier = skill == 2 && skillBehaviors_[actor] == SkillBehavior::BarrierParty;
        for (int i = 0; i < HeroCount; ++i)
            if (units_[i].alive() && (mode == TargetMode::AllAllies || i == target)) {
                auto& ally = units_[i];
                int amount = barrier
                                 ? ally.maxHp / 4
                                 : std::min(ally.maxHp - ally.hp, ally.maxHp * (skill == 2 ? 35 : 25) / 100);
                if (barrier)
                    ally.shield = std::max(ally.shield, amount);
                else
                    ally.hp += amount;
                events.push_back(
                    {barrier ? EventType::Shield : EventType::Heal, actor, i, amount, false, skill});
            }
    } else if (!a.enemy && mode == TargetMode::AllEnemies) {
        for (int i = HeroCount; i < UnitCount; ++i)
            if (units_[i].alive())
                damage(actor, i, 165, skill, events);
    } else
        damage(actor, target, skill == 2 ? 230 : skill == 1 ? 145 : 100, skill, events);
    if (skill > 0)
        a.cooldowns[skill] = skill == 2 ? 4 : 3;
    a.gauge = 0;
    ++turns_;
    if (targetFor(false) < 0) {
        phase_ = Phase::Victory;
        events.push_back({EventType::Victory});
    } else if (targetFor(true) < 0 || turns_ >= 600) {
        phase_ = Phase::Defeat;
        events.push_back({EventType::Defeat});
    }
    if (phase_ == Phase::Fighting)
        selectNextActor();
    else
        active_ = -1;
    return events;
}
std::vector<BattleEvent> Battle::useSkill(int skill, int target) {
    return validTarget(skill, target) ? act(skill, target) : std::vector<BattleEvent>{};
}
std::vector<BattleEvent> Battle::tick() {
    if (phase_ != Phase::Fighting || active_ < 0 || awaitingPlayer())
        return {};
    int skill = 0;
    if (active_ < HeroCount)
        for (int candidate : {2, 1})
            if (canUseSkill(candidate)) {
                skill = candidate;
                break;
            }
    return act(skill, defaultTarget(skill));
}
std::vector<int> Battle::turnOrder() const {
    std::vector<int> result;
    for (const auto& u : units_)
        if (u.alive())
            result.push_back(u.id);
    std::stable_sort(result.begin(), result.end(), [&](int a, int b) {
        if (a == active_ || b == active_)
            return a == active_ && b != active_;
        const auto& x = units_[a];
        const auto& y = units_[b];
        return (1000 - x.gauge) / x.speed < (1000 - y.gauge) / y.speed;
    });
    return result;
}
} // namespace aster
