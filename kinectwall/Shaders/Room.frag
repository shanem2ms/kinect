#version 150 compatibility
uniform float depthImageBlend;
uniform vec3 meshColor;
uniform int colorMode;
uniform float opacity;
in vec3 vNormal;
in vec3 vTexCoord;
void main()
{
	float sideval = vTexCoord.z;
	vec3 sidecolor = vec3(0, 0, 0);
	if (sideval > (5.5 / 6.0))
		sidecolor = vec3(1,1,1);
	else if ((sideval > (4.5 / 6.0)))
		sidecolor = vec3(1,1,0);
	else if ((sideval > (3.5 / 6.0)))
		sidecolor = vec3(1,0,1);
	else if ((sideval > (2.5 / 6.0)))
		sidecolor = vec3(0,1,1);
	else if ((sideval > (1.5 / 6.0)))
		sidecolor = vec3(0,0,1);
	else if ((sideval > (1.5 / 6.0)))
		sidecolor = vec3(0,1,0);
	else if ((sideval > (0.5 / 6.0)))
		sidecolor = vec3(0.5,0.5,0);

	vec3 color = vec3(0, 0, 0);
	if (mod(vTexCoord.x, 0.1) > 0.098 ||
		mod(vTexCoord.y, 0.1) > 0.098)
		color = sidecolor ;
	else
		discard;

	gl_FragColor = vec4(color, 1);
}