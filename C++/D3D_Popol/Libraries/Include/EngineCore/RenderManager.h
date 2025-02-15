class RenderManager
{
	DECLARE_SINGLE(RenderManager);
public:
	~RenderManager();
public:
	void Init(shared_ptr<Shader> shader);
	void Update();
	void Destroy();
};

