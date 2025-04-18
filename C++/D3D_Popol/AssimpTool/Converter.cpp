﻿#include "pch.h"
#include "Converter.h"
#include <filesystem>
#include "Utils.h"
#include "tinyxml2.h"
#include "FileUtils.h"

Converter::Converter()
{
	_importer = make_shared<Assimp::Importer>();

}

Converter::~Converter()
{

}	 

void Converter::ReadAssetFile(wstring file)
{
	wstring fileStr = _assetPath + file;

	auto p = std::filesystem::path(fileStr);
	assert(std::filesystem::exists(p));

	_scene = _importer->ReadFile(
		Utils::ToString(fileStr),
		aiProcess_ConvertToLeftHanded |
		aiProcess_Triangulate |
		aiProcess_GenUVCoords |
		aiProcess_GenNormals |
		aiProcess_CalcTangentSpace
	);

	assert(_scene != nullptr);
}

void Converter::ExportModelData(wstring savePath)
{
	wstring finalPath = _modelPath + savePath + L".mesh";
	ReadModelData(_scene->mRootNode, -1, -1);
	ReadSkinData();

	////Write CSV File
	//{
	//	FILE* file;
	//	::fopen_s(&file, "../Vertices.csv", "w");

	//	for (shared_ptr<asBone>& bone : _bones)
	//	{
	//		string name = bone->name;
	//		::fprintf(file, "%d,%s\n", bone->index, bone->name.c_str());
	//	}

	//	::fprintf(file, "\n");

	//	for (shared_ptr<asMesh>& mesh : _meshes)
	//	{
	//		string name = mesh->name;
	//		::printf("%s\n", name.c_str());

	//		for (UINT i = 0; i < mesh->vertices.size(); i++)
	//		{
	//			Vec3 p = mesh->vertices[i].position;
	//			Vec4 indices = mesh->vertices[i].blendIndices;
	//			Vec4 weights = mesh->vertices[i].blendWeights;

	//			::fprintf(file, "%f,%f,%f,", p.x, p.y, p.z);
	//			::fprintf(file, "%f,%f,%f,%f,", indices.x, indices.y, indices.z, indices.w);
	//			::fprintf(file, "%f,%f,%f,%f\n", weights.x, weights.y, weights.z, weights.w);
	//		}
	//	}

	//	::fclose(file);
	//}


	WriteModelFile(finalPath);
}

void Converter::ExportMaterialData(wstring savePath)
{
	wstring finalPath = _texturePath + savePath + L".xml";
	ReadMaterialData();
	WriteMaterialData(finalPath);
}

void Converter::ExportAnimationData(wstring savePath, uint32 index /*= 0*/)
{
	wstring finalPath = _modelPath + savePath + L".clip";
	assert(index < _scene->mNumAnimations);
	shared_ptr<asAnimation> animation = ReadAnimationData(_scene->mAnimations[index]);
	WriteAnimationData(animation, finalPath);
}

void Converter::ReadModelData(aiNode* node, int32 index, int32 parent)
{
	shared_ptr<asBone> bone = make_shared<asBone>();
	bone->index = index;
	bone->parent = parent;
	bone->name = node->mName.C_Str();

	// Relative Transform
	Matrix transform(node->mTransformation[0]);
	bone->transform = transform.Transpose();

	// 2) Root (Local)
	Matrix matParent = Matrix::Identity;
	if (parent >= 0)
		matParent = _bones[parent]->transform;

	// Local (Root) Transform
	bone->transform = bone->transform * matParent;

	_bones.push_back(bone);

	// Mesh
	ReadMeshData(node, index);

	// 재귀 함수
	for (uint32 i = 0; i < node->mNumChildren; i++)
		ReadModelData(node->mChildren[i], _bones.size(), index);
}

