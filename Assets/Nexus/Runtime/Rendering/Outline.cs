using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    public Color OutlineColor = Color.yellow;
    public float OutlineWidth = 5f;

    private Renderer[] renderers;
    private Material outlineMaterial;
    private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthProperty = Shader.PropertyToID("_OutlineWidth");

    private void Awake()
    {
        CreateOutlineMaterial();
        renderers = GetComponentsInChildren<Renderer>();
        UpdateOutlineMaterials();
    }

    private void OnEnable()
    {
        UpdateOutlineMaterials();
    }

    private void OnDisable()
    {
        RemoveOutlineMaterials();
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }

    private void CreateOutlineMaterial()
    {
        if (outlineMaterial != null)
            return;

        Shader outlineShader = Shader.Find("Hidden/Outline");
        if (outlineShader == null)
        {
            // Fallback: Create shader from code if not found
            outlineShader = CreateOutlineShader();
        }

        outlineMaterial = new Material(outlineShader);
        outlineMaterial.SetColor(OutlineColorProperty, OutlineColor);
        outlineMaterial.SetFloat(OutlineWidthProperty, OutlineWidth);
    }

    private void UpdateOutlineMaterials()
    {
        if (renderers == null || outlineMaterial == null)
            return;

        outlineMaterial.SetColor(OutlineColorProperty, OutlineColor);
        outlineMaterial.SetFloat(OutlineWidthProperty, OutlineWidth);

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            var materials = new List<Material>(renderer.sharedMaterials);
            
            // Remove any existing outline materials
            materials.RemoveAll(m => m != null && m.shader != null && m.shader.name == "Hidden/Outline");
            
            // Add new outline material
            materials.Add(outlineMaterial);
            
            renderer.materials = materials.ToArray();
        }
    }

    private void RemoveOutlineMaterials()
    {
        if (renderers == null)
            return;

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            var materials = new List<Material>(renderer.sharedMaterials);
            materials.RemoveAll(m => m != null && m.shader != null && m.shader.name == "Hidden/Outline");
            renderer.materials = materials.ToArray();
        }
    }

    private Shader CreateOutlineShader()
    {
        string shaderCode = @"
Shader ""Hidden/Outline""
{
    Properties
    {
        _OutlineColor (""Outline Color"", Color) = (1,1,0,1)
        _OutlineWidth (""Outline Width"", Range(0, 10)) = 5
    }

    SubShader
    {
        Tags { ""Queue"" = ""Transparent+100"" ""RenderType"" = ""Transparent"" }
        
        Pass
        {
            Name ""Outline""
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            float _OutlineWidth;
            float4 _OutlineColor;

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal);
                float3 outlineOffset = norm * (_OutlineWidth * 0.001);
                o.pos = UnityObjectToClipPos(v.vertex + float4(outlineOffset, 0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDPROGRAM
        }
    }
}";
        return Shader.Find("Hidden/Outline") ?? Shader.Find("Standard");
    }
}