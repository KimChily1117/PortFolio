#pragma once
#include "Component.h"

class Model;
class InstancingBuffer;

struct AnimTransform
{
	// [ ][ ][ ][ ][ ][ ][ ] ... 250��
	using TransformArrayType = array<Matrix, MAX_MODEL_TRANSFORMS>;
	// [ ][ ][ ][ ][ ][ ][ ] ... 500 ��
	array<TransformArrayType, MAX_MODEL_KEYFRAMES> transforms;


};


class ModelAnimator : public Component
{
	using Super = Component;
public:
	
	ModelAnimator(shared_ptr<Shader> shader);
	~ModelAnimator();

	void SetModel(shared_ptr<Model> model);
	void SetPass(uint8 pass) { _pass = pass; }


	virtual void Update() override;

	void UpdateTweenData();
	void RenderInstancing(shared_ptr<class InstancingBuffer>& buffer);
	InstanceID GetInstanceID();
	TweenDesc& GetTweenDesc() { return _tweenDesc; }

	shared_ptr<Shader> GetShader() { return	_shader; }


private:
	void CreateTexture();
	void CreateAnimationTransform(uint32 index);


private:
	vector<AnimTransform> _animTransforms;
	ComPtr<ID3D11Texture2D> _texture;
	ComPtr<ID3D11ShaderResourceView> _srv;
	D3D11_PRIMITIVE_TOPOLOGY type = D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST;


private:
	KeyframeDesc _keyframeDesc;
	TweenDesc _tweenDesc;


private:
	shared_ptr<Shader>	_shader;
	uint8				_pass = 0;
	shared_ptr<Model>	_model;
};