void Converter::ReadMeshData(aiNode* node, int32 bone)
{
	if (node->mNumMeshes < 1)
		return;

	//모든 머티리얼을 검사하여, 한 개의 asMesh로 합칠지 개별적으로 저장할지 결정
	bool hasMultipleMaterials = false;
	unordered_set<std::string> uniqueMaterials;  // 머티리얼 중복 확인을 위한 Set

	for (uint32 i = 0; i < node->mNumMeshes; i++)
	{
		uint32 index = node->mMeshes[i];
		const aiMesh* srcMesh = _scene->mMeshes[index];

		const aiMaterial* material = _scene->mMaterials[srcMesh->mMaterialIndex];
		std::string materialName = material->GetName().C_Str();

		if (uniqueMaterials.find(materialName) == uniqueMaterials.end())
			uniqueMaterials.insert(materialName);

		if (uniqueMaterials.size() > 1) // 다른 머티리얼이 발견되면 개별 저장으로 변경
		{
			hasMultipleMaterials = true;
			break;
		}
	}

	// 1. 하나의 `asMesh`에 모든 `aiMesh`를 합칠 경우
	if (!hasMultipleMaterials)
	{
		shared_ptr<asMesh> mesh = make_shared<asMesh>();
		mesh->name = node->mName.C_Str();
		mesh->boneIndex = bone;

		// X축 반전 (DirectX 변환)
		Matrix transform(node->mTransformation[0]);
		transform = transform.Transpose();
		Matrix flipX = Matrix::CreateScale(-1.0f, 1.0f, 1.0f);
		
		mesh->transformMatrix = transform * flipX;

		const uint32 startVertex = mesh->vertices.size();

		for (uint32 i = 0; i < node->mNumMeshes; i++)
		{
			uint32 index = node->mMeshes[i];
			const aiMesh* srcMesh = _scene->mMeshes[index];

			// 머티리얼 적용
			const aiMaterial* material = _scene->mMaterials[srcMesh->mMaterialIndex];
			mesh->materialName = material->GetName().C_Str();

			for (uint32 v = 0; v < srcMesh->mNumVertices; v++)
			{
				VertexType vertex;
				::memcpy(&vertex.position, &srcMesh->mVertices[v], sizeof(Vec3));

				if (srcMesh->HasTextureCoords(0))
					::memcpy(&vertex.uv, &srcMesh->mTextureCoords[0][v], sizeof(Vec2));

				if (srcMesh->HasNormals())
					::memcpy(&vertex.normal, &srcMesh->mNormals[v], sizeof(Vec3));

				mesh->vertices.push_back(vertex);
			}

			// 인덱스 처리 (startVertex 고려)
			for (uint32 f = 0; f < srcMesh->mNumFaces; f++)
			{
				aiFace& face = srcMesh->mFaces[f];
				for (uint32 k = 0; k < face.mNumIndices; k++)
				{
					mesh->indices.push_back(face.mIndices[k] + startVertex);
				}
			}
		}
		_meshes.push_back(mesh);
	}
	else
	{
		//2. 각 `aiMesh`를 개별 `asMesh`로 저장
		for (uint32 meshIdx = 0; meshIdx < node->mNumMeshes; meshIdx++)
		{
			uint32 aiMeshIndex = node->mMeshes[meshIdx];
			const aiMesh* srcMesh = _scene->mMeshes[aiMeshIndex];

			shared_ptr<asMesh> subMesh = make_shared<asMesh>();
			subMesh->name = node->mName.C_Str();
			subMesh->boneIndex = bone;

			// 머티리얼 처리
			const aiMaterial* material = _scene->mMaterials[srcMesh->mMaterialIndex];
			subMesh->materialName = material->GetName().C_Str();

			for (uint32 v = 0; v < srcMesh->mNumVertices; v++)
			{
				VertexType vertex;
				::memcpy(&vertex.position, &srcMesh->mVertices[v], sizeof(Vec3));

				if (srcMesh->HasTextureCoords(0))
					::memcpy(&vertex.uv, &srcMesh->mTextureCoords[0][v], sizeof(Vec2));

				if (srcMesh->HasNormals())
					::memcpy(&vertex.normal, &srcMesh->mNormals[v], sizeof(Vec3));

				subMesh->vertices.push_back(vertex);
			}

			for (uint32 f = 0; f < srcMesh->mNumFaces; f++)
			{
				aiFace& face = srcMesh->mFaces[f];
				for (uint32 k = 0; k < face.mNumIndices; k++)
				{
					subMesh->indices.push_back(face.mIndices[k]);
				}
			}

			_meshes.push_back(subMesh);
		}
	}
}


