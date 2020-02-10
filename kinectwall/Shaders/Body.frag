#version 150 compatibility
uniform float depthImageBlend;
uniform vec3 meshColor;
uniform int colorMode;
uniform float opacity;
in vec3 vNormal;
in vec3 vTexCoord;
void main()
{
	gl_FragColor = vec4(vTexCoord, 1);
}