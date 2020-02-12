#version 150 compatibility
uniform vec3 meshColor;
uniform vec3 lightPos;
in vec3 vNormal;
in vec3 vWsPos;
in vec3 vTexCoord;
void main()
{
	vec3 lightVec = normalize(vWsPos - lightPos);
	float ambient = 0.3;
	float lit = abs(dot(lightVec, vNormal));
	gl_FragColor = vec4(meshColor * (lit * (1 - ambient) + ambient), 1);
}