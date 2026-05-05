namespace woscVeldrid;

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;


public class RenderPipeline
{
    
    private DeviceBuffer _vertexBuffer;
    private DeviceBuffer _indexBuffer;
    private DeviceBuffer _cameraBuffer;
    private DeviceBuffer _timeBuffer;
    private DeviceBuffer _pointSettingsBuffer;
    private List<DeviceBuffer> _vertexBuffers = new();
    private List<DeviceBuffer> _indexBuffers = new();
    
    private ResourceLayout _vertexLayout;
    private ResourceLayout _cameraLayout;
    private ResourceLayout _timeLayout;
    private ResourceLayout _pointSettingsLayout;
    private ResourceSet _cameraSet;
    private ResourceSet _timeSet;
    private ResourceSet _pointSettingsSet;
    
    private Matrix4x4[]  _cameraMatrices = new Matrix4x4[3];
    private Veldrid.Shader[] _shaders;
    private Pipeline _pipeline;
    private CommandList _commandList;
    private int _indexCount;
    private int _vertexCount;
    private string _vertexCode;
    private string _fragmentCode;
    private int _width;
    private int _height;
    private float _pointSize = 10;
    private int _maxPointsPerChunk = 10000;
    private int _maxAmountChunks = 10;
    private const float _triSize = 0.1f;
    private Vector3 _cameraPosition;
    private float _time;

    
    public RenderPipeline(string vertexCode, string fragmentCode, int width, int height)
    {
        _vertexCode = vertexCode;
        _fragmentCode = fragmentCode;
        _width = width;
        _height = height;
    }
    
    public void CreateResources(
        ref GraphicsDevice graphicsDevice,
        PrimitiveTopology topology
        )
    {
        
        ResourceFactory factory = graphicsDevice.ResourceFactory;
    
        const float triSize = 0.1f; // small triangle side length
    
        // Build vertex list: 3 vertices per depth point
        List<VertexPositionColor> vertices = new();
        List<ushort> indices = new();
        
        //model Matrix
        _cameraMatrices[0] = Matrix4x4.Identity;
        
        //view Matrix
        var cameraPosition =  new Vector3(0, 5,-2);
        var target = new Vector3(0, 0, 0);
        _cameraMatrices[1] = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
        
        //projection Matrix with 60deg fov
        _cameraMatrices[2] = Matrix4x4.CreatePerspectiveFieldOfView(1.04719755f, _width/_height, 0.1f, 1000);
        
        ushort baseIndex = 0;
        var colorIndex = 0;
        
        // foreach (var p in depthPoints)
        var p = new Vector3(0, 1, 0);
        {
            // Build a small triangle at the point
            Vector3 v0 = new Vector3(p.X, p.Y + triSize, p.Z);
            Vector3 v1 = new Vector3(p.X - triSize, p.Y - triSize, p.Z);
            Vector3 v2 = new Vector3(p.X + triSize, p.Y - triSize, p.Z);
            
            vertices.Add(new VertexPositionColor(v0, RgbaFloat.Blue));
            vertices.Add(new VertexPositionColor(v1, RgbaFloat.Blue));
            vertices.Add(new VertexPositionColor(v2, RgbaFloat.Blue));
        
            indices.Add((ushort)(baseIndex + 0));
            indices.Add((ushort)(baseIndex + 1));
            indices.Add((ushort)(baseIndex + 2));
        
            baseIndex += 3;
        }
        

        
        _indexCount = indices.Count;
    
        // Create buffers
        _vertexBuffer = factory.CreateBuffer(
            new BufferDescription((uint)(_width * _height * 10 * VertexPositionColor.SizeInBytes),
                BufferUsage.VertexBuffer));

        _indexBuffer = factory.CreateBuffer(
            new BufferDescription(
                (uint)(_width * _height * 10 * sizeof(ushort)),
                BufferUsage.IndexBuffer));
        

        // _colorBuffer = factory.CreateBuffer(
        //     new BufferDescription((uint)(_width * _height * sizeof(float) * 3),
        //         BufferUsage.VertexBuffer)
        // );
        
        _timeBuffer = factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        
        _cameraBuffer = factory.CreateBuffer(new BufferDescription(192, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        
        _pointSettingsBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
    
        // graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        // graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());
        graphicsDevice.UpdateBuffer(_cameraBuffer, 0, _cameraMatrices);
        var pointData = new float[4] { _pointSize, 0f, 0f, 0f };
        graphicsDevice.UpdateBuffer(_pointSettingsBuffer, 0, pointData);
    
        // Create vertex layout
        VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
            new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4));
        // VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
        //     new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
        //     new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));

        _cameraLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("CameraBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        ));

        _cameraSet = factory.CreateResourceSet(new ResourceSetDescription(_cameraLayout, _cameraBuffer));

        _pointSettingsLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("PointSettings", ResourceKind.UniformBuffer, ShaderStages.Vertex)
        ));
        
