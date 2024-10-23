#pragma once
class ResourceBase
{
public:
	ResourceBase();
	virtual ~ResourceBase();

	virtual void SaveFile(const wstring& path);
	virtual void LoadFile(const wstring& path);


};

