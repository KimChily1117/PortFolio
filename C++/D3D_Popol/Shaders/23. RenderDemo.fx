#include "00. Global.fx"
#include "00. Light.fx"
#include "00. Render.fx"




float4 PS(MeshOutput input) : SV_TARGET
{
	//float4 color = ComputeLight(input.normal, input.uv, input.worldPosition);

    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);

    clip(color.a - 0.1f); // 알파 값이 낮으면 픽셀을 버림 (완전 투명 처리)
    return color;
}

float4 PS_Notexture(MeshOutput input) : SV_TARGET
{
	//float4 color = ComputeLight(input.normal, input.uv, input.worldPosition);

    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    // 픽셀을 완전히 제거 (렌더링 안됨)
    discard;
	
    return color;
}
float2 RotateUV(float2 uv, float angle)
{
    float s = sin(angle);
    float c = cos(angle);

    float2 center = float2(0.5, 0.5); // 중심 기준 회전

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
// 바 형태 FillAmount (HP, MP)
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
// 원형 (Radial) FillAmount (스킬 쿨다운)
/////////////////////////////////////
float4 PS_RadialFill_Q(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    float2 uv = input.uv - float2(0.5, 0.5); // 중심 기준 좌표 변환

    // 12시 방향에서 시작하여 시계 방향 진행 (각도 정규화)
    float angle = atan2(-uv.x, uv.y) / (3.1415926 * 2.0) + 0.5;

    // FillAmount가 줄어들면서 점점 사라지도록 변경
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
// Technique 정의 (FillAmount 적용)
/////////////////////////////////////
technique11 T0
{
	PASS_VP(P0, VS_Mesh, PS) // 기본 메쉬 쉐이더
	PASS_VP(P1, VS_Model, PS) // 모델 쉐이더
	PASS_VP(P2, VS_Animation, PS) // 애니메이션 쉐이더
	PASS_VP(P3, VS_Mesh, PS_Notexture) // 텍스처 없는 모델 쉐이더
	PASS_VP(P4, VS_Animation, PS_Garen) // 특정 모델 전용 쉐이더
	PASS_VP(P5, VS_Mesh, PS_FillAmount_Hp) // HP 바 Fill 적용
	PASS_VP(P6, VS_Mesh, PS_FillAmount_Mp) // MP 바 Fill 적용
	PASS_VP(P7, VS_Mesh, PS_RadialFill_Q) // Q 스킬 쿨타임
	PASS_VP(P8, VS_Mesh, PS_RadialFill_W) // W 스킬 쿨타임
	PASS_VP(P9, VS_Mesh, PS_RadialFill_E) // E 스킬 쿨타임
	PASS_VP(P10, VS_Mesh, PS_RadialFill_R) // R 스킬 쿨타임
}; 