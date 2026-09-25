#pragma once

#include "NavGridAsset.h"

#include <algorithm>
#include <cmath>
#include <vector>

namespace NavigationPaintAlgorithms
{
    inline std::vector<NavGridCoordinate> RasterizeLine(
        const NavGridCoordinate& from,
        const NavGridCoordinate& to)
    {
        std::vector<NavGridCoordinate> cells;
        int x0 = from.x;
        int z0 = from.z;
        const int dx = std::abs(to.x - x0);
        const int sx = x0 < to.x ? 1 : -1;
        const int dz = -std::abs(to.z - z0);
        const int sz = z0 < to.z ? 1 : -1;
        int error = dx + dz;
        for (;;)
        {
            cells.push_back({ x0, z0 });
            if (x0 == to.x && z0 == to.z) break;
            const int twice = error * 2;
            if (twice >= dz) { error += dz; x0 += sx; }
            if (twice <= dx) { error += dx; z0 += sz; }
        }
        return cells;
    }

    inline std::vector<NavGridCoordinate> EnumerateBrush(
        const NavGridCoordinate& center,
        int radius,
        bool circle,
        uint32_t width,
        uint32_t height)
    {
        radius = (std::max)(0, radius);
        std::vector<NavGridCoordinate> cells;
        cells.reserve(static_cast<size_t>((radius * 2 + 1) * (radius * 2 + 1)));
        for (int dz = -radius; dz <= radius; ++dz)
        {
            for (int dx = -radius; dx <= radius; ++dx)
            {
                if (circle && dx * dx + dz * dz > radius * radius)
                    continue;
                const int x = center.x + dx;
                const int z = center.z + dz;
                if (x >= 0 && z >= 0 && static_cast<uint32_t>(x) < width && static_cast<uint32_t>(z) < height)
                    cells.push_back({ x, z });
            }
        }
        return cells;
    }
}
