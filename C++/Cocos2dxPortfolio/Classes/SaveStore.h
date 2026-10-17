#pragma once
#include "core/GameModel.h"
#include <string>

namespace aster {
// Owns serialization only; game rules never depend on Cocos2d-x or storage.
class SaveStore {
  public:
    explicit SaveStore(std::string path) : path_(std::move(path)) {}
    bool load(Profile& profile, std::string& error) const;
    bool save(const Profile& profile, std::string& error) const;
    const std::string& path() const {
        return path_;
    }

  private:
    std::string path_;
};
} // namespace aster
