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
// ���� (Radial) FillAmount (��ų ��ٿ�?
/////////////////////////////////////
float4 PS_RadialFill_Q(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);
    float2 uv = input.uv - float2(0.5, 0.5); // �߽� ���� ��ǥ ��ȯ

    // 12�� ���⿡�� �����Ͽ� �ð� ���� ���� (���� ����ȭ)
    float angle = atan2(-uv.x, uv.y) / (3.1415926 * 2.0) + 0.5;

    // FillAmount�� �پ��鼭 ���� ���������?����
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

float4 PS_Particle(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);

    // MaterialDesc���� �Ѿ��?diffuse ���� ���ϱ�
    color *= Material.diffuse;

    // ���� Ŭ���� ����
    clip(color.a - 0.1f);

    return color;
}

float4 PS_ProjectileBillboard(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv) * Material.diffuse;
    clip(color.a - 0.02f);
    return color;
}

float4 PS_SkillConeIndicator(MeshOutput input) : SV_TARGET
{
    // The legacy DXT1 indicator has no alpha channel, so using its sampled
    // alpha exposes the entire square Quad. Mask the Quad to the authoritative
    // cone and use the texture only for its cyan line artwork.
    float4 sampled = DiffuseMap.Sample(LinearSampler, input.uv);
    float longitudinal = 1.0f - saturate(input.uv.y);
    float halfWidth = 0.5f * longitudinal;
    float lateral = abs(input.uv.x - 0.5f);
    float coneMask = 1.0f - smoothstep(
        max(0.0f, halfWidth - 0.012f),
        halfWidth + 0.012f,
        lateral);
    clip(coneMask - 0.01f);

    // The texture also contains bright neutral-gray texels behind its curved
    // range arc. Luminance cannot distinguish them from the cyan artwork.
    // Preserve only texels whose green/blue chroma is stronger than red.
    float cyanChroma = max(sampled.g, sampled.b) - sampled.r;
    float cyanMask = smoothstep(0.035f, 0.16f, cyanChroma);
    float indicatorAlpha = coneMask * cyanMask * Material.diffuse.a;
    clip(indicatorAlpha - 0.01f);
    return float4(sampled.rgb * Material.diffuse.rgb, indicatorAlpha);
}
float4 PS_ProjectileRibbon(MeshOutput input) : SV_TARGET
{
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv) * Material.diffuse;
    float trailFade = saturate(input.uv.x);
    color.rgb *= 0.65f + 0.35f * trailFade;
    color.a *= smoothstep(0.0f, 0.08f, trailFade);
    clip(color.a - 0.01f);
    return color;
}

float4 PS_Trail(MeshOutput input) : SV_TARGET
{
 // �ؽ�ó ���ø�
    float4 color = DiffuseMap.Sample(LinearSampler, input.uv);

    // ���� �� (�����?������ ����)
    clip(color.a - 0.1f);

    // ������ �ٶ󺸴� �鸸 ���� (����, ���� ����)
    if (abs(input.normal.y) < 0.99f)
        discard;

    // Material���� ������ diffuse ���� ���ϱ� (���� ������)
    color *= Material.diffuse;

    return color;    
}







float DigitSegment(float2 uv, float2 center, float2 halfSize)
{
    float2 edge = abs(uv - center) - halfSize;
    float distanceToBox = max(edge.x, edge.y);
    return 1.0f - smoothstep(0.0f, 0.025f, distanceToBox);
}

float4 PS_WorldDigit(MeshOutput input) : SV_TARGET
{
    int digit = (int)round(Material.ambient.x);
    bool a = digit == 0 || digit == 2 || digit == 3 || digit == 5 || digit == 6 || digit == 7 || digit == 8 || digit == 9;
    bool b = digit == 0 || digit == 1 || digit == 2 || digit == 3 || digit == 4 || digit == 7 || digit == 8 || digit == 9;
    bool c = digit == 0 || digit == 1 || digit == 3 || digit == 4 || digit == 5 || digit == 6 || digit == 7 || digit == 8 || digit == 9;
    bool d = digit == 0 || digit == 2 || digit == 3 || digit == 5 || digit == 6 || digit == 8 || digit == 9;
    bool e = digit == 0 || digit == 2 || digit == 6 || digit == 8;
    bool f = digit == 0 || digit == 4 || digit == 5 || digit == 6 || digit == 8 || digit == 9;
    bool g = digit == 2 || digit == 3 || digit == 4 || digit == 5 || digit == 6 || digit == 8 || digit == 9;

    float ink = 0.0f;
    if (a) ink = max(ink, DigitSegment(input.uv, float2(0.50f, 0.10f), float2(0.27f, 0.070f)));
    if (b) ink = max(ink, DigitSegment(input.uv, float2(0.78f, 0.31f), float2(0.070f, 0.19f)));
    if (c) ink = max(ink, DigitSegment(input.uv, float2(0.78f, 0.69f), float2(0.070f, 0.19f)));
    if (d) ink = max(ink, DigitSegment(input.uv, float2(0.50f, 0.90f), float2(0.27f, 0.070f)));
    if (e) ink = max(ink, DigitSegment(input.uv, float2(0.22f, 0.69f), float2(0.070f, 0.19f)));
    if (f) ink = max(ink, DigitSegment(input.uv, float2(0.22f, 0.31f), float2(0.070f, 0.19f)));
    if (g) ink = max(ink, DigitSegment(input.uv, float2(0.50f, 0.50f), float2(0.27f, 0.070f)));
    clip(ink * Material.diffuse.a - 0.02f);
    return float4(Material.diffuse.rgb, ink * Material.diffuse.a);
}

