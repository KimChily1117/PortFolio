#include "pch.h"
#include "Converter.h"
#include <array>
#include <fstream>
#include <filesystem>

// SCB 3.2 binary layout reference:
// https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMesh.cs
// https://github.com/LeagueToolkit/LeagueToolkit/blob/main/src/LeagueToolkit/Core/Mesh/StaticMeshFace.cs
// Assimp itself does not import SCB. This adapter belongs to the offline tool;
// the existing FBX/animation conversion path is unchanged.
bool Converter::ExportScbVfxMesh(const wstring& source, const wstring& destination, bool keepAxes)
{
    std::ifstream input(std::filesystem::path(source), std::ios::binary);
    input.seekg(0, std::ios::end);
    const auto length = input.tellg();
    if (length < 192 || length > 64 * 1024 * 1024)
        return false;
    input.seekg(0);
    vector<char> bytes(static_cast<size_t>(length));
    input.read(bytes.data(), bytes.size());
    if (!input || memcmp(bytes.data(), "r3d2Mesh", 8) != 0)
        return false;
    auto read32 = [&](size_t offset) { uint32 value; memcpy(&value, bytes.data() + offset, 4); return value; };
    if (read32(8) != 0x00020003) // major 3, minor 2
        return false;
    const uint32 count = read32(140), faces = read32(144), flags = read32(148), colors = read32(176);
    if (!count || count > 65536 || !faces || faces > 65536 || flags & ~3u || colors > 1)
        return false;
    const size_t faceOffset = 180 + count * (12 + colors * 4) + 12;
    if (bytes.size() != faceOffset + faces * 100 + ((flags & 1) ? faces * 9 : 0))
        return false;
    vector<VertexTextureNormalTangentData> vertices;
    vector<uint32> indices;
    map<std::array<float, 9>, uint32> unique;
    for (uint32 face = 0; face < faces; ++face)
    {
        const size_t offset = faceOffset + face * 100;
        float uv[6];
        memcpy(uv, bytes.data() + offset + 76, sizeof(uv));
        for (uint32 corner = 0; corner < 3; ++corner)
        {
            const uint32 index = read32(offset + corner * 4);
            if (index >= count)
                return false;
            Vec3 sourcePosition;
            memcpy(&sourcePosition, bytes.data() + 180 + index * 12, sizeof(sourcePosition));
            // Make the projectile's longitudinal axis +Z, tail -Z. The effect
            // applies its world scale; importing Annie's FBX scale would be wrong.
            Vec4 color(1.f);
            if (colors)
            {
                const auto* bgra = reinterpret_cast<const unsigned char*>(bytes.data() + 180 + count * 12 + index * 4);
                color = Vec4(bgra[2] / 255.f, bgra[1] / 255.f, bgra[0] / 255.f, bgra[3] / 255.f);
            }
            // W is already a ground-plane mesh. Preserve its axes and edge
            // alpha; welding must retain color discontinuities as well as UVs.
            const std::array<float, 9> key{ sourcePosition.x,
                keepAxes ? sourcePosition.y : sourcePosition.z,
                keepAxes ? sourcePosition.z : sourcePosition.y,
                uv[corner], uv[corner + 3], color.x, color.y, color.z, color.w };
            for (float value : key)
                if (!std::isfinite(value))
                    return false;
            auto found = unique.find(key);
            if (found == unique.end())
            {
                VertexTextureNormalTangentData vertex;
                vertex.position = Vec3(key[0], key[1], key[2]);
                vertex.uv = Vec2(key[3], key[4]);
                vertex.normal = Vec3(color.x, color.y, color.z);
                vertex.tangent = Vec3(color.w, 0.f, 0.f); // alpha, erosion, reserved
                const uint32 next = static_cast<uint32>(vertices.size());
                unique.emplace(key, next);
                vertices.push_back(vertex);
                indices.push_back(next);
            }
            else
                indices.push_back(found->second);
        }
    }
    const auto outputPath = std::filesystem::path(destination);
    std::filesystem::create_directories(outputPath.parent_path());
    std::ofstream output(outputPath, std::ios::binary | std::ios::trunc);
    const uint32 header[] = { 0x4d584656, 1, sizeof(VertexTextureNormalTangentData),
        static_cast<uint32>(vertices.size()), static_cast<uint32>(indices.size()) };
    output.write(reinterpret_cast<const char*>(header), sizeof(header));
    output.write(reinterpret_cast<const char*>(vertices.data()), vertices.size() * sizeof(vertices[0]));
    output.write(reinterpret_cast<const char*>(indices.data()), indices.size() * sizeof(indices[0]));
    output.flush();
    return output.good();
}
