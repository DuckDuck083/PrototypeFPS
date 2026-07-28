using UnityEngine;

public static class RuntimeMaterials
{
    public static Material Lit(Color color)
    {
        return Create("Universal Render Pipeline/Lit", color);
    }

    public static Material Unlit(Color color)
    {
        return Create("Universal Render Pipeline/Unlit", color);
    }

    public static Material ParticlesUnlit(Color color)
    {
        return Create("Universal Render Pipeline/Particles/Unlit", color);
    }

    private static Material Create(string preferredShader, Color color)
    {
        Shader shader = Shader.Find(preferredShader)
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("UI/Default")
            ?? Shader.Find("Hidden/InternalErrorShader");

        if (shader == null)
        {
            Debug.LogError($"No usable shader is available for runtime material '{preferredShader}'.");
            return null;
        }

        return new Material(shader) { color = color };
    }
}