void Converter::ReadSkinData()
{
	for (uint32 i = 0; i < _scene->mNumMeshes; i++)
	{
		aiMesh* srcMesh = _scene->mMeshes[i];
		if (srcMesh->HasBones() == false)
			continue;
		if (i >= _meshes.size())
		{
			std::cerr << "Invalid mesh index: " << i
				<< " (Max: " << _meshes.size() - 1 << ")" << std::endl;
			return; // 또는 continue로 해당 메쉬 건너뛰기
		}
		shared_ptr<asMesh> mesh = _meshes[i];

		vector<asBoneWeights> tempVertexBoneWeights;
		tempVertexBoneWeights.resize(mesh->vertices.size());

		// Bone을 순회하면서 연관된 VertexId, Weight를 구해서 기록한다.
		for (uint32 b = 0; b < srcMesh->mNumBones; b++)
		{
			aiBone* srcMeshBone = srcMesh->mBones[b];
			uint32 boneIndex = GetBoneIndex(srcMeshBone->mName.C_Str());

			for (uint32 w = 0; w < srcMeshBone->mNumWeights; w++)
			{
				uint32 index = srcMeshBone->mWeights[w].mVertexId;
				// index 검증
				if (index >= tempVertexBoneWeights.size())
				{
					std::cerr << "Invalid VertexId: " << index
						<< " (Max: " << tempVertexBoneWeights.size() - 1 << ")"
						<< " in Bone: " << srcMeshBone->mName.C_Str() << std::endl;
					continue; // 잘못된 인덱스 무시
				}

				float weight = srcMeshBone->mWeights[w].mWeight;
				tempVertexBoneWeights[index].AddWeights(boneIndex, weight);
			}
		}

		// 최종 결과 계산
		for (uint32 v = 0; v < tempVertexBoneWeights.size(); v++)
		{
			tempVertexBoneWeights[v].Normalize();

			asBlendWeight blendWeight = tempVertexBoneWeights[v].GetBlendWeights();
			mesh->vertices[v].blendIndices = blendWeight.indices;
			mesh->vertices[v].blendWeights = blendWeight.weights;
		}
	}
}

void Converter::WriteModelFile(wstring finalPath)
{
	auto path = filesystem::path(finalPath);

	// 폴더가 없으면 만든다.
	filesystem::create_directory(path.parent_path());

	shared_ptr<FileUtils> file = make_shared<FileUtils>();
	file->Open(finalPath, FileMode::Write);

	// Bone Data
	file->Write<uint32>(_bones.size());
	for (shared_ptr<asBone>& bone : _bones)
	{
		file->Write<int32>(bone->index);
		file->Write<string>(bone->name);
		file->Write<int32>(bone->parent);
		file->Write<Matrix>(bone->transform);
	}

	// Mesh Data
	file->Write<uint32>(_meshes.size());
	for (shared_ptr<asMesh>& meshData : _meshes)
	{
		file->Write<string>(meshData->name);
		file->Write<int32>(meshData->boneIndex);
		file->Write<string>(meshData->materialName);
		file->Write<Matrix>(meshData->transformMatrix);
		// Vertex Data
		file->Write<uint32>(meshData->vertices.size());
		file->Write(&meshData->vertices[0], sizeof(VertexType) * meshData->vertices.size());

		// Index Data
		file->Write<uint32>(meshData->indices.size());
		file->Write(&meshData->indices[0], sizeof(uint32) * meshData->indices.size());
	}


	//// 폴더 생성
	//auto path = filesystem::path(finalPath);
	//filesystem::create_directory(path.parent_path());

	//shared_ptr<FileUtils> file = make_shared<FileUtils>();
	//file->Open(finalPath, FileMode::Write);

	//// Bone 데이터 저장
	//file->Write<uint32>(_bones.size());
	//for (shared_ptr<asBone>& bone : _bones)
	//{
	//	file->Write<int32>(bone->index);
	//	file->Write<string>(bone->name);
	//	file->Write<int32>(bone->parent);
	//	file->Write<Matrix>(bone->transform);
	//}

	//// Mesh(SubMesh) 데이터 저장
	//file->Write<uint32>(_meshes.size());
	//for (shared_ptr<asMesh>& subMesh : _meshes)
	//{
	//	// SubMesh 이름과 본 인덱스
	//	file->Write<string>(subMesh->name);
	//	file->Write<int32>(subMesh->boneIndex);
	//	file->Write<string>(subMesh->materialName);
	//	file->Write<Matrix>(meshData->transformMatrix);

	//	// Vertex 데이터
	//	uint32 vertexCount = static_cast<uint32>(subMesh->vertices.size());
	//	file->Write<uint32>(vertexCount);
	//	if (vertexCount > 0)
	//		file->Write(&subMesh->vertices[0], sizeof(VertexType) * vertexCount);

	//	// Index 데이터
	//	uint32 indexCount = static_cast<uint32>(subMesh->indices.size());
	//	file->Write<uint32>(indexCount);
	//	if (indexCount > 0)
	//		file->Write(&subMesh->indices[0], sizeof(uint32) * indexCount);
	//}
}

