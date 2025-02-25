#pragma once
class Button : public Component
{
	using Super = Component;

public:
	Button();
	virtual ~Button();
	
	bool Picked(POINT screenPos);

	void Create(Vec2 screenPos, Vec2 size, shared_ptr<class Material> material);

	void AddOnClickedEvent(std::function<void(void)> func);
	void InvokeOnClicked();


	virtual void GUIRender() override;


private:
	std::function<void(void)> _onClicked;
	RECT _rect;
};
