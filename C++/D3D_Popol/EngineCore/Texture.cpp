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
	
	// Ȯ���� ����
	size_t dotPos = path.find_last_of(L'.');
	

	// Ȯ���� ���� �� �ҹ��ڷ� ��ȯ
	std::wstring extension = path.substr(dotPos + 1); // '.' ���ĺ��� ����
	std::transform(extension.begin(), extension.end(), extension.begin(), ::towlower);

	// Ȯ���ڿ� ���� �б� ó��
	if (extension == L"tga")
	{
		hr = ::LoadFromTGAFile(path.c_str(), TGA_FLAGS_NONE, &md, _img);

	}
	else if (extension == L"png" || extension == L"jpg" || extension == L"bmp")
	{
		hr = ::LoadFromWICFile(path.c_str(), WIC_FLAGS_NONE, &md, _img);
	}
	else if (extension == L"dds") // DDS ���� �ε� �߰�
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
