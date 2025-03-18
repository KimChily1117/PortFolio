// ==================== 상수 버퍼 (FillAmount) ====================
cbuffer UIFillMount : register(b0)
{
    float FillAmount; // 0.0 ~ 1.0 (게이지 진행도)
};

// ==================== 상수 버퍼 (Transform - 월드 & 프로젝션 행렬) ====================
cbuffer TransformBuffer : register(b1)
{
    matrix W;
    matrix P; // UI는 View 행렬을 사용하지 않고, Projection만 필요
};

// ==================== Vertex Shader ====================
struct VS_INPUT
{
    float4 position : POSITION;
    float2 uv : TEXCOORD;
};

struct VS_OUTPUT
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD;
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;

    // UI 요소의 최종 위치 계산
    output.position = mul(input.position, W); // 월드 변환 적용
    output.position = mul(output.position, P); // 투영 변환 적용 (뷰 행렬은 사용 X)
    output.uv = input.uv;

    return output;
}

// ==================== Pixel Shader ====================
Texture2D DiffuseMap : register(t0);
Texture2D NormalMap : register(t1);
Texture2D SpecularMap : register(t2);
SamplerState LinearSampler : register(s0);

float4 PS_Main(VS_OUTPUT input) : SV_TARGET
{
    // 텍스처 샘플링 (없을 경우 대비 기본값 처리)
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
 
    // fillAmount보다 큰 영역은 삭제 (좌측부터 차오르는 UI)
    if (input.uv.x > FillAmount)
        discard; // 픽셀을 투명하게 만듦

    return color;
}

// ==================== Technique ====================
technique11 T0
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VS_Main()));
        SetPixelShader(CompileShader(ps_5_0, PS_Main()));
    }
}
