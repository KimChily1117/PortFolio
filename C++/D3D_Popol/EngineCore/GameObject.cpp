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





GameObject::GameObject()
{
}

GameObject::GameObject(string name) : _name(name)
{
	_enableGUI = true;
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


void Save()
{
	// Json?���� ���� �ؾ�����
	// XML�� �����ؾ�����
	// Binary�� �����ؾ�����
	// CSV���� SpreadSheet�� �����ؾ����� ����̴�... Hmmm..

	return;
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

			// Position
			string temp = _name + "_Pos";
			ImGui::DragFloat3(temp.c_str(), (float*)&GetTransform()->GetLocalPosition(), 0.1f);

			//  Rotation (���� �� �� ���� ��ȯ)
			temp = _name + "_Rot";
			XMFLOAT3 currentRot;
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
			ImGui::DragFloat3(temp.c_str(), (float*)&GetTransform()->GetLocalScale(), 0.1f);


			// Todo : Save Btn ����
			// Scene ���Խÿ� Parse�ϵ��� ���� ����
			if (ImGui::Button("Save TransformData")) // ��ư �Լ� (Ŭ���� true ��ȯ ����)
				Save();     // ��ư �Լ��� �μ��� ��ư ���� ������ �̸�

			ImGui::TreePop();
		}
		ImGui::End();
	}
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