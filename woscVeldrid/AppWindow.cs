using System.Collections.Concurrent;
using System.Numerics;
using ImGuiNET;

namespace woscVeldrid;
using System.IO;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

public class AppWindow
{
    
    


    private int _width;
    private int _height;
    private string _title;
    private bool isTakingSnapshot = false;
    private GraphicsDevice _graphicsDevice;
    private RenderPipeline _renderPipeline;
    private Sdl2Window _window;
    
    // public Dictionary<string, int> BlockIds { get; set; }
    public Dictionary<int, Vector3> ColorPalette { get; set; }

    public AppWindow(int width, int height, string title)
    {
        _width = width;
        _height = height;
        _title = title;
    }

   
    

    public void Render(Mesh[] meshes)
    {
        if (_window != null &&    _window.Exists)
        {
            return;
        }

        var captureNextFrame = false;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        double lastTime = stopwatch.Elapsed.TotalSeconds;
        
       
        WindowCreateInfo windowCI = new WindowCreateInfo()
        {
            X = 100,
            Y = 100,
            WindowWidth = _width,
            WindowHeight = _height,
            WindowTitle = _title
        };
        _window = VeldridStartup.CreateWindow(ref windowCI);

        var options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true,
            SwapchainDepthFormat = PixelFormat.D32_Float_S8_UInt,
            SyncToVerticalBlank = true,
            ResourceBindingModel = ResourceBindingModel.Improved
        };
        
        RenderDoc rd;
        if (RenderDoc.Load(out rd))
        {
            rd.SetCaptureSavePath(@"C:\Users\zino\Desktop\renderDocCaptures\");
        }
        
        _graphicsDevice = VeldridStartup.CreateVulkanGraphicsDevice(options, _window);
        
        // ImGuiRenderer imguiRenderer = new ImGuiRenderer(
        //     _graphicsDevice, _graphicsDevice.MainSwapchain.Framebuffer.OutputDescription,
        //     (int)_graphicsDevice.MainSwapchain.Framebuffer.Width, (int)_graphicsDevice.MainSwapchain.Framebuffer.Height);
        
        // var vertexCode = File.ReadAllText("../../../Shader/vertexShader.glsl");
        //
        // var fragmentCode = File.ReadAllText("../../../Shader/fragmentShader.glsl");
        
        var vertexCode = File.ReadAllText("../../../Shader/vertexNormalShader.glsl");

        var fragmentCode = File.ReadAllText("../../../Shader/fragmentNormalShader.glsl");
        
        _renderPipeline = new RenderPipeline(vertexCode, fragmentCode, _width, _height);

  
        
        
        _renderPipeline.CreateResources(ref _graphicsDevice, PrimitiveTopology.TriangleList, meshes, ColorPalette);

        // var uploadQueue = new Queue<Mesh>(meshes);
        ConcurrentQueue<Mesh> readyMeshes = new();

        Task.Run(() =>
        {
            foreach (var mesh in meshes)
            {
                // heavy CPU work
                readyMeshes.Enqueue(mesh);
            }
        });
      
        _window.KeyDown += (keyEvent) =>
        {
            if(keyEvent.Key == Veldrid.Key.Space)
            {
                Console.WriteLine("Space bar pressed");
                uint count = rd.CaptureCount;
                Console.WriteLine($"Captures: {count}");
            }

            if (keyEvent.Key == Veldrid.Key.C)
            {
                captureNextFrame = true;
            }

            if (keyEvent.Key == Key.T)
            {
                rd.LaunchReplayUI();
            }
            
        };
        
        HashSet<Key> _keysDown = new HashSet<Key>();
        
        _window.KeyDown += e => _keysDown.Add(e.Key);
        _window.KeyUp += e => _keysDown.Remove(e.Key);

        var drawOnce = 0;
        // int uploadsPerFrame = 1;

        
        while (_window.Exists)
        {
            _window.PumpEvents();
            
            
            if (!_window.Exists)
                break;
            
            if (captureNextFrame)
            {
                rd.TriggerCapture();
                captureNextFrame = false;
            }

            
            // for (int i = 0; i < 2 && uploadQueue.Count > 0; i++)
            // {
            //     var mesh = uploadQueue.Dequeue();
            //     _renderPipeline.UploadMesh(_graphicsDevice, mesh);
            // }
            
            // for (int i = 0; i < uploadsPerFrame; i++)
            // {
            //     if (readyMeshes.TryDequeue(out var mesh))
            //     {
            //         _renderPipeline.UploadMesh(_graphicsDevice, mesh);
            //         mesh.Vertices = null;
            //         mesh.Indices = null;
            //         mesh.colorIds = null;
            //     }
            // }

            
            double now = stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - lastTime);
            lastTime = now;
            
            Vector3 movement = Vector3.Zero;
            Vector3 rotation = Vector3.Zero;
            float speed = 50f; // units per second

            if (_keysDown.Contains(Key.W))
            {
                // Console.WriteLine("W");
                movement += new Vector3(0, 0, 1);
            }
            if (_keysDown.Contains(Key.S)) movement += new Vector3(0, 0, -1);
            if (_keysDown.Contains(Key.A)) movement += new Vector3(1, 0, 0);
            if (_keysDown.Contains(Key.D)) movement += new Vector3(-1, 0, 0);
            if (_keysDown.Contains(Key.E)) movement += new Vector3(0, 1, 0);
            if (_keysDown.Contains(Key.Q)) movement += new Vector3(0, -1, 0);
            if (_keysDown.Contains(Key.Left)) rotation += new Vector3(0, 1, 0);
            if (_keysDown.Contains(Key.Right)) rotation += new Vector3(0, -1, 0);
            if (_keysDown.Contains(Key.Up)) rotation += new Vector3(1, 0, 0);
            if (_keysDown.Contains(Key.Down)) rotation += new Vector3(-1, 0, 0);
            

            if (movement != Vector3.Zero)
            {
                movement = Vector3.Normalize(movement) * speed * deltaTime;
                _renderPipeline.UpdateCamera(ref _graphicsDevice, movement, Vector3.Zero, deltaTime);
            }

            if (rotation != Vector3.Zero)
            {
                rotation = Vector3.Normalize(rotation) * speed * deltaTime;
                _renderPipeline.UpdateCamera(ref _graphicsDevice, Vector3.Zero, rotation, deltaTime);
            }
            
            
            // _renderPipeline.UpdateTime(ref _graphicsDevice, deltaTime);
            // _renderPipeline.Update(ref _graphicsDevice);
            // _renderPipeline.UpdateMap(ref _graphicsDevice, mesh);
            
            // rd.TriggerCapture(); // capture next frame
            
            
            var cameraPositon = _renderPipeline.getCameraPosition();
            

            // while (drawOnce < 1)
            {
                _renderPipeline.Draw(ref _graphicsDevice);
                // _renderPipeline.TestDraw(ref _graphicsDevice);
                // drawOnce++;
            }
             
           
            
        }

       
        
