// ==================== ��� ���� (FillAmount) ====================
cbuffer UIFillMount : register(b0)
{
    float FillAmount; // 0.0 ~ 1.0 (������ ���൵)
};

// ==================== ��� ���� (Transform - ���� & �������� ���) ====================
cbuffer TransformBuffer : register(b1)
{
    matrix W;
    matrix P; // UI�� View ����� ������� �ʰ�, Projection�� �ʿ�
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

    // UI ����� ���� ��ġ ���
    output.position = mul(input.position, W); // ���� ��ȯ ����
    output.position = mul(output.position, P); // ���� ��ȯ ���� (�� ����� ��� X)
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
    // �ؽ�ó ���ø� (���� ��� ��� �⺻�� ó��)
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
 
    // fillAmount���� ū ������ ���� (�������� �������� UI)
    if (input.uv.x > FillAmount)
        discard; // �ȼ��� �����ϰ� ����

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
