#pragma once
#include "Protocol.pb.h"

// Absent field = legacy/unknown. Never infer Q from speed, animation, last
// skill, or arrival order: a basic attack can overlap a Q in flight.
inline bool UsesAnnieQEffect(int championType, const Protocol::S_ProjectileSpawn& packet)
{
    return championType == Protocol::PLAYER_TYPE_ANNIE && packet.has_skillid() && packet.skillid() == 1;
}
