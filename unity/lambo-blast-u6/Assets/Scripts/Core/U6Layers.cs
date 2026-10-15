using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// lambou6: light separation island <-> lobby showroom with URP rendering layers (Built-in used Light.cullingMask), and the
// studio's trilight ambient as an SH probe (set per showroom camera in Showroom's beginCameraRendering hook).
public static class U6Layers
{
    public const uint IslandBit = 1u, StudioBit = 1u << 2;

    public static void Island(Light l) { Set(l, IslandBit); }
    public static void Studio(Light l) { Set(l, StudioBit); }

    static void Set(Light l, uint bits)
    {
        if (l == null) return;
        l.renderingLayerMask = (int)bits;
        var d = l.GetUniversalAdditionalLightData();
        if (d != null) { d.renderingLayers = bits; d.shadowRenderingLayers = bits; }
    }

    // Unity's trilight ambient as L1 SH: equator everywhere + sky lobe up + ground lobe down (colours in the active space)
    public static SphericalHarmonicsL2 TrilightSH(Color sky, Color eq, Color gr)
    {
        Color s = sky.linear, e = eq.linear, g = gr.linear;
        if (QualitySettings.activeColorSpace == ColorSpace.Gamma) { s = sky; e = eq; g = gr; }
        var sh = new SphericalHarmonicsL2();
        sh.Clear();
        sh.AddAmbientLight(e);
        sh.AddDirectionalLight(Vector3.up, (s - e) * 0.9f, 1f);
        sh.AddDirectionalLight(Vector3.down, (g - e) * 0.9f, 1f);
        return sh;
    }
}
