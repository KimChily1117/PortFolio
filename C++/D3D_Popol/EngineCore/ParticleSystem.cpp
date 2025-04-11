#include "pch.h"
#include "ParticleSystem.h"
#include "MeshRenderer.h"
#include "Mesh.h"
#include "BinaryReader.h"
#include "FileUtils.h"
#include "Camera.h"

ParticleSystem::ParticleSystem(wstring file)
{
	LoadData(file);
	instancingBuffer = make_shared<InstancingBuffer>();

	blendState[0] = new BlendState();
	blendState[1] = new BlendState();
	blendState[1]->Alpha(true);

	depthState[0] = new DepthStencilState();
	depthState[1] = new DepthStencilState();
	depthState[1]->DepthWriteMask(D3D11_DEPTH_WRITE_MASK_ZERO);
}

ParticleSystem::~ParticleSystem()
{
	delete blendState[0];
	delete blendState[1];

	delete depthState[0];
	delete depthState[1];
}

void ParticleSystem::Update()
{
	instancingBuffer->ClearData();

	lifeTime += DT;
	UpdatePhysical();
	UpdateColor();
	// quad에 대한 트랜스폼 업데이트 진행
	if (lifeTime > data.duration)
	{
		if (data.isLoop)
			//Init();
			Stop();
		else
			Stop();
	}
}

void ParticleSystem::Render()
{
	// 랜더링에 대한 셋팅이 필요함
	// 매쉬 랜더러의 기능들을 여기다가 정의한다.



	// 매쉬 랜더러를 붙이긴해야할거같은데...
	// 따로 Scene에 올리진않고. 랜더링을 따로 걸꺼니깐.. 
	// 안에 Data만 가지고온다?
	// 이게 효율적인가? ==> 아님
	// 그치만 데이터를 들고있으니간 컴포넌트를 붙인다.
	// Scene구조가 아닌 따로 Manager를통해 꺼내오기만할것임 -> IMGUI를 생각하면된다.

	// 결론은 파티클은 게임 엔진 시스템이긴하나 , 서드파티 느낌으로 따로 돌고있을것임 


	TransformDesc desc;

	desc.W = _quad->GetOrAddTransform()->GetWorldMatrix();
	_quad->GetMeshRenderer()->GetShader()->PushTransformData(desc);


	// VB 
	_quad->GetMeshRenderer()->GetMesh()->GetVertexBuffer()->PushData();
	// IdxBUffer
	_quad->GetMeshRenderer()->GetMesh()->GetIndexBuffer()->PushData();

	DC->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	
	// InstanceBuffer

	instancingBuffer->PushData();


	// 월드(Transform 자체를 업데이트함)
	// 여기서 TransformDesc 구조체 선언후에 밀어줌



	blendState[1]->SetState();
	depthState[1]->SetState();	
	////////////////////////////////////
	// 랜더링 이제 걸것
	_quad->GetMeshRenderer()->GetShader()->DrawIndexedInstanced(0,12, _quad->GetMeshRenderer()->GetMesh()->GetIndexBuffer()->GetCount(), drawCount);

	//DC->DrawIndexedInstanced(6, drawCount, 0, 0, 0);


	blendState[0]->SetState();
	depthState[0]->SetState();
}

void ParticleSystem::Play(Vec3 pos, Vec3 rot /*= Vec3()*/)
{
	initialized = false; // 초기화 명시적으로 Reset
	isActive = true;
	// Quad(즉)
	_quad->GetOrAddTransform()->SetPosition(pos);
	_quad->GetOrAddTransform()->SetRotation(rot);

	DEBUG_LOG(L"[Play] SetPos: " << pos.x << " , " << pos.y << " , " << pos.z); // ⬅ 이거 추가
	DEBUG_LOG(L"[Play] SetRot: " << rot.x  << " , " << rot.y << " , " << rot.z); // ⬅ 이거 추가
	Init();
}

void ParticleSystem::Stop()
{
	// Todo Stop기능 만들어야함. 근데 뭐.. 정아니면 파괴시키면되니깐.
	// 이 부분은 한번 고민해봅시다.

	isActive = false;

}

