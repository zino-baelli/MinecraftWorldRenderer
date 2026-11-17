using System.Collections.Concurrent;
using System.Numerics;
using Veldrid;
using woscVeldrid;

public class ChunkStreamingManager
{
    private readonly GraphicsDevice _gd;
    private readonly RenderPipeline _pipeline;
    private readonly MeshIOCompressed _meshReader;
    private readonly string _meshFolder;
    
    private readonly Dictionary<ChunkCoord, int> _stableLod = new();
    private const int LOD_HYSTERESIS = 1;

    // =========================
    // RENDERED STATE
    // =========================
    private readonly Dictionary<ChunkCoord, (GpuMesh mesh, int lod)> _loadedChunks = new();

    // latest desired LOD per chunk (STATE)
    private readonly ConcurrentDictionary<ChunkCoord, int> _latestLod = new();

    // work queue (SAFE + ORDERED)
    private readonly ConcurrentQueue<(ChunkCoord coord, int lod)> _queue = new();

    private readonly ConcurrentQueue<(ChunkCoord coord, Mesh mesh, int lod)> _readyQueue = new();

    private readonly int _viewDistance = 6;
    private readonly int _uploadsPerFrame = 2;
    private readonly LodSelectionType _selectionType = LodSelectionType.Distance;
    private float _projectionScale;
    private Vector3 _cameraPosition;

    private bool _running = true;

    private const int CHUNK_SIZE = 16;

    public ChunkStreamingManager(
        GraphicsDevice gd,
        RenderPipeline pipeline,
        MeshIOCompressed meshReader,
        string meshFolder,
        int viewDistance,
        int uploadsPerFrame,       
        float projectionScale,
        LodSelectionType selectionType = LodSelectionType.Distance
        )
    {
        _gd = gd;
        _pipeline = pipeline;
        _meshReader = meshReader;
        _meshFolder = meshFolder;
        _viewDistance = viewDistance;
        _uploadsPerFrame = uploadsPerFrame;
        _projectionScale = projectionScale;
        _selectionType = selectionType;

        StartWorker();
    }

    // =========================
    // UPDATE LOOP
    // =========================
    public void Update(Vector3 cameraPosition)
    {
        var center = WorldToChunk(cameraPosition);
        _cameraPosition = cameraPosition;

        RequestChunks(center);
        UploadReadyMeshes();
        UnloadFarChunks(center);
    }

    // =========================
    // REQUEST CHUNKS (LOD selection happens here)
    // =========================
    private void RequestChunks(ChunkCoord center)
    {
        for (int x = -_viewDistance; x <= _viewDistance; x++)
        for (int z = -_viewDistance; z <= _viewDistance; z++)
        {
            var coord = new ChunkCoord(center.X + x, center.Z + z);

            // int lod = SelectLod(coord, center);
            int lod = -1;
            if (_selectionType == LodSelectionType.Distance)
            {
               lod = GetStableDistanceLod(coord, center);
            }
            else if (_selectionType == LodSelectionType.ScreenSpace)
            {
               lod = GetStableScreenSpaceLod(coord);
            }
            else
            {
                lod = 3;
            }
            
            if (lod < 0)
                continue;

            if (_latestLod.TryGetValue(coord, out int oldLod) && oldLod == lod)
                continue;

            _latestLod[coord] = lod;
            _queue.Enqueue((coord, lod));
        }
    }

    // =========================
    // WORKER THREAD (stable consumer)
    // =========================
    private void StartWorker()
    {
        Task.Run(() =>
        {
            while (_running)
            {
                if (!_queue.TryDequeue(out var item))
                {
                    Thread.Sleep(1);
                    continue;
                }

                // discard outdated work
                if (_latestLod.TryGetValue(item.coord, out int latest) &&
                    latest != item.lod)
                {
                    continue;
                }

                LoadChunk(item.coord, item.lod);
            }
        });
    }

    // =========================
    // LOAD CHUNK (LOD-aware)
    // =========================
    private void LoadChunk(ChunkCoord coord, int lod)
    {
        string file = GetRegionFile(coord, lod);

        _meshReader.LoadFile(file);

        int localX = Mod(coord.X, 32);
        int localZ = Mod(coord.Z, 32);

        var mesh = _meshReader.LoadChunk(localX, localZ);

        if (mesh != null)
        {
            _readyQueue.Enqueue((coord, mesh, lod));
        }
    }

