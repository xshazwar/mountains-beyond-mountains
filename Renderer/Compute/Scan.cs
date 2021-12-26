// // Compute how many thread groups we will need.
// int threadGroupCount = count / threadsPerGroup;
 
// // ScanInBucket.
// int scanInBucketKernel = exclusive ? _scanInBucketExclusiveKernel : _scanInBucketInclusiveKernel;
// _computeShader.SetBuffer( scanInBucketKernel, _inputPropId, countBuffer.computeBuffer );
// _computeShader.SetBuffer( scanInBucketKernel, _resultPropId, resultBuffer.computeBuffer );
// _computeShader.Dispatch( scanInBucketKernel, threadGroupCount, 1, 1 );
 
// // ScanBucketResult.
// _computeShader.SetBuffer( _scanBucketResultKernel, _inputPropId, resultBuffer.computeBuffer );
// _computeShader.SetBuffer( _scanBucketResultKernel, _resultPropId, _auxBuffer.computeBuffer );
// _computeShader.Dispatch( _scanBucketResultKernel, 1, 1, 1 );
 
// // ScanAddBucketResult.
// _computeShader.SetBuffer( _scanAddBucketResultKernel, _inputPropId, _auxBuffer.computeBuffer );
// _computeShader.SetBuffer( _scanAddBucketResultKernel, _resultPropId, resultBuffer.computeBuffer );
// _computeShader.Dispatch( _scanAddBucketResultKernel, threadGroupCount, 1, 1 );