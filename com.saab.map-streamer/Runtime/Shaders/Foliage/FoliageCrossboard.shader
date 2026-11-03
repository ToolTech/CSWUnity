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

Shader "Custom/Foliage/Billboard"
{
	Properties
	{
		_MainTexArray("Tex2DArray (RGB)", 2DArray) = "white" {}
		_CutoffMax("Alpha cutoff close", Range(0, 1)) = 0.2
		_CutoffMin("Alpha cutoff far", Range(0, 1)) = 0.2
		_Threshold("Alpha cutoff distance", float) = 1000
		[MaterialToggle] _isToggled("Up Normals", Float) = 1
	}

		SubShader
		{
			Tags { "Thermal"="Foliage" }
			CGINCLUDE

			#pragma multi_compile __ CROSSBOARD_ON

			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "UnityGBuffer.cginc"
			#include "UnityStandardUtils.cginc"
			#include "UnityPBSLighting.cginc"
			#include "CompressionUtil.cginc"
			#include "FoliageShaderTypes.cginc"

			// ---- Global ----
			sampler2D _WindTexture;
			float3 _WorldOffset;
			float3 _WindVector;

			// ----------------

			uint _foliageCount;
			float _CutoffMax;
			float _CutoffMin;
			float _Threshold;
			float _isToggled;
	
			sampler2D _PerlinNoise;

			UNITY_DECLARE_TEX2DARRAY(_MainTexArray);

			// ****** foliage point cloud ******
			StructuredBuffer<FoliagePoint> _PointBuffer;
			StructuredBuffer<FoliageShaderData> _foliageData;

			// Vertex shader
			uint vert(uint id : SV_VertexID, uint instanceID : SV_InstanceID) : TEXCOORD
			{
				return id + instanceID * 64000;
			}

			// Alpha Blended weight
			float AlphaWeight(float4 color, float depth)
			{
				float weight = max(min(1.0, max(max(color.r, color.g), color.b) * color.a), color.a) *
					clamp(0.03 / (0.00001 + pow(depth / 200, 4.0)), 0.01, 3000);
				return weight;
			}

			float3 SphereProjectedNormal(FS_INPUT i)
			{
				float3 arrayUV = float3(i.uv.xy, i.layer);

				fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, arrayUV);

				float3 camToPixelDir = normalize(i.worldPos - _WorldSpaceCameraPos);
				float3 camToCenter = (i.center - _WorldSpaceCameraPos);
				float centerProjection = dot(camToPixelDir, camToCenter);
				float3 rightAnglePoint = _WorldSpaceCameraPos + camToPixelDir * centerProjection;

				float3 centerOffset = i.center - rightAnglePoint;
				float centerDistanceSquared = dot(centerOffset, centerOffset);

				float radiusSquared = i.radius * i.radius;

				if (centerDistanceSquared > radiusSquared)
					return normalize(rightAnglePoint - i.center);

				float rightAngleDistance = sqrt(radiusSquared - centerDistanceSquared);
				float3 projectedPoint = rightAnglePoint - camToPixelDir * (rightAngleDistance * (10 * col.r));

				return normalize(projectedPoint - i.center);
			}

			float CutoffDistance(float distance)
			{
				float c = _CutoffMax - ((_CutoffMin - _CutoffMax) / _Threshold) * -distance;
				c = clamp(c, _CutoffMin, _CutoffMax);
				return c;
			}

			float2 VectorToSphereUV(float3 normal)
			{
				float u = 0.5 + atan2(normal.z, normal.x) / (2 * 3.14159265359);
				float v = 0.5 - asin(normal.y) / 3.14159265359;
				return float2(u, v);
			}

			float invLerp(float from, float to, float value) 
            {
                return (value - from) / (to - from);
            }

            float remap(float origFrom, float origTo, float targetFrom, float targetTo, float value) 
            {
                float rel = invLerp(origFrom, origTo, value);
                return lerp(targetFrom, targetTo, rel);
            }

			ENDCG

			// ********* Opaque alpha cutoff - Deferred  *********
			Pass
			{
				Name "Deferred"
				Tags
				{ 
					"Queue" = "AlphaTest" 
					"RenderType" = "Opaque"
					"LightMode" = "Deferred"
				}

				Cull Off
				Blend Off
				CGPROGRAM
				#include "Geometry.cginc" 
				#pragma geometry geo

				#pragma target 5.0
				#pragma multi_compile_prepassfinal noshadowmask nodynlightmap nodirlightmap nolightmap noshadow 
				
				void frag(FS_INPUT i, out half4 outGBuffer0 : SV_Target0, out half4 outGBuffer1 : SV_Target1, out half4 outGBuffer2 : SV_Target2, out half4 outEmission : SV_Target3)
				{
					float3 arrayUV = float3(i.uv.xy, i.layer);
					fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, arrayUV);
					//half3 tnormal = UnpackNormal(tex2D(_NormalMap, i.arrayUV));

					float c = CutoffDistance(distance(i.center, _WorldSpaceCameraPos));
					clip(col.a - c);


					fixed3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
					float depth = i.pos.z / i.pos.w;
					
					float3 LookDir = normalize(_WorldSpaceCameraPos - i.center);
					float3 normal = UnpackNormalOct(i.normal);

					//float blend = dot(i.normal, LookDir);

					// *********** sphere normals ************* 
					float3 sphereNormal = SphereProjectedNormal(i);
					float2 SphereUV = VectorToSphereUV(sphereNormal);	// sphere normal uv coord 
					
					float3 finalNormal = sphereNormal;

					float4 ColorA = UnpackColor(i.colorA);

					col.rgb = col.rgb * 0.75 + ColorA.rgb * 0.25;
	
					if (_isToggled)
					{
						finalNormal = normal;
						col.rgb = col.rgb * arrayUV.y + ColorA.rgb * 0.4 * (1 - arrayUV.y);
					}

					const half4x4 thresholdMatrix =
					{
						0, 8, 2, 10,
						12, 4, 14, 6,
						3, 11, 1, 9,
						15, 7, 13, 5
					};

					fixed threshold = thresholdMatrix[i.pos.x % 4][i.pos.y % 4] / 17;
					float power = 3;
					clip(ColorA.a - threshold);

					if(1 - abs(normal.y) < 0.01)
					{
						float upGradient = abs(dot(LookDir, normal));
						float angle = 2 * acos(upGradient) / 3.141592653589793238462643;
						float linearAngle = remap(0.3, 0.7, 1, 0, angle);

						if(linearAngle <= threshold)
							discard;
					}


					#ifdef UNITY_COMPILER_HLSL
						SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
					#else
						SurfaceOutputStandardSpecular o;
					#endif

					o.Albedo = col.rgb;//normalize(finalNormal.xyz).rgb;
					o.Emission = 0.0;
					o.Alpha = col.a;
					o.Occlusion = 1.0;
					o.Smoothness = 0.2;
					o.Specular = 0.0;
					o.Normal = finalNormal; // This is used only for ambient occlusion

					// Setup lighting environment
					UnityGI gi;
					UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
					gi.indirect.diffuse = 0;
					gi.indirect.specular = 0;
					gi.light.color = 0;
					gi.light.dir = fixed3(0, 1, 0);

					// Call GI (lightmaps/SH/reflections) lighting function
					UnityGIInput giInput;
					UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
					giInput.light = gi.light;
					giInput.worldPos = i.worldPos;
					giInput.worldViewDir = worldViewDir;
					giInput.atten = 1;
					giInput.lightmapUV = 0.0;


					#if UNITY_SHOULD_SAMPLE_SH
						//giInput.ambient = IN.sh;
					#else
						giInput.ambient.rgb = 0.0;
					#endif

					giInput.probeHDR[0] = unity_SpecCube0_HDR;
					giInput.probeHDR[1] = unity_SpecCube1_HDR;

					#if defined( UNITY_SPECCUBE_BLENDING ) || defined( UNITY_SPECCUBE_BOX_PROJECTION )
						giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
					#endif

					#ifdef UNITY_SPECCUBE_BOX_PROJECTION
						giInput.boxMax[0] = unity_SpecCube0_BoxMax;
						giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
						giInput.boxMax[1] = unity_SpecCube1_BoxMax;
						giInput.boxMin[1] = unity_SpecCube1_BoxMin;
						giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
					#endif

					// Standard specular global illumination
					LightingStandardSpecular_GI(o, giInput, gi);

					// Call lighting function to output g-buffer
					outEmission = LightingStandardSpecular_Deferred(o, worldViewDir, gi, outGBuffer0, outGBuffer1, outGBuffer2);
					outGBuffer2.w = 0;

					#ifndef UNITY_HDR_ON
						outEmission.rgb = exp2(-outEmission.rgb);
					#endif
				}
				ENDCG
			}

			// ********* SHADOW CASTER PASS *********	
			Pass
			{
				Name "ShadowCaster"
				Tags { "LightMode" = "ShadowCaster" }
				//ColorMask 0				// won't write color to frame buffer
				//Cull back

				CGPROGRAM
				#define	SHADOW_BILLBOARD
				#define	CROSSBOARD
				#include "Geometry.cginc" 

				#pragma multi_compile_shadowcaster
				#pragma multi_compile_instancing	// allow instanced shadow pass for most of the shaders

				#pragma geometry Billboard

				// Fragment Shader 
				float4 frag(FS_INPUT i) : COLOR
				{
					float3 arrayUV = float3(i.uv.xy, i.layer);
					fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, arrayUV);
					float c = CutoffDistance(distance(i.center, _WorldSpaceCameraPos));
					clip(col.a - c);

					return float4(0,0,0,0);
				}
				ENDCG
			}
	}
}