        DisposeResources();

        
    }
    
    public void RenderStreamed(string meshLocation)
    {
        if (_window != null &&    _window.Exists)
        {
            return;
        }

        var captureNextFrame = false;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        double lastTime = stopwatch.Elapsed.TotalSeconds;
        
       
        WindowCreateInfo windowCI = new WindowCreateInfo()
        {
            X = 100,
            Y = 100,
            WindowWidth = _width,
            WindowHeight = _height,
            WindowTitle = _title
        };
        _window = VeldridStartup.CreateWindow(ref windowCI);

        var options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true,
            SwapchainDepthFormat = PixelFormat.D32_Float_S8_UInt,
            SyncToVerticalBlank = true,
            ResourceBindingModel = ResourceBindingModel.Improved
        };
        
        RenderDoc rd;
        if (RenderDoc.Load(out rd))
        {
            rd.SetCaptureSavePath(@"C:\Users\zino\Desktop\renderDocCaptures\");
        }
        
        _graphicsDevice = VeldridStartup.CreateVulkanGraphicsDevice(options, _window);
        
        
        var vertexCode = File.ReadAllText("../../../Shader/vertexNormalShader.glsl");

        var fragmentCode = File.ReadAllText("../../../Shader/fragmentNormalShader.glsl");
        
        _renderPipeline = new RenderPipeline(vertexCode, fragmentCode, _width, _height);

  
        
        
        _renderPipeline.CreateResources(ref _graphicsDevice, PrimitiveTopology.TriangleList, ColorPalette);

        // var uploadQueue = new Queue<Mesh>(meshes);
        ConcurrentQueue<Mesh> readyMeshes = new();

        Task.Run(() =>
        {
            foreach (var mesh in meshes)
            {
                // heavy CPU work
                readyMeshes.Enqueue(mesh);
            }
        });
      
        _window.KeyDown += (keyEvent) =>
        {
            if(keyEvent.Key == Veldrid.Key.Space)
            {
                Console.WriteLine("Space bar pressed");
                uint count = rd.CaptureCount;
                Console.WriteLine($"Captures: {count}");
            }

            if (keyEvent.Key == Veldrid.Key.C)
            {
                captureNextFrame = true;
            }

            if (keyEvent.Key == Key.T)
            {
                rd.LaunchReplayUI();
            }
            
        };
        
        HashSet<Key> _keysDown = new HashSet<Key>();
        
        _window.KeyDown += e => _keysDown.Add(e.Key);
        _window.KeyUp += e => _keysDown.Remove(e.Key);

        var drawOnce = 0;
        // int uploadsPerFrame = 1;

        
        while (_window.Exists)
        {
            _window.PumpEvents();
            
            
            if (!_window.Exists)
                break;
            
            if (captureNextFrame)
            {
                rd.TriggerCapture();
                captureNextFrame = false;
            }

            
            // for (int i = 0; i < 2 && uploadQueue.Count > 0; i++)
            // {
            //     var mesh = uploadQueue.Dequeue();
            //     _renderPipeline.UploadMesh(_graphicsDevice, mesh);
            // }
            
            // for (int i = 0; i < uploadsPerFrame; i++)
            // {
            //     if (readyMeshes.TryDequeue(out var mesh))
            //     {
            //         _renderPipeline.UploadMesh(_graphicsDevice, mesh);
            //         mesh.Vertices = null;
            //         mesh.Indices = null;
            //         mesh.colorIds = null;
            //     }
            // }

            
            double now = stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - lastTime);
            lastTime = now;
            
            Vector3 movement = Vector3.Zero;
            Vector3 rotation = Vector3.Zero;
            float speed = 50f; // units per second

            if (_keysDown.Contains(Key.W))
            {
                // Console.WriteLine("W");
                movement += new Vector3(0, 0, 1);
            }
            if (_keysDown.Contains(Key.S)) movement += new Vector3(0, 0, -1);
            if (_keysDown.Contains(Key.A)) movement += new Vector3(1, 0, 0);
            if (_keysDown.Contains(Key.D)) movement += new Vector3(-1, 0, 0);
            if (_keysDown.Contains(Key.E)) movement += new Vector3(0, 1, 0);
            if (_keysDown.Contains(Key.Q)) movement += new Vector3(0, -1, 0);
            if (_keysDown.Contains(Key.Left)) rotation += new Vector3(0, 1, 0);
            if (_keysDown.Contains(Key.Right)) rotation += new Vector3(0, -1, 0);
            if (_keysDown.Contains(Key.Up)) rotation += new Vector3(1, 0, 0);
            if (_keysDown.Contains(Key.Down)) rotation += new Vector3(-1, 0, 0);
            

            if (movement != Vector3.Zero)
            {
                movement = Vector3.Normalize(movement) * speed * deltaTime;
                _renderPipeline.UpdateCamera(ref _graphicsDevice, movement, Vector3.Zero, deltaTime);
            }

            if (rotation != Vector3.Zero)
            {
                rotation = Vector3.Normalize(rotation) * speed * deltaTime;
                _renderPipeline.UpdateCamera(ref _graphicsDevice, Vector3.Zero, rotation, deltaTime);
            }
            
            
            
            var cameraPositon = _renderPipeline.getCameraPosition();
            

            // while (drawOnce < 1)
            {
                _renderPipeline.Draw(ref _graphicsDevice);
                // _renderPipeline.TestDraw(ref _graphicsDevice);
                // drawOnce++;
            }
             
           
            
        }

       
        
        DisposeResources();

        
    }

    public void CloseWindow()
    {
        if ( _window is { Exists: true })
        {
            _window.Close();
        }
    }

    private void DisposeResources()
    {
        _graphicsDevice.WaitForIdle();
        _renderPipeline.DisposeResources();
        _graphicsDevice.Dispose();
    }

}