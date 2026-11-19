#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using MissionRacer.Vehicle;
using MissionRacer.CameraSystem;
using MissionRacer.Tracks;
using MissionRacer.AI;
using MissionRacer.Network;
using MissionRacer.Audio;
using MissionRacer.Missions;
using MissionRacer.Voice;

namespace MissionRacer.EditorTools
{
    [InitializeOnLoad]
    public static class MissionRacerSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/MissionRacer_MonacoGP_3D.unity";

        static MissionRacerSceneBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfSceneMissing;
        }

        private static void AutoBuildIfSceneMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool sceneFileExists = File.Exists(SCENE_PATH);
            bool trackExistsInActiveScene = GameObject.Find("--- MONACO GP & BRIDGE 3D CIRCUIT ---") != null;
            if (!sceneFileExists)
            {
                BuildCompleteSceneInternal(showPopup: true);
            }
            else if (!trackExistsInActiveScene)
            {
                EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
                if (GameObject.Find("--- MONACO GP & BRIDGE 3D CIRCUIT ---") == null)
                {
                    BuildCompleteSceneInternal(showPopup: true);
                }
            }
        }

        [MenuItem("Mission Racer 3D/⚡ 1-Click Build Monaco GP, Bridges, Mud & City Scene (Unity 6 URP)", false, 1)]
        public static void BuildCompleteScene()
        {
            BuildCompleteSceneInternal(showPopup: true);
        }

        [MenuItem("Mission Racer 3D/🎯 Open Mission Control Dashboard (Web & Phone QR)", false, 2)]
        public static void OpenWebMissionDashboard()
        {
            Application.OpenURL("http://localhost:3000/?tab=missions");
        }

        public static void BuildCompleteSceneHeadless()
        {
            BuildCompleteSceneInternal(showPopup: false);
        }

        private static void BuildCompleteSceneInternal(bool showPopup)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "MissionRacer_MonacoGP_3D";

            // 1. Configure Unity 6 URP Lighting & Global Volume
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.52f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.32f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.14f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.80f, 0.96f);
            RenderSettings.fogDensity = 0.0012f;

            Light dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null)
            {
                dirLight.color = new Color(1.0f, 0.96f, 0.88f);
                dirLight.intensity = 1.6f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                if (dirLight.GetComponent<UniversalAdditionalLightData>() == null)
                {
                    dirLight.gameObject.AddComponent<UniversalAdditionalLightData>();
                }
            }

            // Attach URP Global Post-Processing Volume
            VolumeProfile defaultProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            if (defaultProfile != null)
            {
                GameObject volumeObj = new GameObject("URP_Global_Volume");
                Volume vol = volumeObj.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = defaultProfile;
            }

            // 2. Game Managers Root
            GameObject managersRoot = new GameObject("--- MISSION RACER MANAGERS ---");
            managersRoot.AddComponent<MissionRacerManager>();
            managersRoot.AddComponent<FamilyLobbyManager>();
            managersRoot.AddComponent<NetworkRaceManager>();
            managersRoot.AddComponent<PitRadioVoiceManager>();
            managersRoot.AddComponent<AudioManager>();

            // 3. Monaco GP + Forest Mud + Suspension Bridge 3D Circuit
            GameObject trackRoot = new GameObject("--- MONACO GP & BRIDGE 3D CIRCUIT ---");
            WaypointPath waypointPath = trackRoot.AddComponent<WaypointPath>();

            // Ground & Harbor Water
            GameObject terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = "Ground_Terrain";
            terrain.transform.SetParent(trackRoot.transform);
            terrain.transform.position = new Vector3(0f, -0.2f, 0f);
            terrain.transform.localScale = new Vector3(140f, 1f, 140f);
            ApplyUrpMaterial(terrain, new Color(0.11f, 0.26f, 0.14f), 0.15f, 0.0f);

            GameObject harborWater = GameObject.CreatePrimitive(PrimitiveType.Plane);
            harborWater.name = "Mediterranean_Harbor_Water";
            harborWater.transform.SetParent(trackRoot.transform);
            harborWater.transform.position = new Vector3(-190f, 0.05f, 80f);
            harborWater.transform.localScale = new Vector3(45f, 1f, 45f);
            ApplyUrpMaterial(harborWater, new Color(0.02f, 0.52f, 0.78f), 0.92f, 0.35f);

            // Smooth Wide Grand Prix 3D Control Points (No Sharp Hairpins or Overlapping Segments)
            Vector3[] controlPts = new Vector3[]
            {
                new Vector3(0f, 0.5f, 0f),          // Wide Pit Straight
                new Vector3(0f, 1.5f, -165f),       // Sweeping Sainte Devote Entry
                new Vector3(115f, 8.5f, -240f),     // Beau Rivage Sweeping Climb
                new Vector3(230f, 12.0f, -120f),    // Casino Square Grand Curve
                new Vector3(230f, 7.0f, 95f),       // Forest Wet Mud Sweeping Sector
                new Vector3(115f, 2.0f, 225f),      // Seafront Tunnel Curve
                new Vector3(-85f, 9.5f, 210f),      // Harbor Suspension Bridge Approach
                new Vector3(-145f, 10.0f, 35f),     // Harbor Suspension Bridge Span
                new Vector3(-85f, 2.0f, -110f)      // Sweeping Final Turn onto Main Straight
            };

            int totalSamples = 120;
            float roadWidth = 16f;
            List<Transform> waypointsList = new List<Transform>();

            for (int i = 0; i < totalSamples; i++)
            {
                float t0 = i / (float)totalSamples;
                float t1 = ((i + 1) % totalSamples) / (float)totalSamples;

                Vector3 p0 = SampleClosedCatmullRom(controlPts, t0);
                Vector3 p1 = SampleClosedCatmullRom(controlPts, t1);
                Vector3 mid = (p0 + p1) * 0.5f;
                Vector3 dir = (p1 - p0).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                float segLen = Vector3.Distance(p0, p1) * 1.05f;

                bool isMudSector = (t0 >= 0.42f && t0 <= 0.56f);
                bool isTunnelSector = (t0 >= 0.58f && t0 <= 0.68f);
                bool isBridgeSector = (t0 >= 0.70f && t0 <= 0.85f);

                GameObject wp = new GameObject($"Waypoint_{i:000}");
                wp.transform.SetParent(trackRoot.transform);
                wp.transform.position = p0 + Vector3.up * 0.5f;
                waypointsList.Add(wp.transform);

                // Road Segment (Asphalt vs Glossy Wet Mud)
                GameObject roadSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roadSeg.name = isMudSector ? $"Mud_Road_{i:000}" : $"Asphalt_Road_{i:000}";
                roadSeg.transform.SetParent(trackRoot.transform);
                roadSeg.transform.position = mid;
                roadSeg.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                roadSeg.transform.localScale = new Vector3(roadWidth, 0.25f, segLen);
                if (isMudSector)
                    ApplyUrpMaterial(roadSeg, new Color(0.26f, 0.15f, 0.07f), 0.88f, 0.15f); // Wet glossy mud
                else
                    ApplyUrpMaterial(roadSeg, new Color(0.13f, 0.15f, 0.19f), 0.35f, 0.05f); // Dry tarmac

                // Raised Curbs & Guardrails
                Color curbCol = (i % 2 == 0) ? new Color(0.92f, 0.15f, 0.22f) : Color.white;
                foreach (float side in new float[] { -1f, 1f })
                {
                    GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    curb.name = "Curb";
                    curb.transform.SetParent(roadSeg.transform, false);
                    curb.transform.localPosition = new Vector3(side * 0.53f, 0.6f, 0f);
                    curb.transform.localScale = new Vector3(0.07f, 1.2f, 1f);
                    ApplyUrpMaterial(curb, curbCol, 0.25f, 0.0f);

                    GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rail.name = "Guardrail";
                    rail.transform.SetParent(roadSeg.transform, false);
                    rail.transform.localPosition = new Vector3(side * 0.57f, 3.2f, 0f);
                    rail.transform.localScale = new Vector3(0.03f, 4.2f, 1f);
                    ApplyUrpMaterial(rail, new Color(0.58f, 0.64f, 0.72f), 0.75f, 0.7f);
                }

                // Monaco Seafront 3D Tunnel Roof
                if (isTunnelSector)
                {
                    GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roof.name = "Tunnel_Roof";
                    roof.transform.SetParent(roadSeg.transform, false);
                    roof.transform.localPosition = new Vector3(0f, 28f, 0f);
                    roof.transform.localScale = new Vector3(1.25f, 4f, 1f);
                    ApplyUrpMaterial(roof, new Color(0.22f, 0.26f, 0.32f), 0.4f, 0.2f);
                }

                // Suspension Bridge Pylons
                if (isBridgeSector && i % 6 == 0)
                {
                    GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tower.name = "Suspension_Bridge_Tower";
                    tower.transform.SetParent(trackRoot.transform);
                    tower.transform.position = mid + Vector3.up * 16f;
                    tower.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    tower.transform.localScale = new Vector3(roadWidth + 5f, 36f, 2.2f);
                    Object.DestroyImmediate(tower.GetComponent<Collider>());
                    ApplyUrpMaterial(tower, new Color(0.88f, 0.12f, 0.28f), 0.55f, 0.4f);
                }

                // City Buildings & Pine/Palm Trees along Roadside
                if (!isBridgeSector && !isTunnelSector && i % 3 == 0)
                {
                    Vector3 bPos = mid + right * ((i % 2 == 0 ? 1f : -1f) * (roadWidth * 0.5f + 16f));
                    GameObject bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bldg.name = $"City_Building_{i:000}";
                    bldg.transform.SetParent(trackRoot.transform);
                    float h = 16f + (i % 5) * 8f;
                    bldg.transform.position = new Vector3(bPos.x, mid.y + h * 0.5f - 1f, bPos.z);
                    bldg.transform.localScale = new Vector3(14f, h, 14f);
                    ApplyUrpMaterial(bldg, (i % 4 == 0) ? new Color(0.88f, 0.82f, 0.74f) : new Color(0.18f, 0.24f, 0.34f), 0.5f, 0.2f);

                    Vector3 treePos = mid + right * ((i % 2 == 0 ? -1f : 1f) * (roadWidth * 0.5f + 8f));
                    Create3DTree(trackRoot.transform, treePos);
                }
            }

            waypointPath.SetWaypoints(waypointsList.ToArray());

            // 4. Spawn Player & AI Supercars on the Starting Grid
            Vector3 startPos = waypointsList[0].position;
            Vector3 startForward = (waypointsList[1].position - waypointsList[0].position).normalized;
            Quaternion startRot = Quaternion.LookRotation(startForward, Vector3.up);
            Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;

            GameObject playerCar = CreateDetailedSupercar("Player_Supercar_Lightning7", new Color(0f, 0.9f, 0.46f), new Color(0f, 0.9f, 1f), false, waypointPath, null);
            playerCar.transform.position = startPos - startRight * 2.8f + Vector3.up * 0.6f;
            playerCar.transform.rotation = startRot;

            GameObject turboPup = CreateDetailedSupercar("AI_TurboPup", new Color(1f, 0.76f, 0.05f), new Color(1f, 0.5f, 0f), true, waypointPath, playerCar.transform);
            turboPup.transform.position = startPos + startRight * 2.8f + startForward * 8f + Vector3.up * 0.6f;
            turboPup.transform.rotation = startRot;

            GameObject roboRacer = CreateDetailedSupercar("AI_RoboRacer", new Color(0f, 0.85f, 1f), new Color(0.6f, 0.2f, 1f), true, waypointPath, playerCar.transform);
            roboRacer.transform.position = startPos - startRight * 2.6f + startForward * 16f + Vector3.up * 0.6f;
            roboRacer.transform.rotation = startRot;

            // 5. Configure Main Camera with URP Additional Camera Data & Need for Speed Chase Cam
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = new Color(0.05f, 0.65f, 0.9f);
                UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;

                VehicleChaseCamera chaseCam = mainCam.gameObject.AddComponent<VehicleChaseCamera>();
                chaseCam.SetTarget(playerCar.transform, playerCar.GetComponent<Rigidbody>());
            }

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = playerCar;
            SceneView.lastActiveSceneView?.FrameSelected();

            if (showPopup && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Unity 6 URP Scene Ready!",
                    $"Generated the complete Unity 6 Universal Render Pipeline (URP) 3D Circuit with Monaco Tunnel, Harbor Suspension Bridge, Wet Mud Sector, 3D Trees, Buildings, and Supercars!\n\nSaved to: {SCENE_PATH}\n\nPress PLAY (▶) at the top of Unity 6 to drive!",
                    "Let's Race!");
            }
        }

        private static Vector3 SampleClosedCatmullRom(Vector3[] pts, float t)
        {
            int n = pts.Length;
            float scaled = t * n;
            int i = Mathf.FloorToInt(scaled) % n;
            float u = scaled - Mathf.Floor(scaled);

            Vector3 p0 = pts[(i - 1 + n) % n];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[(i + 1) % n];
            Vector3 p3 = pts[(i + 2) % n];

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) +
                (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u)
            );
        }

        private static void Create3DTree(Transform parent, Vector3 pos)
        {
            GameObject tree = new GameObject("Tree_3D");
            tree.transform.SetParent(parent);
            tree.transform.position = pos;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            trunk.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            ApplyUrpMaterial(trunk, new Color(0.32f, 0.16f, 0.05f), 0.2f, 0f);

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            crown.transform.localScale = new Vector3(3.6f, 4.5f, 3.6f);
            Object.DestroyImmediate(crown.GetComponent<Collider>());
            ApplyUrpMaterial(crown, new Color(0.1f, 0.52f, 0.22f), 0.3f, 0f);
        }

        private static GameObject CreateDetailedSupercar(
            string name,
            Color bodyColor,
            Color neonColor,
            bool isAi,
            WaypointPath path,
            Transform trailingPlayer)
        {
            GameObject carRoot = new GameObject(name);
            Rigidbody rb = carRoot.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 2.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            SphereCollider sphere = carRoot.AddComponent<SphereCollider>();
            sphere.radius = 0.9f;
            sphere.center = new Vector3(0f, 0.4f, 0f);

            GameObject visualRoot = new GameObject("Chassis_Visual");
            visualRoot.transform.SetParent(carRoot.transform, false);

            GameObject lowerBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lowerBody.transform.SetParent(visualRoot.transform, false);
            lowerBody.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            lowerBody.transform.localScale = new Vector3(2.05f, 0.52f, 4.4f);
            Object.DestroyImmediate(lowerBody.GetComponent<Collider>());
            ApplyUrpMaterial(lowerBody, bodyColor, 0.88f, 0.55f);

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.transform.SetParent(visualRoot.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.72f, -0.15f);
            cabin.transform.localScale = new Vector3(1.55f, 0.42f, 2.1f);
            Object.DestroyImmediate(cabin.GetComponent<Collider>());
            ApplyUrpMaterial(cabin, new Color(0.05f, 0.08f, 0.14f), 0.95f, 0.85f);

            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.transform.SetParent(visualRoot.transform, false);
            wing.transform.localPosition = new Vector3(0f, 0.82f, -2.05f);
            wing.transform.localScale = new Vector3(1.95f, 0.08f, 0.38f);
            Object.DestroyImmediate(wing.GetComponent<Collider>());
            ApplyUrpMaterial(wing, new Color(0.1f, 0.12f, 0.16f), 0.6f, 0.5f);

            // Four 3D Wheels
            Vector3[] wheelOffsets = new Vector3[]
            {
                new Vector3(-1.02f, 0.22f, 1.35f),
                new Vector3(1.02f, 0.22f, 1.35f),
                new Vector3(-1.02f, 0.24f, -1.35f),
                new Vector3(1.02f, 0.24f, -1.35f)
            };
            for (int w = 0; w < wheelOffsets.Length; w++)
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel_{w}";
                wheel.transform.SetParent(visualRoot.transform, false);
                wheel.transform.localPosition = wheelOffsets[w];
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.72f, 0.22f, 0.72f);
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                ApplyUrpMaterial(wheel, new Color(0.08f, 0.08f, 0.1f), 0.3f, 0.1f);
            }

            ArcadeVehicleController vehicleCtrl = carRoot.AddComponent<ArcadeVehicleController>();
            vehicleCtrl.SetVisualBody(visualRoot.transform);
            vehicleCtrl.useKeyboardInput = !isAi;

            if (isAi)
            {
                ArcadeAiVehicleController aiCtrl = carRoot.AddComponent<ArcadeAiVehicleController>();
                aiCtrl.SetWaypointPath(path);
                if (trailingPlayer != null) aiCtrl.SetTrailingPlayer(trailingPlayer);
            }
            else
            {
                carRoot.AddComponent<NetworkVehicleSync>();
            }

            return carRoot;
        }

        private static void ApplyUrpMaterial(GameObject obj, Color color, float smoothness, float metallic)
        {
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            rend.sharedMaterial = mat;
        }
    }
}
#endif
