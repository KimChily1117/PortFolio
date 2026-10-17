#pragma once
#include <cstddef>
#include <vector>

namespace aster {
struct SkillCue { double time=0; double duration=0; size_t sourceIndex=0; };
// A deterministic clock over an already compiled source schedule. Source tree
// interpretation remains in the importer, with the original tree retained.
class SkillTimeline {
public:
    void reset(std::vector<SkillCue> cues, double duration);
    std::vector<size_t> advance(double delta);
    void stop();
    bool complete() const { return stopped_ || (cursor_==cues_.size() && elapsed_>=duration_); }
    double elapsed() const { return elapsed_; }
    double duration() const { return duration_; }
private:
    std::vector<SkillCue> cues_;
    size_t cursor_=0;
    double elapsed_=0, duration_=0;
    bool stopped_=false;
};
}
