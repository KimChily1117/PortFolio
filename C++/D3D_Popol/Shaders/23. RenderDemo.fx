#include "00. Global.fx"
#include "00. Light.fx"
#include "00. Render.fx"




float4 PS(MeshOutput input) : SV_TARGET
{
	//float4 color = ComputeLight(input.normal, input.uv, input.worldPosition);

    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);

    clip(color.a - 0.1f); // ���� ���� ������ �ȼ��� ���� (���� ���� ó��)
    return color;
}

float4 PS_Notexture(MeshOutput input) : SV_TARGET
{
	//float4 color = ComputeLight(input.normal, input.uv, input.worldPosition);

    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    // �ȼ��� ������ ���� (������ �ȵ�)
    discard;
	
    return color;
}
float2 RotateUV(float2 uv, float angle)
{
    float s = sin(angle);
    float c = cos(angle);

    float2 center = float2(0.5, 0.5); // �߽� ���� ȸ��

    uv -= center;
    uv = float2(
        uv.x * c - uv.y * s,
        uv.x * s + uv.y * c
    );
    uv += center;

    return uv;
}

float4 PS_Garen(MeshOutput input) : SV_Target
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    return color;
    
}

/////////////////////////////////////
// �� ���� FillAmount (HP, MP)
/////////////////////////////////////
float4 PS_FillAmount_Hp(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    if (input.uv.x > FillAmount_Hp)
        discard;
    return color;
}

float4 PS_FillAmount_Mp(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    if (input.uv.x > FillAmount_Mp)
        discard;
    return color;
}

/////////////////////////////////////
// ���� (Radial) FillAmount (��ų ��ٿ�)
/////////////////////////////////////
float4 PS_RadialFill_Q(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    float2 uv = input.uv - float2(0.5, 0.5); // �߽� ���� ��ǥ ��ȯ

    // 12�� ���⿡�� �����Ͽ� �ð� ���� ���� (���� ����ȭ)
    float angle = atan2(-uv.x, uv.y) / (3.1415926 * 2.0) + 0.5;

    // FillAmount�� �پ��鼭 ���� ��������� ����
    if (angle > (1.0 - FillAmount_Q))
        discard;

    return color;
}

float4 PS_RadialFill_W(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    float2 uv = input.uv - float2(0.5, 0.5);
    float angle = atan2(-uv.x, uv.y) / (3.1415926 * 2.0) + 0.5;
    if (angle > (1.0 - FillAmount_W))
        discard;
    return color;
}

float4 PS_RadialFill_E(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    float2 uv = input.uv - float2(0.5, 0.5);
    float angle = atan2(-uv.x, uv.y) / (3.1415926 * 2.0) + 0.5;
    if (angle > (1.0 - FillAmount_E))
        discard;
    return color;
}

float4 PS_RadialFill_R(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    float2 uv = input.uv - float2(0.5, 0.5);
    float angle = atan2(-uv.x, uv.y) / (3.1415926 * 2.0) + 0.5;
    if (angle > (1.0 - FillAmount_R))
        discard;
    return color;
}



/////////////////////////////////////
// Technique ���� (FillAmount ����)
/////////////////////////////////////
technique11 T0
{
	PASS_VP(P0, VS_Mesh, PS) // �⺻ �޽� ���̴�
	PASS_VP(P1, VS_Model, PS) // �� ���̴�
	PASS_VP(P2, VS_Animation, PS) // �ִϸ��̼� ���̴�
	PASS_VP(P3, VS_Mesh, PS_Notexture) // �ؽ�ó ���� �� ���̴�
	PASS_VP(P4, VS_Animation, PS_Garen) // Ư�� �� ���� ���̴�
	PASS_VP(P5, VS_Mesh, PS_FillAmount_Hp) // HP �� Fill ����
	PASS_VP(P6, VS_Mesh, PS_FillAmount_Mp) // MP �� Fill ����
	PASS_VP(P7, VS_Mesh, PS_RadialFill_Q) // Q ��ų ��Ÿ��
	PASS_VP(P8, VS_Mesh, PS_RadialFill_W) // W ��ų ��Ÿ��
	PASS_VP(P9, VS_Mesh, PS_RadialFill_E) // E ��ų ��Ÿ��
	PASS_VP(P10, VS_Mesh, PS_RadialFill_R) // R ��ų ��Ÿ��
}; 