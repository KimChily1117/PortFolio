#include "NavGridAsset.h"
#include "NavGridAssetWriter.h"
#include "../../../GameCoding2/NavigationPaintAlgorithms.h"

#include <bit>
#include <chrono>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace
{
	int gAssertions = 0;

	void Check(bool condition, const std::string& message)
	{
		++gAssertions;
		if (!condition)
			throw std::runtime_error(message);
	}

	std::vector<uint8_t> ReadBytes(const std::filesystem::path& path)
	{
		std::ifstream input(path, std::ios::binary | std::ios::ate);
		Check(static_cast<bool>(input), "Cannot open fixture: " + path.string());
		const auto size = input.tellg();
		std::vector<uint8_t> bytes(static_cast<size_t>(size));
		input.seekg(0);
		if (!bytes.empty())
			input.read(reinterpret_cast<char*>(bytes.data()), size);
		Check(static_cast<bool>(input), "Cannot read fixture: " + path.string());
		return bytes;
	}

	void WriteBytes(const std::filesystem::path& path, const std::vector<uint8_t>& bytes)
	{
		std::filesystem::create_directories(path.parent_path());
		std::ofstream output(path, std::ios::binary | std::ios::trunc);
		Check(static_cast<bool>(output), "Cannot create mutation fixture.");
		if (!bytes.empty())
			output.write(reinterpret_cast<const char*>(bytes.data()), bytes.size());
		Check(static_cast<bool>(output), "Cannot write mutation fixture.");
	}

	void PutU32(std::vector<uint8_t>& bytes, size_t offset, uint32_t value)
	{
		Check(offset + 4 <= bytes.size(), "Mutation offset is outside fixture.");
		bytes[offset] = static_cast<uint8_t>(value);
		bytes[offset + 1] = static_cast<uint8_t>(value >> 8);
		bytes[offset + 2] = static_cast<uint8_t>(value >> 16);
		bytes[offset + 3] = static_cast<uint8_t>(value >> 24);
	}

	void PutF32(std::vector<uint8_t>& bytes, size_t offset, float value)
	{
		PutU32(bytes, offset, std::bit_cast<uint32_t>(value));
	}

	void ExpectRejected(
		const std::filesystem::path& directory,
		const std::string& name,
		std::vector<uint8_t> bytes,
		NavGridLoadError expected)
	{
		const auto path = directory / (name + ".navgrid");
		WriteBytes(path, bytes);
		NavGridAsset asset;
		const NavGridLoadStatus status = NavGridAssetLoader::Load(path.wstring(), asset);
		Check(!status, name + " unexpectedly loaded.");
		Check(status.error == expected, name + " returned the wrong error.");
	}

	std::vector<std::string> Split(const std::string& value, char separator)
	{
		std::vector<std::string> fields;
		std::stringstream stream(value);
		std::string field;
		while (std::getline(stream, field, separator))
			fields.push_back(field);
		return fields;
	}

	float ParseFloat(const std::string& value)
	{
		if (value == "NaN")
			return std::numeric_limits<float>::quiet_NaN();
		if (value == "Infinity")
			return std::numeric_limits<float>::infinity();
		if (value == "-Infinity")
			return -std::numeric_limits<float>::infinity();
		return std::stof(value);
	}

	std::string CoordinateText(const std::optional<NavGridCoordinate>& cell)
	{
		if (!cell)
			return "invalid";
		return std::to_string(cell->x) + ":" + std::to_string(cell->z);
	}

	std::string CenterText(const Vec3& center)
	{
		std::ostringstream stream;
		stream.setf(std::ios::fixed);
		stream.precision(6);
		stream << center.x << "," << center.y << "," << center.z;
		return stream.str();
	}

	std::string JsonEscape(const std::string& value)
	{
		std::string escaped;
		for (char character : value)
		{
			if (character == '\\' || character == '"')
				escaped.push_back('\\');
			escaped.push_back(character);
		}
		return escaped;
	}
}

