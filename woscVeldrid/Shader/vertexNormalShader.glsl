#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;

layout(set = 0, binding = 0) uniform CameraBuffer {
    mat4 model;
    mat4 view;
    mat4 projection;
};

layout(location = 0) out vec3 fsNormal;

void main()
{
      vec4 viewPos = view * vec4(Position, 1.0);

    gl_Position = projection * viewPos;
    fsNormal = Normal;
}