BlendState WorldTextAlphaBlend
{
    BlendEnable[0] = TRUE;
    SrcBlend = SRC_ALPHA;
    DestBlend = INV_SRC_ALPHA;
    BlendOp = ADD;
    SrcBlendAlpha = ONE;
    DestBlendAlpha = INV_SRC_ALPHA;
    BlendOpAlpha = ADD;
    RenderTargetWriteMask[0] = 0x0F;
};

BlendState ProjectileTrailAdditiveBlend
{
    BlendEnable[0] = TRUE;
    SrcBlend = SRC_ALPHA;
    DestBlend = ONE;
    BlendOp = ADD;
    SrcBlendAlpha = ONE;
    DestBlendAlpha = ONE;
    BlendOpAlpha = ADD;
    RenderTargetWriteMask[0] = 0x0F;
};

DepthStencilState WorldTextDepthRead
{
    DepthEnable = FALSE;
    DepthWriteMask = ZERO;
    DepthFunc = LESS_EQUAL;
};

DepthStencilState ProjectileBillboardDepthRead
{
    DepthEnable = TRUE;
    DepthWriteMask = ZERO;
    DepthFunc = LESS_EQUAL;
};

DepthStencilState SkillIndicatorDepthState
{
    DepthEnable = TRUE;
    DepthWriteMask = ALL;
    DepthFunc = LESS_EQUAL;
};

RasterizerState WorldTextNoCull
{
    FillMode = SOLID;
    CullMode = NONE;
};
RasterizerState SkillIndicatorRasterizer
{
    FillMode = SOLID;
    CullMode = NONE;
    DepthBias = -16;
    DepthBiasClamp = 0.0f;
    SlopeScaledDepthBias = -1.0f;
};
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
	PASS_VP(P11, VS_Animation_Static, PS) // ������ ���� ���̴�
	PASS_VP(P12, VS_Mesh, PS_Particle) // ��ƼŬ ���� ���̴�
	PASS_VP(P13, VS_Mesh, PS_Trail) // trail
	pass P14
	{
		SetBlendState(WorldTextAlphaBlend, float4(0, 0, 0, 0), 0xffffffff);
		SetDepthStencilState(WorldTextDepthRead, 0);
		SetRasterizerState(WorldTextNoCull);
		SetVertexShader(CompileShader(vs_5_0, VS_Billboard()));
		SetPixelShader(CompileShader(ps_5_0, PS_WorldDigit()));
	}
	pass P15
	{
		SetBlendState(WorldTextAlphaBlend, float4(0, 0, 0, 0), 0xffffffff);
		SetDepthStencilState(ProjectileBillboardDepthRead, 0);
		SetRasterizerState(WorldTextNoCull);
		SetVertexShader(CompileShader(vs_5_0, VS_Billboard()));
		SetPixelShader(CompileShader(ps_5_0, PS_ProjectileBillboard()));
	}
	pass P16
	{
		SetBlendState(ProjectileTrailAdditiveBlend, float4(0, 0, 0, 0), 0xffffffff);
		SetDepthStencilState(ProjectileBillboardDepthRead, 0);
		SetRasterizerState(WorldTextNoCull);
		SetVertexShader(CompileShader(vs_5_0, VS_Mesh()));
		SetPixelShader(CompileShader(ps_5_0, PS_ProjectileRibbon()));
	}
	pass P17
	{
		SetBlendState(WorldTextAlphaBlend, float4(0, 0, 0, 0), 0xffffffff);
		SetDepthStencilState(SkillIndicatorDepthState, 0);
		SetRasterizerState(SkillIndicatorRasterizer);
		SetVertexShader(CompileShader(vs_5_0, VS_Mesh()));
		SetPixelShader(CompileShader(ps_5_0, PS_SkillConeIndicator()));
	}
}; 