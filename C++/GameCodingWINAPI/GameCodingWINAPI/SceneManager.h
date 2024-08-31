#pragma once
class SceneManager
{
	DECLARE_SINGLE(SceneManager);

public:
	void Init();
	void Update();
	void Render(HDC hdc);


private:
	class Scene* _scene;
	SceneType currentType = SceneType::NONE;


};

