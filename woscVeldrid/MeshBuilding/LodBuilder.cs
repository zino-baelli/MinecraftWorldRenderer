namespace woscVeldrid;

public class LodBuilder
{
    private float _initialError = 0.5f;
    private float _initialRatio = 0.3f;
    private float _lodRatioFactor = 1.0f;
    private float _lodErrorFactor = 1.0f;
    private float _maxError = 1.0f;
    private float _maxRatio = 1.0f;
    
    public LodBuilder(float error, float ratio, float maxError, float maxRatio)
    {
        _initialError = error;
        _initialRatio = ratio;
       _maxRatio = maxRatio;
       _maxError = maxError;
    }
    
    public  Dictionary<(int x, int z), Mesh>[] BuildLods(Dictionary<(int x, int z), Mesh> meshes, int levels)
    {
        var lods = new Dictionary<(int x, int z),Mesh>[levels];

        for (int l = 0; l < levels; l++)
        {
            lods[l] = new Dictionary<(int x, int z), Mesh>();
        }
        
        // for (int i = 0; i < levels; i++)
        // {
        //     var keys =  meshes.Keys;
        //     var lodMeshes =  new Dictionary<(int x, int z), Mesh>();
        //     
        //     float t = levels == 1 ? 0f : i / (float)(levels - 1);
        //     float error = Lerp(_initialError, _maxError, t);
        //     float ratio = Lerp(_initialRatio, _maxRatio, t);
        //
        //     foreach (var key in keys)
        //     {
        //         var mesh = meshes[key].Clone();
        //         mesh.Weld();
        //         mesh.SimplifySloppy(ratio, error);
        //         lodMeshes[key] = mesh;
        //     }
        //     lods[i] = lodMeshes;
        // }
        var keys = meshes.Keys;
        foreach (var key in keys)
        {
            var baseMesh =  meshes[key].Clone();
            baseMesh.Weld();
            
            for (int i = 0; i < levels; i++)
            {
                float t = levels == 1 ? 0f : i / (float)(levels - 1);
                float error = Lerp(_initialError, _maxError, t);
                float ratio = Lerp(_initialRatio, _maxRatio, t);

                var mesh = baseMesh.Clone();
                mesh.SimplifySloppy(ratio, error);
                lods[i][key] = mesh;
            }
        }
        
        return lods;
    }
    
    private float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
       
        
        
}