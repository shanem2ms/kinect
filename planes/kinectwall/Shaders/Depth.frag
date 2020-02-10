#version 150 compatibility
uniform float depthImageBlend;
uniform vec3 meshColor;
uniform int colorMode;
uniform float opacity;
uniform highp sampler2D depthSampler;
in vec3 vNormal;
in vec3 vTexCoord;
void main()
{
	//float d = texture2D(depthSampler, vTexCoord.xy).r;
	gl_FragColor = vec4(vNormal, 1);
}