void Converter::WriteMeshFile(wstring finalPath)
{
}


void Converter::ReadMaterialData()
{
	for (uint32 i = 0; i < _scene->mNumMaterials; i++)
	{
		aiMaterial* srcMaterial = _scene->mMaterials[i];

		shared_ptr<asMaterial> material = make_shared<asMaterial>();
		material->name = srcMaterial->GetName().C_Str();

		aiColor3D color;
		// Ambient
		srcMaterial->Get(AI_MATKEY_COLOR_AMBIENT, color);
		material->ambient = Color(color.r, color.g, color.b, 1.f);

		// Diffuse
		srcMaterial->Get(AI_MATKEY_COLOR_DIFFUSE, color);
		material->diffuse = Color(color.r, color.g, color.b, 1.f);

		// Specular
		srcMaterial->Get(AI_MATKEY_COLOR_SPECULAR, color);
		material->specular = Color(color.r, color.g, color.b, 1.f);
		srcMaterial->Get(AI_MATKEY_SHININESS, material->specular.w);

		// Emissive
		srcMaterial->Get(AI_MATKEY_COLOR_EMISSIVE, color);
		material->emissive = Color(color.r, color.g, color.b, 1.0f);

		aiString file;

		// Diffuse Texture
		srcMaterial->GetTexture(aiTextureType_DIFFUSE, 0, &file);
		material->diffuseFile = file.C_Str();

		// Specular Texture
		srcMaterial->GetTexture(aiTextureType_SPECULAR, 0, &file);
		material->specularFile = file.C_Str();

		// Normal Texture
		srcMaterial->GetTexture(aiTextureType_NORMALS, 0, &file);
		material->normalFile = file.C_Str();

		_materials.push_back(material);
	}
}

void Converter::WriteMaterialData(wstring finalPath)
{
	auto path = filesystem::path(finalPath);

	// 폴더가 없으면 만든다.
	filesystem::create_directory(path.parent_path());

	string folder = path.parent_path().string();

	shared_ptr<tinyxml2::XMLDocument> document = make_shared<tinyxml2::XMLDocument>();

	tinyxml2::XMLDeclaration* decl = document->NewDeclaration();
	document->LinkEndChild(decl);

	tinyxml2::XMLElement* root = document->NewElement("Materials");
	document->LinkEndChild(root);

	for (shared_ptr<asMaterial> material : _materials)
	{
		tinyxml2::XMLElement* node = document->NewElement("Material");
		root->LinkEndChild(node);

		tinyxml2::XMLElement* element = nullptr;

		element = document->NewElement("Name");
		element->SetText(material->name.c_str());
		node->LinkEndChild(element);

		element = document->NewElement("DiffuseFile");
		element->SetText(WriteTexture(folder, material->diffuseFile).c_str());
		node->LinkEndChild(element);

		element = document->NewElement("SpecularFile");
		element->SetText(WriteTexture(folder, material->specularFile).c_str());
		node->LinkEndChild(element);

		element = document->NewElement("NormalFile");
		element->SetText(WriteTexture(folder, material->normalFile).c_str());
		node->LinkEndChild(element);

		element = document->NewElement("Ambient");
		element->SetAttribute("R", material->ambient.x);
		element->SetAttribute("G", material->ambient.y);
		element->SetAttribute("B", material->ambient.z);
		element->SetAttribute("A", material->ambient.w);
		node->LinkEndChild(element);

		element = document->NewElement("Diffuse");
		element->SetAttribute("R", material->diffuse.x);
		element->SetAttribute("G", material->diffuse.y);
		element->SetAttribute("B", material->diffuse.z);
		element->SetAttribute("A", material->diffuse.w);
		node->LinkEndChild(element);

		element = document->NewElement("Specular");
		element->SetAttribute("R", material->specular.x);
		element->SetAttribute("G", material->specular.y);
		element->SetAttribute("B", material->specular.z);
		element->SetAttribute("A", material->specular.w);
		node->LinkEndChild(element);

		element = document->NewElement("Emissive");
		element->SetAttribute("R", material->emissive.x);
		element->SetAttribute("G", material->emissive.y);
		element->SetAttribute("B", material->emissive.z);
		element->SetAttribute("A", material->emissive.w);
		node->LinkEndChild(element);
	}

	document->SaveFile(Utils::ToString(finalPath).c_str());
}

