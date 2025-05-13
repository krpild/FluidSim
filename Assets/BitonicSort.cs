using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental;
using UnityEditor.VersionControl;
using UnityEngine;

public class BitonicSort : MonoBehaviour
{
    const int sortKernel = 0;
    const int calculateOffsetsKernel = 1;

    readonly ComputeShader compute;
    ComputeBuffer indexBuffer;

    public BitonicSort()
    {
        compute = Resources.Load<ComputeShader>("csSort".Split('.')[0]);
    }

    public void SetBuffers(ComputeBuffer indexBuffer, ComputeBuffer offsetBuffer)
    {
        this.indexBuffer = indexBuffer;
        
        compute.SetBuffer(sortKernel, "Entries", indexBuffer);
        compute.SetBuffer(calculateOffsetsKernel, "Offsets", offsetBuffer);
        compute.SetBuffer(calculateOffsetsKernel, "Entries", indexBuffer);
    }

    public void Sort()
    {
        compute.SetInt("numEntries", indexBuffer.count);

        int numStages = (int)Mathf.Log(Mathf.NextPowerOfTwo(indexBuffer.count), 2);

        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex; stepIndex++)
            {
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;
                compute.SetInt("groupWidth", groupWidth);
                compute.SetInt("groupHeight", groupHeight);
                compute.SetInt("stepIndex", stepIndex);

                uint x, y, z;
                
                compute.GetKernelThreadGroupSizes(sortKernel, out x, out y, out z);

                Vector3Int threadGroupSizes = new Vector3Int((int)x, (int)y, (int)z);

                int threadGroupX =
                    Mathf.CeilToInt((Mathf.NextPowerOfTwo(indexBuffer.count) / 2) / (float)threadGroupSizes.x);
                int threadGroupY = Mathf.CeilToInt(1 / (float)threadGroupSizes.y);
                int threadGroupZ = Mathf.CeilToInt(1 / (float)threadGroupSizes.z);
                
                compute.Dispatch(sortKernel, threadGroupX , threadGroupY, threadGroupZ);
            }
        }
    }

    public void SortAndCalculateOffsets()
    {
        Sort();

        uint x, y, z;
                
        compute.GetKernelThreadGroupSizes(sortKernel, out x, out y, out z);

        Vector3Int threadGroupSizes = new Vector3Int((int)x, (int)y, (int)z);

        int threadGroupX =
            Mathf.CeilToInt(indexBuffer.count / (float)threadGroupSizes.x);
        int threadGroupY = Mathf.CeilToInt(1 / (float)threadGroupSizes.y);
        int threadGroupZ = Mathf.CeilToInt(1 / (float)threadGroupSizes.z);
        
        compute.Dispatch(calculateOffsetsKernel, threadGroupX, threadGroupY, threadGroupZ);
    }
}
