float MAX_OFFSET;
float TS; // tilesize;
float HEIGHT; // tileheight

struct OffsetData {
    float x;
    float offset;
    float z;
};

RWStructuredBuffer<float4> planes;
RWStructuredBuffer<float> scores;
RWStructuredBuffer<OffsetData> _Offset;
RWStructuredBuffer<uint> _FOV;
RWStructuredBuffer<float> fovscores;

float pointNotInPlane(in float4 pt, in float4 plane){
    // 1 if "behind plane", 0 if infront of plane
    return dot(plane, pt) < 0 ? 1 : 0;
}

void _ScorePlane (uint3 id : SV_DispatchThreadID)
{
    if (id.x >= MAX_OFFSET){
        return;
    }
    OffsetData d = _Offset[id.x];
    float c = 0;
    //bottom
    c += pointNotInPlane(float4(d.x, d.offset, d.z, 1), planes[id.y]);
    c += pointNotInPlane(float4(d.x + TS, d.offset, d.z, 1), planes[id.y]);
    c += pointNotInPlane(float4(d.x + TS, d.offset, d.z + TS, 1), planes[id.y]);
    c += pointNotInPlane(float4(d.x, d.offset, d.z + TS, 1), planes[id.y]);
    //top
    c += pointNotInPlane(float4(d.x, d.offset + HEIGHT, d.z, 1), planes[id.y]);
    c += pointNotInPlane(float4(d.x + TS, d.offset + HEIGHT, d.z, 1), planes[id.y]);
    c += pointNotInPlane(float4(d.x + TS, d.offset + HEIGHT, d.z + TS, 1), planes[id.y]);
    c += pointNotInPlane(float4(d.x, d.offset + HEIGHT, d.z + TS, 1), planes[id.y]);

    scores[6*id.x + id.y] = (c == 8) ? 0: 1;
}

void _SetFOV (uint3 id : SV_DispatchThreadID)
{
    uint idx = id.x;
    if (idx >= MAX_OFFSET){
        return;
    }
    OffsetData d = _Offset[idx];
    uint score = 0;
    uint start = idx * 6;
    [unroll]
    for ( uint i = 0; i < 6; i++ ){
        score += scores[start + i];
    }
    // cull out of fov
    float inView = score == 6 ? 1 : -1;
    float notSunken = d.offset > -100 ? 1 : -1;
    // // cull sunken while we're here
    _FOV[idx] = idx;
    fovscores[idx] = (inView + notSunken) > 1 ? -1 : 0;
}