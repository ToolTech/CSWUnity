/* 
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s) 
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied. 
 * 
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

static const float kPi = 3.1415926535897932384626433832795028841971;
static const float kDefaultMutator = 0.546;
static const float kFoliageHeightMutator = 0.321;

void PopulateVertex(inout FS_INPUT vertex, float3 worldPos, float3 center, float radius, float3 arrayUV, float3 normal, float3 color, float alpha)
{
    vertex.pos = UnityObjectToClipPos(worldPos);
    vertex.worldPos = worldPos;
	vertex.center = center;
    vertex.radius = (half)radius;
    vertex.uv = (half2) arrayUV.xy;
    vertex.layer = (uint) round(arrayUV.z);
    vertex.normal = PackNormalOct(normal);
    vertex.colorA = PackColor(float4(color.xyz, alpha));
}

float Random(float input, float mutator = kDefaultMutator)
{
    float nonInteger = 43758.5453; // A different constant to avoid common factors with typical inputs
    float seed = input + mutator * nonInteger;
    return frac(sin(seed) * 12345.6789);
}

float Random2D(float2 input, float mutator = kDefaultMutator)
{
	float i = (input.x * 131.071) + (input.y * 655.37);
	return Random(i, mutator);
}

int WeightedRandomHeight(float random, float height)
{
	float weightSum = 0;
	for (int index = 0; index < _foliageCount; index++)
	{
		float heightWeight = _foliageData[index].MaxMin.x > height ? 0 : 1;
		weightSum += _foliageData[index].Weight * heightWeight;
	}

	float r = random * weightSum;

	for (int i = 0; i < _foliageCount; i++)
	{
		float heightWeight = _foliageData[i].MaxMin.x > height ? 0 : 1;
		r -= (_foliageData[i].Weight * heightWeight);
		if (r >= 0)
			continue;

		if (heightWeight == 0)
			return -1;

		return i;
	}
	return -1;
}

int WeightedRandom(float random)
{
	float weightSum = 0;
	for (int index = 0; index < _foliageCount; index++)
	{
		weightSum += _foliageData[index].Weight;
	}

	float r = random * weightSum;

	for (int i = 0; i < _foliageCount; i++)
	{
		r -= _foliageData[i].Weight;
		if (r >= 0)
			continue;

		return i;
	}
	return -1;
}

// Geometry Shader Billboard
[maxvertexcount(4)]
void Billboard(point uint p[1] : TEXCOORD, inout TriangleStream<FS_INPUT> triStream)
{
	// ********************* point cloud data  ********************* //

	float3 pos = _PointBuffer[p[0]].Position;
    float3 color = UnpackColor(_PointBuffer[p[0]].ColorA).rgb;
    half height = UnpackHalfLo(_PointBuffer[p[0]].packed0);
    half random = UnpackHalfHi(_PointBuffer[p[0]].packed0);
    half visibility = UnpackHalfLo(_PointBuffer[p[0]].packed1);

    float3 up = UnpackNormalOct(_PointBuffer[p[0]].up);
    	
	// ********************* foliage type data  ********************* //

    int type = WeightedRandomHeight(random, height);
	if (type < 0)
		return;

	float2 minMaxHeight = _foliageData[type].MaxMin;
	float2 offset = _foliageData[type].Offset;

	// ********************* ***************  ********************* //
	
	//Randomized height within the valid range
    float foliageHeight = minMaxHeight.x + ((minMaxHeight.y - minMaxHeight.x) * Random(random, kFoliageHeightMutator));
	foliageHeight = clamp(foliageHeight, minMaxHeight.x, height);
	height = foliageHeight;
	pos -= offset.y * height * up; // offset to handle Roots
	float3 look;

	float halfHeight = 0.5 * height;
    float3 center = pos + up * halfHeight;

#ifdef SHADOW_BILLBOARD
	look = -_WorldSpaceLightPos0;
#else
    look = pos - _WorldSpaceCameraPos;
#endif
	
    float3 right = normalize(-cross(up, look));

	float3 v[4];
    v[0] = pos + halfHeight * right;
    v[1] = pos + halfHeight * right + height * up;
    v[2] = pos - halfHeight * right;
    v[3] = pos - halfHeight * right + height * up;

	float3 uv0 = float3(1.0, 0.0, type);
	float3 uv1 = float3(1.0, 1.0, type);
	float3 uv2 = float3(0.0, 0.0, type);
	float3 uv3 = float3(0.0, 1.0, type);

#ifdef CROSSBOARD
	uv0 = float3(0.5, 0.0, type);
	uv1 = float3(0.5, 0.5, type);
	uv2 = float3(0.0, 0.0, type);
	uv3 = float3(0.0, 0.5, type);
#endif

	FS_INPUT pIn;

    PopulateVertex(pIn, v[0], center, halfHeight, uv0, look, color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, v[1], center, halfHeight, uv1, look, color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, v[2], center, halfHeight, uv2, look, color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, v[3], center, halfHeight, uv3, look, color, visibility);
	triStream.Append(pIn);
}

// Geometry Shader CrossBoard
[maxvertexcount(12)]
void Crossboard(point uint p[1] : TEXCOORD, inout TriangleStream<FS_INPUT> triStream)
{
	// ********************* point cloud data  ********************* //

	float3 pos = _PointBuffer[p[0]].Position;
    float3 color = UnpackColor(_PointBuffer[p[0]].ColorA).rgb;
    half height = UnpackHalfLo(_PointBuffer[p[0]].packed0);
    half random = UnpackHalfHi(_PointBuffer[p[0]].packed0);
    half visibility = UnpackHalfLo(_PointBuffer[p[0]].packed1);

    float3 up = UnpackNormalOct(_PointBuffer[p[0]].up);
    float3 east = UnpackNormalOct(_PointBuffer[p[0]].right);
    float3 north = normalize(cross(east, up));
	
    float3x3 M = float3x3(
		east, // first column
		up, // second column
		north // third column
	);
	
	// ********************* foliage type data  ********************* //

	// calculate the foliage (index) to place down
	int type = WeightedRandomHeight(random, height);
	if (type < 0)
		return;

	//The minimum and maximum height that the chosen foliage asset can be used for
	float2 minMaxHeight = _foliageData[type].MaxMin;
	//Offset used to hide the roots of the foliage asset below ground
	float2 offset = _foliageData[type].Offset;

	// ********************* ***************  ********************* /d/

	//Randomized height within the valid range
    float foliageHeight = minMaxHeight.x + ((minMaxHeight.y - minMaxHeight.x) * Random(random, kFoliageHeightMutator));
	foliageHeight = clamp(foliageHeight, minMaxHeight.x, height);
	height = foliageHeight;
	pos -= offset.y * height * up;		// offset to handle Roots

	float angle = (Random(random, 0.131) * 2 - 1) * kPi;
	float3 randDir = float3(cos(angle), 0, sin(angle));
    float3 right = mul(randDir, M);
	
	//float3 right = randDir;
    float3 front = normalize(-cross(up, right));

    float halfHeight = 0.5 * height;
    float3 center = pos + up * halfHeight;

	float3 wind = float3(0, 0, 0);

    float3 WorldPos = _WorldOffset * 2 + pos;
    float curve = tex2Dlod(_WindTexture, float4(WorldPos.xz * 0.0005 - _Time.x * _WindVector.xy * _WindVector.z * 0.1, 1, 1)).r;
    curve = saturate(curve + (_WindVector.z / 100)) * 0.01;
	
    if (length(_WindVector.xy) > 0)
	{
        float2 windDir = normalize(_WindVector.xy);
        wind = float3(windDir.x, 0, windDir.y) * curve * _WindVector.z * foliageHeight;
    }

	// *********** front points ***********

	float3 f[4];
    f[0] = pos + halfHeight * right;
    f[1] = pos + halfHeight * right + height * up + wind;
    f[2] = pos - halfHeight * right;
    f[3] = pos - halfHeight * right + height * up + wind;

	// *********** Right points ***********

	float3 r[4];
    r[0] = pos + halfHeight * front;
    r[1] = pos + halfHeight * front + height * up + wind;
    r[2] = pos - halfHeight * front;
    r[3] = pos - halfHeight * front + height * up + wind;

	// *********** Top points ***********

	float3 t[4];
    t[0] = center + halfHeight * right - halfHeight * front + wind * 0.5;
    t[1] = center + halfHeight * right + halfHeight * front + wind * 0.5;
    t[2] = center - halfHeight * right - halfHeight * front + wind * 0.5;
    t[3] = center - halfHeight * right + halfHeight * front + wind * 0.5;

	// *********** Crossboards ***********

	FS_INPUT pIn;

	// *********** front face ***********

    PopulateVertex(pIn, f[0], center, halfHeight, float3(0.5, 0.0, type), normalize(front), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, f[1], center, halfHeight, float3(0.5, 0.5, type), normalize(front), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, f[2], center, halfHeight, float3(0.0, 0.0, type), normalize(front), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, f[3], center, halfHeight, float3(0.0, 0.5, type), normalize(front), color, visibility);
	triStream.Append(pIn);

	// *********** right face ***********

	triStream.RestartStrip();

    PopulateVertex(pIn, r[0], center, halfHeight, float3(0.5, 0.5, type), normalize(right), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, r[1], center, halfHeight, float3(0.5, 1.0, type), normalize(right), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, r[2], center, halfHeight, float3(0.0, 0.5, type), normalize(right), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, r[3], center, halfHeight, float3(0.0, 1.0, type), normalize(right), color, visibility);
	triStream.Append(pIn);

	// *********** top face ***********

	triStream.RestartStrip();

    PopulateVertex(pIn, t[0], center, halfHeight, float3(1.0, 0.0, type), normalize(up), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, t[1], center, halfHeight, float3(1.0, 0.5, type), normalize(up), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, t[2], center, halfHeight, float3(0.5, 0.0, type), normalize(up), color, visibility);
	triStream.Append(pIn);

    PopulateVertex(pIn, t[3], center, halfHeight, float3(0.5, 0.5, type), normalize(up), color, visibility);
	triStream.Append(pIn);
}

// Geometry Shader Grass
[maxvertexcount(48)]
void Grass(point uint p[1] : TEXCOORD, inout TriangleStream<FS_INPUT> triStream)
{
	// ********************* point cloud data  ********************* //

	float3 pos = _PointBuffer[p[0]].Position;
    float3 color = UnpackColor(_PointBuffer[p[0]].ColorA).rgb;
    half height = UnpackHalfLo(_PointBuffer[p[0]].packed0);
    half random = UnpackHalfHi(_PointBuffer[p[0]].packed0);
    half visibility = UnpackHalfLo(_PointBuffer[p[0]].packed1);

    float3 up = UnpackNormalOct(_PointBuffer[p[0]].up);
    float3 east = UnpackNormalOct(_PointBuffer[p[0]].right);
    float3 north = normalize(cross(east, up));
	
    float3x3 M = float3x3(
		east, // first column
		up, // second column
		north // third column
	);
	
	// ********************* foliage type data  ********************* //

	int type = floor(random * _foliageCount);	// calculate the foliage (index) to place down
	
    type = WeightedRandomHeight(random, height);

	float2 minMaxHeight = _foliageData[type].MaxMin;
	float2 offset = _foliageData[type].Offset;
	float weight = _foliageData[type].Weight;

	// *********** billboard ***********
	float3 center = pos;

	//float distance = clamp((length(_WorldSpaceCameraPos - pos) / 20), 0, 1);

	for (uint i = 0; i < 12; i++)
	{
		float rand = Random(random + i, 0.912);
        float angle = (rand * 2 - 1) * kPi;
        float3 randDir = mul(float3(cos(angle), 0, sin(angle)), M);
        float3 pos = center + randDir * rand * 1.5;

		float rand02 = Random(random + i, 0.643);
        angle = (rand02 * 2 - 1) * kPi;
        randDir = mul(float3(cos(angle), 0, sin(angle)), M);

		float3 camDir = _WorldSpaceCameraPos - pos;
		float blend = normalize(camDir).y;
		camDir = normalize(camDir);

		float3 billboardRight = cross(up, camDir);
		float rotateDir = sign(dot(billboardRight, randDir));

		float3 right = lerp(billboardRight * rotateDir, randDir, blend);
		float3 front = normalize(-cross(up, right));

		float side = sign(dot(camDir, front));

        float foliageHeight = minMaxHeight.x + ((minMaxHeight.y - minMaxHeight.x) * Random(random, kFoliageHeightMutator));
		float h = foliageHeight;
        float halfHeight = 0.5 * h;
		
		pos -= offset.y * h * up; // offset to handle Roots

		float3 wind = float3(0, 0, 0);

        float3 WorldPos = _WorldOffset * 2 + pos;
        float curve = tex2Dlod(_WindTexture, float4(WorldPos.xz * 0.0005 - _Time.x * _WindVector.xy * _WindVector.z * 0.1, 1, 1)).r;
		
        curve = saturate(curve + (_WindVector.z / 100)) * 0.1;
		
		if (length(_WindVector.xy) > 0)
		{
			float2 windDir = normalize(_WindVector.xy);
            wind = float3(windDir.x, 0, windDir.y) * curve * _WindVector.z * h;
        }

		float3 f[4];
		// right bottom
        f[0] = pos + halfHeight * right;
		// right top
        f[1] = pos + halfHeight * right + h * up + wind;
		// left bottom
        f[2] = pos - halfHeight * right;
		// left top
        f[3] = pos - halfHeight * right + h * up + wind;

		FS_INPUT pIn;

		// right bottom
        PopulateVertex(pIn, f[0], center, height, float3(1.0, 0.0, type), normalize(front * side + right + up * 0.5), color, visibility);
		triStream.Append(pIn);
		// right top
        PopulateVertex(pIn, f[1], center, height, float3(1.0 + (rand * 0.2), 1.0 + (rand02 * 0.2), type), normalize(up + right), color, visibility);
		triStream.Append(pIn);
		// left bottom
        PopulateVertex(pIn, f[2], center, height, float3(0.0, 0.0, type), normalize(front * side - right + up * 0.5), color, visibility);
		triStream.Append(pIn);
		// left top
        PopulateVertex(pIn, f[3], center, height, float3(0.0 + (rand02 * 0.2), 1.0 + (rand * 0.2), type), normalize(up + right), color, visibility);
		triStream.Append(pIn);

		triStream.RestartStrip();
	}
}

[maxvertexcount(48)]
void geo(point uint p[1] : TEXCOORD, inout TriangleStream<FS_INPUT> triStream)
{
#ifdef CROSSBOARD_ON
	Crossboard(p, triStream);
#else
	Grass(p, triStream);
#endif
}
