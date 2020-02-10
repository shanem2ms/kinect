#version 300 es
uniform mat4 uMVP;
const int MAX_BONES = 100;
uniform mat4 gBones[MAX_BONES];
uniform vec3 gBonesColor[MAX_BONES];
uniform bool gUseBones;


in vec3 aPosition;
in vec3 aTexCoord0;
in highp ivec4 aTexCoord1;
in vec4 aTexCoord2;
in vec3 aNormal;
out vec3 vTexCoord;
out vec3 vNormal;
out vec3 vBoneColor;
void main() {    
    mat4 bonemat = gBones[aTexCoord1.x] * aTexCoord2.x;
    if (aTexCoord1.y >= 0) bonemat += gBones[aTexCoord1.y] * aTexCoord2.y;
    if (aTexCoord1.z >= 0) bonemat += gBones[aTexCoord1.z] * aTexCoord2.z;
    if (aTexCoord1.w >= 0) bonemat += gBones[aTexCoord1.w] * aTexCoord2.w;

    vec3 bonecol = gBonesColor[aTexCoord1.x] * aTexCoord2.x;
    if (aTexCoord1.y >= 0) bonecol += gBonesColor[aTexCoord1.y] * aTexCoord2.y;
    if (aTexCoord1.z >= 0) bonecol += gBonesColor[aTexCoord1.z] * aTexCoord2.z;
    if (aTexCoord1.w >= 0) bonecol += gBonesColor[aTexCoord1.w] * aTexCoord2.w;

    mat4 mvp = uMVP;
    if (gUseBones)
        mvp = mvp * bonemat;
    vBoneColor = bonecol;
    vTexCoord = aTexCoord0;
    vNormal = aNormal;
    gl_Position = mvp * vec4(aPosition, 1.0);
}
