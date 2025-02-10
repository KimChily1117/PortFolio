#pragma once
class TestScene : public Scene
{
	using Super = Scene;
public:
	void Start() override;
	void Update() override;
	void LateUpdate() override;

	void InitializeObject();

public:
	shared_ptr<Shader> _shader;
};

