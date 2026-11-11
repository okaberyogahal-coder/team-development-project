using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FutureCity.EditorTools
{
    /// <summary>Purchased asset prefabs become visual-only world avatars; vendor files remain intact.</summary>
    public sealed class CreativeCharactersSetup : EditorWindow
    {
        const string Output = "Assets/FutureCity/Resources/FutureCityAvatars";
        [SerializeField] GameObject[] characters = Array.Empty<GameObject>();
        [SerializeField] AnimationClip idle, walk, sitting;
        [SerializeField] float facing;
        Vector2 scroll;

        [MenuItem("Tools/Future City/Configure Creative Characters Avatars")]
        public static void Open() => GetWindow<CreativeCharactersSetup>("World Avatars");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Creative Characters FREE の完成した人物Prefabとアニメーションを指定します。移動やカメラはミライフ側の処理を使用します。", MessageType.Info);
            var serialized = new SerializedObject(this);
            serialized.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.PropertyField(serialized.FindProperty("characters"), true);
            EditorGUILayout.PropertyField(serialized.FindProperty("idle"));
            EditorGUILayout.PropertyField(serialized.FindProperty("walk"));
            EditorGUILayout.PropertyField(serialized.FindProperty("sitting"));
            EditorGUILayout.PropertyField(serialized.FindProperty("facing"), new GUIContent("Forward correction (degrees)"));
            EditorGUILayout.EndScrollView();
            serialized.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(characters.Length == 0 || characters.Any(x => x == null) || idle == null || walk == null))
            {
                if (GUILayout.Button("Generate world avatars"))
                {
                    try { Configure(characters, idle, walk, sitting, facing); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        [Serializable]
        sealed class Configuration
        {
            public string[] characters;
            public string idle, walk, sitting;
            public float facing;
        }

        // For reproducible setup after importing the package, including CI/batch mode.
        public static void ConfigureFromFile()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-avatarConfig");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -avatarConfig JSON path");
            var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(args[index + 1]));
            Configure(config.characters.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray(),
                LoadClip(config.idle), LoadClip(config.walk), LoadClip(config.sitting), config.facing);
        }

        static AnimationClip LoadClip(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            var parts = reference.Split(new[] { "::" }, StringSplitOptions.None);
            return AssetDatabase.LoadAllAssetsAtPath(parts[0]).OfType<AnimationClip>()
                .FirstOrDefault(x => !x.name.StartsWith("__preview__", StringComparison.Ordinal) && (parts.Length == 1 || x.name == parts[1]));
        }

        public static void Configure(GameObject[] prefabs, AnimationClip idle, AnimationClip walk, AnimationClip sitting, float facing = 0f)
        {
            if (prefabs == null || prefabs.Length == 0 || prefabs.Any(x => x == null))
                throw new ArgumentException("Choose complete character prefabs from the imported pack.");
            if (idle == null || walk == null) throw new ArgumentException("Idle and walk animation clips are required.");
            foreach (var prefab in prefabs)
            {
                var rig = prefab.GetComponentInChildren<Animator>(true);
                if (rig == null || rig.avatar == null || !rig.avatar.isValid || !rig.avatar.isHuman)
                    throw new ArgumentException(prefab.name + " needs a valid Humanoid rig.");
                if (prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                    throw new ArgumentException(prefab.name + " contains no skinned character mesh.");
            }
            if (!idle.humanMotion || !walk.humanMotion || (sitting != null && !sitting.humanMotion))
                throw new ArgumentException("Use Humanoid animation clips for the character rigs.");
            if (sitting == null)
                throw new ArgumentException("A seated pose clip is required because this world also contains seated people.");

            Directory.CreateDirectory(Output + "/Characters");
            AssetDatabase.Refresh();
            idle = CreateLoopClip(idle, "Idle");
            walk = CreateLoopClip(walk, "Walk");
            sitting = CreateLoopClip(sitting, "Sit");
            var controller = CreateController(idle, walk, sitting);
            for (int i = 0; i < prefabs.Length; i++)
            {
                var wrapper = new GameObject("Character_" + i.ToString("D2"));
                wrapper.SetActive(false);
                try
                {
                    var model = Instantiate(prefabs[i], wrapper.transform, false);
                    model.name = "Model";
                    // Strip controller scripts before activation, so vendor movement never runs.
                    foreach (var component in model.GetComponentsInChildren<MonoBehaviour>(true))
                        if (component != null) DestroyImmediate(component);
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
                    foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
                    foreach (var camera in model.GetComponentsInChildren<Camera>(true)) DestroyImmediate(camera);
                    foreach (var listener in model.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(listener);
                    var animator = model.GetComponentInChildren<Animator>(true);
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    model.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
                    Normalize(model.transform);
                    wrapper.SetActive(true);
                    PrefabUtility.SaveAsPrefabAsset(wrapper, Output + "/Characters/" + wrapper.name + ".prefab");
                }
                finally { DestroyImmediate(wrapper); }
            }
            // Remove only obsolete prefabs generated by this tool, retaining other user assets.
            foreach (var path in Directory.GetFiles(Output + "/Characters", "Character_*.prefab"))
                if (int.TryParse(Path.GetFileNameWithoutExtension(path).Substring(10), out int variant) && variant >= prefabs.Length)
                    AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            Debug.Log("[FutureCity] Configured " + prefabs.Length + " Creative Characters avatars.");
        }

        static void Normalize(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>().Where(x => x.enabled && x.gameObject.activeInHierarchy).ToArray();
            // wrapper is inactive while sanitizing; activeSelf is sufficient for full prefab bounds.
            if (renderers.Length == 0) renderers = model.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled && x.gameObject.activeSelf).ToArray();
            if (renderers.Length == 0) throw new ArgumentException("The character has no visible renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y <= 0.01f) throw new ArgumentException("The character has no measurable height.");
            float factor = 1.8f / bounds.size.y;
            model.localScale *= factor;
            model.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * factor;
        }

        static AnimationClip CreateLoopClip(AnimationClip source, string name)
        {
            string path = Output + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Instantiate(source);
                AssetDatabase.CreateAsset(clip, path);
            }
            else EditorUtility.CopySerialized(source, clip);
            clip.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController CreateController(AnimationClip idle, AnimationClip walk, AnimationClip sitting)
        {
            string path = Output + "/WorldAvatar.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("WalkRate", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            var still = machine.AddState("Idle"); still.motion = idle;
            var moving = machine.AddState("Walk"); moving.motion = walk;
            moving.speedParameter = "WalkRate"; moving.speedParameterActive = true;
            var seated = machine.AddState("Sit"); seated.motion = sitting;
            machine.defaultState = still;
            var toWalk = Transition(still, moving);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            toWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");
            Transition(moving, still).AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            Transition(still, seated).AddCondition(AnimatorConditionMode.If, 0f, "Sitting");
            Transition(moving, seated).AddCondition(AnimatorConditionMode.If, 0f, "Sitting");
            Transition(seated, still).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            return transition;
        }
    }
}