    // =========================
    // GPU UPLOAD (main thread only)
    // =========================
    private void UploadReadyMeshes()
    {
        for (int i = 0; i < _uploadsPerFrame && i < _readyQueue.Count ; i++)
        {
            if (!_readyQueue.TryDequeue(out var item))
                break;

            var gpuMesh = _pipeline.UploadMeshStreamed(_gd, item.mesh);

            gpuMesh.WorldPosition = new Vector3(
                item.coord.X * CHUNK_SIZE,
                0,
                item.coord.Z * CHUNK_SIZE
            );

            // _loadedChunks[item.coord] = (gpuMesh, item.lod);
            if (_loadedChunks.TryGetValue(item.coord, out var old))
            {
                old.mesh.VertexBuffer.Dispose();
                old.mesh.IndexBuffer.Dispose();
            }

            _loadedChunks[item.coord] = (gpuMesh, item.lod);

            item.mesh.Vertices = null;
            item.mesh.Indices = null;
        }
    }

    // =========================
    // UNLOAD FAR CHUNKS
    // =========================
    private void UnloadFarChunks(ChunkCoord center)
    {
        var toRemove = new List<ChunkCoord>();

        foreach (var kv in _loadedChunks)
        {
            if (Distance(kv.Key, center) > _viewDistance + 2)
            {
                kv.Value.mesh.VertexBuffer.Dispose();
                kv.Value.mesh.IndexBuffer.Dispose();
                toRemove.Add(kv.Key);
            }
        }

        foreach (var coord in toRemove)
        {
            _loadedChunks.Remove(coord);
            _latestLod.TryRemove(coord, out _);
        }
    }

    // =========================
    // LOD SELECTION
    // =========================
    // private int SelectLod(ChunkCoord chunk, ChunkCoord center)
    // {
    //     float dist = Distance(chunk, center);
    //
    //     // if (dist < 10) return 0;
    //     // if (dist < 20) return 1;
    //     // if (dist < 30) return 2;
    //     // if (dist < 40) return 3;
    //     // if (dist < 50) return 4;
    //     return 0;
    //     if (dist < 20) return 0;
    //     if (dist < 30) return 1;
    //     if (dist < 40) return 2;
    //     if (dist < 50) return 3;
    //     if (dist < 60) return 4;
    //
    //     return -1;
    // }
    private int GetStableDistanceLod(ChunkCoord chunk, ChunkCoord center)
    {
        float dist = Distance(chunk, center);

        int desired;

        if (dist < 20) desired = 0;
        else if (dist < 30) desired = 1;
        else if (dist < 40) desired = 2;
        else desired = 3;

        if (_stableLod.TryGetValue(chunk, out int current))
        {
            // only change if difference is big enough
            // if (Math.Abs(current - desired) <= LOD_HYSTERESIS)
            //     return current;
            
            if (desired < current)
            {
                _stableLod[chunk] = desired;
                return desired;
            }

            if (desired > current && (desired - current) <= LOD_HYSTERESIS)
            {
                return current;
            }
        }

        _stableLod[chunk] = desired;
        return desired;
    }

    // private int GetStableScreenSpaceLod(ChunkCoord chunk)
    // {
    //     float screenSize = ComputeScreenSize(chunk, _cameraPosition, _projectionScale);
    //
    //     int desired;
    //
    //     // same thresholds as before
    //     if (screenSize > 200) desired = 0;
    //     else if (screenSize > 100) desired = 1;
    //     else if (screenSize > 50)  desired = 2;
    //     else if (screenSize > 20)  desired = 3;
    //     else desired = -1; // culled
    //
    //     if (_stableLod.TryGetValue(chunk, out int current))
    //     {
    //         // handle "not rendered" case separately
    //         if (desired == -1)
    //         {
    //             // only drop to -1 if already very low detail
    //             if (current >= 3)
    //                 return -1;
    //
    //             return current;
    //         }
    //
    //         // if coming from -1, allow immediate upgrade
    //         if (current == -1)
    //         {
    //             _stableLod[chunk] = desired;
    //             return desired;
    //         }
    //
    //         // normal hysteresis (same as your distance version)
    //         // if (Math.Abs(current - desired) <= LOD_HYSTERESIS)
    //         //     return current;
    //         if (desired < current)
    //         {
    //             _stableLod[chunk] = desired;
    //             return desired;
    //         }
    //         if (desired > current && (desired - current) <= LOD_HYSTERESIS)
    //             return current;
    //     }
    //
    //     return current;
    // }
    
