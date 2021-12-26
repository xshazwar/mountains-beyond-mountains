#define MAX_DIM_GROUPS 1024
#define MAX_DIM_THREADS (GROUP_SIZE * MAX_DIM_GROUPS)

int block;
int dim;
uint count;
RWStructuredBuffer<uint> Keys;
RWStructuredBuffer<float> Values;
StructuredBuffer<int> IntValues;

void _BitonicSort(uint3 id : SV_DispatchThreadID) {
	uint i = id.x + id.y * MAX_DIM_THREADS;
	uint j = i^block;
	
	if (j < i || i >= count) 
		return;
	
	uint key_i = Keys[i];
	uint key_j = Keys[j];
	float value_i = Values[key_i];
	float value_j = Values[key_j];
	
	float diff = (value_i - value_j) * ((i&dim) == 0 ? 1 : -1);
	if (diff > 0) {
		Keys[i] = key_j;
		Keys[j] = key_i;
	}
}

void _BitonicSortInt(uint3 id : SV_DispatchThreadID) {
	uint i = id.x + id.y * MAX_DIM_THREADS;
	uint j = i^block;
	
	if (j < i || i >= count) 
		return;
	
	uint key_i = Keys[i];
	uint key_j = Keys[j];
	int value_i = IntValues[key_i];
	int value_j = IntValues[key_j];
	
	int diff = (value_i - value_j) * ((i&dim) == 0 ? 1 : -1);
	if (diff > 0) {
		Keys[i] = key_j;
		Keys[j] = key_i;
	}
}

void _InitKeys(uint3 id : SV_DispatchThreadID) {
	uint i = id.x + id.y * MAX_DIM_THREADS;
	if (i < count)
		Keys[i] = i;
}
