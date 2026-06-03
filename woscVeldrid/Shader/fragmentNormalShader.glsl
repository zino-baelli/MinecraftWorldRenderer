#version 450

layout(location = 0) in vec3 fsNormal;
layout(location = 1) in vec4 fsColor;
layout(location = 0) out vec4 outColor;

vec3 colorFromNormal(vec3 n)
{
    return normalize(n) * 0.5 + 0.5;
}

void main()
{
    vec3 lightDir = normalize(vec3(0.3, 1.0, 0.5));

    float ambient = 0.2;
    float diffuse = max(dot(normalize(fsNormal), lightDir), 0.0);

    float lighting = ambient + diffuse;
    lighting = clamp(lighting, 0.0, 1.0);

    vec3 baseColor = vec3(0.5, 0.5, 0.5);

    vec3 finalColor = baseColor * lighting;
    //vec3 finalColor = colorFromNormal(fsNormal);
    //vec3 finalColor = fsColor;



    outColor = vec4(finalColor, 1.0);
    outColor = fsColor * lighting;

    //float depth = gl_FragCoord.z;
    //outColor = vec4(vec3(depth), 1.0);
}

