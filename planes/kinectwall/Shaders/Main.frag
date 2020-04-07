#version 150 compatibility
uniform vec3 meshColor;
uniform float ambient;
uniform vec3 lightPos;
uniform float opacity;
in vec3 vNormal;
in vec3 vWsPos;
in vec3 vTexCoord;
void main()
{
	vec3 lightVec = normalize(vWsPos - lightPos);
	float lit = abs(dot(lightVec, vNormal));
	gl_FragColor = vec4(meshColor * (lit * (1 - ambient) + ambient), 1) * opacity;
}