int wmain(int argc, wchar_t* argv[])
{
	try
	{
		if (argc != 4)
		{
			std::cerr << "Usage: NavGridCppTests <golden.navgrid> <coordinate-vectors.csv> <result.json>\n";
			return 2;
		}

		const std::filesystem::path goldenPath = argv[1];
		const std::filesystem::path vectorsPath = argv[2];
		const std::filesystem::path resultPath = argv[3];
		const std::filesystem::path mutationDirectory = resultPath.parent_path() / "mutations";

		NavGridAsset asset;
		const NavGridLoadStatus loadStatus =
			NavGridAssetLoader::Load(goldenPath.wstring(), asset);
		Check(static_cast<bool>(loadStatus), "Golden asset load failed: " + loadStatus.message);
		Check(asset.GetFormatVersion() == 1, "FormatVersion mismatch.");
		Check(asset.GetMapId() == "golden-grid-v1", "MapId mismatch.");
		Check(asset.GetWidth() == 4 && asset.GetHeight() == 3, "Dimensions mismatch.");
		Check(asset.GetCellSize() == 1.0f, "CellSize mismatch.");
		Check(asset.GetOrigin() == Vec3(-2.0f, 0.0f, -1.0f), "Origin mismatch.");
		Check(asset.GetDefaultAgentRadius() == 0.5f, "Agent radius mismatch.");
		Check(asset.GetContentHashHex() ==
			"d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77",
			"Golden content hash mismatch.");

		const uint8_t expectedCells[12] = { 0, 0, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0 };
		std::vector<uint8_t> cells;
		for (int32_t z = 0; z < 3; ++z)
		{
			for (int32_t x = 0; x < 4; ++x)
			{
				const uint8_t value = static_cast<uint8_t>(asset.GetCell(x, z));
				Check(value == expectedCells[z * 4 + x], "Cell value mismatch.");
				cells.push_back(value);
			}
		}

		std::array<uint8_t, 32> cellHash{};
		std::string hashError;
		Check(NavGridHash::ComputeSha256(cells.data(), cells.size(), cellHash, hashError),
			"Cell array SHA-256 failed: " + hashError);

		std::ifstream vectorInput(vectorsPath);
		Check(static_cast<bool>(vectorInput), "Cannot open coordinate vectors.");
		std::string line;
		std::getline(vectorInput, line);
		std::vector<std::pair<std::string, std::string>> coordinateResults;
		while (std::getline(vectorInput, line))
		{
			if (line.empty())
				continue;
			const auto fields = Split(line, ',');
			Check(fields.size() == 5, "Coordinate vector row must have five columns.");
			const Vec3 world(ParseFloat(fields[1]), ParseFloat(fields[2]), ParseFloat(fields[3]));
			const std::string actual = CoordinateText(asset.WorldToCell(world));
			Check(actual == fields[4], "Coordinate mismatch for " + fields[0] + ".");
			coordinateResults.emplace_back(fields[0], actual);
		}

		const auto center00 = asset.CellToWorldCenter({ 0, 0 });
		const auto center32 = asset.CellToWorldCenter({ 3, 2 });
		Check(center00 && *center00 == Vec3(-1.5f, 0.0f, -0.5f), "Cell 0:0 center mismatch.");
		Check(center32 && *center32 == Vec3(1.5f, 0.0f, 1.5f), "Cell 3:2 center mismatch.");
		Check(!asset.CellToWorldCenter({ -1, 0 }), "Invalid cell center must be rejected.");

		const auto goldenBytes = ReadBytes(goldenPath);
		constexpr size_t widthOffset = 30;
		constexpr size_t heightOffset = 34;
		constexpr size_t cellSizeOffset = 38;
		constexpr size_t originXOffset = 42;
		constexpr size_t agentRadiusOffset = 54;
		constexpr size_t encodingOffset = 58;
		constexpr size_t dataLengthOffset = 62;
		constexpr size_t hashOffset = 66;
		constexpr size_t dataOffset = 98;

		auto mutated = goldenBytes;
		mutated[0] = 'X';
		ExpectRejected(mutationDirectory, "bad_magic", mutated, NavGridLoadError::InvalidMagic);
		mutated = goldenBytes; PutU32(mutated, 4, 2);
		ExpectRejected(mutationDirectory, "unsupported_version", mutated, NavGridLoadError::UnsupportedVersion);
		mutated = goldenBytes; PutU32(mutated, 8, 99);
		ExpectRejected(mutationDirectory, "bad_header_size", mutated, NavGridLoadError::InvalidHeaderSize);
		mutated = goldenBytes; PutU32(mutated, 12, 129);
		ExpectRejected(mutationDirectory, "map_id_too_long", mutated, NavGridLoadError::MapIdTooLong);
		mutated = goldenBytes; PutU32(mutated, widthOffset, 0);
		ExpectRejected(mutationDirectory, "width_zero", mutated, NavGridLoadError::InvalidDimensions);
		mutated = goldenBytes; PutU32(mutated, heightOffset, 0);
		ExpectRejected(mutationDirectory, "height_zero", mutated, NavGridLoadError::InvalidDimensions);
		mutated = goldenBytes; PutU32(mutated, widthOffset, 4096); PutU32(mutated, heightOffset, 4096);
		ExpectRejected(mutationDirectory, "cell_count_too_large", mutated, NavGridLoadError::CellCountTooLarge);
		mutated = goldenBytes; PutU32(mutated, widthOffset, 0xffffffffu);
		ExpectRejected(mutationDirectory, "dimension_overflow", mutated, NavGridLoadError::InvalidDimensions);
		mutated = goldenBytes; PutF32(mutated, cellSizeOffset, 0.0f);
		ExpectRejected(mutationDirectory, "cell_size_zero", mutated, NavGridLoadError::InvalidCellSize);
		mutated = goldenBytes; PutF32(mutated, cellSizeOffset, -1.0f);
		ExpectRejected(mutationDirectory, "cell_size_negative", mutated, NavGridLoadError::InvalidCellSize);
		mutated = goldenBytes; mutated[16] = 0xff;
		ExpectRejected(mutationDirectory, "invalid_utf8_map_id", mutated, NavGridLoadError::InvalidUtf8MapId);
		mutated = goldenBytes; PutF32(mutated, cellSizeOffset, std::numeric_limits<float>::quiet_NaN());
		ExpectRejected(mutationDirectory, "cell_size_nan", mutated, NavGridLoadError::InvalidCellSize);
		mutated = goldenBytes; PutF32(mutated, cellSizeOffset, std::numeric_limits<float>::infinity());
		ExpectRejected(mutationDirectory, "cell_size_infinity", mutated, NavGridLoadError::InvalidCellSize);
		mutated = goldenBytes; PutF32(mutated, originXOffset, std::numeric_limits<float>::infinity());
		ExpectRejected(mutationDirectory, "origin_infinity", mutated, NavGridLoadError::InvalidOrigin);
		mutated = goldenBytes; PutF32(mutated, agentRadiusOffset, -0.5f);
		ExpectRejected(mutationDirectory, "agent_radius_negative", mutated, NavGridLoadError::InvalidAgentRadius);
		mutated = goldenBytes; PutU32(mutated, encodingOffset, 99);
		ExpectRejected(mutationDirectory, "unknown_encoding", mutated, NavGridLoadError::UnsupportedCellEncoding);
		mutated.assign(goldenBytes.begin(), goldenBytes.begin() + 20);
		ExpectRejected(mutationDirectory, "truncated_header", mutated, NavGridLoadError::TruncatedHeader);
		mutated.assign(goldenBytes.begin(), goldenBytes.end() - 1);
		ExpectRejected(mutationDirectory, "truncated_cell_data", mutated, NavGridLoadError::TruncatedCellData);
		mutated = goldenBytes; PutU32(mutated, dataLengthOffset, 0xffffffffu);
		ExpectRejected(mutationDirectory, "excessive_cell_data_length", mutated, NavGridLoadError::InvalidCellDataLength);
		mutated = goldenBytes; PutU32(mutated, dataLengthOffset, 11);
		ExpectRejected(mutationDirectory, "cell_data_length_mismatch", mutated, NavGridLoadError::InvalidCellDataLength);
		mutated = goldenBytes; mutated[dataOffset] = 3;
		ExpectRejected(mutationDirectory, "unknown_cell", mutated, NavGridLoadError::UnknownCellValue);
		mutated = goldenBytes; mutated[hashOffset] ^= 0x80;
		ExpectRejected(mutationDirectory, "hash_tamper", mutated, NavGridLoadError::HashMismatch);
		mutated = goldenBytes; mutated.push_back(0);
		ExpectRejected(mutationDirectory, "trailing_data", mutated, NavGridLoadError::TrailingData);

		const auto fastStroke = NavigationPaintAlgorithms::RasterizeLine({ 0, 0 }, { 5, 2 });
		Check(fastStroke.front() == NavGridCoordinate{ 0, 0 }, "Stroke must include its start Cell.");
		Check(fastStroke.back() == NavGridCoordinate{ 5, 2 }, "Stroke must include its end Cell.");
		for (size_t i = 1; i < fastStroke.size(); ++i)
		{
			Check(std::abs(fastStroke[i].x - fastStroke[i - 1].x) <= 1 &&
				std::abs(fastStroke[i].z - fastStroke[i - 1].z) <= 1,
				"Fast Drag rasterization skipped a Cell adjacency.");
		}
		Check(NavigationPaintAlgorithms::EnumerateBrush({ 2, 2 }, 1, false, 5, 5).size() == 9,
			"Square radius-1 Brush must contain 9 Cells.");
		Check(NavigationPaintAlgorithms::EnumerateBrush({ 2, 2 }, 1, true, 5, 5).size() == 5,
			"Circle radius-1 Brush must contain 5 Cells.");
		Check(NavigationPaintAlgorithms::EnumerateBrush({ 0, 0 }, 1, false, 5, 5).size() == 4,
			"Boundary-clipped Square Brush must contain 4 Cells.");
		EditableNavGridData editable = EditableNavGridData::FromAsset(asset);
		Check(editable.GetMapId() == asset.GetMapId(), "Editable copy MapId mismatch.");
		Check(editable.GetCells().size() == 12, "Editable copy cell count mismatch.");
		Check(editable.SetCell(0, 0, NavCellType::Slow), "Editable SetCell failed.");
		Check(editable.GetCell(0, 0) == NavCellType::Slow, "Editable SetCell value mismatch.");
		Check(!editable.SetCell(-1, 0, NavCellType::Blocked), "Editable out-of-bounds write must fail.");
		Check(editable.SetCell(0, 0, NavCellType::Walkable), "Editable restore failed.");

		const auto roundTripPath = resultPath.parent_path() / "golden-writer-roundtrip.navgrid";
		std::array<uint8_t, 32> writerHash{};
		std::string writerError;
		Check(NavGridAssetWriter::WriteAtomic(roundTripPath.wstring(), editable, writerHash, writerError),
			"Writer round-trip failed: " + writerError);
		Check(NavGridHash::ToHex(writerHash) == asset.GetContentHashHex(), "Writer hash mismatch.");
		Check(ReadBytes(roundTripPath) == goldenBytes, "Writer output is not byte-identical to golden V1.");
		NavGridAsset roundTrip;
		Check(static_cast<bool>(NavGridAssetLoader::Load(roundTripPath.wstring(), roundTrip)),
			"Writer output failed Loader verification.");
		Check(roundTrip.GetContentHashHex() == asset.GetContentHashHex(), "Round-trip Loader hash mismatch.");

		const auto preservedBytes = ReadBytes(roundTripPath);
		EditableNavGridData invalidWriterData = editable;
		invalidWriterData.SetMapId(std::string(129, 'x'));
		Check(!NavGridAssetWriter::WriteAtomic(roundTripPath.wstring(), invalidWriterData, writerHash, writerError),
			"Writer must reject an oversized MapId.");
		Check(ReadBytes(roundTripPath) == preservedBytes, "Failed save changed the existing asset.");

		EditableNavGridData created;
		Check(!EditableNavGridData::Create("bad", 0, 3, 1.0f, Vec3(0.0f, 0.0f, 0.0f), 0.5f, created, writerError),
			"Create must reject Width 0.");
		Check(!EditableNavGridData::Create("bad", 3, 3, 0.0f, Vec3(0.0f, 0.0f, 0.0f), 0.5f, created, writerError),
			"Create must reject CellSize 0.");
		Check(EditableNavGridData::Create("room-0-nav-v1", 145, 145, 1.0f, Vec3(0.0f, 0.0f, 0.0f), 0.5f, created, writerError),
			"Cannot create Room 0 editor fixture: " + writerError);
		Check(created.GetCells().size() == 21025, "Room 0 fixture cell count mismatch.");
		Check(std::all_of(created.GetCells().begin(), created.GetCells().end(),
			[](uint8_t value) { return value == 0; }), "New grid must be all Walkable.");

		for (int z = 60; z <= 84; ++z)
			if (z != 72) Check(created.SetCell(70, z, NavCellType::Blocked), "Room 0 wall paint failed.");
		for (int z = 67; z <= 76; ++z)
			for (int x = 62; x <= 66; ++x)
				Check(created.SetCell(x, z, NavCellType::Slow), "Room 0 Slow paint failed.");
		Check(created.GetCell(70, 72) == NavCellType::Walkable, "Room 0 wall gap must remain Walkable.");
		Check(created.GetCell(70, 71) == NavCellType::Blocked, "Room 0 wall must contain Blocked cells.");
		Check(created.GetCell(64, 70) == NavCellType::Slow, "Room 0 Slow region mismatch.");
		Check(created.GetCell(6, 3) == NavCellType::Walkable, "Known spawn area was blocked.");

		const auto room0Path = resultPath.parent_path() / "room-0-nav-v1.navgrid";
		const auto hashStart = std::chrono::steady_clock::now();
		Check(NavGridAssetWriter::ComputeContentHash(created, writerHash, writerError),
			"Room 0 hash benchmark failed: " + writerError);
		const auto hashEnd = std::chrono::steady_clock::now();
		const auto saveStart = std::chrono::steady_clock::now();
		Check(NavGridAssetWriter::WriteAtomic(room0Path.wstring(), created, writerHash, writerError),
			"Room 0 writer failed: " + writerError);
		const auto saveEnd = std::chrono::steady_clock::now();
		NavGridAsset room0;
		const auto loadStart = std::chrono::steady_clock::now();
		Check(static_cast<bool>(NavGridAssetLoader::Load(room0Path.wstring(), room0)),
			"Room 0 writer output failed Loader verification.");
		const auto loadEnd = std::chrono::steady_clock::now();
		Check(room0.GetContentHash() == writerHash, "Room 0 Writer/Loader hash mismatch.");
		Check(room0.GetWidth() == 145 && room0.GetHeight() == 145, "Room 0 metadata mismatch.");
		const double hashMilliseconds = std::chrono::duration<double, std::milli>(hashEnd - hashStart).count();
		const double saveMilliseconds = std::chrono::duration<double, std::milli>(saveEnd - saveStart).count();
		const double loadMilliseconds = std::chrono::duration<double, std::milli>(loadEnd - loadStart).count();
		std::cout << "Room 0 Phase 6 hash: " << NavGridHash::ToHex(writerHash) << "\n";
		std::cout << "Room 0 hash: " << hashMilliseconds << " ms, atomic save: " << saveMilliseconds << " ms, load: " << loadMilliseconds << " ms\n";
		std::filesystem::create_directories(resultPath.parent_path());
		std::ofstream json(resultPath, std::ios::trunc | std::ios::binary);
		Check(static_cast<bool>(json), "Cannot create C++ result JSON.");
		json << "{\n";
		json << "  \"mapId\": \"" << JsonEscape(asset.GetMapId()) << "\",\n";
		json << "  \"formatVersion\": 1,\n";
		json << "  \"width\": 4,\n";
		json << "  \"height\": 3,\n";
		json << "  \"cellSize\": \"1.000000\",\n";
		json << "  \"origin\": \"-2.000000,0.000000,-1.000000\",\n";
		json << "  \"defaultAgentRadius\": \"0.500000\",\n";
		json << "  \"contentHash\": \"" << asset.GetContentHashHex() << "\",\n";
		json << "  \"cellBytesHex\": \"000001000001010000000000\",\n";
		json << "  \"cellBytesSha256\": \"" << NavGridHash::ToHex(cellHash) << "\",\n";
		json << "  \"worldToCell\": {\n";
		for (size_t i = 0; i < coordinateResults.size(); ++i)
		{
			json << "    \"" << JsonEscape(coordinateResults[i].first) << "\": \""
				<< coordinateResults[i].second << "\""
				<< (i + 1 == coordinateResults.size() ? "\n" : ",\n");
		}
		json << "  },\n";
		json << "  \"cellCenters\": {\n";
		json << "    \"0:0\": \"" << CenterText(*center00) << "\",\n";
		json << "    \"3:2\": \"" << CenterText(*center32) << "\"\n";
		json << "  }\n";
		json << "}\n";
		Check(static_cast<bool>(json), "Cannot write C++ result JSON.");

		std::cout << "C++ NavGrid tests passed (" << gAssertions << " assertions).\n";
		std::cout << "Content SHA-256: " << asset.GetContentHashHex() << "\n";
		return 0;
	}
	catch (const std::exception& exception)
	{
		std::cerr << "C++ NavGrid tests failed: " << exception.what() << "\n";
		return 1;
	}
}
