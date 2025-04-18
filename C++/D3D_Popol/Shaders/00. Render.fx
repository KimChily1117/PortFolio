#ifndef _RENDER_FX_
#define _RENDER_FX_

#include "00. Global.fx"

#define MAX_MODEL_TRANSFORMS 700
#define MAX_MODEL_KEYFRAMES 750
#define MAX_MODEL_INSTANCE 500

// ************** Constant Buffers **************

cbuffer BoneBuffer
{
    matrix BoneTransforms[MAX_MODEL_TRANSFORMS];
};

cbuffer MeshTransformBuffer
{
    matrix MeshTransformMatrix; //추가된 Mesh 변환 행렬
};

// ************** MeshRender ****************

struct VertexMesh
{
    float4 position : POSITION;
    float2 uv : TEXCOORD;
    float3 normal : NORMAL;
    float3 tangent : TANGENT;
    uint instanceID : SV_INSTANCEID;
    matrix world : INST;
};

MeshOutput VS_Mesh(VertexMesh input)
{
    MeshOutput output;
    output.position = mul(input.position, input.world); // W
    output.worldPosition = output.position;
    output.position = mul(output.position, VP);
    output.uv = input.uv;
    output.normal = input.normal;
    return output;
}

// ************** ModelRender ****************

struct VertexModel
{
    float4 position : POSITION;
    float2 uv : TEXCOORD;
    float3 normal : NORMAL;
    float3 tangent : TANGENT;
    float4 blendIndices : BLEND_INDICES;
    float4 blendWeights : BLEND_WEIGHTS;
    uint instanceID : SV_INSTANCEID;
    matrix world : INST;
};

uint BoneIndex;

MeshOutput VS_Model(VertexModel input)
{
    MeshOutput output;
    output.position = mul(input.position, BoneTransforms[BoneIndex]); // Model Global
    output.position = mul(output.position, input.world); // W
    output.worldPosition = output.position;
    output.position = mul(output.position, VP);
    output.uv = input.uv;
    output.normal = input.normal;
    return output;
}

// ************** AnimRender ****************

struct KeyframeDesc
{
    int animIndex;
    uint currFrame;
    uint nextFrame;
    float ratio;
    float sumTime;
    float speed;
    float2 padding;
};

struct TweenFrameDesc
{
    float tweenDuration;
    float tweenRatio;
    float tweenSumTime;
    float padding;
    KeyframeDesc curr;
    KeyframeDesc next;
};

cbuffer TweenBuffer
{
    TweenFrameDesc TweenFrames[MAX_MODEL_INSTANCE];
};

Texture2DArray TransformMap;