std::string Converter::WriteTexture(string saveFolder, string file)
{
	string fileName = filesystem::path(file).filename().string();
	string folderName = filesystem::path(saveFolder).filename().string();

	const aiTexture* srcTexture = _scene->GetEmbeddedTexture(file.c_str());
	if (srcTexture)
	{
		string pathStr = (filesystem::path(saveFolder) / fileName).string();

		if (srcTexture->mHeight == 0)
		{
			shared_ptr<FileUtils> file = make_shared<FileUtils>();
			file->Open(Utils::ToWString(pathStr), FileMode::Write);
			file->Write(srcTexture->pcData, srcTexture->mWidth);
		}
		else
		{
			D3D11_TEXTURE2D_DESC desc;
			ZeroMemory(&desc, sizeof(D3D11_TEXTURE2D_DESC));
			desc.Width = srcTexture->mWidth;
			desc.Height = srcTexture->mHeight;
			desc.MipLevels = 1;
			desc.ArraySize = 1;
			desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
			desc.SampleDesc.Count = 1;
			desc.SampleDesc.Quality = 0;
			desc.Usage = D3D11_USAGE_IMMUTABLE;

			D3D11_SUBRESOURCE_DATA subResource = { 0 };
			subResource.pSysMem = srcTexture->pcData;

			ComPtr<ID3D11Texture2D> texture;
			HRESULT hr = DEVICE->CreateTexture2D(&desc, &subResource, texture.GetAddressOf());
			CHECK(hr);

			DirectX::ScratchImage img;
			::CaptureTexture(DEVICE.Get(), DC.Get(), texture.Get(), img);

			// Save To File
			hr = DirectX::SaveToDDSFile(*img.GetImages(), DirectX::DDS_FLAGS_NONE, Utils::ToWString(fileName).c_str());
			CHECK(hr);
		}
	}
	else
	{
		string originStr = (filesystem::path(_assetPath) / folderName / file).string();
		Utils::Replace(originStr, "\\", "/");

		string pathStr = (filesystem::path(saveFolder) / fileName).string();
		Utils::Replace(pathStr, "\\", "/");

		::CopyFileA(originStr.c_str(), pathStr.c_str(), false);
	}

	return fileName;
}

std::shared_ptr<asAnimation> Converter::ReadAnimationData(aiAnimation* srcAnimation)
{
	shared_ptr<asAnimation> animation = make_shared<asAnimation>();
	animation->name = srcAnimation->mName.C_Str();
	animation->frameRate = (float)srcAnimation->mTicksPerSecond;
	animation->frameCount = (uint32)srcAnimation->mDuration + 1;

	map<string, shared_ptr<asAnimationNode>> cacheAnimNodes;

	for (uint32 i = 0; i < srcAnimation->mNumChannels; i++)
	{
		aiNodeAnim* srcNode = srcAnimation->mChannels[i];

		// 애니메이션 노드 데이터 파싱
		shared_ptr<asAnimationNode> node = ParseAnimationNode(animation, srcNode);

		// 현재 찾은 노드 중에 제일 긴 시간으로 애니메이션 시간 갱신
		animation->duration = max(animation->duration, node->keyframe.back().time);

		cacheAnimNodes[srcNode->mNodeName.C_Str()] = node;
	}

	ReadKeyframeData(animation, _scene->mRootNode, cacheAnimNodes);

	return animation;
}

