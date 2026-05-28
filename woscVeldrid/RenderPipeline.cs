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
    private DeviceBuffer _paletteBuffer;
    private List<DeviceBuffer> _vertexBuffers = new();
    private List<DeviceBuffer> _indexBuffers = new();
    
    private ResourceLayout _vertexLayout;
    private ResourceLayout _cameraLayout;
    private ResourceLayout _timeLayout;
    private ResourceLayout _pointSettingsLayout;
    private ResourceLayout _paletteLayout;
    private ResourceSet _cameraSet;
    private ResourceSet _timeSet;
    private ResourceSet _pointSettingsSet;
    private ResourceSet _paletteSet;
    
    private Texture _depthTexture;
    // private Framebuffer _framebuffer;
    
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
    private Vector3 _cameraRotation;
    private Vector3 _cameraTarget;
    private float _time;
    private Dictionary<int, Vector3> _colorPalette;
    private List<GpuMesh> _gpuMeshes = new();

    
    public RenderPipeline(string vertexCode, string fragmentCode, int width, int height)
    {
        _vertexCode = vertexCode;
        _fragmentCode = fragmentCode;
        _width = width;
        _height = height;
    }
    
    public void CreateResources(
        ref GraphicsDevice graphicsDevice,
        PrimitiveTopology topology,
        Mesh[] meshes,
        Dictionary<int,  Vector3> colorPalette
        )
    {
        
        ResourceFactory factory = graphicsDevice.ResourceFactory;
    
        const float triSize = 0.1f; // small triangle side length
        
        _colorPalette = colorPalette;
    
        // Build vertex list: 3 vertices per depth point
        // List<VertexPositionColor> vertices = new();
        // List<VertexPositionNormal> vertices = new();
        List<VertexPositionNormalColor> vertices = new();
        List<uint> indices = new();
        
        _cameraRotation = Vector3.Zero;
        var rotation = Matrix4x4.CreateRotationX(_cameraRotation.X) * 
                       Matrix4x4.CreateRotationY(_cameraRotation.Y)  * 
                       Matrix4x4.CreateRotationZ(_cameraRotation.Z);
        
        //model Matrix
        _cameraMatrices[0] = Matrix4x4.Identity;
        
        //view Matrix
        var cameraPosition =  new Vector3(0, 5,-2);
        // var cameraPosition =  new Vector3(6000, 1,1500);
        _cameraPosition = cameraPosition;
        
        var forward =Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));


        // var target = new Vector3(0, 0, 0);
        var target = _cameraPosition + forward;
        // _cameraTarget = new Vector3(6100, 1, 1520);

        _cameraMatrices[1] = Matrix4x4.CreateLookAt(cameraPosition, _cameraTarget, Vector3.UnitY);
        // _cameraMatrices[1] = Matrix4x4.CreateLookAt(cameraPosition, _cameraForward, Vector3.UnitY);

        
        //projection Matrix with 60deg fov
        _cameraMatrices[2] = Matrix4x4.CreatePerspectiveFieldOfView(1.04719755f, _width/_height, 0.1f, 1000);
        
        uint baseIndex = 0;
        var colorIndex = 0;
        
        // foreach (var p in depthPoints)
        // var p = new Vector3(0, 1, 0);
        // {
        //     // Build a small triangle at the point
        //     Vector3 v0 = new Vector3(p.X, p.Y + triSize, p.Z);
        //     Vector3 v1 = new Vector3(p.X - triSize, p.Y - triSize, p.Z);
        //     Vector3 v2 = new Vector3(p.X + triSize, p.Y - triSize, p.Z);
        //     
        //     // vertices.Add(new VertexPositionColor(v0, RgbaFloat.Blue));
        //     // vertices.Add(new VertexPositionColor(v1, RgbaFloat.Blue));
        //     // vertices.Add(new VertexPositionColor(v2, RgbaFloat.Blue));
        //     vertices.Add(new VertexPositionNormal(v0, new Vector3(0, 1, 0)));
        //     vertices.Add(new VertexPositionNormal(v1, new Vector3(0, 1, 0)));
        //     vertices.Add(new VertexPositionNormal(v2, new Vector3(0, 1, 0)));
        //
        //     indices.Add((uint)(baseIndex + 0));
        //     indices.Add((uint)(baseIndex + 1));
        //     indices.Add((uint)(baseIndex + 2));
        //
        //     baseIndex += 3;
        // }
        var idx = 0;
        // foreach (var mesh in meshes.TakeLast(140).Take(1))
        Console.WriteLine("Start building vertex and index buffers");
        foreach (var mesh in meshes)
        {
            idx = 0;
            foreach (var vertex in mesh.Vertices)
            {
                // vertices.Add(new VertexPositionColor(vertex.Position, RgbaFloat.White));
                // vertices.Add(new VertexPositionNormal(vertex.Position, vertex.Normal));
                vertices.Add(new VertexPositionNormalColor(vertex.Position, vertex.Normal, mesh.ColorIds[idx]));
                idx++;
        
            }
            
            foreach (var meshIndex in mesh.Indices)
            {
                // indices.Add((uint)(baseIndex+meshIndex));
                indices.Add(baseIndex+meshIndex);
            }
            baseIndex += (uint)mesh.Vertices.Length;
            // baseIndex += (uint)mesh.Vertices.Count;

        }

     
        Console.WriteLine("Finish building  vertex and index buffers");
        
        _indexCount = indices.Count;
        _vertexCount = vertices.Count;
    
        // Create buffers
        _vertexBuffer = factory.CreateBuffer(
            new BufferDescription((uint)(_vertexCount * VertexPositionNormalColor.SizeInBytes),
                BufferUsage.VertexBuffer));
        // _vertexBuffer = factory.CreateBuffer(
        //     new BufferDescription((uint)(_vertexCount * VertexPositionNormal.SizeInBytes),
        //         BufferUsage.VertexBuffer));
        // _vertexBuffer = factory.CreateBuffer(
        //     new BufferDescription((uint)(_vertexCount * VertexPositionColor.SizeInBytes),
        //         BufferUsage.VertexBuffer));

        _indexBuffer = factory.CreateBuffer(
            new BufferDescription(
                (uint)(_indexCount * sizeof(uint)),
                BufferUsage.IndexBuffer));
        
        graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());

        // _colorBuffer = factory.CreateBuffer(
        //     new BufferDescription((uint)(_width * _height * sizeof(float) * 3),
        //         BufferUsage.VertexBuffer)
        // );
        
        _timeBuffer = factory.CreateBuffer(new BufferDescription(32, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        
        _cameraBuffer = factory.CreateBuffer(new BufferDescription(192, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        
        _pointSettingsBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        
        _paletteBuffer = factory.CreateBuffer(new BufferDescription((uint)(colorPalette.Count * 16), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
    
        // graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        // graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());
        graphicsDevice.UpdateBuffer(_cameraBuffer, 0, _cameraMatrices);
        var pointData = new float[4] { _pointSize, 0f, 0f, 0f };
        graphicsDevice.UpdateBuffer(_pointSettingsBuffer, 0, pointData);
        // graphicsDevice.UpdateBuffer(_paletteBuffer, 0, _colorPalette.Values.ToArray());
        var arr = _colorPalette.Values.ToArray();
        // Console.WriteLine(arr.Length);
        // Console.WriteLine(arr[0]);
        graphicsDevice.UpdateBuffer(_paletteBuffer, 0, _colorPalette.Values.Select(v => new Vector4(v,1f)).ToArray());

    
        // Create vertex layout
        // VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
        //     new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
        //     new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4));
        // VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
        //     new VertexElementDescription("Position", VertexElementSemantic.Position,
        //         VertexElementFormat.Float3),
        //     new VertexElementDescription("Normal", VertexElementSemantic.Normal, VertexElementFormat.Float3)
        // );
        VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.Position,
                VertexElementFormat.Float3),
            new VertexElementDescription("Normal", VertexElementSemantic.Normal, VertexElementFormat.Float3),
            new VertexElementDescription("ColorId",VertexElementSemantic.Color, VertexElementFormat.UInt1)
        );


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

        _paletteLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("PaletteBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
        _paletteSet = factory.CreateResourceSet(new ResourceSetDescription(_paletteLayout, _paletteBuffer));
        
        _depthTexture = factory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)_width,
                (uint)_height,
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.D32_Float_S8_UInt,
                TextureUsage.DepthStencil
            )
        );
        
        // _framebuffer = factory.CreateFramebuffer(
        //     new FramebufferDescription(
        //         _depthTexture,
        //         graphicsDevice.SwapchainFramebuffer.ColorTargets[0].Target
        //     )
        // );
        
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
                FaceCullMode.Back,
                PolygonFillMode.Solid,
                FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: false),
            // PrimitiveTopology = PrimitiveTopology.TriangleList,
            PrimitiveTopology = topology,
            // ResourceLayouts = Array.Empty<ResourceLayout>(),
            ResourceLayouts = new[] { _cameraLayout, _pointSettingsLayout, _timeLayout, _paletteLayout },
            ShaderSet = new ShaderSetDescription(
                new[] { vertexLayout },
                _shaders),
            Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription
            // Outputs = _framebuffer.OutputDescription

        };
    
        _pipeline = factory.CreateGraphicsPipeline(ref pipelineDescription);
    
        _commandList = factory.CreateCommandList();
    }

    public void UpdateMap(ref GraphicsDevice graphicsDevice, Mesh mesh)
    {
        var vertices = new List<VertexPositionColor>();
        var indices = new List<uint>();
        
        //for testing
        var baseIndex = 0;
        var p = new Vector3(0, 1, 0);
        {
            // Build a small triangle at the point
            Vector3 v0 = new Vector3(p.X, p.Y + _triSize, p.Z);
            Vector3 v1 = new Vector3(p.X - _triSize, p.Y - _triSize, p.Z);
            Vector3 v2 = new Vector3(p.X + _triSize, p.Y - _triSize, p.Z);
            
          
            var color = RgbaFloat.White;

            // var colorData = pointcloud.colors[colorIndex];

            // color = new RgbaFloat(colorData.X,  colorData.Y, colorData.Z, 1) ; 
            
            vertices.Add(new VertexPositionColor(v0, color));
            vertices.Add(new VertexPositionColor(v1, color));
            vertices.Add(new VertexPositionColor(v2, color));
            
            indices.Add((uint)(baseIndex + 0));
            indices.Add((uint)(baseIndex + 1));
            indices.Add((uint)(baseIndex + 2));
            
            // baseIndex += 3;
            
        }
        
        var colorIndex = 0;
        foreach (var vertex in mesh.Vertices)
        {
            vertices.Add(new VertexPositionColor(vertex.Position, RgbaFloat.White));
        }
        
        foreach (var meshIndex in mesh.Indices)
        {
            // indices.Add((uint)(baseIndex+meshIndex));
            indices.Add(meshIndex);
        }
        
        _vertexCount = vertices.Count;
        
        graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());
    }
    
    public void Update( ref GraphicsDevice graphicsDevice)
    {
       
        var vertices = new List<VertexPositionColor>();
        var indices = new List<uint>();
        // UpdateCameraMatrices(pointcloud, ref graphicsDevice);
        uint baseIndex = 0;
        
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
            
            indices.Add((uint)(baseIndex + 0));
            indices.Add((uint)(baseIndex + 1));
            indices.Add((uint)(baseIndex + 2));
            
            baseIndex += 3;
            
        }
    
        _vertexCount = vertices.Count;
        
        graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices.ToArray());
        graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices.ToArray());
    }
    
    

    public void UploadMesh(GraphicsDevice gd, Mesh mesh)
    {
        var factory = gd.ResourceFactory;

        var vertices = new VertexPositionNormalColor[mesh.Vertices.Length];
        // var vertices = new VertexPositionNormalColor[mesh.Vertices.Count];


        for (int i = 0; i < vertices.Length; i++)
        {
            var v = mesh.Vertices[i];
            vertices[i] = new VertexPositionNormalColor(
                v.Position,
                v.Normal,
                mesh.ColorIds[i]);
        }

        var indices = mesh.Indices.ToArray();

        var vb = factory.CreateBuffer(new BufferDescription(
            (uint)(vertices.Length * VertexPositionNormalColor.SizeInBytes),
            BufferUsage.VertexBuffer));

        var ib = factory.CreateBuffer(new BufferDescription(
            (uint)(indices.Length * sizeof(uint)),
            BufferUsage.IndexBuffer));

        gd.UpdateBuffer(vb, 0, vertices);
        gd.UpdateBuffer(ib, 0, indices);

        _gpuMeshes.Add(new GpuMesh
        {
            VertexBuffer = vb,
            IndexBuffer = ib,
            IndexCount = (uint)indices.Length
        });
    }
    
    
    
    
    public void Draw(ref GraphicsDevice graphicsDevice)
    {
        
        
        _commandList.Begin();
        _commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
        // _commandList.SetFramebuffer(_framebuffer);
        
        _commandList.ClearColorTarget(0, RgbaFloat.Black);       // clear to black (or any color)
        _commandList.ClearDepthStencil(1f);                     // clear depth to 1.0 (max)
        
        _commandList.SetPipeline(_pipeline);
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
        _commandList.SetGraphicsResourceSet(0, _cameraSet);
        _commandList.SetGraphicsResourceSet(1, _pointSettingsSet);
        _commandList.SetGraphicsResourceSet(2, _timeSet); 
        _commandList.SetGraphicsResourceSet(3, _paletteSet);
        // _commandList.DrawIndexed(
        //     indexCount: (uint)_indexCount,
        //     instanceCount: 1,
        //     indexStart: 0,
        //     vertexOffset: 0,
        //     instanceStart: 0);
  
        
        // foreach (var mesh in _gpuMeshes)
        // {
        //     _commandList.SetVertexBuffer(0, mesh.VertexBuffer);
        //     _commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
        //
        //     _commandList.DrawIndexed(
        //         mesh.IndexCount,
        //         1,
        //         0,
        //         0,
        //         0);
        // }
        _commandList.DrawIndexed(
            (uint)_indexCount,
            1,
            0,
            0,
            0);
        _commandList.End();
        
        

        graphicsDevice.SubmitCommands(_commandList);
        graphicsDevice.SwapBuffers();
    }

    public void TestDraw(ref GraphicsDevice graphicsDevice)
    {
        _commandList.Begin();

        _commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Black);

        _commandList.End();
        graphicsDevice.SubmitCommands(_commandList);

        graphicsDevice.WaitForIdle(); 
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
    
    struct VertexPositionNormal
    {
        public Vector3 Position;
        public Vector3 Normal;

        public VertexPositionNormal(Vector3 pos, Vector3 normal)
        {
            Position = pos;
            Normal = normal;
        }

        public const uint SizeInBytes = 24;
    }
    
    struct VertexPositionNormalColor
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int ColorId;

        public VertexPositionNormalColor(Vector3 pos, Vector3 normal, int colorId)
        {
            Position = pos;
            Normal = normal;
            ColorId = colorId;
        }

        public const uint SizeInBytes = 24+sizeof(int);
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

    public void UpdateCamera(ref GraphicsDevice graphicsDevice, Vector3 movement, Vector3 rotationInput, float deltaTime)
    {
        var rotationSpeed = 5f;
       
        
        _cameraRotation += rotationInput * deltaTime * rotationSpeed;
        
        //model Matrix
        _cameraMatrices[0] = Matrix4x4.Identity;
        //view Matrix
        var rotation = Matrix4x4.CreateRotationX(_cameraRotation.X) * 
                       Matrix4x4.CreateRotationY(_cameraRotation.Y)  * 
                       Matrix4x4.CreateRotationZ(_cameraRotation.Z);
        var forward =Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));
        
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        
        _cameraPosition += 
            movement.Z * forward +
            movement.X * -right +
            movement.Y * Vector3.UnitY; 
     
        // var target = new Vector3(0, 1, 0);
        var target = _cameraPosition + forward;
        _cameraMatrices[1] = Matrix4x4.CreateLookAt(_cameraPosition, target, Vector3.UnitY);
        
        //projection Matrix with 60deg fov
        _cameraMatrices[2] = Matrix4x4.CreatePerspectiveFieldOfView(1.04719755f, _width/_height, 0.1f, 10000);
        graphicsDevice.UpdateBuffer(_cameraBuffer, 0, _cameraMatrices.ToArray());
    }

    public void DisposeResources()
    {
        foreach (var m in _gpuMeshes)
        {
            m.VertexBuffer.Dispose();
            m.IndexBuffer.Dispose();
        } 
        
        _pipeline.Dispose();
        _commandList.Dispose();
    }

    public Vector3 getCameraPosition()
    {
        return _cameraPosition;
    }

    struct TimeUniform
    {
        public float Time;
        private Vector3 _padding;
    }
}

