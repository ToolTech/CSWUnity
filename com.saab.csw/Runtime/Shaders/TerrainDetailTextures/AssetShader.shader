Shader "Custom/AssetShader"
{
    Properties
    {
		[NoScaleOffset] _MainTex("Satellite / Coarse texture", 2D) = "white" {}
    }
    SubShader
    {
       Name "Deferred"
		Tags
		{ 
			"Queue" = "Geometry" 
			"RenderType" = "Opaque"
			"LightMode" = "Deferred"
			"Thermal" = "Asset"
		}

		Cull Back
		Blend Off
        LOD 100

        CGINCLUDE

		#include "UnityCG.cginc"

		#include "UnityGBuffer.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityPBSLighting.cginc"

		#pragma exclude_renderers nomrt

		#pragma target 5.0
		#pragma multi_compile_prepassfinal noshadowmask nodynlightmap nodirlightmap nolightmap noshadow 

        struct appdata
        {
            float4 position : POSITION;
			float2 uv : TEXCOORD0;
			float3 normal : NORMAL;
			uint vertexID : SV_VertexID;  // System value for vertex ID
        };

        struct v2f
        {
            float4 position : SV_POSITION;
			float2 uv : TEXCOORD0;
			float3 normal : TEXCOORD2;
			float4 worldPos : TEXCOORD3;
        };

		sampler2D _MainTex;
		StructuredBuffer<float3> _NormalBuffer;

        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            v2f vert (appdata v)
            {
                v2f o;
				o.position = UnityObjectToClipPos(v.position);
				o.uv = v.uv;
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.worldPos = mul(unity_ObjectToWorld, v.position);
                return o;
            }

			void frag(v2f i, out half4 outGBuffer0 : SV_Target0, out half4 outGBuffer1 : SV_Target1, out half4 outGBuffer2 : SV_Target2, out half4 outEmission : SV_Target3)
			{
				float4 satellite = tex2D(_MainTex, i.uv);
				fixed3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));

				clip(satellite.a - 0.5);

				// ************ Set Deferred Buffer ************
				
				#ifdef UNITY_COMPILER_HLSL
					SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
				#else
					SurfaceOutputStandardSpecular o;
				#endif

				o.Albedo = satellite.rgb;
				o.Emission = 0.0;
				o.Alpha = satellite.a;
				o.Occlusion = 1.0;
				o.Smoothness = 0.0;
				o.Specular = 0.0;
				o.Normal = i.normal;

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

		UsePass "Standard/SHADOWCASTER"
    }
}
