#version 300 es
uniform highp float depthImageBlend;
uniform highp vec3 meshColor;
uniform int colorMode;
uniform highp float opacity;
uniform sampler2D diffuseMap;
in highp vec3 vNormal;
in highp vec3 vTexCoord;
in highp vec3 vBoneColor;
out highp vec4 fragColor;
void main()
{
    highp float light = abs(dot(vNormal, vec3(0,1,0)));
	highp vec4 diffuseColor = texture2D(diffuseMap, vec2(vTexCoord.x, 1.0 - vTexCoord.y));
	highp vec3 finalColor = diffuseColor.zyx;
	highp float ambient = 0.75;
	fragColor = vec4(finalColor * (light * (1.0 - ambient) + ambient), 1);
}
