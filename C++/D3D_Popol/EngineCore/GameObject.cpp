#include "pch.h"
#include "GameObject.h"
#include "MonoBehaviour.h"
#include "Transform.h"
#include "Camera.h"
#include "MeshRenderer.h"
#include "ModelRenderer.h"
#include "ModelAnimator.h"
#include "Light.h"
#include "BaseCollider.h"
#include "Terrain.h"
#include "Button.h"

GameObject::GameObject()
{
}

GameObject::GameObject(string name) : _name(name)
{
	_enableGUI = true;
	SetLayerIndex(Layer_Default);	
}

GameObject::~GameObject()
{

}

void GameObject::Awake()
{
	for (shared_ptr<Component>& component : _components)
	{
		if (component)
			component->Awake();
	}

	for (shared_ptr<MonoBehaviour>& script : _scripts)
	{
		script->Awake();
	}
}

void GameObject::Start()
{
	for (shared_ptr<Component>& component : _components)
	{
		if (component)
			component->Start();
	}

	for (shared_ptr<MonoBehaviour>& script : _scripts)
	{
		script->Start();
	}
}

void GameObject::Update()
{
	for (shared_ptr<Component>& component : _components)
	{
		if (component)
			component->Update();
	}

	for (shared_ptr<MonoBehaviour>& script : _scripts)
	{
		script->Update();
	}
}

void GameObject::LateUpdate()
{
	for (shared_ptr<Component>& component : _components)
	{
		if (component)
			component->LateUpdate();
	}

	for (shared_ptr<MonoBehaviour>& script : _scripts)
	{
		script->LateUpdate();
	}
}

void GameObject::FixedUpdate()
{
	for (shared_ptr<Component>& component : _components)
	{
		if (component)
			component->FixedUpdate();
	}

	for (shared_ptr<MonoBehaviour>& script : _scripts)
	{
		script->FixedUpdate();
	}
}


void GameObject::SaveTrasnformData()
{	
	auto transform = GetOrAddTransform();

	json jsonData;
	jsonData["name"] = _name;
	jsonData["position"] = { transform->GetLocalPosition().x, transform->GetLocalPosition().y, transform->GetLocalPosition().z };
	jsonData["rotation"] = { transform->GetLocalRotation().x, transform->GetLocalRotation().y, transform->GetLocalRotation().z };
	jsonData["scale"] = { transform->GetLocalScale().x, transform->GetLocalScale().y, transform->GetLocalScale().z };
	
	
	// ������ ��� ����
	std::string savePath = "../Resources/TrasformDatas/SavePath/";

	// ��ΰ� ������ ����
	if (!fs::exists(savePath))
		fs::create_directories(savePath);

	// ������Ʈ �̸��� ������ ���� �̸� ����
	std::string fileName = savePath + "TransformData_" + _name + ".json";

	std::ofstream file(fileName);
	if (file.is_open())
	{
		file << jsonData.dump(4); // 4�� �鿩���� (Pretty Print)
		file.close();
		DEBUG_LOG("Transform Data Saved Successfully: " << fileName);
	}
}
void GameObject::LoadTrasnformData()
{
	auto transform = GetOrAddTransform();

	// ����� ���� ��� ����
	std::string savePath = "../Resources/TrasformDatas/SavePath/";
	std::string fileName = savePath + "TransformData_" + _name + ".json";

	std::ifstream file(fileName);
	if (!file.is_open()) {
		DEBUG_LOG("Failed to load Transform Data: " << fileName);
		return;
	}

	json jsonData;
	file >> jsonData;
	file.close();

	// JSON ������ ����
	Vec3 position(
		jsonData["position"][0],
		jsonData["position"][1],
		jsonData["position"][2]
	);

	Vec3 rotation(
		jsonData["rotation"][0],
		jsonData["rotation"][1],
		jsonData["rotation"][2]
	);

	Vec3 scale(
		jsonData["scale"][0],
		jsonData["scale"][1],
		jsonData["scale"][2]
	);

	transform->SetLocalPosition(position);
	transform->SetLocalRotation(rotation);
	transform->SetLocalScale(scale);

	DEBUG_LOG("Transform Data Loaded Successfully: " << fileName);
}



