#include "pch.h"
#include "Texture.h"

Texture::Texture() : Super(ResourceType::Texture)
{

}

Texture::~Texture()
{

}

void Texture::Load(const wstring& path)
{
	DirectX::TexMetadata md;
	HRESULT hr;
	
	// 확장자 추출
	size_t dotPos = path.find_last_of(L'.');
	

	// 확장자 추출 및 소문자로 변환
	std::wstring extension = path.substr(dotPos + 1); // '.' 이후부터 추출
	std::transform(extension.begin(), extension.end(), extension.begin(), ::towlower);

	// 확장자에 따른 분기 처리
	if (extension == L"tga")
	{
		hr = ::LoadFromTGAFile(path.c_str(), TGA_FLAGS_NONE, &md, _img);

	}
	else if (extension == L"png" || extension == L"jpg" || extension == L"bmp")
	{
		hr = ::LoadFromWICFile(path.c_str(), WIC_FLAGS_NONE, &md, _img);
	}
	else if (extension == L"dds") // DDS 파일 로드 추가
	{
		hr = ::LoadFromDDSFile(path.c_str(), DDS_FLAGS_NONE, &md, _img);
	}
	else
	{
		return;
	}
	hr = ::CreateShaderResourceView(DEVICE.Get(), _img.GetImages(), _img.GetImageCount(), md, _shaderResourveView.GetAddressOf());
	CHECK(hr);
	
	_size.x = md.width;
	_size.y = md.height;
}

Microsoft::WRL::ComPtr<ID3D11Texture2D> Texture::GetTexture2D()
{
	ComPtr<ID3D11Texture2D> texture;
	_shaderResourveView->GetResource((ID3D11Resource**)texture.GetAddressOf());
	return texture;
}
