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
            PreferDepthRangeZeroToOne = true
        };
        
        RenderDoc rd;
        if (RenderDoc.Load(out rd))
        {
            rd.SetCaptureSavePath(@"C:\Users\zino\Desktop\renderDocCaptures\");
        }
        
        _graphicsDevice = VeldridStartup.CreateVulkanGraphicsDevice(options, _window);
        

        
        // var vertexCode = File.ReadAllText("../../../Shader/vertexShader.glsl");
        //
        // var fragmentCode = File.ReadAllText("../../../Shader/fragmentShader.glsl");
        
        var vertexCode = File.ReadAllText("../../../Shader/vertexNormalShader.glsl");

        var fragmentCode = File.ReadAllText("../../../Shader/fragmentNormalShader.glsl");
        
        _renderPipeline = new RenderPipeline(vertexCode, fragmentCode, _width, _height);

  
        
        
        _renderPipeline.CreateResources(ref _graphicsDevice, PrimitiveTopology.TriangleList, meshes);

        
      
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
                
            if(keyEvent.Key == Veldrid.Key.W)
            {
                _renderPipeline.UpdateCamera(ref _graphicsDevice, new Vector3(0,0,0.01f));
            }
            if(keyEvent.Key == Veldrid.Key.A)
            {
                _renderPipeline.UpdateCamera(ref _graphicsDevice, new Vector3(0.01f,0,0));
            }
            if(keyEvent.Key == Veldrid.Key.S)
            {
                _renderPipeline.UpdateCamera(ref _graphicsDevice, new Vector3(0,0,-0.01f));
            }
            if(keyEvent.Key == Veldrid.Key.D)
            {
                _renderPipeline.UpdateCamera(ref _graphicsDevice, new Vector3(-0.01f,0,0));
            }
        };


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

            
           

            
            double now = stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - lastTime);
            lastTime = now;
            _renderPipeline.UpdateTime(ref _graphicsDevice, deltaTime);
            // _renderPipeline.Update(ref _graphicsDevice);
            // _renderPipeline.UpdateMap(ref _graphicsDevice, mesh);
            
            // rd.TriggerCapture(); // capture next frame

          
             _renderPipeline.Draw(ref _graphicsDevice);
             
            
            
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