#pragma once

// Shared by cast checks, targeting guides, packets and VFX. Keep WRange in
// sync with the authoritative AnnieSkillHandler on the server.
namespace AnnieSkillTuning
{
    inline constexpr float QRange = 8.f; // Preserve the existing actual Q cast distance.
    inline constexpr float QImpactScale = 1.4f;
    inline constexpr float WRange = 5.6f;
    inline constexpr float WConeAngleDegrees = 50.f;
    inline constexpr float WVisualScale = WRange / 4.f;
}