void ParticleSystem::UpdatePhysical()
{
	// 쉐이더 입자들에대한 트랜스폼 업데이트

	drawCount = 0;

	for (int i = 0; i < data.count; i++)
	{
		if (particleInfos[i].startTime > lifeTime) continue;

		particleInfos[i].velocity += particleInfos[i].accelation * DT;
		particleInfos[i].transform._localPosition += particleInfos[i].velocity * particleInfos[i].speed * DT;
		particleInfos[i].transform._localRotation.z += particleInfos[i].angularVelocity * DT;
		if (data.isBillboard)
		{
			particleInfos[i].transform._localRotation.x = CUR_SCENE->GetMainCamera()->GetTransform()->_localRotation.x;
			particleInfos[i].transform._localRotation.y = CUR_SCENE->GetMainCamera()->GetTransform()->_localRotation.y;
		}

		float t = (lifeTime - particleInfos[i].startTime)
			/ (data.duration - particleInfos[i].startTime);

		particleInfos[i].transform._localScale.x = std::lerp(particleInfos[i].startScale.x, particleInfos[i].endScale.x, t);
		particleInfos[i].transform._localScale.y = std::lerp(particleInfos[i].startScale.y, particleInfos[i].endScale.y, t);
		particleInfos[i].transform._localScale.z = std::lerp(particleInfos[i].startScale.z, particleInfos[i].endScale.z, t);

		particleInfos[i].transform.UpdateTransform();
		instances[drawCount].world = particleInfos[i].transform.GetWorldMatrix();
		instancingBuffer->AddData(instances[drawCount]);
		drawCount++;
	}

	instancingBuffer->PushData();

	// 쉐이더 입자들에대한 트랜스폼 업데이트

	//drawCount = 0;
	//for (int i = 0; i < data.count; i++)
	//{
	//	if (particleInfos[i].startTime > lifeTime) continue;

	//	particleInfos[i].velocity += particleInfos[i].accelation * DT;
	//	particleInfos[i].transform._localPosition += particleInfos[i].velocity * particleInfos[i].speed * DT;
	//	particleInfos[i].transform._localRotation.z += particleInfos[i].angularVelocity * DT;

	//	if (data.isBillboard)
	//	{
	//		particleInfos[i].transform._localRotation.x = CUR_SCENE->GetMainCamera()->GetTransform()->_localRotation.x;
	//		particleInfos[i].transform._localRotation.y = CUR_SCENE->GetMainCamera()->GetTransform()->_localRotation.y;
	//	}

	//	// [👀 보간값 t 보정]
	//	float denom = max(data.duration - particleInfos[i].startTime, 0.0001f);
	//	float t = (lifeTime - particleInfos[i].startTime) / denom;
	//	t = std::clamp(t, 0.0f, 1.0f);

	//	particleInfos[i].transform._localScale.x = std::lerp(particleInfos[i].startScale.x, particleInfos[i].endScale.x, t);
	//	particleInfos[i].transform._localScale.y = std::lerp(particleInfos[i].startScale.y, particleInfos[i].endScale.y, t);
	//	particleInfos[i].transform._localScale.z = std::lerp(particleInfos[i].startScale.z, particleInfos[i].endScale.z, t);

	//	particleInfos[i].transform.UpdateTransform();
	//	instances[drawCount].world = particleInfos[i].transform.GetWorldMatrix();
	//	instancingBuffer->AddData(instances[drawCount]);
	//	drawCount++;
	//}

	//instancingBuffer->PushData();


}

