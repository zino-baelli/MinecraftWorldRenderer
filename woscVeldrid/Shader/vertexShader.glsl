#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;

layout(set = 0, binding = 0) uniform CameraBuffer {
    mat4 model;
    mat4 view;
    mat4 projection;
};

//global variables
layout(set = 2, binding = 0, std140) uniform TimeBuffer {
    float uTime;
    float uRippleThickness;
    float uRippleFrequency;
    float uRippleSpeed;
    float uRippleSpacing;
    float uRippleAmplitude;
    float _pad0;
    float _pad1;
};

layout(location = 0) out vec4 vColor;

void main()
{
    vec4 viewPos = view * vec4(Position, 1.0);

    gl_Position = projection * viewPos;

    vColor = Color;
}