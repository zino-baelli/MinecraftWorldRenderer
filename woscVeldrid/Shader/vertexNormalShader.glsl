#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in uint ColorId;

layout(set = 0, binding = 0) uniform CameraBuffer {
    mat4 model;
    mat4 view;
    mat4 projection;
};

layout(set = 3, binding = 0) uniform PaletteBuffer {
    vec4[512] palette;
};

layout(location = 0) out vec3 fsNormal;
layout(location = 1) out vec4 fsColor;

float rand(vec2 co)
{
    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

void main()
{
     vec4 viewPos = view * vec4(Position, 1.0);

    gl_Position = projection * viewPos;
    fsNormal = Normal;
    fsColor = palette[ColorId];
    //float r = rand(Position.xy);
    //int index = int(r * 400.0);
    //fsColor = palette[index];
}