void ParticleSystem::UpdateColor()
{
	// Mat desc 구조체 만들어서 Update 함

	//float t = lifeTime / data.duration;

	//Float4 color;
	//color.x = Lerp(data.startColor.x, data.endColor.x, t);
	//color.y = Lerp(data.startColor.y, data.endColor.y, t);
	//color.z = Lerp(data.startColor.z, data.endColor.z, t);
	//color.w = Lerp(data.startColor.w, data.endColor.w, t);

	//quad->GetMaterial()->GetData().diffuse = color;
	float t = lifeTime / data.duration;
	
	//auto desc = _quad->GetMeshRenderer()->GetMaterial()->GetMaterialDesc();
	_quad->GetMeshRenderer()->GetMaterial()->GetMaterialDesc().diffuse.x = std::lerp(data.startColor.x, data.endColor.x, t);
	_quad->GetMeshRenderer()->GetMaterial()->GetMaterialDesc().diffuse.y = std::lerp(data.startColor.y, data.endColor.y, t);
	_quad->GetMeshRenderer()->GetMaterial()->GetMaterialDesc().diffuse.z = std::lerp(data.startColor.z, data.endColor.z, t);
	_quad->GetMeshRenderer()->GetMaterial()->GetMaterialDesc().diffuse.w = std::lerp(data.startColor.w, data.endColor.w, t);	
	
	/*DEBUG_LOG("Update Start Color  : " << data.startColor.x << ", " <<  data.startColor.y << ", " << data.startColor.z << ", " << data.startColor.w);
	DEBUG_LOG("Update End Color  : " << data.endColor.x << ", " << data.endColor.y << ", " << data.endColor.z << ", " << data.endColor.w);
	*/

	_quad->GetMeshRenderer()->GetMaterial()->Update();
}


void ParticleSystem::Init()
{
	// _quad에 대한 메모리 할당 및 Component 부착 필요함



	if (data.isAdditive)
		blendState[1]->Additive();
	else
		blendState[1]->Alpha(true);

	lifeTime = 0.0f;
	drawCount = 0;

	// Particle 기준 위치
	if (_quad->GetTransform() == nullptr)
		return;
	Vec3 originPos = _quad->GetTransform()->GetPosition();

	if (initialized == false)
	{
		_originPos = originPos;
		initialized = true;
	}

	DEBUG_LOG("????" << _originPos.x << " , " << _originPos.y << " , " << _originPos.z);
	

	Vec3 rotation = _quad->GetTransform()->GetRotation();
	Matrix rotMatrix = XMMatrixRotationY(rotation.y);




	for (ParticleInfo& info : particleInfos)
	{
		info.transform.SetPosition(_originPos);
		
		
		//info.velocity = Utils::Random(data.minVelocity, data.maxVelocity);
		Vec3 baseVel = Utils::Random(data.minVelocity, data.maxVelocity);
		XMStoreFloat3(&info.velocity, XMVector3Transform(XMLoadFloat3(&baseVel), rotMatrix));
		info.velocity.Normalize();


		info.accelation = Utils::Random(data.minAccelation, data.maxAccelation);
		info.angularVelocity = Utils::Random(data.minAngularVelocity, data.maxAngularVelocity);
		info.speed = Utils::Random(data.minSpeed, data.maxSpeed);
		info.startTime = Utils::Random(data.minStartTime, data.maxStartTime);
		info.startScale = Utils::Random(data.minStartScale, data.maxStartScale);
		info.endScale = Utils::Random(data.minEndScale, data.maxEndScale);
		info.velocity.Normalize();
	}
}

