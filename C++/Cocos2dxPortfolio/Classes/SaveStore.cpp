#include "SaveStore.h"
#include "json/document.h"
#include "json/writer.h"
#include "json/stringbuffer.h"
#include <fstream>
#include <cstdio>
#include <algorithm>
#include <cerrno>
#include <sys/stat.h>
#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#else
#include <unistd.h>
#if defined(__ANDROID__)
#include <fcntl.h>
#include <sys/syscall.h>
#endif
#endif

namespace aster {
namespace {
enum class ReadResult { Missing, Read, Failed };
ReadResult readSave(const std::string& path, std::string& contents, std::string& error) {
#if defined(_WIN32)
    const DWORD attributes = GetFileAttributesA(path.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES) {
        const DWORD code = GetLastError();
        if (code == ERROR_FILE_NOT_FOUND || code == ERROR_PATH_NOT_FOUND)
            return ReadResult::Missing;
        error = "Cannot inspect save file; original retained.";
        return ReadResult::Failed;
    }
    const bool directory = (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
#else
    struct stat status;
    if (stat(path.c_str(), &status) != 0) {
        if (errno == ENOENT)
            return ReadResult::Missing;
        error = "Cannot inspect save file; original retained.";
        return ReadResult::Failed;
    }
    const bool directory = S_ISDIR(status.st_mode);
#endif
    std::ifstream in;
    if (!directory)
        in.open(path, std::ios::binary);
    if (directory || !in) {
        error = "Cannot open existing save file; original retained.";
        return ReadResult::Failed;
    }
    char chunk[4096];
    contents.clear();
    do {
        in.read(chunk, sizeof(chunk));
        contents.append(chunk, static_cast<size_t>(in.gcount()));
    } while (in);
    if (in.bad() || !in.eof()) {
        error = "Cannot read save file; original retained.";
        return ReadResult::Failed;
    }
    return ReadResult::Read;
}

// Shared validation has no file operations, including when validating a backup.
bool parseSave(const std::string& contents, Profile& next, int& version, std::string& error) {
    rapidjson::Document d;
    d.Parse(contents.c_str(), contents.size());
    if (d.HasParseError() || contents.find('\0') != std::string::npos) {
        error = "Save data is invalid; original file retained.";
        return false;
    }
    auto integer = [&](const char* key) { return d.IsObject() && d.HasMember(key) && d[key].IsInt64(); };
    for (auto key : {"version", "gold", "stage", "bestCleared", "victories"})
        if (!integer(key)) {
            error = "Save data is invalid; original file retained.";
            return false;
        }
    const auto storedVersion = d["version"].GetInt64();
    if ((storedVersion != 1 && storedVersion != 2) || !d.HasMember("levels") || !d["levels"].IsArray() ||
        d["levels"].Size() != (storedVersion == 1 ? 3u : 4u) || !d["stage"].IsInt() ||
        !d["bestCleared"].IsInt() || !d["victories"].IsInt()) {
        error = "Unsupported save version or fields.";
        return false;
    }
    version = static_cast<int>(storedVersion);
    next = Profile{};
    next.gold = d["gold"].GetInt64();
    next.stage = d["stage"].GetInt();
    next.bestCleared = d["bestCleared"].GetInt();
    next.victories = d["victories"].GetInt();
    for (unsigned i = 0; i < d["levels"].Size(); ++i) {
        if (!d["levels"][i].IsInt()) {
            error = "Invalid hero level.";
            return false;
        }
        next.levels[i] = d["levels"][i].GetInt();
    }
    if (!validProfile(next)) {
        error = "Save values are out of range.";
        return false;
    }
    if (version == 1) {
        for (auto key : {"pendingGold", "pendingSeconds", "lastAccrual"})
            if (!integer(key)) {
                error = "Invalid legacy save.";
                return false;
            }
        const auto pending = d["pendingGold"].GetInt64();
        if (pending < 0 || pending > GoldLimit || d["pendingSeconds"].GetInt64() < 0 ||
            d["pendingSeconds"].GetInt64() > 28800 || d["lastAccrual"].GetInt64() < 0 ||
            d["lastAccrual"].GetInt64() > 4102444800LL) {
            error = "Invalid legacy reward fields.";
            return false;
        }
        // Preserve previously earned income once. No new time-based income is generated.
        next.gold = std::min(GoldLimit, next.gold + pending);
    }
    return true;
}

bool preserveLegacyBackup(const std::string& backup, const std::string& contents, std::string& error) {
    std::string existing;
    const auto result = readSave(backup, existing, error);
    if (result == ReadResult::Failed)
        return false;
    if (result == ReadResult::Read) {
        Profile ignored;
        int version = 0;
        if (!parseSave(existing, ignored, version, error) || version != 1) {
            error = "Existing legacy backup is invalid; original save retained.";
            return false;
        }
        // A valid earlier backup may differ from this save. Never replace it.
        return true;
    }
    const auto temporary = backup + ".tmp";
    {
        std::ofstream out(temporary, std::ios::binary | std::ios::trunc);
        out.write(contents.data(), static_cast<std::streamsize>(contents.size()));
        out.flush();
        out.close();
        if (!out) {
            error = "Cannot write legacy backup; original save retained.";
            return false;
        }
    }
#if defined(_WIN32)
    const bool published = MoveFileExA(temporary.c_str(), backup.c_str(), MOVEFILE_WRITE_THROUGH) != 0;
#elif defined(__ANDROID__)
    // App storage can deny hard links. Use the kernel's atomic no-replace rename;
    // the libc renameat2 wrapper itself is only available from Android API 30.
    const bool published = syscall(__NR_renameat2, AT_FDCWD, temporary.c_str(), AT_FDCWD,
                                   backup.c_str(), RENAME_NOREPLACE) == 0;
#else
    // link publishes the complete file atomically without replacing a racing backup.
    const bool published = link(temporary.c_str(), backup.c_str()) == 0;
    if (published)
        std::remove(temporary.c_str());
#endif
    if (!published) {
        error = "Cannot preserve legacy backup; original save retained.";
        return false;
    }
    return true;
}
} // namespace

bool SaveStore::load(Profile& p, std::string& error) const {
    std::string contents;
    const auto result = readSave(path_, contents, error);
    if (result == ReadResult::Missing) {
        error.clear();
        return false;
    }
    if (result == ReadResult::Failed)
        return false;
    Profile next;
    int version = 0;
    if (!parseSave(contents, next, version, error))
        return false;
    if (version == 1 && !preserveLegacyBackup(path_ + ".v1.bak", contents, error))
        return false;
    p = next;
    error.clear();
    return true;
}
bool SaveStore::save(const Profile& p, std::string& error) const {
    if (!validProfile(p)) {
        error = "Refusing invalid save.";
        return false;
    }
    rapidjson::StringBuffer buffer;
    rapidjson::Writer<rapidjson::StringBuffer> w(buffer);
    w.StartObject();
    w.Key("version");
    w.Int(2);
    w.Key("gold");
    w.Int64(p.gold);
    w.Key("stage");
    w.Int(p.stage);
    w.Key("bestCleared");
    w.Int(p.bestCleared);
    w.Key("victories");
    w.Int(p.victories);
    w.Key("levels");
    w.StartArray();
    for (auto level : p.levels)
        w.Int(level);
    w.EndArray();
    w.EndObject();
    auto temporary = path_ + ".tmp";
    {
        std::ofstream out(temporary, std::ios::binary | std::ios::trunc);
        out.write(buffer.GetString(), buffer.GetSize());
        out.flush();
        if (!out) {
            error = "Cannot write save file.";
            return false;
        }
    }
#if defined(_WIN32)
    bool ok = MoveFileExA(temporary.c_str(), path_.c_str(),
                          MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH) != 0;
#else
    bool ok = std::rename(temporary.c_str(), path_.c_str()) == 0;
#endif
    if (!ok) {
        error = "Cannot replace save file.";
        return false;
    }
    error.clear();
    return true;
}
} // namespace aster