        _pointSettingsSet = factory.CreateResourceSet(new ResourceSetDescription(_pointSettingsLayout, _pointSettingsBuffer));
        
        _timeLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription("TimeBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

        _timeSet = factory.CreateResourceSet(new ResourceSetDescription(_timeLayout, _timeBuffer));
        
        // Load shaders
        ShaderDescription vertexShaderDesc = new ShaderDescription(
            ShaderStages.Vertex,
            Encoding.UTF8.GetBytes(_vertexCode),
            "main");
    
        ShaderDescription fragmentShaderDesc = new ShaderDescription(
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(_fragmentCode),
            "main");
        
    
        _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
    
        // Pipeline
        GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            DepthStencilState = new DepthStencilStateDescription(
                depthTestEnabled: true,
                depthWriteEnabled: true,
                comparisonKind: ComparisonKind.LessEqual),
            RasterizerState = new RasterizerStateDescription(
                FaceCullMode.None,
                PolygonFillMode.Solid,
                FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: false),
            // PrimitiveTopology = PrimitiveTopology.TriangleList,
            PrimitiveTopology = topology,
            // ResourceLayouts = Array.Empty<ResourceLayout>(),
            ResourceLayouts = new[] { _cameraLayout, _pointSettingsLayout, _timeLayout },
            ShaderSet = new ShaderSetDescription(
                new[] { vertexLayout },
                _shaders),
            Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription
        };
    
        _pipeline = factory.CreateGraphicsPipeline(ref pipelineDescription);
    