void GameObject::GUIRender()
{
	if (!_enableGUI || GetTransform() == nullptr)
		return;

	if (ImGui::Begin("Inspector")) // â ����
	{
		if (ImGui::TreeNode(_name.c_str())) // TreeNode
		{
			ImGui::Text(_name.c_str());
			auto transform = GetOrAddTransform();

			Vec3 pos = transform->GetLocalPosition();
			Vec3 scale = transform->GetLocalScale();

			// ȸ�� ���� ���ȿ��� �� ������ ��ȯ
			XMFLOAT3 currentRot;
			currentRot.x = XMConvertToDegrees(transform->GetLocalRotation().x);
			currentRot.y = XMConvertToDegrees(transform->GetLocalRotation().y);
			currentRot.z = XMConvertToDegrees(transform->GetLocalRotation().z);

			// Position
			string temp = _name + "_Pos";
			if (ImGui::DragFloat3(temp.c_str(), (float*)&pos, 0.1f, -1000.0f, 1000.0f))
			{
				transform->SetLocalPosition(pos);
			}

			//  Rotation (���� �� �� ���� ��ȯ)
			temp = _name + "_Rot";
			currentRot.x = XMConvertToDegrees(GetTransform()->GetLocalRotation().x);
			currentRot.y = XMConvertToDegrees(GetTransform()->GetLocalRotation().y);
			currentRot.z = XMConvertToDegrees(GetTransform()->GetLocalRotation().z);

			//  IMGUI���� ȸ������ ���������� ���̵��� �ϱ� ���� ��ȯ
			XMFLOAT3 newRot = currentRot;
			if (ImGui::DragFloat3(temp.c_str(), (float*)&newRot, 1.0f, -180, 180))
			{
				//  X���� ������������ Ŀ���� �ʵ��� ����
				newRot.x = fmod(newRot.x, 360.0f);
				newRot.y = fmod(newRot.y, 360.0f);
				newRot.z = fmod(newRot.z, 360.0f);

				//  ����� ���� �ٽ� �������� ��ȯ�Ͽ� ����
				GetTransform()->SetLocalRotation(Vec3(
					XMConvertToRadians(newRot.x),
					XMConvertToRadians(newRot.y),
					XMConvertToRadians(newRot.z)
				));

				DEBUG_LOG("IMGUI Rotation (After Update): X=" << newRot.x
					<< ", Y=" << newRot.y
					<< ", Z=" << newRot.z);
			}

			//  Scale
			temp = _name + "_Scale";
			if (ImGui::DragFloat3(temp.c_str(), (float*)&scale, 0.1f, 0.1f, 10.0f))
			{
				transform->SetLocalScale(scale);
			}

			// Todo : Save Btn ����
			// Scene ���Խÿ� Parse�ϵ��� ���� ����
			if (ImGui::Button("Save TransformData")) // ��ư �Լ� (Ŭ���� true ��ȯ ����)
				SaveTrasnformData();     // ��ư �Լ��� �μ��� ��ư ���� ������ �̸�

			ImGui::TreePop();
		}
	}
	ImGui::End();
}

std::shared_ptr<Component> GameObject::GetFixedComponent(ComponentType type)
{
	uint8 index = static_cast<uint8>(type);
	assert(index < FIXED_COMPONENT_COUNT);
	return _components[index];
}

std::shared_ptr<Transform> GameObject::GetTransform()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::Transform);
	return static_pointer_cast<Transform>(component);
}

std::shared_ptr<Camera> GameObject::GetCamera()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::Camera);
	return static_pointer_cast<Camera>(component);
}

std::shared_ptr<MeshRenderer> GameObject::GetMeshRenderer()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::MeshRenderer);
	return static_pointer_cast<MeshRenderer>(component);
}

std::shared_ptr<ModelRenderer> GameObject::GetModelRenderer()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::ModelRenderer);
	return static_pointer_cast<ModelRenderer>(component);
}

shared_ptr<ModelAnimator> GameObject::GetModelAnimator()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::ModelAnimator);
	return static_pointer_cast<ModelAnimator>(component);
}

std::shared_ptr<Light> GameObject::GetLight()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::Light);
	return static_pointer_cast<Light>(component);
}

shared_ptr<BaseCollider> GameObject::GetCollider()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::Collider);
	return static_pointer_cast<BaseCollider>(component);
}

shared_ptr<Terrain> GameObject::GetTerrain()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::Terrain);
	return static_pointer_cast<Terrain>(component);
}

shared_ptr<Button> GameObject::GetButton()
{
	shared_ptr<Component> component = GetFixedComponent(ComponentType::Button);
	return static_pointer_cast<Button>(component);
}

//std::shared_ptr<Animator> GameObject::GetAnimator()
//{
//	shared_ptr<Component> component = GetFixedComponent(ComponentType::Animator);
//	return static_pointer_cast<Animator>(component);
//}

std::shared_ptr<Transform> GameObject::GetOrAddTransform()
{
	if (GetTransform() == nullptr)
	{
		shared_ptr<Transform> transform = make_shared<Transform>();
		AddComponent(transform);
	}

	return GetTransform();
}

void GameObject::AddComponent(shared_ptr<Component> component)
{
	component->SetGameObject(shared_from_this());

	uint8 index = static_cast<uint8>(component->GetType());

	if (index < FIXED_COMPONENT_COUNT)
	{
		_components[index] = component;
	}
	else
	{
		_scripts.push_back(dynamic_pointer_cast<MonoBehaviour>(component));
	}
}