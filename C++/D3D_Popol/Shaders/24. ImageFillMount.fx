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

technique11 T0
{
	PASS_VP(P0, VS_Mesh, PS)
	PASS_VP(P1, VS_Model, PS)
	PASS_VP(P2, VS_Animation, PS)
	PASS_VP(P3, VS_Mesh, PS_Notexture)
};
