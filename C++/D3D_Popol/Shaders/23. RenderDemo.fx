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

technique11 T0
{
	PASS_VP(P0, VS_Mesh, PS)
	PASS_VP(P1, VS_Model, PS)
	PASS_VP(P2, VS_Animation, PS)
	PASS_VP(P3, VS_Mesh, PS_Notexture)
	PASS_VP(P4, VS_Animation, PS_Garen)
};
