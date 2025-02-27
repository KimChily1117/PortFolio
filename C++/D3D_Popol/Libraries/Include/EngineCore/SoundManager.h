#pragma once
#include "Fmod/fmod.hpp"

class SoundManager
{
	DECLARE_SINGLE(SoundManager)


public:
	bool Init();
	void Update();


	void Release();

	bool LoadSound(const std::string& name, const std::string& filepath, bool loop = false);
	void PlaySound(const std::string& name, float volume = 1.0f);
	void StopSound(const std::string& name);
	void PauseSound(const std::string& name, bool pause);
	void SetVolume(const std::string& name, float volume);



	FMOD::System* system = nullptr;
	std::unordered_map<std::string, FMOD::Sound*> sounds;
	std::unordered_map<std::string, FMOD::Channel*> channels;

	bool _running = true;
};

