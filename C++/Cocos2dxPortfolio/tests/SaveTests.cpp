#include "SaveStore.h"
#include <fstream>
#include <iostream>
#include <cstdio>
#include <cstdlib>
#include <sstream>
#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#else
#include <sys/stat.h>
#include <unistd.h>
#endif
using namespace aster;
void check(bool v, const char* n) {
    if (!v) {
        std::cerr << "FAIL: " << n << '\n';
        std::exit(1);
    }
}
void writeFile(const std::string& path, const std::string& contents) {
    std::ofstream out(path, std::ios::binary | std::ios::trunc);
    out << contents;
    out.close();
    check(bool(out), "test fixture written");
}
std::string readFile(const std::string& path) {
    std::ifstream in(path, std::ios::binary);
    std::ostringstream out;
    out << in.rdbuf();
    return out.str();
}
bool makeDirectory(const std::string& path) {
#if defined(_WIN32)
    return CreateDirectoryA(path.c_str(), nullptr) != 0;
#else
    return mkdir(path.c_str(), 0700) == 0;
#endif
}
void removeDirectory(const std::string& path) {
#if defined(_WIN32)
    check(RemoveDirectoryA(path.c_str()) != 0, "remove test directory");
#else
    check(rmdir(path.c_str()) == 0, "remove test directory");
#endif
}
int main() {
    const std::string path = "aster-test-profile.json";
    SaveStore store(path);
    Profile p;
    std::string error;
    const auto backup = path + ".v1.bak";
    std::remove(path.c_str());
    std::remove(backup.c_str());
    std::remove((backup + ".tmp").c_str());
    error = "previous error";
    check(!store.load(p, error) && error.empty(), "only missing file permits new profile");
    const auto directory = path + ".directory";
    check(makeDirectory(directory), "create existing directory fixture");
    check(!SaveStore(directory).load(p, error) && !error.empty(), "existing directory is not a missing save");
    removeDirectory(directory);
    p.gold = 742;
    p.levels = {{3, 2, 4, 5}};
    p.stage = 4;
    p.bestCleared = 3;
    check(store.save(p, error), "v2 save");
    Profile loaded;
    check(store.load(loaded, error), "load");
    check(loaded.gold == 742 && loaded.levels == p.levels && loaded.stage == 4, "four-slot roundtrip");
    p.gold = 912;
    check(store.save(p, error) && store.load(loaded, error) && loaded.gold == 912, "atomic replace");
#if defined(_WIN32)
    const auto beforeLock = readFile(path);
    HANDLE locked =
        CreateFileA(path.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    check(locked != INVALID_HANDLE_VALUE, "exclusively lock existing profile");
    check(!store.load(loaded, error) && !error.empty() && loaded.gold == 912,
          "locked existing save blocks writes instead of resetting profile");
    check(CloseHandle(locked) != 0, "unlock profile");
    check(readFile(path) == beforeLock, "failed read preserves original bytes");
#endif
    {
        std::ofstream out(path);
        out << "{invalid";
    }
    check(!store.load(loaded, error) && !error.empty() && loaded.gold == 912,
          "corruption keeps live profile");
    {
        std::ofstream out(path);
        out << R"({"version":3,"gold":300,"stage":1,"bestCleared":0,"victories":0,"levels":[1,1,1,1]})";
    }
    check(!store.load(loaded, error), "future version rejected");
    const std::string legacy =
        R"({"version":1,"gold":300,"stage":2,"bestCleared":1,"victories":1,"lastAccrual":1700000000,"pendingGold":28,"pendingSeconds":125,"levels":[3,2,4]})";
    writeFile(path, legacy);
    // A failed earlier backup attempt must not authorize overwriting the source.
    writeFile(backup, "{\"version\":1");
    check(!store.load(loaded, error) && !error.empty() && loaded.gold == 912,
          "partial existing backup blocks migration");
    check(readFile(path) == legacy && readFile(backup) == "{\"version\":1",
          "failed validation preserves both original files");
    auto invalidLegacy = legacy;
    invalidLegacy.replace(invalidLegacy.find("\"pendingGold\":28"), 16, "\"pendingGold\":-1");
    writeFile(backup, invalidLegacy);
    check(!store.load(loaded, error) && !error.empty() && readFile(path) == legacy,
          "parseable backup with invalid legacy values blocks migration");
    std::remove(backup.c_str());
    check(makeDirectory(backup + ".tmp"), "block atomic backup temporary path");
    check(!store.load(loaded, error) && !error.empty() && loaded.gold == 912,
          "backup write failure blocks migration");
    check(readFile(path) == legacy && !std::ifstream(backup),
          "failed backup never publishes partial file or replaces original");
    removeDirectory(backup + ".tmp");
    check(store.load(loaded, error) && loaded.gold == 328 && loaded.levels[2] == 4 && loaded.levels[3] == 1,
          "legacy rewards migrated without new accrual");
    check(readFile(backup) == legacy && !std::ifstream(backup + ".tmp"),
          "complete legacy backup published atomically");
    auto newerLegacy = legacy;
    newerLegacy.replace(newerLegacy.find("300"), 3, "400");
    writeFile(path, newerLegacy);
    check(store.load(loaded, error) && loaded.gold == 428 && readFile(backup) == legacy,
          "valid older backup retained when current legacy save differs");
    writeFile(path, legacy);
    check(store.load(loaded, error) && loaded.gold == 328, "legacy load is repeatable before v2 commit");
    check(store.save(loaded, error) && store.load(loaded, error) && loaded.gold == 328,
          "migration credited only once");
    p.gold = -1;
    check(!store.save(p, error), "invalid profile cannot overwrite");
    std::remove(path.c_str());
    std::remove(backup.c_str());
    std::cout
        << "PASS: save v2, read failures, corruption, atomic validated v1 backup and one-time migration\n";
}