    private int GetStableScreenSpaceLod(ChunkCoord chunk)
    {
        float screenSize = ComputeScreenSize(chunk, _cameraPosition, _projectionScale);
        // Console.WriteLine($"screen={screenSize}");

        int desired;

        // if (screenSize > 200) desired = 0;
        // else if (screenSize > 100) desired = 1;
        // else if (screenSize > 50)  desired = 2;
        // else if (screenSize > 2)  desired = 3;
        // else desired = -1;
        
        // if (screenSize > 0.5f)       desired = 0; // very close
        // else if (screenSize > 0.25f) desired = 1;
        // else if (screenSize > 0.1f)  desired = 2;
        // else if (screenSize > 0.05f) desired = 3;
        // else                         desired = -1; // culled
        
        // if (screenSize > 0.08f)       desired = 0;
        // else if (screenSize > 0.065f) desired = 1;
        // else if (screenSize > 0.055f) desired = 2;
        // else if (screenSize > 0.045f) desired = 3;
        // else                          desired = -1;
        
        if (screenSize > 0.04f)      desired = 0;
        else if (screenSize > 0.03f)  desired = 1;
        else if (screenSize > 0.02f) desired = 2;
        else if (screenSize > 0.01f)  desired = 3;
        else                          desired = -1;

        // if (desired > -1)
        // {
        //     Console.WriteLine($" ss = {screenSize} lod = {desired}");
        // }

        if (!_stableLod.TryGetValue(chunk, out int current))
        {
            _stableLod[chunk] = desired;
            return desired;
        }

        
        if (desired == -1)
        {
            if (current >= 3)
            {
                _stableLod[chunk] = -1;
                return -1;
            }

            return current;
        }

        if (current == -1)
        {
            _stableLod[chunk] = desired;
            return desired;
        }

       

        if (desired < current)
        {
            _stableLod[chunk] = desired;
            return desired;
        }

        if (desired > current && (desired - current) <= LOD_HYSTERESIS)
        {
            return current;
        }
        
        _stableLod[chunk] = desired;
        return desired;
    }

    // =========================
    // FILE PATH
    // =========================
    private string GetRegionFile(ChunkCoord coord, int lod)
    {
        int regionX = (int)MathF.Floor(coord.X / 32f);
        int regionZ = (int)MathF.Floor(coord.Z / 32f);

        string folder = Path.Combine(_meshFolder, $"lod{lod}");

        return Path.Combine(folder, $"r.{regionX}.{regionZ}.mesh");
    }

    // =========================
    // HELPERS
    // =========================
    private ChunkCoord WorldToChunk(Vector3 pos)
    {
        return new ChunkCoord(
            (int)MathF.Floor(pos.X / 16f),
            (int)MathF.Floor(pos.Z / 16f)
        );
    }

    private float Distance(ChunkCoord a, ChunkCoord b)
    {
        int dx = a.X - b.X;
        int dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
    
    // float ComputeScreenSize(ChunkCoord chunk, Vector3 cameraPos, float projectionScale)
    // {
    //     Vector3 chunkCenter = new Vector3(
    //         chunk.X * CHUNK_SIZE + CHUNK_SIZE * 0.5f,
    //         0,
    //         chunk.Z * CHUNK_SIZE + CHUNK_SIZE * 0.5f
    //     );
    //
    //     float distance = Vector3.Distance(cameraPos, chunkCenter);
    //
    //     // avoid division by zero
    //     distance = MathF.Max(distance, 0.001f);
    //
    //     return (CHUNK_SIZE / distance) * projectionScale;
    // }
    
    float ComputeScreenSize(ChunkCoord chunk, Vector3 cameraPos, float projectionScale)
    {
        Vector3 chunkCenter = new Vector3(
            chunk.X * CHUNK_SIZE + CHUNK_SIZE * 0.5f,
            0,
            chunk.Z * CHUNK_SIZE + CHUNK_SIZE * 0.5f
        );

        float distance = Vector3.Distance(cameraPos, chunkCenter);

        distance = MathF.Max(distance, CHUNK_SIZE);
        distance = MathF.Max(distance, 0.001f);

        float radius = CHUNK_SIZE * 0.707f;

        // return (radius / distance) * projectionScale;
        return radius / distance;
    }

    private int Mod(int x, int m) => (x % m + m) % m;

    public IEnumerable<(GpuMesh mesh, int lod)> GetMeshes()
        => _loadedChunks.Values;

    public void Dispose()
    {
        _running = false;

        foreach (var kv in _loadedChunks)
        {
            kv.Value.mesh.VertexBuffer.Dispose();
            kv.Value.mesh.IndexBuffer.Dispose();
        }
    }
}