void ParticleSystem::LoadData(wstring file)
{

	BinaryReader* reader = new BinaryReader(file);

	wstring textureFile = reader->WString();
	{
		/*
		
		기존 코드들

		quad = new Quad(Vector2(1, 1));
		quad->GetMaterial()->SetDiffuseMap(textureFile);
		quad->GetMaterial()->SetShader(L"Effect/Particle.hlsl");
		*/
		// Todo quad 형태의 Mesh Get해서 Mesh에 넣어준다.
		_quad = make_shared<GameObject>();
		_quad->AddComponent(make_shared<MeshRenderer>());


		// Setting Quad Mesh 
		auto mesh = RESOURCES->Get<Mesh>(L"Quad");
		_quad->GetMeshRenderer()->SetMesh(mesh);


		// 필요하다면 Quad mesh이용한 shader pass도 여기서 Setting
		_quad->GetMeshRenderer()->SetPass(12);


		// 여기서 하는이유? mat이나 이런부분들을 여기서 셋팅해서 tex로 넣어줘야함
		{
			// Setting Matrial	
			shared_ptr<Material> material = make_shared<Material>();

			// 일단은 사람들이 사용하는 General Shader 사용함
			material->SetShader(CUR_SCENE->_shader);


			auto texture = RESOURCES->Load<Texture>( L"TEX_" + Utils::GetFileNameWithoutExtension(textureFile), L"..\\" + textureFile);

			material->SetDiffuseMap(texture);
			MaterialDesc& desc = material->GetMaterialDesc();
			desc.ambient = Vec4(1.f);
			desc.diffuse = Vec4(1.f);
			desc.specular = Vec4(1.f);
			RESOURCES->Add(L"MAT_" + Utils::GetFileNameWithoutExtension(textureFile), material);
			_quad->GetMeshRenderer()->SetMaterial(material);
		}
	}
	

	//ParticleData particleData = {};

	//// Color 이전까지의 데이터만 복사 (startColor 위치까지)
	//reader->Byte(&particleData, offsetof(ParticleData, startColor));


	//// Color(startColor) 개별 파싱
	//particleData.startColor.x = std::clamp(reader->Float(), 0.0f, 1.0f);
	//particleData.startColor.y = std::clamp(reader->Float(), 0.0f, 1.0f);
	//particleData.startColor.z = std::clamp(reader->Float(), 0.0f, 1.0f);
	//particleData.startColor.w = std::clamp(reader->Float(), 0.0f, 1.0f);

	//// Color(endColor) 개별 파싱
	//particleData.endColor.x = std::clamp(reader->Float(), 0.0f, 1.0f);
	//particleData.endColor.y = std::clamp(reader->Float(), 0.0f, 1.0f);
	//particleData.endColor.z = std::clamp(reader->Float(), 0.0f, 1.0f);
	//particleData.endColor.w = std::clamp(reader->Float(), 0.0f, 1.0f);

	//// 최종 구조체 저장
	//data = particleData;

	//// 파티클 버퍼 설정
	//instances.resize(data.count);
	//particleInfos.resize(data.count);

	//delete reader;

	ParticleData particleData = {};

	particleData.isLoop = reader->Bool();
	particleData.isAdditive = reader->Bool();
	particleData.isBillboard = reader->Bool();

	particleData.count = reader->UInt();
	particleData.duration = reader->Float();

	particleData.minVelocity.x = reader->Float();
	particleData.minVelocity.y = reader->Float();
	particleData.minVelocity.z = reader->Float();

	particleData.maxVelocity.x = reader->Float();
	particleData.maxVelocity.y = reader->Float();
	particleData.maxVelocity.z = reader->Float();

	particleData.minAccelation.x = reader->Float();
	particleData.minAccelation.y = reader->Float();
	particleData.minAccelation.z = reader->Float();

	particleData.maxAccelation.x = reader->Float();
	particleData.maxAccelation.y = reader->Float();
	particleData.maxAccelation.z = reader->Float();

	particleData.minStartScale.x = reader->Float();
	particleData.minStartScale.y = reader->Float();
	particleData.minStartScale.z = reader->Float();

	particleData.maxStartScale.x = reader->Float();
	particleData.maxStartScale.y = reader->Float();
	particleData.maxStartScale.z = reader->Float();

	particleData.minEndScale.x = reader->Float();
	particleData.minEndScale.y = reader->Float();
	particleData.minEndScale.z = reader->Float();

	particleData.maxEndScale.x = reader->Float();
	particleData.maxEndScale.y = reader->Float();
	particleData.maxEndScale.z = reader->Float();

	particleData.minSpeed = reader->Float();
	particleData.maxSpeed = reader->Float();
	particleData.minAngularVelocity = reader->Float();
	particleData.maxAngularVelocity = reader->Float();
	particleData.minStartTime = reader->Float();
	particleData.maxStartTime = reader->Float();

	particleData.startColor.x = reader->Float();
	particleData.startColor.y = reader->Float();
	particleData.startColor.z = reader->Float();
	particleData.startColor.w = reader->Float();

	particleData.endColor.x = reader->Float();
	particleData.endColor.y = reader->Float();
	particleData.endColor.z = reader->Float();
	particleData.endColor.w = reader->Float();

	// 구조체 저장
	data = particleData;

	// 파티클 버퍼 초기화
	instances.resize(data.count);
	particleInfos.resize(data.count);

	delete reader;
}
