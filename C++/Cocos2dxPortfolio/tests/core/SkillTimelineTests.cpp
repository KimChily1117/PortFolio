#include "core/SkillTimeline.h"
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <limits>
#include <vector>
static void require(bool result,const char* message){if(!result){std::cerr<<message<<'\n';std::exit(1);}}
int main(){
    aster::SkillTimeline t;
    t.reset({{.5,.5,2},{0,0,0},{.5,0,3},{.2,0,1}},.8);
    require(t.advance(0)==std::vector<size_t>{0},"time-zero cue fires immediately");
    require(t.advance(0).empty(),"cue is not repeated");
    require(t.advance(-1).empty()&&t.elapsed()==0,"negative delta rejected");
    require(t.advance(std::numeric_limits<double>::quiet_NaN()).empty()&&t.elapsed()==0,"nonfinite delta rejected");
    require(t.advance(.7)==std::vector<size_t>({1,2,3}),"large frame catches every crossed cue, equal-time source order preserved");
    require(!t.complete(),"active visual duration extends finish boundary");
    require(t.advance(.3).empty()&&t.complete(),"finishes at end of last visual");
    t.reset({{0,0,0},{1,0,1}},2);t.advance(0);t.stop();
    require(t.advance(10).empty(),"skip/stop prevents future cues");
    t.reset({{0,0,8}},0);require(t.advance(0)==std::vector<size_t>{8},"reset reuses stopped clock");
    std::cout<<"PASS: stable cues, catch-up, boundaries, invalid delta, skip and reset\n";
}
