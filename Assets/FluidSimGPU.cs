using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FluidSimGPU : MonoBehaviour
{
    public Vector2 Gravity = new Vector2(0, -10);
    public ParticleSystem particleSystem;
    public ParticleBox particleBox = new ParticleBox();
    private SphParticle2[] _particles;
    public ParticleSystem.Particle[] particleArray;
    public int particleCount;
    public float kernelRadius = 16f;
    public float mass = 2.5f;
    public float stiffness = 2000;
    public float restDensity = 300;
    public float viscocityConstant = 200f;
    public float timeStep = 0.0007f;
    public Gradient speedGradient;
    public float interactPressure;
    public float interactionRadius;
    public ComputeShader compute;

    public ComputeBuffer particleBuffer;
    public ComputeBuffer spatialIndices;
    public ComputeBuffer spatialOffsets;

    private float poly6Kernel;
    private float spikyGradient;
    private float viscosityLaplacian;

    private Vector2 boxMin;
    private Vector2 boxMax;
    
    private const int hashingKernel = 0;
    private const int densityKernel = 1;
    private const int forceKernel = 2;
    private const int velocitykernel = 3;
    private const int positionKernel = 4;
    private const int collisionKernel = 5;
    BitonicSort sort;
    
    

    private void Start()
    {
        Application.targetFrameRate = 50;

        //Initialising buffers
        spatialIndices = new ComputeBuffer(particleCount, 12);
        spatialOffsets = new ComputeBuffer(particleCount, 4);
        particleBuffer = new ComputeBuffer(particleCount, 32);
        
        PlaceParticlesRandomly();
        particleBuffer.SetData(_particles);

        boxMin = new Vector2(particleBox.min.x, particleBox.min.y);
        boxMax = new Vector2(particleBox.max.x, particleBox.max.y);

        
        //init compute for each kernel
        compute.SetBuffer(hashingKernel, "spatialIndices", spatialIndices);
        compute.SetBuffer(hashingKernel, "spatialOffsets", spatialOffsets);
        compute.SetBuffer(hashingKernel, "particles", particleBuffer);
        
        compute.SetBuffer(densityKernel, "spatialIndices", spatialIndices);
        compute.SetBuffer(densityKernel, "spatialOffsets", spatialOffsets);
        compute.SetBuffer(densityKernel, "particles", particleBuffer);
        
        compute.SetBuffer(forceKernel, "spatialIndices", spatialIndices);
        compute.SetBuffer(forceKernel, "spatialOffsets", spatialOffsets);
        compute.SetBuffer(forceKernel, "particles", particleBuffer);
        
        compute.SetBuffer(velocitykernel, "particles", particleBuffer);
        
        compute.SetBuffer(positionKernel, "particles", particleBuffer);
        
        compute.SetBuffer(collisionKernel, "particles", particleBuffer);
        
        
        compute.SetFloat("kernelRadius", kernelRadius);
        compute.SetInt("particleCount", particleCount);
        compute.SetFloat("mass", mass);
        compute.SetFloat("stiffness", stiffness);
        compute.SetFloat("restDensity", restDensity);
        compute.SetFloat("viscosityConstant", viscocityConstant);
        compute.SetFloats("gravity", 0, Gravity.y);
        compute.SetFloats("boxMin", boxMin.x, boxMin.y);
        compute.SetFloats("boxMax", boxMax.x, boxMax.y);
        compute.SetFloat("timestep", timeStep);
        
        poly6Kernel = 4f / (Mathf.PI * Mathf.Pow(kernelRadius, 8));
        spikyGradient = -10f / (Mathf.PI * Mathf.Pow(kernelRadius, 5));
        viscosityLaplacian =  40f / (Mathf.PI * Mathf.Pow(kernelRadius, 5f));
        
        compute.SetFloat("poly6KernelScalingFactor", poly6Kernel);
        compute.SetFloat("spikyGradientScalingFactor", spikyGradient);
        compute.SetFloat("viscosityLaplacianScalingFactor", viscosityLaplacian);

        sort = new ();
        sort.SetBuffers(spatialIndices, spatialOffsets);

        
    }

    void BugCheck()
    {
        Dispatch(compute, particleCount, kernelIndex: hashingKernel);
        sort.SortAndCalculateOffsets();
        Dispatch(compute, particleCount, kernelIndex: densityKernel); // okay
        

        Dispatch(compute, particleCount, kernelIndex: forceKernel); // okay (Force Calculations likely faulty)
        UpdateRender();

        MoveParticles(); // not okay

        Dispatch(compute, particleCount, kernelIndex: collisionKernel); //okay
        
    }
    void PlaceParticlesRandomly()
    {
        
        _particles = new SphParticle2[particleCount];
        particleArray = new ParticleSystem.Particle[particleCount];
        
        particleSystem.Emit(particleCount);
        particleSystem.GetParticles(particleArray); 

        for (int i = 0; i < particleCount; i++)
        {
            _particles[i].Position = new Vector2(
                Random.Range(particleBox.min.x, particleBox.max.x), 
                Random.Range(particleBox.min.y, particleBox.max.y));

            _particles[i].Velocity = Vector2.zero;
            _particles[i].Density = 0;
            _particles[i].Pressure = 0;

            // _particles[i].Velocity = new Vector2(
            //     Random.Range(-3f, 3f),
            //     Random.Range(-3f, 3f));
            
            particleArray[i].position = _particles[i].Position;
        }
        particleSystem.SetParticles(particleArray, particleCount);
    }

    private void Update()
    {
        Dispatch(compute, particleCount, kernelIndex: hashingKernel);
        sort.SortAndCalculateOffsets();
        Dispatch(compute, particleCount, kernelIndex: densityKernel);
        Dispatch(compute, particleCount, kernelIndex: forceKernel);
        MoveParticles();
        Dispatch(compute, particleCount, kernelIndex: collisionKernel);
        UpdateRender();
        UpdateParameters();
    }

    void UpdateParameters()
    {
        compute.SetFloat("kernelRadius", kernelRadius);
        compute.SetInt("particleCount", particleCount);
        compute.SetFloat("mass", mass);
        compute.SetFloat("stiffness", stiffness);
        compute.SetFloat("restDensity", restDensity);
        compute.SetFloat("viscosityConstant", viscocityConstant);
        compute.SetFloats("gravity", 0, Gravity.y);
        compute.SetFloats("boxMin", boxMin.x, boxMin.y);
        compute.SetFloats("boxMax", boxMax.x, boxMax.y);
        compute.SetFloat("timestep", timeStep);
    }

    void MoveParticles()
    {
        Dispatch(compute, particleCount, kernelIndex: velocitykernel);
        


        Dispatch(compute, particleCount, kernelIndex: positionKernel);

        Dispatch(compute, particleCount, kernelIndex: hashingKernel);
        sort.SortAndCalculateOffsets();
        Dispatch(compute, particleCount, kernelIndex: densityKernel);
        Dispatch(compute, particleCount, kernelIndex: forceKernel);
        Dispatch(compute, particleCount, kernelIndex: velocitykernel);
    }
    
    void UpdateRender()
    {
        particleBuffer.GetData(_particles);
        for (int i = 0; i < particleCount; i++)
        {
            // float t = Mathf.InverseLerp(0f, 200f, _particles[i].Velocity.magnitude);
            // particleArray[i].startColor = speedGradient.Evaluate(t);
            particleArray[i].position = _particles[i].Position;
        }
        particleSystem.SetParticles(particleArray, particleCount);
    }

    private void OnDestroy()
    {
        particleBuffer.Release();
        spatialOffsets.Release();
        spatialIndices.Release();
    }
    
    public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
    {
        uint x, y, z;
        compute.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
        return new Vector3Int((int)x, (int)y, (int)z);
    }
    
    public static void Dispatch(ComputeShader cs, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
    {
        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
        int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
        int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.z);
        cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
    }
}
