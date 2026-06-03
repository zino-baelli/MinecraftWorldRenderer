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

    private readonly Dictionary<ChunkCoord, GpuMesh> _loadedChunks = new();
    private readonly HashSet<ChunkCoord> _requestedChunks = new();

    private readonly ConcurrentQueue<ChunkCoord> _requestQueue = new();
    private readonly ConcurrentQueue<(ChunkCoord coord, Mesh mesh)> _readyQueue = new();

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
        int uploadsPerFrame
        )
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
    // MAIN UPDATE (CALL EACH FRAME)
    // =========================
    public void Update(Vector3 cameraPosition)
    {
        var currentChunk = WorldToChunk(cameraPosition);

        RequestChunks(currentChunk);
        UploadReadyMeshes();
        UnloadFarChunks(currentChunk);
    }

    // =========================
    // REQUEST CHUNKS
    // =========================
    private void RequestChunks(ChunkCoord center)
    {
        for (int x = -_viewDistance; x <= _viewDistance; x++)
        for (int z = -_viewDistance; z <= _viewDistance; z++)
        {
            var coord = new ChunkCoord(center.X + x, center.Z + z);

            if (_loadedChunks.ContainsKey(coord) || _requestedChunks.Contains(coord))
                continue;

            _requestQueue.Enqueue(coord);
            _requestedChunks.Add(coord);
        }
    }

    // =========================
    // GPU UPLOAD (MAIN THREAD)
    // =========================
    private void UploadReadyMeshes()
    {
        
        // int uploadsPerFrame = 2;

        for (int i = 0; i < _uploadsPerFrame; i++)
        {
            if (!_readyQueue.TryDequeue(out var item))
                break;
            // Console.WriteLine($"Uploading chunk {item.coord}");
            
            var gpuMesh = _pipeline.UploadMeshStreamed(_gd, item.mesh);
            
            gpuMesh.WorldPosition = new Vector3(item.coord.X * CHUNK_SIZE,0,item.coord.Z * CHUNK_SIZE);

            _loadedChunks[item.coord] = gpuMesh;

            // free CPU memory
            item.mesh.Vertices = null;
            item.mesh.Indices = null;
            item.mesh.ColorIds = null;
            // Console.WriteLine($"GPU meshes: {_loadedChunks.Count}");
        }
    }

    // =========================
    // UNLOAD
    // =========================
    private void UnloadFarChunks(ChunkCoord center)
    {
        var toRemove = new List<ChunkCoord>();

        foreach (var kv in _loadedChunks)
        {
            if (Distance(kv.Key, center) > _viewDistance + 2)
            {
                kv.Value.VertexBuffer.Dispose();
                kv.Value.IndexBuffer.Dispose();
                toRemove.Add(kv.Key);
            }
        }

        foreach (var coord in toRemove)
        {
            _loadedChunks.Remove(coord);
            _requestedChunks.Remove(coord);
        }
    }

    // =========================
    // WORKER THREAD
    // =========================
    private void StartWorker()
    {
        Task.Run(() =>
        {
            while (_running)
            {
                if (!_requestQueue.TryDequeue(out var coord))
                {
                    Thread.Sleep(1);
                    continue;
                }

                try
                {
                    LoadChunk(coord);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Chunk load failed {coord.X},{coord.Z}: {e.Message}");
                }
            }
        });
    }

    private void LoadChunk(ChunkCoord coord)
    {
        string file = GetRegionFile(coord);

        _meshReader.LoadFile(file);

        int localX = Mod(coord.X, 32);
        int localZ = Mod(coord.Z, 32);

        var mesh = _meshReader.LoadChunk(localX, localZ);

        if (mesh != null)
        {
            _readyQueue.Enqueue((coord, mesh));
        }
    }

    // =========================
    // HELPERS
    // =========================
    private ChunkCoord WorldToChunk(Vector3 pos)
    {
        int x = (int)MathF.Floor(pos.X / 16f);
        int z = (int)MathF.Floor(pos.Z / 16f);
        return new ChunkCoord(x, z);
    }

    private float Distance(ChunkCoord a, ChunkCoord b)
    {
        int dx = a.X - b.X;
        int dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private int Mod(int x, int m)
    {
        return (x % m + m) % m;
    }

    private string GetRegionFile(ChunkCoord coord)
    {
        int regionX = (int)MathF.Floor(coord.X / 32f);
        int regionZ = (int)MathF.Floor(coord.Z / 32f);

        return Path.Combine(_meshFolder, $"r.{regionX}.{regionZ}.mesh");
    }

    public void Dispose()
    {
        _running = false;

        foreach (var kv in _loadedChunks)
        {
            kv.Value.VertexBuffer.Dispose();
            kv.Value.IndexBuffer.Dispose();
        }
    }

    public IEnumerable<GpuMesh> GetMeshes()
    {
        return _loadedChunks.Values;
    }
}