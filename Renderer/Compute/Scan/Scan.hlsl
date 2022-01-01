
// Create a RenderTexture with enableRandomWrite flag and set it
// with cs.SetTexture

#define GRP (SCAN_GROUPSIZE) // handled by single wavelet && number in a wave group
#define GRPSQR (GRP * GRP)
RWStructuredBuffer<float> REDUCE_BLOCK;
RWStructuredBuffer<float> SCAN_VALUES;
RWStructuredBuffer<uint> DRAW_BUFFER;

groupshared float SHARED[GRP];

void _Scan (uint3 id)
{
    uint addr = id.x * GRP;
    int local = id.x % GRP;
    int cache[GRP];
    int prev = 0;
    int x = 0; 
    for (int i = 0; i < GRP; i ++){
        x = i + addr;
        cache[i] = prev + SCAN_VALUES[x];
        prev = cache[i];
    }
    SHARED[local] = cache[GRP - 1];
    GroupMemoryBarrierWithGroupSync();  // sync if large GRP
    prev = 0;
    for (i = local - 1; i >= 0; i --){  // avoid memory clash
        prev += SHARED[i];
    }
    for (i = 0; i < GRP; i ++){
        x = i + addr;
        SCAN_VALUES[x] = prev + cache[i];
    }
    if ((id.x + 1) % (uint) GRP  == 0 ){
        uint baddr = ((id.x + 1) - (uint) GRP) / (uint) GRP;
        REDUCE_BLOCK[baddr] = prev + cache[GRP - 1];
    }
}

// if we actually want to turn the original array into a proper scan
void _ReduceScan (uint3 id)
{
    uint addr = id.x * (uint) GRP;
    int local = id.x % (uint) GRP;
    uint end_baddr = (uint) floor( (uint) addr / (uint) GRPSQR);
    // get result of previous block
    float block_start = 0;
    for (uint bidx = 0; bidx < end_baddr; bidx ++){
        block_start = block_start + REDUCE_BLOCK[bidx];
    }
    int x = 0;
    for (int i = 0; i < GRP; i ++){
        x = i + addr;
        float old = SCAN_VALUES[x];
        SCAN_VALUES[x] = old + block_start;
    }
}

// We can just read the reduce blocks and last value to get the final count
void _SetDrawBuffer (uint3 id)
{
    uint stride = 0;
    uint count = 0;
    uint tSize = 0;
    SCAN_VALUES.GetDimensions(tSize, stride);
    count = (uint) SCAN_VALUES[tSize - 1];
    if (id.x == 0){
        DRAW_BUFFER[1] =  count;
    }
}