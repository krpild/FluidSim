using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FluidSim2 : MonoBehaviour
{
    public Vector2 Gravity = new Vector2(0, -10);
    public ParticleSystem particleSystem;
    public ParticleBox particleBox = new ParticleBox();
    private SphParticle2[] _particles;
    public ParticleSystem.Particle[] particleArray;
    public int particleCount;
    public float kernelRadius = 16f;
    // private float poly6Kernel = 4f / (Mathf.PI * Mathf.Pow(kernelRadius, 8));
    // private float spikyGradient = -10f / (Mathf.PI * Mathf.Pow(kernelRadius, 5));
    // private float viscocityLaplacian = 40f / (Mathf.PI * Mathf.Pow(kernelRadius, 5f));
    public float mass = 2.5f;
    public float stiffness = 2000;
    public float restDensity = 300;
    public float viscocityConstant = 200f;
    public float timeStep = 0.0007f;
    public Gradient speedGradient;
    public float interactPressure;
    public float interactionRadius;
    private Dictionary<int, List<int>> hashTable = new Dictionary<int, List<int>>();
    private Dictionary<int, List<int>> neighbors = new Dictionary<int, List<int>>();



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

            // _particles[i].Velocity = new Vector2(
            //     Random.Range(-3f, 3f),
            //     Random.Range(-3f, 3f));
            
            particleArray[i].position = _particles[i].Position;
        }
        particleSystem.SetParticles(particleArray, particleCount);
    }

    float Poly6Kernel()
    {
        return 4f / (Mathf.PI * Mathf.Pow(kernelRadius, 8));
    }

    float SpikyGradient()
    {
        return -10f / (Mathf.PI * Mathf.Pow(kernelRadius, 5));
    }

    float ViscocityLaplacian()
    {
        return 40f / (Mathf.PI * Mathf.Pow(kernelRadius, 5f));
    }
    
    void Start()
    {
        Application.targetFrameRate = 50;
        PlaceParticlesRandomly();
    }
    
    void Update()
    {
        
        hashTable.Clear();
        AddToHashTable();
        FindAllNeighbors();
        ComputeDensity();
        ComputeForces();
        if (Input.GetMouseButton(0))
        {
            Interact(0);
        }
        if (Input.GetMouseButton(1))
        {
            Interact(1);
        }
        MoveParticles();
        UpdateRender();
        
    }

    void UpdateRender()
    {
        for (int i = 0; i < particleCount; i++)
        {
            particleArray[i].position = _particles[i].Position;
            float t = Mathf.InverseLerp(0f, 200f, _particles[i].Velocity.magnitude);
            particleArray[i].startColor = speedGradient.Evaluate(t);
        }
        particleSystem.SetParticles(particleArray, particleCount);
    }

    void ComputeDensity()
    {
        float poly6Kernel = Poly6Kernel();
        for (int i = 0; i < particleCount; i++)
        {
            _particles[i].Density = 0f;
            List<int> iNeighbors = this.neighbors[i];

            for (int j = 0; j < iNeighbors.Count; j++)
            {
                Vector2 vectorDistance = _particles[iNeighbors[j]].Position - _particles[i].Position;
                float distance = vectorDistance.sqrMagnitude;
                if (distance < kernelRadius)
                {
                    _particles[i].Density += mass * poly6Kernel * Mathf.Pow(Mathf.Pow(kernelRadius, 2) - distance, 3);
                }
            }

            _particles[i].Pressure = stiffness * (_particles[i].Density - restDensity);
        }
    }

    void ComputeForces()
    {
        var spikyGradient = SpikyGradient();
        var viscosityLaplacian = ViscocityLaplacian();
        for (int i = 0; i < particleCount; i++)
        {
            Vector2 pressureForce = Vector2.zero;
            Vector2 viscosityForce = Vector2.zero;
            //We check neighbors here and then iterate through them
            List<int> iNeighbors = this.neighbors[i];
            
            for (int j = 0; j < iNeighbors.Count; j++)
            {
                if (i == iNeighbors[j]) continue;
                Vector2 vectorDistance = _particles[iNeighbors[j]].Position - _particles[i].Position;
                var distance = vectorDistance.magnitude;

                if (distance < kernelRadius)
                {
                    pressureForce += -vectorDistance.normalized * mass *
                                     (_particles[i].Pressure + _particles[iNeighbors[j]].Pressure) / (2f * _particles[iNeighbors[j]].Density) *
                                     spikyGradient * Mathf.Pow(kernelRadius - distance, 3f);
                    
                    viscosityForce += viscocityConstant * mass * (_particles[iNeighbors[j]].Velocity - _particles[i].Velocity) /
                        _particles[iNeighbors[j]].Density * viscosityLaplacian * (kernelRadius - distance);
                }
            }
            Vector2 gravityForce = Gravity * mass / _particles[i].Density;
            _particles[i].Force = pressureForce + viscosityForce + gravityForce;
        }
    }

    void MoveParticles()
    {
        for (int i = 0; i < particleCount; i++)
        {
            Vector2 velocityHalfStep = _particles[i].Velocity + (_particles[i].Force / mass) * (timeStep / 2);
            _particles[i].Velocity = velocityHalfStep;
        }

        for (int i = 0; i < particleCount; i++)
        {
            _particles[i].Position += _particles[i].Velocity * timeStep;
        }   
        // Find neighbors 
        hashTable.Clear();
        AddToHashTable();
        FindAllNeighbors();
        ComputeDensity();
        ComputeForces();
        if (Input.GetMouseButton(0))
        {
            Interact(0);
        }
        if (Input.GetMouseButton(1))
        {
            Interact(1);
        }

        for (int i = 0; i < particleCount; i++)
        {
            _particles[i].Velocity = _particles[i].Velocity + (_particles[i].Force / mass) * (timeStep / 2);
        }
        for (int i = 0; i < particleCount; i++)
        {
            
            
            if (_particles[i].Position.x  < particleBox.min.x)
            {
                _particles[i].Velocity.x *= -0.9f;
                
                _particles[i].Position.x = particleBox.min.x;
            }
            else if (_particles[i].Position.x > particleBox.max.x)
            {
                _particles[i].Velocity.x *= -0.9f;

                _particles[i].Position.x = particleBox.max.x;
            }
        
            if (_particles[i].Position.y < particleBox.min.y)
            {
                _particles[i].Velocity.y *= -0.5f;

                _particles[i].Position.y = particleBox.min.y;
            
            }
            else if (_particles[i].Position.y > particleBox.max.y)
            {
                _particles[i].Velocity.y *= -0.9f;
                
                _particles[i].Position.y = particleBox.max.y;
            }
        }
        
    }

    void Interact(int mouseButton)
    {
        var spikyGradient = SpikyGradient();

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane + 97f));

        Vector2 mousePos2d = new Vector2(mousePosition.x, mousePosition.y);
        

        for (int i = 0; i < particleCount; i++)
        {
            Vector2 distanceVector = mousePos2d - _particles[i].Position;
            float distance = distanceVector.magnitude;

            if (distance < interactionRadius)
            {
                Vector2 pressureForce = -distanceVector.normalized *
                                        (interactPressure) / 0.1f *
                                        spikyGradient * Mathf.Pow(kernelRadius - distance, 3f);
                if (mouseButton > 0)
                {
                    _particles[i].Force -= pressureForce;
                }
                else
                {
                    _particles[i].Force += pressureForce;
                }
               
            }
        }
    }

    void FindAllNeighbors()
    {
        neighbors.Clear();
        for (int i = 0; i < particleCount; i++)
        {
            if (!neighbors.ContainsKey(i))
                neighbors[i] = GetNeighbors(_particles[i]);
        }
    }
    

    List<int> GetNeighbors(SphParticle2 particle)
    {
        List<int> neighbors = new List<int>();
        int cellX = Mathf.FloorToInt(particle.Position.x / kernelRadius);
        int cellY = Mathf.FloorToInt(particle.Position.y / kernelRadius);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                int neighborCellHash = ComputeHash(cellX + x, cellY + y);
                
                if (hashTable.TryGetValue(neighborCellHash, out var value))
                {
                    neighbors.AddRange(value);
                }
            }
        }

        return neighbors;
    }

    void AddToHashTable()
    {
        for (int i = 0; i < particleCount; i++)
        {
            int cellX = Mathf.FloorToInt(_particles[i].Position.x / kernelRadius);
            int cellY = Mathf.FloorToInt(_particles[i].Position.y / kernelRadius);
            int hash = ComputeHash(cellX, cellY);
            
            if (!hashTable.ContainsKey(hash))
                hashTable[hash] = new List<int>();

            hashTable[hash].Add(i);
        }
    }

    int ComputeHash(int x, int y)
    {
        int prime1 = 73856093;
        int prime2 = 19349663;
        
        return (x * prime1) ^ (y * prime2);
    }
    
    

    
}
