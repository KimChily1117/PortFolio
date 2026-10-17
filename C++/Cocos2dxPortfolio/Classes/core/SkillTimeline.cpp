#include "SkillTimeline.h"
#include <algorithm>
#include <cmath>

namespace aster {
void SkillTimeline::reset(std::vector<SkillCue> cues,double duration) {
    cues_=std::move(cues);
    std::stable_sort(cues_.begin(),cues_.end(),[](const SkillCue&a,const SkillCue&b){return a.time<b.time;});
    duration_=std::max(0.0,duration);
    for(const auto& cue:cues_) duration_=std::max(duration_,cue.time+cue.duration);
    elapsed_=0;cursor_=0;stopped_=false;
}
std::vector<size_t> SkillTimeline::advance(double delta) {
    std::vector<size_t> due;
    if(stopped_ || !std::isfinite(delta) || delta<0) return due;
    elapsed_+=delta;
    while(cursor_<cues_.size() && cues_[cursor_].time<=elapsed_+0.000001) due.push_back(cues_[cursor_++].sourceIndex);
    return due;
}
void SkillTimeline::stop(){stopped_=true;cursor_=cues_.size();}
}
