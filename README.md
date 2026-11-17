# Minecraft World Renderer

A Minecraft world viewer built with C#, [Veldrid](https://github.com/veldrid/veldrid) and Vulkan. WoSc converts Minecraft Anvil region files (`.mca`) into compact, pre-simplified triangle meshes with multiple levels of detail (LODs), then streams and renders them in real time as you fly around the world.

## Features

- **Region import**: reads Anvil `.mca` region files, parses the chunk NBT data and decodes blocks.
- **Mesh generation**: builds one mesh per chunk (32×32 chunks per region) with positions, normals and per-vertex block color IDs.
- **LOD generation**: uses [meshoptimizer](https://github.com/zeux/meshoptimizer) to simplify meshes into 4 additional LOD levels.
- **Compact storage**: meshes are written per region to compressed `.mesh` files, so the slow import step only has to run once.
- **Streaming renderer**: a `ChunkStreamingManager` loads and unloads chunk meshes around the camera and picks an LOD per chunk.
- **Palette-based coloring**: block colors come from a CSV palette uploaded to the GPU as a uniform buffer.
- **Free-fly camera** with keyboard controls.

## How it works

```
 .mca region files
        │
        ▼
 MCAReader ──► NbtParser ──► NbtDecoder ──► Chunk
        │
        ▼
 MeshBuilder  (one Mesh per chunk)
        │
        ▼
 LodBuilder   (meshoptimizer, 4 LOD levels)
        │
        ▼
 MeshIOCompressed  ──►  <meshLocation>/r.X.Z.mesh
                        <meshLocation>/lod0/r.X.Z.mesh
                        <meshLocation>/lod1/…  lod2/…  lod3/…
        │
        ▼
 ChunkStreamingManager ──► RenderPipeline (Veldrid / Vulkan) ──► AppWindow
```

The program has two modes, controlled by flags at the top of `Program.cs`:

| Mode | `fileLoadAmount` | `loadMeshFromFiles` | `renderMeshes` | What it does |
|---|---|---|---|---|
| **Build meshes** | number of `.mca` files to process (e.g. `files.Length`) | `false` | `false` | Converts regions to `.mesh` files and LODs, then exits |
| **Render** | `0` | `true` | `true` | Opens the window and streams meshes from `meshLocation` |

## Requirements

- .NET SDK (version matching the project's `TargetFramework`)
- A GPU and driver with **Vulkan** support
- NuGet packages (restored automatically):
  - `Veldrid`, `Veldrid.StartupUtilities`, `Veldrid.SPIRV`
  - `ImGuiNET`
  - `Meshoptimizer`
- A Minecraft world's `region` folder containing `.mca` files

## Setup

1. Clone the repository and restore packages:
   ```bash
   dotnet restore
   ```
2. Edit the paths in `Program.cs` to match your machine. They are currently hard-coded:
   - The `.mca` source folder (the `files` variable)
   - `meshLocation`: where generated `.mesh` files are written and read
3. Make sure the following files exist (paths are relative to the build output folder):
   - `TestData/blockPaletteComplete.csv`: maps block names to IDs and colors
   - `Shader/vertexNormalShader.glsl`
   - `Shader/fragmentNormalShader.glsl`

## Usage

### 1. Build the meshes (run once per world)

In `Program.cs` set:

```csharp
var fileLoadAmount   = files.Length; // or a smaller number for testing
var loadMeshFromFiles = false;
var renderMeshes      = false;
```

Run the project. For each region file this writes:

- `<meshLocation>/r.X.Z.mesh`: full-detail meshes
- `<meshLocation>/lod0` … `lod3/r.X.Z.mesh`: simplified meshes

Processing time is printed to the console when finished.

### 2. View the world

Set:

```csharp
var fileLoadAmount    = 0;
var loadMeshFromFiles = true;
var renderMeshes      = true;
```

Run the project. The window (1920×1080) opens and meshes stream in around the camera.

## Controls

| Key | Action |
|---|---|
| `W` / `S` | Move forward / backward |
| `A` / `D` | Move left / right |
| `E` / `Q` | Move up / down |
| Arrow keys | Look around (yaw / pitch) |
| `Space` | Print a debug message to the console |
| `C` / `T` | Reserved for RenderDoc capture / replay (currently disabled) |

Camera speed is 50 units per second.

## Configuration

### LOD simplification

`LodBuilder` controls how aggressively meshes are simplified:

```csharp
var lodBuilder = new LodBuilder(
    error: 0.08f,     // target error for the first LOD
    ratio: 0.12f,     // target triangle ratio for the first LOD
    maxError: 1.2f,   // error limit for the coarsest LOD
    maxRatio: 0.65f   // ratio limit for the coarsest LOD
);
```

Lower error and higher ratio keep more detail; the reverse gives smaller, faster meshes.

### LOD selection

Set the `LodSelectionType` passed to `ChunkStreamingManager` in `AppWindow.RenderStreamed`:

| Value | Behavior |
|---|---|
| `None` | Always use full-detail meshes |
| `Distance` | Choose LOD by distance from the camera (default) |
| `ScreenSpace` | Choose LOD by projected size on screen |

The other manager parameters (`50`, `500`) are the streaming distance settings; see `ChunkStreamingManager` for their exact meaning.

## Project structure

| File / Class | Responsibility |
|---|---|
| `Program.cs` | Entry point; selects build vs. render mode |
| `FileProcessor` | Imports one `.mca` file: chunks → meshes → LODs → `.mesh` files |
| `MCAReader`, `NbtParser`, `NbtDecoder` | Region and NBT reading, chunk decoding |
| `MeshBuilder` | Builds a mesh from a decoded chunk |
| `LodBuilder` | Generates simplified LOD meshes via meshoptimizer |
| `MeshIOCompressed` | Reads and writes compressed `.mesh` region files |
| `ChunkStreamingManager` | Loads, unloads and selects LODs for chunk meshes around the camera |
| `AppWindow` | Window, input handling and main render loop |
| `RenderPipeline` | Vulkan pipeline, uniform buffers (camera, palette), mesh upload and drawing |
| `Helper` | Loads the block ID map and color palette from the CSV |
| `Shader/*.glsl` | Vertex and fragment shaders (compiled to SPIR-V at runtime) |

## Known limitations

- File paths are hard-coded and modes are switched by editing source rather than command-line arguments.
- Shader and palette paths are relative to the build output (`../../../`), so the app is expected to be run from the IDE or `bin/<config>/<tfm>`.
- Rendering uses the Vulkan backend only.

## Acknowledgements

- [Veldrid](https://github.com/veldrid/veldrid) for the graphics abstraction
- [meshoptimizer](https://github.com/zeux/meshoptimizer) for mesh simplification
- The Minecraft Anvil / NBT format documentation
