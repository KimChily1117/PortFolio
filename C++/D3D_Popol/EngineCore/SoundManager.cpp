#include "pch.h"
#include "SoundManager.h"

bool SoundManager::Init()
{
	FMOD_RESULT result = FMOD::System_Create(&system);
	if (result != FMOD_OK)
	{
		std::cerr << "FMOD System Create Failed!" << std::endl;
		return false;
	}

	result = system->init(512, FMOD_INIT_NORMAL, nullptr);
	if (result != FMOD_OK)
	{
		std::cerr << "FMOD System Init Failed!" << std::endl;
		return false;
	}

	return true;
}


void SoundManager::Release()
{
	for (auto& pair : sounds)
	{
		pair.second->release();
	}
	sounds.clear();

	if (system)
	{
		system->close();
		system->release();
		system = nullptr;
	}
}

void SoundManager::Update()
{
	if (system)
		system->update();
}

bool SoundManager::LoadSound(const std::string& name, const std::string& filepath, bool loop)
{
	if (sounds.find(name) != sounds.end()) return true; // 이미 로드된 경우

	FMOD::Sound* sound = nullptr;
	FMOD_MODE mode = loop ? FMOD_LOOP_NORMAL : FMOD_DEFAULT;
	FMOD_RESULT result = system->createSound(filepath.c_str(), mode, nullptr, &sound);

	if (result != FMOD_OK)
	{
		std::cerr << "Failed to load sound: " << filepath << std::endl;
		return false;
	}

	sounds[name] = sound;
	return true;
}

void SoundManager::PlaySound(const std::string& name, float volume)
{
	if (sounds.find(name) == sounds.end()) return;

	FMOD::Channel* channel = nullptr;
	system->playSound(sounds[name], nullptr, false, &channel);

	if (channel)
	{
		channel->setVolume(volume);
		channels[name] = channel;
	}
}

void SoundManager::StopSound(const std::string& name)
{
	if (channels.find(name) == channels.end()) return;

	channels[name]->stop();
	channels.erase(name);
}

void SoundManager::PauseSound(const std::string& name, bool pause)
{
	if (channels.find(name) == channels.end()) return;

	channels[name]->setPaused(pause);
}

void SoundManager::SetVolume(const std::string& name, float volume)
{
	if (channels.find(name) == channels.end()) return;

	channels[name]->setVolume(volume);
}
