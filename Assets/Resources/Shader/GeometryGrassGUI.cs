using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class GeometryGrassGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
    {
        // Start a new section for grass albedo colors.
        GUILayout.Label("Grass Albedo Color", EditorStyles.boldLabel);

        var target = editor.target as Material;

        var baseColor = FindProperty("_BaseColor", properties);
        var baseColorLabel = new GUIContent(baseColor.displayName, "Color of the grass blade at the ground level!!!!!!!!!!!");
        editor.ShaderProperty(baseColor, baseColorLabel);

        var tipColor = FindProperty("_TipColor", properties);
        var tipColorLabel = new GUIContent(tipColor.displayName, "Color of the grass blade at the very tip");
        editor.ShaderProperty(tipColor, tipColorLabel);

        var baseTexture = FindProperty("_BaseTex", properties);
        var baseTextureLabel = new GUIContent(baseTexture.displayName, "Tint texture for each grass blade");
        editor.TexturePropertySingleLine(baseTextureLabel, baseTexture);

        GUILayout.Space(20);

        // Start a new section for grass size properties.
        GUILayout.Label("Grass Blade Size", EditorStyles.boldLabel);

        var bladeWidthMin = FindProperty("_BladeWidthMin", properties);
        var bladeWidthMinLabel = new GUIContent(bladeWidthMin.displayName, "Minimum width (in meters) of each grass blade.");
        editor.ShaderProperty(bladeWidthMin, bladeWidthMinLabel);

        var bladeWidthMax = FindProperty("_BladeWidthMax", properties);
        var bladeWidthMaxLabel = new GUIContent(bladeWidthMax.displayName, "Maximum width (in meters) of each grass blade.");
        editor.ShaderProperty(bladeWidthMax, bladeWidthMaxLabel);

        var bladeHeightMin = FindProperty("_BladeHeightMin", properties);
        var bladeHeightMinLabel = new GUIContent(bladeHeightMin.displayName, "Minimum height (in meters) of each grass blade.");
        editor.ShaderProperty(bladeHeightMin, bladeHeightMinLabel);

        var bladeHeightMax = FindProperty("_BladeHeightMax", properties);
        var bladeHeightMaxLabel = new GUIContent(bladeHeightMax.displayName, "Maximum height (in meters) of each grass blade.");
        editor.ShaderProperty(bladeHeightMax, bladeHeightMaxLabel);

        GUILayout.Space(20);

		// Start a new section for grass bend properties.
		GUILayout.Label("Grass Bend", EditorStyles.boldLabel);

        var bladeBendDistance = FindProperty("_BladeBendDistance", properties);
        var bladeBendDistanceLabel = new GUIContent(bladeBendDistance.displayName, "The maximum distance each blade can bend forward.");
        editor.ShaderProperty(bladeBendDistance, bladeBendDistanceLabel);

        var bladeBendCurve = FindProperty("_BladeBendCurve", properties);
        var bladeBendCurveLabel = new GUIContent(bladeBendCurve.displayName, "The amount of curvature of each blade of grass.");
        editor.ShaderProperty(bladeBendCurve, bladeBendCurveLabel);

        var bladeBendDelta = FindProperty("_BladeBendDelta", properties);
        var bladeBendDeltaLabel = new GUIContent(bladeBendDelta.displayName, "The amount of bend variation between blades.");
        editor.ShaderProperty(bladeBendDelta, bladeBendDeltaLabel);

        GUILayout.Space(20);

        // Start a new section for tessellation.
        GUILayout.Label("Tessellation", EditorStyles.boldLabel);

        var tessellationAmount = FindProperty("_TessAmount", properties);
        var tessellationAmountLabel = new GUIContent(tessellationAmount.displayName, "The higher this value, the closer the blades are.");
        editor.ShaderProperty(tessellationAmount, tessellationAmountLabel);

        var tessellationMinDistance = FindProperty("_TessMinDistance", properties);
        var tessellationMinDistanceLabel = new GUIContent(tessellationMinDistance.displayName, "Applies full tessellation when the camera is closer than this distance.");
        editor.ShaderProperty(tessellationMinDistance, tessellationMinDistanceLabel);

        var tessellationMaxDistance = FindProperty("_TessMaxDistance", properties);
        var tessellationMaxDistanceLabel = new GUIContent(tessellationMaxDistance.displayName, "Applies no extra tessellation when the camera is further than this distance.");
        editor.ShaderProperty(tessellationMaxDistance, tessellationMaxDistanceLabel);

        GUILayout.Space(20);

        // Start a new section for the grass visibility map.
        GUILayout.Label("Grass Visibility", EditorStyles.boldLabel);

        var grassMap = FindProperty("_GrassMap", properties);
        var grassMapLabel = new GUIContent(grassMap.displayName, "Visibility map. White = grass at full height, black = no grass");

        EditorGUI.BeginChangeCheck();
        editor.TexturePropertySingleLine(grassMapLabel, grassMap);
        if(EditorGUI.EndChangeCheck())
        {
            SetKeywordValue("VISIBILITY_ON", grassMap.textureValue, target);
        }

        if(grassMap.textureValue)
        {
            editor.TextureScaleOffsetProperty(grassMap);

            var grassThreshold = FindProperty("_GrassThreshold", properties);
            var grassThresholdLabel = new GUIContent(grassThreshold.displayName, "Grass is rendered when visibility map values exceed this threshold.");
            editor.ShaderProperty(grassThreshold, grassThresholdLabel);

            var grassFalloff = FindProperty("_GrassFalloff", properties);
            var grassFalloffLabel = new GUIContent(grassFalloff.displayName, "The amount of falloff between no grass and full grass.");
            editor.ShaderProperty(grassFalloff, grassFalloffLabel);
        }

        GUILayout.Space(20);

        // Start a new section for the wind map.
        GUILayout.Label("Wind", EditorStyles.boldLabel);

        var windMap = FindProperty("_WindMap", properties);
        var windMapLabel = new GUIContent(windMap.displayName, "Wind map. Each pixel is a vector controlling the wind direction.");

        EditorGUI.BeginChangeCheck();
        editor.TexturePropertySingleLine(windMapLabel, windMap);
        if(EditorGUI.EndChangeCheck())
        {
            SetKeywordValue("WIND_ON", windMap.textureValue, target);
        }

        if(windMap.textureValue)
        {
            editor.TextureScaleOffsetProperty(windMap);

            var windVelocity = FindProperty("_WindVelocity", properties);
            var windVelocityLabel = new GUIContent(windVelocity.displayName, "Strength and direction of the wind, expressed as a vector.");
            editor.ShaderProperty(windVelocity, windVelocityLabel);

            var windFrequency = FindProperty("_WindFrequency", properties);
            var windFrequencyLabel = new GUIContent(windFrequency.displayName, "How frequently the wind pulses over the grass.");
            editor.ShaderProperty(windFrequency, windFrequencyLabel);
        }
    }

    private void SetKeywordValue(string keyword, bool state, Material target)
    {
        if (state)
        {
            target.EnableKeyword(keyword);
        }
        else
        {
            target.DisableKeyword(keyword);
        }
    }
}
