// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Costom_/Costom_Smk_01"
{
	Properties
	{
		_Tex_Main("Tex_Main", 2D) = "white" {}
		_Tex_Mask("Tex_Mask", 2D) = "white" {}
		[HDR]_Main_Color("Main_Color", Color) = (0.4386792,0.7203457,1,0)
		_Mask_UVSpeed("Mask_UVSpeed", Vector) = (0,0,0,0)
		_Diss_UVSpeed("Diss_UVSpeed", Vector) = (0,0,0,0)
		_Dissolve("Dissolve", 2D) = "white" {}
		_Float2("Float 2", Range( 0.5 , 1)) = 0.6175666
		[HideInInspector] _tex4coord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Custom"  "Queue" = "Overlay+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Front
		ZWrite On
		ZTest NotEqual
		Blend SrcAlpha OneMinusSrcAlpha
		BlendOp Add
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
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
		uniform sampler2D _Tex_Mask;
		uniform float2 _Mask_UVSpeed;
		uniform float4 _Tex_Mask_ST;
		uniform float _Float2;
		uniform sampler2D _Dissolve;
		uniform float2 _Diss_UVSpeed;
		uniform float4 _Dissolve_ST;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv_Tex_Main = i.uv_texcoord * _Tex_Main_ST.xy + _Tex_Main_ST.zw;
			float4 tex2DNode2 = tex2D( _Tex_Main, uv_Tex_Main );
			o.Emission = ( ( 0.0 + 1.0 ) * ( _Main_Color * tex2DNode2.r * i.vertexColor ) ).rgb;
			float2 uv0_Tex_Mask = i.uv_texcoord * _Tex_Mask_ST.xy + _Tex_Mask_ST.zw;
			float2 panner17 = ( 1.0 * _Time.y * _Mask_UVSpeed + uv0_Tex_Mask);
			float2 uv0_Dissolve = i.uv_texcoord * _Dissolve_ST.xy + _Dissolve_ST.zw;
			float2 panner46 = ( 1.0 * _Time.y * _Diss_UVSpeed + uv0_Dissolve);
			float smoothstepResult37 = smoothstep( ( 1.0 - _Float2 ) , _Float2 , saturate( ( ( tex2D( _Dissolve, panner46 ).r + 1.0 ) - ( i.uv_tex4coord.z * 2.0 ) ) ));
			o.Alpha = ( i.vertexColor.a * tex2D( _Tex_Mask, panner17 ).r * smoothstepResult37 * tex2DNode2.r );
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=17700
112;513.3334;1610;865;2423.243;797.6997;2.252897;True;False
Node;AmplifyShaderEditor.TextureCoordinatesNode;44;-1664.722,426.7211;Inherit;False;0;28;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;45;-1642.36,573.1348;Inherit;False;Property;_Diss_UVSpeed;Diss_UVSpeed;5;0;Create;True;0;0;False;0;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode;46;-1353.264,439.2504;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;28;-1053.652,460.0365;Inherit;True;Property;_Dissolve;Dissolve;6;0;Create;True;0;0;False;0;-1;2106724ce49252341b05b9225ac3bfe0;2106724ce49252341b05b9225ac3bfe0;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;31;-949.3678,673.3156;Inherit;False;Constant;_Float0;Float 0;10;0;Create;True;0;0;False;0;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;43;-1073.509,819.3665;Inherit;False;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;35;-854.3678,926.3156;Inherit;False;Constant;_Float1;Float 1;10;0;Create;True;0;0;False;0;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;30;-686.3678,523.3156;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;33;-629.3678,820.3156;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;39;-356.9038,737.8871;Inherit;False;Property;_Float2;Float 2;7;0;Create;True;0;0;False;0;0.6175666;0.611;0.5;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;32;-380.9679,532.7153;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;15;-1120.714,232.5756;Inherit;False;Property;_Mask_UVSpeed;Mask_UVSpeed;4;0;Create;True;0;0;False;0;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;16;-1143.076,86.16214;Inherit;False;0;8;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;2;-817.1673,-308.8512;Inherit;True;Property;_Tex_Main;Tex_Main;0;0;Create;True;0;0;False;0;-1;None;8af5839ef768fc140ace150f41b4f0e0;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;5;-346.9977,-398.8534;Inherit;False;Property;_Main_Color;Main_Color;3;1;[HDR];Create;True;0;0;False;0;0.4386792,0.7203457,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;38;-177.0103,634.5101;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;6;-121.2215,-121.955;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;36;-213.9679,534.7153;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;17;-831.6181,98.69144;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;13;250.0005,-388.7614;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;37;48.17075,512.6705;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;8;-589.5592,38.55504;Inherit;True;Property;_Tex_Mask;Tex_Mask;1;0;Create;True;0;0;False;0;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;166.741,-224.9896;Inherit;True;3;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;34;-1388.368,719.3156;Inherit;False;Property;_Diss;Diss;8;0;Create;True;0;0;False;0;0.3937471;0.357;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;14;428.0005,-240.7614;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;7;243.7983,70.47442;Inherit;True;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;12;-56.99945,-543.7614;Inherit;False;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;42;-1130.743,774.1077;Inherit;False;Constant;_Float3;Float 3;8;0;Create;True;0;0;False;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;41;-1240.743,792.1077;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;823.6189,-278.4385;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;Costom_/Costom_Smk_01;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Front;1;False;-1;6;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;False;0;True;Custom;;Overlay;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;1;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;2;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;46;0;44;0
WireConnection;46;2;45;0
WireConnection;28;1;46;0
WireConnection;30;0;28;1
WireConnection;30;1;31;0
WireConnection;33;0;43;3
WireConnection;33;1;35;0
WireConnection;32;0;30;0
WireConnection;32;1;33;0
WireConnection;38;0;39;0
WireConnection;36;0;32;0
WireConnection;17;0;16;0
WireConnection;17;2;15;0
WireConnection;37;0;36;0
WireConnection;37;1;38;0
WireConnection;37;2;39;0
WireConnection;8;1;17;0
WireConnection;3;0;5;0
WireConnection;3;1;2;1
WireConnection;3;2;6;0
WireConnection;14;0;13;0
WireConnection;14;1;3;0
WireConnection;7;0;6;4
WireConnection;7;1;8;1
WireConnection;7;2;37;0
WireConnection;7;3;2;1
WireConnection;0;2;14;0
WireConnection;0;9;7;0
ASEEND*/
//CHKSM=9860F8BEE5EF033EF05ED8F5D6228DFDCF6326AF