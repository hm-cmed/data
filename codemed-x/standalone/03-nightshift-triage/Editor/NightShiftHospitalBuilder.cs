using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CodemedX.NightShift;

namespace CodemedX.NightShift.EditorTools
{
    /// <summary>
    /// グレーボックス（灰色の板だけの仮モデル）で病棟を自動生成する。
    ///
    /// 目的は「3D にすると学習内容が体験できる」ことを先に確かめること。
    /// 本物の病室モデルへの差し替えは、ここで作ったオブジェクトの見た目だけを
    /// 後から入れ替えればよい（BedStation / NurseStationPhone のロジックはそのまま使える）。
    ///
    /// 使い方: Tools &gt; Codemed-x &gt; 夜勤: グレーボックス病棟を生成
    /// 空のシーンで実行することを推奨（既存オブジェクトは変更しない。重ねて置かれる）。
    /// </summary>
    public static class NightShiftHospitalBuilder
    {
        private const string MenuPath = "Tools/Codemed-x/夜勤: グレーボックス病棟を生成";

        // ナースステーションからの距離。奥に行くほど遠い部屋、という単純な一列配置にしてある。
        // 込み入った間取りより、まず「歩いて移動する」体験を作ることを優先している。
        private const float RoomSpacing = 6f;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            NightShiftScenarioData scenario = NightShiftScenarioData.CreateDefault();

            GameObject root = new GameObject("NightShiftHospital (generated)");
            Undo.RegisterCreatedObjectUndo(root, "Generate Night Shift Hospital");

            EnsureDirectionalLight(root.transform);
            CreateFloor(root.transform);

            NightShiftSim sim = CreateSimObject(root.transform);
            CreatePlayer(root.transform, sim);

            // ナースステーション（起点）。ここから各病室へ向かって配置していく。
            Transform nurseStation = CreateNurseStation(root.transform, sim);

            float z = RoomSpacing;
            CreatePatientBed(root.transform, sim, "patientA_spo2", "病室A", new Vector3(-3f, 0f, z));
            CreatePatientBed(root.transform, sim, "patientB_fall", "病室B", new Vector3(3f, 0f, z));

