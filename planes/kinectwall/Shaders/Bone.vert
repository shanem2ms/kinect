#version 150 compatibility
uniform mat4 uMVP;
in vec3 aPosition;
in vec3 aTexCoord0;
in vec3 aNormal;
out vec3 vTexCoord;
out vec3 vNormal;
void main() {
    gl_Position = uMVP * vec4(aPosition, 1.0);
    vNormal = aNormal;
}