std::shared_ptr<asAnimationNode> Converter::ParseAnimationNode(shared_ptr<asAnimation> animation, aiNodeAnim* srcNode)
{
	std::shared_ptr<asAnimationNode> node = make_shared<asAnimationNode>();
	node->name = srcNode->mNodeName;

	uint32 keyCount = max(max(srcNode->mNumPositionKeys, srcNode->mNumScalingKeys), srcNode->mNumRotationKeys);

	for (uint32 k = 0; k < keyCount; k++)
	{
		asKeyframeData frameData;

		bool found = false;
		uint32 t = node->keyframe.size();

		// Position
		if (::fabsf((float)srcNode->mPositionKeys[k].mTime - (float)t) <= 0.0001f)
		{
			aiVectorKey key = srcNode->mPositionKeys[k];
			frameData.time = (float)key.mTime;
			::memcpy_s(&frameData.translation, sizeof(Vec3), &key.mValue, sizeof(aiVector3D));

			found = true;
		}

		// Rotation
		if (::fabsf((float)srcNode->mRotationKeys[k].mTime - (float)t) <= 0.0001f)
		{
			aiQuatKey key = srcNode->mRotationKeys[k];
			frameData.time = (float)key.mTime;

			frameData.rotation.x = key.mValue.x;
			frameData.rotation.y = key.mValue.y;
			frameData.rotation.z = key.mValue.z;
			frameData.rotation.w = key.mValue.w;

			found = true;
		}

		// Scale
		if (::fabsf((float)srcNode->mScalingKeys[k].mTime - (float)t) <= 0.0001f)
		{
			aiVectorKey key = srcNode->mScalingKeys[k];
			frameData.time = (float)key.mTime;
			::memcpy_s(&frameData.scale, sizeof(Vec3), &key.mValue, sizeof(aiVector3D));

			found = true;
		}

		if (found == true)
			node->keyframe.push_back(frameData);
	}

	// Keyframe 늘려주기
	if (node->keyframe.size() < animation->frameCount)
	{
		uint32 count = animation->frameCount - node->keyframe.size();
		asKeyframeData keyFrame = node->keyframe.back();

		for (uint32 n = 0; n < count; n++)
			node->keyframe.push_back(keyFrame);
	}

	return node;
}

void Converter::ReadKeyframeData(shared_ptr<asAnimation> animation, aiNode* srcNode, map<string, shared_ptr<asAnimationNode>>& cache)
{
	shared_ptr<asKeyframe> keyframe = make_shared<asKeyframe>();
	keyframe->boneName = srcNode->mName.C_Str();

	shared_ptr<asAnimationNode> findNode = cache[srcNode->mName.C_Str()];

	for (uint32 i = 0; i < animation->frameCount; i++)
	{
		asKeyframeData frameData;

		if (findNode == nullptr)
		{
			Matrix transform(srcNode->mTransformation[0]);
			transform = transform.Transpose();
			frameData.time = (float)i;
			transform.Decompose(OUT frameData.scale, OUT frameData.rotation, OUT frameData.translation);
		}
		else
		{
			frameData = findNode->keyframe[i];
		}

		keyframe->transforms.push_back(frameData);
	}

	// 애니메이션 키프레임 채우기
	animation->keyframes.push_back(keyframe);

	for (uint32 i = 0; i < srcNode->mNumChildren; i++)
		ReadKeyframeData(animation, srcNode->mChildren[i], cache);
}

void Converter::WriteAnimationData(shared_ptr<asAnimation> animation, wstring finalPath)
{
	auto path = filesystem::path(finalPath);

	// 폴더가 없으면 만든다.
	filesystem::create_directory(path.parent_path());

	shared_ptr<FileUtils> file = make_shared<FileUtils>();
	file->Open(finalPath, FileMode::Write);

	file->Write<string>(animation->name);
	file->Write<float>(animation->duration);
	file->Write<float>(animation->frameRate);
	file->Write<uint32>(animation->frameCount);

	file->Write<uint32>(animation->keyframes.size());

	for (shared_ptr<asKeyframe> keyframe : animation->keyframes)
	{
		file->Write<string>(keyframe->boneName);

		file->Write<uint32>(keyframe->transforms.size());
		file->Write(&keyframe->transforms[0], sizeof(asKeyframeData) * keyframe->transforms.size());
	}
}

uint32 Converter::GetBoneIndex(const string& name)
{
	for (shared_ptr<asBone>& bone : _bones)
	{
		if (bone->name == name)
			return bone->index;
	}

	assert(false);
	return 0;
}
