// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Costom_/Par_01_A_2"
{
	Properties
	{
		_Tex_Main("Tex_Main", 2D) = "white" {}
		[HDR]_Main_Color("Main_Color", Color) = (0.4386792,0.7203457,1,0)
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _tex4coord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Custom"  "Queue" = "Overlay+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		BlendOp Add
		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf Unlit keepalpha noshadow 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
			float4 uv_tex4coord;
		};

		uniform float4 _Main_Color;
		uniform sampler2D _Tex_Main;
		uniform float4 _Tex_Main_ST;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv0_Tex_Main = i.uv_texcoord * _Tex_Main_ST.xy + _Tex_Main_ST.zw;
			float4 tex2DNode2 = tex2D( _Tex_Main, uv0_Tex_Main );
			o.Emission = ( _Main_Color * tex2DNode2 * i.vertexColor * ( i.uv_tex4coord.z + 1.0 ) ).rgb;
			o.Alpha = ( i.vertexColor.a * tex2DNode2.a );
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=17700
112;513.3334;1610;865;1544.556;661.5247;1.690191;True;False
Node;AmplifyShaderEditor.TextureCoordinatesNode;17;-1037.656,-167.3599;Inherit;False;0;2;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;13;-358.441,-607.056;Inherit;False;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;2;-713.3658,-126.8483;Inherit;True;Property;_Tex_Main;Tex_Main;0;0;Create;True;0;0;False;0;-1;None;992f20e31446af44b80882c77fdab0ef;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;5;-571.748,-430.1809;Inherit;False;Property;_Main_Color;Main_Color;2;1;[HDR];Create;True;0;0;False;0;0.4386792,0.7203457,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;15;-62.58123,-421.9397;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;6;-171.7012,-168.9409;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;192.3558,-268.5122;Inherit;True;4;4;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;7;190.8444,41.66258;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;747.0585,-297.9431;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;Costom_/Par_01_A_2;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;2;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;False;0;True;Custom;;Overlay;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;1;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;2;1;17;0
WireConnection;15;0;13;3
WireConnection;3;0;5;0
WireConnection;3;1;2;0
WireConnection;3;2;6;0
WireConnection;3;3;15;0
WireConnection;7;0;6;4
WireConnection;7;1;2;4
WireConnection;0;2;3;0
WireConnection;0;9;7;0
ASEEND*/
//CHKSM=BB9571EC15E7B25A201B21A0817E02B09C8D71AB