        _commandList = factory.CreateCommandList();
    }

    public void UpdateMap(ref GraphicsDevice graphicsDevice, Mesh mesh)
    {
        var vertices = new List<VertexPositionColor>();
        var indices = new List<ushort>();
        
        
        var colorIndex = 0;
        foreach (var vertex in mesh.Vertices)
        {
            vertices.Add(new VertexPositionColor(vertex.Position, RgbaFloat.White));
        }

        foreach (var meshIndex in mesh.Indices)
        {
            indices.Add((ushort)(meshIndex));
        }
        
        graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());
    }
    
    public void Update( ref GraphicsDevice graphicsDevice)
    {
       
        var vertices = new List<VertexPositionColor>();
        var indices = new List<ushort>();
        // UpdateCameraMatrices(pointcloud, ref graphicsDevice);
        ushort baseIndex = 0;
        
        var colorIndex = 0;
        // foreach (var p in depthPoints)
        var p = new Vector3(0, 1, 0);
        {
            // Build a small triangle at the point
            Vector3 v0 = new Vector3(p.X, p.Y + _triSize, p.Z);
            Vector3 v1 = new Vector3(p.X - _triSize, p.Y - _triSize, p.Z);
            Vector3 v2 = new Vector3(p.X + _triSize, p.Y - _triSize, p.Z);
            
          
            var color = RgbaFloat.White;

            // var colorData = pointcloud.colors[colorIndex];

            // color = new RgbaFloat(colorData.X,  colorData.Y, colorData.Z, 1) ; 
            colorIndex++;
            
            vertices.Add(new VertexPositionColor(v0, color));
            vertices.Add(new VertexPositionColor(v1, color));
            vertices.Add(new VertexPositionColor(v2, color));
            
            indices.Add((ushort)(baseIndex + 0));
            indices.Add((ushort)(baseIndex + 1));
            indices.Add((ushort)(baseIndex + 2));
            
            baseIndex += 3;
            
        }
    
        _vertexCount = vertices.Count;
        
        graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());
    }
    
    

    // private void UpdateCameraMatrices(Pointcloud pointcloud, ref GraphicsDevice graphicsDevice)
    // {
    //     //model Matrix
    //     _cameraMatrices[0] = Matrix4x4.Identity;
    //     Console.WriteLine(_pointConfig.CameraRotationX);
    //     //view Matrix
    //     var rotation = Matrix4x4.CreateRotationX(_pointConfig.CameraRotationX) * 
    //                    Matrix4x4.CreateRotationY(_pointConfig.CameraRotationY)  * 
    //                    Matrix4x4.CreateRotationZ(_pointConfig.CameraRotationZ);
    //     var forward =Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));
    //  
    //     // var target = new Vector3(0, 1, 0);
    //     var target = _pointConfig.GetCameraPosition() + forward;
    //     _cameraMatrices[1] = Matrix4x4.CreateLookAt(_pointConfig.GetCameraPosition(), target, Vector3.UnitY);
    //     
    //     //projection Matrix with 60deg fov
    //     _cameraMatrices[2] = Matrix4x4.CreatePerspectiveFieldOfView(1.04719755f, _width/_height, 0.1f, pointcloud.farthestPoint);
    //     graphicsDevice.UpdateBuffer(_cameraBuffer, 0, _cameraMatrices.ToArray());
    //
    // }
    

    
    
    public void Draw(ref GraphicsDevice graphicsDevice)
    {
        _commandList.Begin();
        _commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
        
        _commandList.ClearColorTarget(0, RgbaFloat.Black);       // clear to black (or any color)
        // _commandList.ClearDepthStencil(1f);                     // clear depth to 1.0 (max)
        
        _commandList.SetPipeline(_pipeline);
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _commandList.SetGraphicsResourceSet(0, _cameraSet);
        _commandList.SetGraphicsResourceSet(1, _pointSettingsSet);
        _commandList.SetGraphicsResourceSet(2, _timeSet); 
        // _commandList.DrawIndexed(
        //     indexCount: (uint)_indexCount,
        //     instanceCount: 1,
        //     indexStart: 0,
        //     vertexOffset: 0,
        //     instanceStart: 0);
        _commandList.Draw(
            vertexCount: (uint)_vertexCount,
            instanceCount: 1,
            vertexStart: 0,
            instanceStart: 0
        );
        _commandList.End();

        graphicsDevice.SubmitCommands(_commandList);
        graphicsDevice.SwapBuffers();
    }
    
    
    struct VertexPositionColor
    {
        public Vector3 Position;
        public RgbaFloat Color;

        public const uint SizeInBytes = 12 + 16;

        public VertexPositionColor(Vector3 pos, RgbaFloat color)
        {
            Position = pos;
            Color = color;
        }
    }
    
    struct TimeData
    {
        public float uTime;
        public float uA;
        public float uB;
        public float uC;

        public float _pad0;
        public float _pad1;
        public float _pad2;
        public float _pad3;
    }
    
    public void UpdateTime(ref GraphicsDevice graphicsDevice, float deltaSeconds)
    {
        _time += deltaSeconds;
        TimeData tdata = new TimeData
        {
            uTime = _time,
            // uA = _pointConfig.A,
            // uB = _pointConfig.B,
            // uC = _pointConfig.C
        };
        graphicsDevice.UpdateBuffer(_timeBuffer, 0, ref tdata);
        // Console.WriteLine(_time);
    }

    public void DisposeResources()
    {
        _pipeline.Dispose();
        _commandList.Dispose();
        // _vertexBuffer.Dispose();
        // _indexBuffer.Dispose();
    }

    struct TimeUniform
    {
        public float Time;
        private Vector3 _padding;
    }
}

