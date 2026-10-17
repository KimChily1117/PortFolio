#pragma once
#include <string>
#include <vector>

// Main-thread facade. Only requested samples are opened; catalog access never
// preloads the library's PCM data. Original FMOD routing is not implied.
class ReferenceAudio {
public:
    struct Track {
        std::string id,name,bank,path,category;
        float duration=0;
    };
    static bool initialize();
    static void playMusic(const std::string& context);
    static void playUi(const std::string& action="click");
    static void playAttack(const std::string& heroId="");
    static void playSkill(const std::string& heroId,int skill=3);
    static int playTrack(const std::string& trackId,bool loop=false,float volume=.7f);
    static void stopTrack(int audioId);
    static void stopMusic();
    static void stopEffects();
    static void setPaused(bool paused);
    static void setEnabled(bool enabled);
    static bool isEnabled();
    static void shutdown();
    static const std::vector<Track>& catalog();
    static std::string diagnostic();
};