matrix GetAnimationMatrix(VertexModel input)
{
    float indices[4] = { input.blendIndices.x, input.blendIndices.y, input.blendIndices.z, input.blendIndices.w };
    float weights[4] = { input.blendWeights.x, input.blendWeights.y, input.blendWeights.z, input.blendWeights.w };

    int animIndex[2];
    int currFrame[2];
    int nextFrame[2];
    float ratio[2];

    animIndex[0] = TweenFrames[input.instanceID].curr.animIndex;
    currFrame[0] = TweenFrames[input.instanceID].curr.currFrame;
    nextFrame[0] = TweenFrames[input.instanceID].curr.nextFrame;
    ratio[0] = TweenFrames[input.instanceID].curr.ratio;

    animIndex[1] = TweenFrames[input.instanceID].next.animIndex;
    currFrame[1] = TweenFrames[input.instanceID].next.currFrame;
    nextFrame[1] = TweenFrames[input.instanceID].next.nextFrame;
    ratio[1] = TweenFrames[input.instanceID].next.ratio;

    float4 c0, c1, c2, c3;
    float4 n0, n1, n2, n3;
    matrix curr = 0;
    matrix next = 0;
    matrix transform = 0;

    for (int i = 0; i < 4; i++)
    {
        c0 = TransformMap.Load(int4(indices[i] * 4 + 0, currFrame[0], animIndex[0], 0));
        c1 = TransformMap.Load(int4(indices[i] * 4 + 1, currFrame[0], animIndex[0], 0));
        c2 = TransformMap.Load(int4(indices[i] * 4 + 2, currFrame[0], animIndex[0], 0));
        c3 = TransformMap.Load(int4(indices[i] * 4 + 3, currFrame[0], animIndex[0], 0));
        curr = matrix(c0, c1, c2, c3);

        n0 = TransformMap.Load(int4(indices[i] * 4 + 0, nextFrame[0], animIndex[0], 0));
        n1 = TransformMap.Load(int4(indices[i] * 4 + 1, nextFrame[0], animIndex[0], 0));
        n2 = TransformMap.Load(int4(indices[i] * 4 + 2, nextFrame[0], animIndex[0], 0));
        n3 = TransformMap.Load(int4(indices[i] * 4 + 3, nextFrame[0], animIndex[0], 0));
        next = matrix(n0, n1, n2, n3);

        matrix result = lerp(curr, next, ratio[0]);

        if (animIndex[1] >= 0)
        {
            c0 = TransformMap.Load(int4(indices[i] * 4 + 0, currFrame[1], animIndex[1], 0));
            c1 = TransformMap.Load(int4(indices[i] * 4 + 1, currFrame[1], animIndex[1], 0));
            c2 = TransformMap.Load(int4(indices[i] * 4 + 2, currFrame[1], animIndex[1], 0));
            c3 = TransformMap.Load(int4(indices[i] * 4 + 3, currFrame[1], animIndex[1], 0));
            curr = matrix(c0, c1, c2, c3);

            n0 = TransformMap.Load(int4(indices[i] * 4 + 0, nextFrame[1], animIndex[1], 0));
            n1 = TransformMap.Load(int4(indices[i] * 4 + 1, nextFrame[1], animIndex[1], 0));
            n2 = TransformMap.Load(int4(indices[i] * 4 + 2, nextFrame[1], animIndex[1], 0));
            n3 = TransformMap.Load(int4(indices[i] * 4 + 3, nextFrame[1], animIndex[1], 0));
            next = matrix(n0, n1, n2, n3);

            matrix nextResult = lerp(curr, next, ratio[1]);
            result = lerp(result, nextResult, TweenFrames[input.instanceID].tweenRatio);
        }

        transform += mul(weights[i], result);
    }

    return transform;
}

MeshOutput VS_Animation(VertexModel input)
{
    MeshOutput output;

    matrix animMatrix = GetAnimationMatrix(input);

    // Bone Animation 후 MeshTransformMatrix 적용
    matrix finalMatrix = mul(animMatrix, MeshTransformMatrix);

    output.position = mul(input.position, finalMatrix);
    output.position = mul(output.position, input.world); // W
    output.worldPosition = output.position;
    output.position = mul(output.position, VP);
    output.uv = input.uv;
    output.normal = mul(input.normal, (float3x3) finalMatrix);
    output.tangent = mul(input.tangent, (float3x3) finalMatrix);

    return output;
}



MeshOutput VS_Animation_Static(VertexModel input)
{
    MeshOutput output;

    matrix animMatrix = GetAnimationMatrix(input);

    output.position = mul(input.position, animMatrix);
    output.position = mul(output.position, input.world); // W
    output.worldPosition = output.position;
    output.position = mul(output.position, VP);
    output.uv = input.uv;
    output.normal = mul(input.normal, (float3x3) animMatrix);
    output.tangent = mul(input.tangent, (float3x3) animMatrix);

    return output;
}



// ************** ParticleRender (추가됨) ****************

MeshOutput VS_Billboard(VertexMesh input)
{
    MeshOutput output;

    float4 worldPos = mul(float4(0, 0, 0, 1), W); // 중심 위치만 사용 (회전/스케일 없음)

    // 카메라 방향 계산
    float3 camRight = float3(V._11, V._21, V._31);
    float3 camUp = float3(V._12, V._22, V._32);

    // Quad 중심 기준 보정 (0.5 오프셋 기준)
    float3 offset = (input.uv.x - 0.5f) * camRight + (0.5f - input.uv.y) * camUp;
    float3 finalPos = worldPos.xyz + offset;

    output.position = mul(float4(finalPos, 1.0f), VP);
    output.uv = input.uv;

    return output;
}

#endif
