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

    private bool _running = true;

    private const int CHUNK_SIZE = 16;

    public ChunkStreamingManager(
        GraphicsDevice gd,
        RenderPipeline pipeline,
        MeshIOCompressed meshReader,
        string meshFolder,
        int viewDistance,
        int uploadsPerFrame)
    {
        _gd = gd;
        _pipeline = pipeline;
        _meshReader = meshReader;
        _meshFolder = meshFolder;
        _viewDistance = viewDistance;
        _uploadsPerFrame = uploadsPerFrame;

        StartWorker();
    }

    // =========================
    // UPDATE LOOP
    // =========================
    public void Update(Vector3 cameraPosition)
    {
        var center = WorldToChunk(cameraPosition);

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
            int lod = GetStableLod(coord, center);
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
        for (int i = 0; i < _uploadsPerFrame; i++)
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
    private int GetStableLod(ChunkCoord chunk, ChunkCoord center)
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
            if (Math.Abs(current - desired) <= LOD_HYSTERESIS)
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
// using System.Collections.Concurrent;
// using System.Numerics;
// using Veldrid;
// using woscVeldrid;
//
// public class ChunkStreamingManager
// {
//     private readonly GraphicsDevice _gd;
//     private readonly RenderPipeline _pipeline;
//     private readonly MeshIOCompressed _meshReader;
//     private readonly string _meshFolder;
//
//     // private readonly Dictionary<ChunkCoord, GpuMesh> _loadedChunks = new();
//     private readonly Dictionary<ChunkCoord, (GpuMesh, int lod)> _loadedChunks = new();
//
//     // private readonly HashSet<ChunkCoord> _requestedChunks = new();
//     private readonly Dictionary<ChunkCoord, int> _requestedChunks = new();
//
//     // private readonly ConcurrentQueue<ChunkCoord> _requestQueue = new();
//     private readonly ConcurrentQueue<(ChunkCoord,int)> _requestQueue = new();
//     private readonly ConcurrentQueue<(ChunkCoord coord, Mesh mesh)> _readyQueue = new();
//
//     private readonly int _viewDistance = 6;
//     private readonly int _uploadsPerFrame = 2;
//
//     private bool _running = true;
//
//     private const int CHUNK_SIZE = 16;
//
//     public ChunkStreamingManager(
//         GraphicsDevice gd,
//         RenderPipeline pipeline,
//         MeshIOCompressed meshReader,
//         string meshFolder,
//         int viewDistance,
//         int uploadsPerFrame
//         )
//     {
//         _gd = gd;
//         _pipeline = pipeline;
//         _meshReader = meshReader;
//         _meshFolder = meshFolder;
//         _viewDistance = viewDistance;
//         _uploadsPerFrame = uploadsPerFrame;
//
//         StartWorker();
//     }
//
//     // =========================
//     // MAIN UPDATE (CALL EACH FRAME)
//     // =========================
//     public void Update(Vector3 cameraPosition)
//     {
//         var currentChunk = WorldToChunk(cameraPosition);
//         
//         foreach (var kv in _loadedChunks.ToList())
//         {
//             var coord = kv.Key;
//             var currentLod = kv.Value.lod;
//
//             var center = WorldToChunk(cameraPosition);
//             int desiredLod = SelectLod(coord, center);
//
//             if (desiredLod != currentLod)
//             {
//                 _requestQueue.Enqueue((coord, desiredLod));
//             }
//         }
//
//         RequestChunks(currentChunk);
//         UploadReadyMeshes();
//         UnloadFarChunks(currentChunk);
//     }
//
//     // =========================
//     // REQUEST CHUNKS
//     // =========================
//     private void RequestChunks(ChunkCoord center)
//     {
//         for (int x = -_viewDistance; x <= _viewDistance; x++)
//         for (int z = -_viewDistance; z <= _viewDistance; z++)
//         {
//             var coord = new ChunkCoord(center.X + x, center.Z + z);
//
//             // if (_loadedChunks.ContainsKey(coord) || _requestedChunks.Contains(coord))
//             //     continue;
//             
//             int lod = SelectLod(coord, center);
//             
//             if (_requestedChunks.TryGetValue(coord, out int oldLod))
//             {
//                 if (oldLod == lod)
//                     continue; // already requested same LOD
//             }
//
//             // _requestQueue.Enqueue(coord);
//             _requestQueue.Enqueue((coord,lod));
//             // _requestedChunks.Add(coord);
//             _requestedChunks[coord] = lod;
//
//         }
//     }
//
//     // =========================
//     // GPU UPLOAD (MAIN THREAD)
//     // =========================
//     private void UploadReadyMeshes()
//     {
//         
//         // int uploadsPerFrame = 2;
//
//         for (int i = 0; i < _uploadsPerFrame; i++)
//         {
//             if (!_readyQueue.TryDequeue(out var item))
//                 break;
//             // Console.WriteLine($"Uploading chunk {item.coord}");
//             
//             var gpuMesh = _pipeline.UploadMeshStreamed(_gd, item.mesh);
//             
//             gpuMesh.WorldPosition = new Vector3(item.coord.X * CHUNK_SIZE,0,item.coord.Z * CHUNK_SIZE);
//
//             // _loadedChunks[item.coord] = gpuMesh;
//             _loadedChunks[item.coord] = (gpuMesh,SelectLod(item.coord, WorldToChunk(gpuMesh.WorldPosition)));
//
//
//             // free CPU memory
//             item.mesh.Vertices = null;
//             item.mesh.Indices = null;
//             // item.mesh.ColorIds = null;
//             // Console.WriteLine($"GPU meshes: {_loadedChunks.Count}");
//         }
//     }
//
//     // =========================
//     // UNLOAD
//     // =========================
//     private void UnloadFarChunks(ChunkCoord center)
//     {
//         var toRemove = new List<ChunkCoord>();
//
//         foreach (var kv in _loadedChunks)
//         {
//             if (Distance(kv.Key, center) > _viewDistance + 2)
//             {
//                 // kv.Value.VertexBuffer.Dispose();
//                 // kv.Value.IndexBuffer.Dispose();
//                 kv.Value.Item1.VertexBuffer.Dispose();
//                 kv.Value.Item1.IndexBuffer.Dispose();
//                 toRemove.Add(kv.Key);
//             }
//         }
//
//         foreach (var coord in toRemove)
//         {
//             // _loadedChunks[coord].Dispose();
//             _loadedChunks[coord].Item1.Dispose();
//             _loadedChunks.Remove(coord);
//             _requestedChunks.Remove(coord);
//         }
//     }
//
//     // =========================
//     // WORKER THREAD
//     // =========================
//     private void StartWorker()
//     {
//         Task.Run(() =>
//         {
//             while (_running)
//             {
//                 if (!_requestQueue.TryDequeue(out var coord))
//                 {
//                     Thread.Sleep(1);
//                     continue;
//                 }
//
//                 try
//                 {
//                     LoadChunk(coord.Item1, coord.Item2);
//                 }
//                 catch (Exception e)
//                 {
//                     // Console.WriteLine($"Chunk load failed {coord.X},{coord.Z}: {e.Message}");
//                 }
//             }
//         });
//     }
//
//     private void LoadChunk(ChunkCoord coord, int lod)
//     {
//         
//         string file = GetRegionFile(coord, lod);
//
//         _meshReader.LoadFile(file);
//
//         int localX = Mod(coord.X, 32);
//         int localZ = Mod(coord.Z, 32);
//
//         var mesh = _meshReader.LoadChunk(localX, localZ);
//
//         if (mesh != null)
//         {
//             _readyQueue.Enqueue((coord, mesh));
//         }
//     }
//
//     // =========================
//     // HELPERS
//     // =========================
//     private ChunkCoord WorldToChunk(Vector3 pos)
//     {
//         int x = (int)MathF.Floor(pos.X / 16f);
//         int z = (int)MathF.Floor(pos.Z / 16f);
//         return new ChunkCoord(x, z);
//     }
//
//     private float Distance(ChunkCoord a, ChunkCoord b)
//     {
//         int dx = a.X - b.X;
//         int dz = a.Z - b.Z;
//         return MathF.Sqrt(dx * dx + dz * dz);
//     }
//
//     private int Mod(int x, int m)
//     {
//         return (x % m + m) % m;
//     }
//
//     private string GetRegionFile(ChunkCoord coord, int lod)
//     {
//         int regionX = (int)MathF.Floor(coord.X / 32f);
//         int regionZ = (int)MathF.Floor(coord.Z / 32f);
//
//         var folder = "";
//         if (lod >= 0)
//         {
//             folder = $"/lod{lod}";
//         }
//         // Console.WriteLine(Path.Combine(_meshFolder+folder, $"r.{regionX}.{regionZ}.mesh"));
//         return Path.Combine(_meshFolder+folder, $"r.{regionX}.{regionZ}.mesh");
//     }
//
//     public void Dispose()
//     {
//         _running = false;
//
//         foreach (var kv in _loadedChunks)
//         {
//             // kv.Value.VertexBuffer.Dispose();
//             // kv.Value.IndexBuffer.Dispose();
//             kv.Value.Item1.VertexBuffer.Dispose();
//             kv.Value.Item1.IndexBuffer.Dispose();
//         }
//     }
//
//     public IEnumerable<(GpuMesh,int)> GetMeshes()
//     {
//         return _loadedChunks.Values;
//     }
//     
//     private int SelectLod(ChunkCoord chunk, ChunkCoord center)
//     {
//         float dist = Distance(chunk, center);
//
//         if (dist < 2) return 0; // highest detail
//         if (dist < 4) return 1;
//         if (dist < 7) return 2;
//         if (dist < 10) return 3;
//         // if (dist < 20) return 3; // highest detail
//         // if (dist < 30) return 2;
//         // if (dist < 50) return 1;
//         // if (dist < 60) return 0;
//         return -1; // lowest detail
//     }
// }