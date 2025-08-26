#nullable enable


using System.Collections.Generic;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Passses.Transforming
{
    public static class ApplyChangesPass
    {
        public static void Run(Transform avatarRootTransform, IEnumerable<ImmobilizeBonesData> domainObjects)
        {
            foreach (var data in domainObjects)
            {
                var entries = new List<(ImmobilizeBonesData.BoneRotation entry, Transform dummyBone)>();
                var targetPaths = new List<string>();
                foreach (var entry in data.targets)
                {
                    var reference = entry.reference;
                    var targetObject = reference.Get(data);
                    if (targetObject == null)
                    {
                        Debug.Log(
                            $"Skip: ImmobilizeBones target GameObject {reference.referencePath} not found");
                        continue;
                    }

                    var target = targetObject.transform;
                    var parent = target.parent;
                    if (parent == null)
                    {
                        Debug.Log(
                            $"Skip: ImmobilizeBones target GameObject {reference.referencePath} has no parent transform (cannot create sibling dummy bone)");
                        continue;
                    }

                    var dummy = CreateDummyBone(parent, target, target.name);
                    entries.Add((entry, dummy));
                    targetPaths.Add(GetRelativePath(avatarRootTransform, target));
                    Debug.Log($"targetPath: {GetRelativePath(avatarRootTransform, target)}");
                }

                if (entries.Count == 0) continue;

                // Modify Animations
                foreach (var (entry, dummy) in entries)
                {
                    var path = GetRelativePath(avatarRootTransform, dummy);
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"),
                        new AnimationCurve(new Keyframe(0f, entry.rotation.x))
                    );
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"),
                        new AnimationCurve(new Keyframe(0f, entry.rotation.y))
                    );
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"),
                        new AnimationCurve(new Keyframe(0f, entry.rotation.z))
                    );
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"),
                        new AnimationCurve(new Keyframe(0f, entry.rotation.w))
                    );
                }

                foreach (var originalBonePath in targetPaths)
                {
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(originalBonePath, typeof(VRCRotationConstraint), "IsActive"),
                        new AnimationCurve(new Keyframe(0f, 1f))
                    );
                    AnimationUtility.SetEditorCurve(data.clipUnlocked,
                        EditorCurveBinding.FloatCurve(originalBonePath, typeof(VRCRotationConstraint), "IsActive"),
                        new AnimationCurve(new Keyframe(0f, 0f))
                    );
                }
            }
        }

        private static Transform CreateDummyBone(Transform parent, Transform source, string baseName)
        {
            string boneName = MakeUniqueChildName(parent, $"{baseName}.ImmobilizeSource");

            var gameObject = new GameObject(boneName);
            var transform = gameObject.transform;
            transform.SetParent(parent, worldPositionStays: false);

            transform.localPosition = source.localPosition;
            transform.localRotation = source.localRotation;
            transform.localScale = source.localScale;

            var constraint = source.gameObject.AddComponent<VRCRotationConstraint>();
            constraint.Sources = new VRCConstraintSourceKeyableList(1)
            {
                [0] = new VRCConstraintSource(transform, 1.0f)
            };
            constraint.ActivateConstraint();

            return transform;
        }

        private static Transform? FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
            }

            return null;
        }

        private static string MakeUniqueChildName(Transform parent, string baseName)
        {
            if (FindChildByName(parent, baseName) == null) return baseName;
            int i = 1;
            while (FindChildByName(parent, $"{baseName} ({i})") != null) i++;
            return $"{baseName} ({i})";
        }

        private static string GetRelativePath(Transform root, Transform t)
        {
            if (t == root) return "";
            var stack = new Stack<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }

            return string.Join("/", stack.ToArray());
        }
    }
}