            z += RoomSpacing;
            CreateNurseCallPanel(root.transform, sim, "nurse_calls", "病室C/D ナースコール", new Vector3(0f, 0f, z));

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                "[Codemed-x] グレーボックス病棟を生成しました。\n" +
                "WASD + マウスで移動、[E] でベッド／電話を操作します。\n" +
                "見た目は灰色の板だけなので、本物のモデルに差し替えても " +
                "BedStation / NurseStationPhone コンポーネントはそのまま使えます。");
        }

        private static void EnsureDirectionalLight(Transform parent)
        {
            if (Object.FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
        }

        private static void CreateFloor(Transform parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localScale = new Vector3(16f, 0.2f, 24f);
            floor.transform.position = new Vector3(0f, -0.1f, RoomSpacing);
            SetColor(floor, new Color(0.75f, 0.75f, 0.78f));
        }

        private static NightShiftSim CreateSimObject(Transform parent)
        {
            NightShiftSim existing = Object.FindAnyObjectByType<NightShiftSim>();
            if (existing != null)
            {
                AssignBoolField(existing, "compactHud", true);
                return existing;
            }

            GameObject simObject = new GameObject("NightShiftSim");
            simObject.transform.SetParent(parent, false);

            NightShiftSim sim = simObject.AddComponent<NightShiftSim>();

            // 全画面のパネルのままだと病室が隠れてしまうので、勤務中は小さな表示にする。
            AssignBoolField(sim, "compactHud", true);
            return sim;
        }

        private static void CreatePlayer(Transform parent, NightShiftSim sim)
        {
            SimpleWalker existing = Object.FindAnyObjectByType<SimpleWalker>();
            if (existing != null)
            {
                AssignSimField(existing, sim);
                return;
            }

            GameObject player = new GameObject("Player");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0f, 1f, -1f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.radius = 0.3f;

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            AssignSimField(player.AddComponent<SimpleWalker>(), sim);
        }

        private static Transform CreateNurseStation(Transform parent, NightShiftSim sim)
        {
            GameObject station = new GameObject("NurseStation");
            station.transform.SetParent(parent, false);
            station.transform.position = new Vector3(0f, 0f, 0f);

            GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Desk";
            desk.transform.SetParent(station.transform, false);
            desk.transform.localScale = new Vector3(2.4f, 0.8f, 0.9f);
            desk.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            SetColor(desk, new Color(0.55f, 0.4f, 0.3f));

            // 事務連絡の電話（タスク "phone_admin"）。医師への疑義電話とは別物なので、
            // 名前と表示ラベルをはっきり分けておく。
            CreateStationTask(
                station.transform, sim, "phone_admin", "外線電話 (事務連絡)",
                new Vector3(-0.8f, 0.9f, 0.5f), new Vector3(0.2f, 0.2f, 0.2f));

            // 医師への疑義照会（エスカレーション）用の電話。TaskId を持たない別コンポーネント。
            GameObject doctorPhone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doctorPhone.name = "DoctorPhone";
            doctorPhone.transform.SetParent(station.transform, false);
            doctorPhone.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            doctorPhone.transform.localPosition = new Vector3(0.8f, 0.9f, 0.5f);
            SetColor(doctorPhone, new Color(0.2f, 0.25f, 0.9f));
            SetupTrigger(doctorPhone, 1.4f);
            NurseStationPhone phoneComponent = doctorPhone.AddComponent<NurseStationPhone>();
            AssignSimField(phoneComponent, sim);

            return station.transform;
        }

        private static void CreatePatientBed(
            Transform parent, NightShiftSim sim, string taskId, string roomLabel, Vector3 position)
        {
            GameObject room = new GameObject(roomLabel);
            room.transform.SetParent(parent, false);
            room.transform.position = position;

            GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Bed";
            bed.transform.SetParent(room.transform, false);
            bed.transform.localScale = new Vector3(1f, 0.5f, 2f);
            bed.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            SetColor(bed, new Color(0.9f, 0.9f, 0.85f));

            // モニタ役の小さな板。BedStation が発生中はここを光らせる。
            CreateStationTask(
                room.transform, sim, taskId, roomLabel,
                new Vector3(0.8f, 1.1f, -0.9f), new Vector3(0.3f, 0.3f, 0.05f));
        }

        private static void CreateNurseCallPanel(
            Transform parent, NightShiftSim sim, string taskId, string label, Vector3 position)
        {
            GameObject panel = new GameObject("NurseCallPanel");
            panel.transform.SetParent(parent, false);
            panel.transform.position = position;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(panel.transform, false);
            wall.transform.localScale = new Vector3(2f, 1.5f, 0.1f);
            wall.transform.localPosition = new Vector3(0f, 0.75f, 0.5f);
            SetColor(wall, new Color(0.85f, 0.85f, 0.82f));

            CreateStationTask(
                panel.transform, sim, taskId, label,
                new Vector3(0f, 1f, 0.4f), new Vector3(0.4f, 0.3f, 0.05f));
        }

        /// <summary>
        /// 「発生するとランプが点滅し、近づいて [E] で対応する」という 1 ユニットを作る。
        /// 患者ベッド・電話・ナースコールパネルはすべてこの組み合わせで表現できる。
        /// </summary>
        private static void CreateStationTask(
            Transform parent, NightShiftSim sim, string taskId, string displayName,
            Vector3 localPosition, Vector3 lampScale)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = displayName + " (" + taskId + ")";
            lamp.transform.SetParent(parent, false);
            lamp.transform.localScale = lampScale;
            lamp.transform.localPosition = localPosition;
            SetColor(lamp, new Color(0.5f, 0.5f, 0.5f));

            SetupTrigger(lamp, 1.6f);

            BedStation station = lamp.AddComponent<BedStation>();
            AssignTaskIdField(station, taskId);
            AssignSimField(station, sim);
            AssignAlarmRendererField(station, lamp.GetComponent<Renderer>());
        }

        private static void SetupTrigger(GameObject target, float radius)
        {
            // 見た目の当たり判定（Cube の Collider）とは別に、近接判定用の球を追加する。
            SphereCollider trigger = target.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;
        }

        private static void SetColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            // グレーボックス段階では共有マテリアルを複製して色だけ変える。
            // 本番の見た目に差し替えるときは、このマテリアルごと置き換えればよい。
            Material material = new Material(renderer.sharedMaterial);
            material.color = color;
            renderer.sharedMaterial = material;
        }

        // BedStation / NurseStationPhone の対象フィールドは Inspector 専用の private だが、
        // このビルダーは Inspector 操作の代わりに生成時点で配線してしまうためリフレクションで設定する。
        // シリアライズ経路と同じ SerializedProperty を使うので、Undo やシーンの dirty 化にも正しく乗る。

        private static void AssignTaskIdField(BedStation station, string taskId)
        {
            SerializedObject serialized = new SerializedObject(station);
            serialized.FindProperty("taskId").stringValue = taskId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSimField(BedStation station, NightShiftSim sim)
        {
            SerializedObject serialized = new SerializedObject(station);
            serialized.FindProperty("sim").objectReferenceValue = sim;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSimField(NurseStationPhone phone, NightShiftSim sim)
        {
            SerializedObject serialized = new SerializedObject(phone);
            serialized.FindProperty("sim").objectReferenceValue = sim;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSimField(SimpleWalker walker, NightShiftSim sim)
        {
            SerializedObject serialized = new SerializedObject(walker);
            serialized.FindProperty("sim").objectReferenceValue = sim;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBoolField(Object target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignAlarmRendererField(BedStation station, Renderer renderer)
        {
            SerializedObject serialized = new SerializedObject(station);
            serialized.FindProperty("alarmRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
