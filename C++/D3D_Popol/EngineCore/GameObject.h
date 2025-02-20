#pragma once
#include "Component.h"
class MonoBehaviour;
class Transform;
class Camera;
class MeshRenderer;
class ModelRenderer;
class ModelAnimator;
class Light;
class BaseCollider;
class Terrain;

class GameObject : public enable_shared_from_this<GameObject>
{
public:
	GameObject();
	GameObject(std::string name);
	~GameObject();

	void Awake();
	void Start();
	void Update();
	void LateUpdate();
	void FixedUpdate();

	void GUIRender();

	shared_ptr<Component> GetFixedComponent(ComponentType type);
	shared_ptr<Transform> GetTransform();
	shared_ptr<Camera> GetCamera();
	shared_ptr<MeshRenderer> GetMeshRenderer();
	shared_ptr<ModelRenderer> GetModelRenderer();
	shared_ptr<ModelAnimator> GetModelAnimator();
	shared_ptr<Light> GetLight();
	shared_ptr<BaseCollider> GetCollider();
	shared_ptr<Terrain> GetTerrain();

	shared_ptr<Transform> GetOrAddTransform();
	void AddComponent(shared_ptr<Component> component);

	std::string _name; // objectName;
	bool _enableGUI = false; // GUI on off;
	

protected:
	array<shared_ptr<Component>, FIXED_COMPONENT_COUNT> _components;
	vector<shared_ptr<MonoBehaviour>> _scripts;
};

