#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace AWR.Editor
{
    public static class AWRGitHubAutoBuild
    {
        const string CharacterFolder = "Assets/YourAssets/Character_Put_FBX_Here";
        const string AnimFolder = "Assets/YourAssets/Animations_Put_FBX_Here";
        const string ControllerPath = "Assets/AWR_Battle_Arena/AllInOne/AWR_AutoAnimator.controller";
        const string ScenePath = "Assets/AWR_Battle_Arena/AllInOne/AWR_AutoBuildScene.unity";

        public static void PerformAndroidBuild()
        {
            Debug.Log("AWR: Starting automatic Unity setup...");

            AssetDatabase.Refresh();

            GameObject characterPrefab = FindCharacterPrefab();

            RuntimeAnimatorController controller = null;

            if (characterPrefab != null)
            {
                ConfigureCharacterAndAnimations();
                controller = CreateAnimatorController();
            }
            else
            {
                Debug.LogWarning("AWR: No character FBX found. The game will build with fallback player.");
            }

            CreateScene(characterPrefab, controller);
            BuildAndroid();
        }

        static GameObject FindCharacterPrefab()
        {
            if (!Directory.Exists(CharacterFolder))
            {
                Debug.LogWarning("AWR: Character folder not found: " + CharacterFolder);
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder });
            if (guids.Length == 0)
            {
                Debug.LogWarning("AWR: No model found in: " + CharacterFolder);
                return null;
            }

            string selectedPath = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path).ToLowerInvariant();

                if (file.Contains("character2"))
                {
                    selectedPath = path;
                    break;
                }
            }

            if (selectedPath == null)
                selectedPath = AssetDatabase.GUIDToAssetPath(guids[0]);

            Debug.Log("AWR: Character selected: " + selectedPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(selectedPath);
        }

        static string FindCharacterPath()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder });
            if (guids.Length == 0) return null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path).ToLowerInvariant();

                if (file.Contains("character2"))
                    return path;
            }

            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        static void ConfigureCharacterAndAnimations()
        {
            string characterPath = FindCharacterPath();
            if (characterPath == null)
            {
                Debug.LogWarning("AWR: Cannot configure character because no FBX was found.");
                return;
            }

            var characterImporter = AssetImporter.GetAtPath(characterPath) as ModelImporter;
            if (characterImporter != null)
            {
                characterImporter.animationType = ModelImporterAnimationType.Human;
                characterImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                characterImporter.animationCompression = ModelImporterAnimationCompression.Optimal;
                characterImporter.resampleCurves = true;
                characterImporter.SaveAndReimport();
                Debug.Log("AWR: Character configured as Humanoid.");
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(characterPath).OfType<Avatar>().FirstOrDefault();

            if (!Directory.Exists(AnimFolder))
            {
                Debug.LogWarning("AWR: Animation folder not found: " + AnimFolder);
                return;
            }

            string[] animGuids = AssetDatabase.FindAssets("t:Model", new[] { AnimFolder });

            foreach (string guid in animGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;

                if (avatar != null)
                    importer.sourceAvatar = avatar;

                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.resampleCurves = true;
                importer.SaveAndReimport();

                Debug.Log("AWR: Animation configured: " + path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static RuntimeAnimatorController CreateAnimatorController()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));

            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("DirectionX", AnimatorControllerParameterType.Float);
            controller.AddParameter("DirectionY", AnimatorControllerParameterType.Float);

            controller.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Prone", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SwitchWeapon", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            AnimationClip idle = FindClip("idle") ?? FirstClip();
            AnimationClip walk = FindClip("walk");
            AnimationClip run = FindClip("run");
            AnimationClip sprint = FindClip("sprint");
            AnimationClip crouch = FindClip("crouch");
            AnimationClip prone = FindClip("prone") ?? FindClip("crawl");
            AnimationClip fire = FindClip("fire") ?? FindClip("shoot");
            AnimationClip reload = FindClip("reload");
            AnimationClip throwClip = FindClip("throw");
            AnimationClip hit = FindClip("hit");
            AnimationClip death = FindClip("death") ?? FindClip("die");

            var idleState = sm.AddState("Idle"); if (idle) idleState.motion = idle;
            var walkState = sm.AddState("Walk"); if (walk) walkState.motion = walk;
            var runState = sm.AddState("Run"); if (run) runState.motion = run;
            var sprintState = sm.AddState("Sprint"); if (sprint) sprintState.motion = sprint;
            var crouchState = sm.AddState("Crouch"); if (crouch) crouchState.motion = crouch;
            var proneState = sm.AddState("Prone"); if (prone) proneState.motion = prone;
            var fireState = sm.AddState("Fire"); if (fire) fireState.motion = fire;
            var reloadState = sm.AddState("Reload"); if (reload) reloadState.motion = reload;
            var throwState = sm.AddState("Throw"); if (throwClip) throwState.motion = throwClip;
            var hitState = sm.AddState("Hit"); if (hit) hitState.motion = hit;
            var deathState = sm.AddState("Death"); if (death) deathState.motion = death;

            sm.defaultState = idleState;

            AddFloatTransition(idleState, walkState, "Speed", 0.15f, true);
            AddFloatTransition(walkState, idleState, "Speed", 0.15f, false);
            AddFloatTransition(walkState, runState, "Speed", 0.8f, true);
            AddFloatTransition(runState, walkState, "Speed", 0.8f, false);

            AddBoolTransition(runState, sprintState, "Sprint", true);
            AddBoolTransition(sprintState, runState, "Sprint", false);

            AddBoolTransition(idleState, crouchState, "Crouch", true);
            AddBoolTransition(crouchState, idleState, "Crouch", false);

            AddBoolTransition(idleState, proneState, "Prone", true);
            AddBoolTransition(proneState, idleState, "Prone", false);

            AddTrigger(sm, fireState, "Fire");
            AddTrigger(sm, reloadState, "Reload");
            AddTrigger(sm, throwState, "Throw");
            AddTrigger(sm, hitState, "Hit");
            AddTrigger(sm, deathState, "Death");

            AddReturn(fireState, idleState);
            AddReturn(reloadState, idleState);
            AddReturn(throwState, idleState);
            AddReturn(hitState, idleState);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("AWR: Animator Controller created: " + ControllerPath);

            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        }

        static AnimationClip FindClip(string key)
        {
            if (!Directory.Exists(AnimFolder)) return null;

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>();

                foreach (var clip in clips)
                {
                    if (file.Contains(key) || clip.name.ToLowerInvariant().Contains(key))
                        return clip;
                }
            }

            return null;
        }

        static AnimationClip FirstClip()
        {
            if (!Directory.Exists(AnimFolder)) return null;

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { AnimFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault();

                if (clip != null)
                    return clip;
            }

            return null;
        }

        static void AddFloatTransition(AnimatorState from, AnimatorState to, string param, float threshold, bool greater)
        {
            if (from == null || to == null) return;

            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.12f;
            t.AddCondition(greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, threshold, param);
        }

        static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool val)
        {
            if (from == null || to == null) return;

            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.12f;
            t.AddCondition(val ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
        }

        static void AddTrigger(AnimatorStateMachine sm, AnimatorState to, string trigger)
        {
            if (sm == null || to == null) return;

            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        static void AddReturn(AnimatorState from, AnimatorState to)
        {
            if (from == null || to == null) return;

            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.85f;
            t.duration = 0.12f;
        }

        static void CreateScene(GameObject characterPrefab, RuntimeAnimatorController controller)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gameObject = new GameObject("AWR_Game");

            var gameType = Type.GetType("AWR_BattleArena_GameplayComplete_v3, Assembly-CSharp");

            if (gameType == null)
                throw new Exception("AWR: Could not find AWR_BattleArena_GameplayComplete_v3. Make sure the game script exists.");

            var component = gameObject.AddComponent(gameType);

            if (component == null)
                throw new Exception("AWR: Could not add AWR_BattleArena_GameplayComplete_v3 component.");

            var characterField = gameType.GetField("externalCharacterPrefab");
            if (characterField != null)
                characterField.SetValue(component, characterPrefab);

            var animatorField = gameType.GetField("externalAnimatorController");
            if (animatorField != null)
                animatorField.SetValue(component, controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("AWR: Auto scene created: " + ScenePath);
        }

        static void BuildAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Directory.CreateDirectory("build/Android");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "build/Android/AWR_Battle_Arena.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("AWR: Android build failed: " + report.summary.result);

            Debug.Log("AWR: Android build succeeded.");
        }
    }
}
#endif
