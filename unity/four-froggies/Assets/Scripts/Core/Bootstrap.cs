using UnityEngine;

// Lives in the generated Main scene. Builds the ranch, the vehicles and the game at runtime.
public class Bootstrap : MonoBehaviour
{
    public Material litMat;
    public Material unlitMat;
    public Material fxMat;
    public Material waterMat;
    public Material glassMat;
    public Terrain terrain;
    public Light sun;
    public Material skinMat, foliageMat, pondMat;   // graphics overhaul (FF/Skin, FF/Foliage, LB/Water); may be null
    public Material underwaterMat, unlitTexMat;     // stage B (FF/Underwater, Unlit/Texture); may be null
    public Material realTriMat, realWaterMat, realTerrainMat;   // ?realism=1 test (FF/RealTri, FF/RealWater, terrain + _NORMALMAP)

    public static Bootstrap I;

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.pixelLightCount = 1;
        QualitySettings.antiAliasing = 0;
        QualitySettings.lodBias = 1f;
        if (Application.isMobilePlatform)
        {
            QualitySettings.shadowDistance = 30f;
            QualitySettings.shadowResolution = ShadowResolution.Low;
        }
        Debug.Log("Four Froggies: graphics overhaul (skin " + (skinMat != null) + ", foliage " + (foliageMat != null) + ", pond " + (pondMat != null) + ")");
        if (sun != null) sun.shadows = LightShadows.Soft;
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            terrain.heightmapPixelError = 6f;
        }

        // physics: vehicles ignore projectiles they fire (handled per shot); props and frogs collide with everything
        Physics.defaultSolverIterations = 8;
        Physics.IgnoreLayerCollision(Vehicle.VehicleLayer, Vehicle.FrogLayer, true);

        Mats.Init(litMat, unlitMat, fxMat, waterMat, glassMat);
        Mats.SkinBase = skinMat; Mats.FoliageBase = foliageMat; Mats.PondBase = pondMat;
        Mats.UnderwaterBase = underwaterMat; Mats.UnlitTexBase = unlitTexMat;
        Look.Init(sun);   // before Worlds.Init: Worlds captures the ranch fog / ambient it restores per camera
        Realism.TriBase = realTriMat; Realism.WaterBase = realWaterMat; Realism.TerrainNM = realTerrainMat;
        Realism.Detect(sun);   // ?realism=1 only: HDRI sky / sun / haze / SH ambient (before Worlds.Init captures them)
        Sfx.Init();
        FX.Init();
        Worlds.Init();
        Ranch.Build(terrain);
        Countryside.Build();   // ffu14: fields, woods and hills out to the horizon
        HouseWorld.Create();
        if (Worlds.UnderwaterOn) UnderwaterWorld.Create();
        if (Worlds.SpaceOn) { SpaceWorld.Create(); SurfaceWorlds.Create(); }
        SpawnVehicles();
        if (Worlds.StageEOn)
        {
            // the walking Optimus robot now lives by the garage, so the rideable bay-5 suit gets a clearer name
            foreach (Vehicle v in Vehicle.All) if (v.Title == "Optimus") { v.Title = "Optimus mech suit"; v.EnterVerb = "ride the Optimus mech suit"; }
            RanchLife.Create();
        }
        gameObject.AddComponent<Game>();
        StartCoroutine(Look.BuildProbes(new Vector3(-10f, Ranch.GY(-10f, 20f), 20f), 0));
        Realism.Begin(this, terrain);   // ?realism=1 only: streams the scans, then the pond corner
    }

    static Vector3 G(float x, float z, float up) { return new Vector3(x, Ranch.GY(x, z) + up, z); }

    static void SpawnVehicles()
    {
        float bz = Layout.GarageC.y;
        VehicleFactory.Cybertruck("James's Cybertruck", G(Layout.BayX(0), bz, 0.6f), 0f, Froggies.Color(0));
        VehicleFactory.Cybertruck("Cybertruck", G(Layout.BayX(1), bz, 0.6f), 0f, Froggies.Color(2));
        VehicleFactory.Monster(G(Layout.BayX(2), bz, 0.8f), 0f);
        VehicleFactory.TankAt(G(Layout.BayX(3), bz, 0.6f), 0f);
        VehicleFactory.Ripsaw(G(Layout.BayX(4), bz, 0.6f), 0f);
        VehicleFactory.Mech(G(Layout.BayX(5), bz, 0.6f), 0f);
        // a third truck out on the drive so all four frogs can drive at once even without the flyers
        VehicleFactory.Cybertruck("Cybertruck", G(14f, 44f, 0.6f), 30f, Froggies.Color(3));
        float roof = Layout.HouseH + 0.1f + Ranch.PadTop + 0.05f;   // on the pad paint, above the roof slab
        VehicleFactory.Helicopter(new Vector3(-52f, roof, -6f), 90f);
        VehicleFactory.Drone(new Vector3(-30f, roof, -6f), 90f);
        Vector2 pc = Layout.PondC, pr = Layout.PondR;
        VehicleFactory.BoatAt(new Vector3(pc.x - pr.x + 12f, Layout.WaterY - 0.3f, pc.y + 4f), 90f);
        VehicleFactory.BoatAt(new Vector3(pc.x - pr.x + 16f, Layout.WaterY - 0.3f, pc.y + 9f), 70f, new Color(0.85f, 0.22f, 0.2f));
    }
}
