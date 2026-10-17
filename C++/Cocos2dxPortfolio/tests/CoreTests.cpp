#include "core/GameModel.h"
#include <cstdlib>
#include <iostream>
using namespace aster;
int checks = 0;
void check(bool ok, const char* text) {
    ++checks;
    if (!ok) {
        std::cerr << "FAIL: " << text << '\n';
        std::exit(1);
    }
}
int main() {
    Profile p;
    check(validProfile(p), "new profile");
    check(!upgrade(p, -1) && !upgrade(p, HeroCount), "hero bounds");
    auto gold = p.gold;
    check(upgrade(p, 3) && p.levels[3] == 2 && p.gold == gold - upgradeCost(1), "fourth hero progression");
    p = Profile{};
    Battle a, b;
    a.begin(defaultHeroes(), p);
    check(a.units().size() == 8 && a.activeActor() == 3 && a.awaitingPlayer(),
          "four heroes and four opponents; fastest hero starts");
    auto before = a.units();
    for (int i = 0; i < 500; ++i)
        check(a.tick().empty(), "manual input never advances time");
    check(a.ticks() == 0 && a.units()[0].hp == before[0].hp && a.units()[0].gauge == before[0].gauge,
          "manual turn frozen");
    check(a.useSkill(3, 4).empty() && a.useSkill(0, 0).empty() && a.useSkill(0, 8).empty(),
          "invalid skill and side rejected");
    check(a.ticks() == 0, "invalid commands do not consume turn");
    auto barrierDefs = defaultHeroes();
    barrierDefs[3].skillBehavior = SkillBehavior::BarrierParty;
    a.begin(barrierDefs, p);
    auto barrier = a.useSkill(2, 0);
    check(barrier.size() == 4 && a.units()[3].cooldowns[2] == 4, "party S3 is one turn and owner cooldown");
    for (const auto& e : barrier)
        check(e.type == EventType::Shield && e.target < HeroCount, "barrier targets party");
    check(a.units()[3].shield > 0, "barrier applied");
    check(a.activeActor() == 2, "next CR actor");
    const int ownerCooldown = a.units()[3].cooldowns[2];
    a.useSkill(0, 4);
    check(a.units()[3].cooldowns[2] == ownerCooldown, "other actor does not tick cooldown");
    a.setAutoSkills(true);
    while (a.activeActor() != 3 && a.phase() == Phase::Fighting)
        a.tick();
    check(a.units()[3].cooldowns[2] == 3, "cooldown decrements on owner's next turn");
    check(!a.canUseSkill(2) && a.useSkill(2, 0).empty(), "cooldown blocks manual skill");
    a.begin(defaultHeroes(), p);
    b.begin(defaultHeroes(), p);
    a.setAutoSkills(true);
    b.setAutoSkills(true);
    int count = 0;
    for (int i = 0; i < 600 && a.phase() == Phase::Fighting; ++i) {
        auto x = a.tick(), y = b.tick();
        check(x.size() == y.size(), "deterministic event count");
        for (size_t j = 0; j < x.size(); ++j) {
            check(x[j].actor == y[j].actor && x[j].target == y[j].target && x[j].amount == y[j].amount &&
                      x[j].type == y[j].type && x[j].skillIndex == y[j].skillIndex,
                  "deterministic replay");
            ++count;
        }
        for (const auto& u : a.units())
            check(u.hp >= 0 && u.hp <= u.maxHp && u.gauge >= 0 && u.gauge <= 1000, "bounded units");
        auto order = a.turnOrder();
        if (a.phase() == Phase::Fighting)
            check(!order.empty() && order.front() == a.activeActor(), "readiness order active first");
    }
    check(a.phase() == Phase::Victory && count > 0, "starter party clears first stage");
    check(a.tick().empty() && a.useSkill(0, 4).empty(), "terminal battle no further action");
    bool settled = false;
    check(settleBattle(p, a, settled) && p.gold == 400 && p.stage == 2, "stage reward");
    check(!settleBattle(p, a, settled) && p.gold == 400, "reward once");
    p = Profile{};
    p.stage = 50;
    p.bestCleared = 49;
    a.begin(defaultHeroes(), p);
    while (a.phase() == Phase::Fighting)
        a.tick();
    check(a.phase() == Phase::Defeat, "underleveled high stage defeats");
    settled = false;
    check(!settleBattle(p, a, settled), "defeat no rewards");
    p.levels.fill(100);
    a.begin(defaultHeroes(), p);
    while (a.phase() == Phase::Fighting)
        a.tick();
    check(a.phase() == Phase::Victory, "max level can finish");
    settled = false;
    settleBattle(p, a, settled);
    check(p.stage == 50 && p.bestCleared == 50 && validProfile(p), "campaign cap");
    p = Profile{};
    check(!selectStage(p, 2) && selectStage(p, 1), "stage unlocks");
    p.bestCleared = 12;
    check(selectStage(p, 11) && canEnterStage(p, 13) && !canEnterStage(p, 14), "replay keeps unlocks");
    auto defs = defaultHeroes();
    defs[0].speed = 1000;
    defs[0].skillBehavior = SkillBehavior::HealParty;
    a.setAutoSkills(false);
    a.begin(defs, Profile{});
    check(a.targetMode(1) == TargetMode::Ally && a.validTarget(1, 2) && !a.validTarget(1, 4),
          "single ally heal targeting");
    auto healed = a.useSkill(1, 2);
    check(healed.size() == 1 && healed[0].type == EventType::Heal && healed[0].target == 2,
          "selected ally receives healing");
    std::cout << "PASS: " << checks
              << " checks (manual turns, CR, targeting, cooldown, replay, progression)\n";
}
