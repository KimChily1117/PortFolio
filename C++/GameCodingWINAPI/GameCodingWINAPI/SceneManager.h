#pragma once
class SceneManager
{
	DECLARE_SINGLE(SceneManager);

public:
	void Init();
	void Update();
	void Render(HDC hdc);


	void Clear();

public:
	void ChangeScene(SceneType sceneType);

private:
	class Scene* _scene;
	SceneType _currentSceneType = SceneType::NONE;

public:
	Vec2 GetCameraPos() { return _cameraPos; }
	void SetCameraPos(Vec2 pos) { _cameraPos = pos; }

private:
	Vec2 _cameraPos = { 400, 300 };
};

