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

   
    

    public void Render()
    {
        if (_window != null &&    _window.Exists)
        {
            return;
        }
        
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
        
        _graphicsDevice = VeldridStartup.CreateVulkanGraphicsDevice(options, _window);
        

        
        var vertexCode = File.ReadAllText("../../../Shader/vertexShader.glsl");

        var fragmentCode = File.ReadAllText("../../../Shader/fragmentShader.glsl");
        
        _renderPipeline = new RenderPipeline(vertexCode, fragmentCode, _width, _height);

  
        
        
        _renderPipeline.CreateResources(ref _graphicsDevice, PrimitiveTopology.TriangleList);

        
      
        


        while (_window.Exists)
        {
            _window.PumpEvents();
            
            
            if (!_window.Exists)
                break;
            
            double now = stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(now - lastTime);
            lastTime = now;
            _renderPipeline.UpdateTime(ref _graphicsDevice, deltaTime);
            _renderPipeline.Update(ref _graphicsDevice);
            
            
          
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