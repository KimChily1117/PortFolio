#pragma once
#include <array>
#include <cstdint>
#include <string>
#include <vector>
namespace aster {
constexpr int HeroCount = 4, UnitCount = 8, MaxLevel = 100, MaxStage = 50;
constexpr std::int64_t GoldLimit = 1000000000;
// Local reconstruction rules, not decoded server skill coefficients.
enum class SkillBehavior { HealParty, AreaAttack, SingleAttack, BarrierParty };
enum class TargetMode { Enemy, Ally, AllEnemies, AllAllies };
struct HeroDefinition {
    std::string name, role, skill;
    int hp = 400, attack = 70, defense = 12, speed = 100;
    SkillBehavior skillBehavior = SkillBehavior::AreaAttack;
};
struct Profile {
    std::int64_t gold = 300;
    int stage = 1, bestCleared = 0;
    std::array<int, HeroCount> levels{{1, 1, 1, 1}};
    int victories = 0;
};
struct Unit {
    int id = 0;
    bool enemy = false;
    int hp = 0, maxHp = 0, attack = 0, defense = 0, speed = 0;
    double gauge = 0;
    std::array<int, 3> cooldowns{};
    int shield = 0;
    bool alive() const {
        return hp > 0;
    }
};
enum class Phase { Fighting, Victory, Defeat };
enum class EventType { Attack, Skill, Heal, Shield, Victory, Defeat };
struct BattleEvent {
    EventType type;
    int actor = -1, target = -1, amount = 0;
    bool critical = false;
    int skillIndex = 0;
};
class Battle {
  public:
    void begin(const std::array<HeroDefinition, HeroCount>& definitions, const Profile& profile);
    // One automatic action; manual player selection freezes combat indefinitely.
    std::vector<BattleEvent> tick();
    std::vector<BattleEvent> useSkill(int skill, int target);
    bool canUseSkill(int skill) const;
    bool validTarget(int skill, int target) const;
    TargetMode targetMode(int skill) const;
    int defaultTarget(int skill) const;
    int activeActor() const {
        return active_;
    }
    bool awaitingPlayer() const {
        return phase_ == Phase::Fighting && active_ >= 0 && active_ < HeroCount && !autoSkills_;
    }
    void setAutoSkills(bool v) {
        autoSkills_ = v;
    }
    bool autoSkills() const {
        return autoSkills_;
    }
    const std::array<Unit, UnitCount>& units() const {
        return units_;
    }
    std::vector<int> turnOrder() const;
    Phase phase() const {
        return phase_;
    }
    int ticks() const {
        return turns_;
    }
    int stage() const {
        return stage_;
    }

  private:
    int targetFor(bool enemy) const;
    void selectNextActor();
    std::vector<BattleEvent> act(int skill, int target);
    void damage(int actor, int target, int multiplier, int skill, std::vector<BattleEvent>& events);
    std::array<Unit, UnitCount> units_{};
    std::array<SkillBehavior, HeroCount> skillBehaviors_{};
    Phase phase_ = Phase::Fighting;
    int stage_ = 1, turns_ = 0, active_ = -1;
    std::uint32_t rng_ = 1;
    bool autoSkills_ = false;
};
std::array<HeroDefinition, HeroCount> defaultHeroes();
int upgradeCost(int level);
bool upgrade(Profile& profile, int hero);
int stageReward(int stage);
bool settleBattle(Profile& profile, const Battle& battle, bool& alreadySettled);
bool validProfile(const Profile& profile);
bool canEnterStage(const Profile& profile, int stage);
bool selectStage(Profile& profile, int stage);
} // namespace aster
