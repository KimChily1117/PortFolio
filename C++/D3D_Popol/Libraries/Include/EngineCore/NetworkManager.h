#pragma once

using ServerSessionRef		= shared_ptr<class ServerSession>;
using SendBufferRef			= shared_ptr<class SendBuffer>;
using ClientServiceRef		= shared_ptr<class ClientService>;



class NetworkManager
{
	DECLARE_SINGLE(NetworkManager);
public:
	void Init();
	void Update();
	void SendPacket(SendBufferRef sendBuffer);


private:
	ServerSessionRef CreateSession();
	ClientServiceRef _service;
	ServerSessionRef _session;
};

