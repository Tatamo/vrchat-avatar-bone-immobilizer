#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using Tatamo.AvatarBoneImmobilizer.Components;
using Tatamo.AvatarBoneImmobilizer.Editor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;

[assembly: ExportsPlugin(typeof(BoneImmobilizerPlugin))]

namespace Tatamo.AvatarBoneImmobilizer.Editor
{
    public class BoneImmobilizerPlugin : Plugin<BoneImmobilizerPlugin>
    {
        public override string QualifiedName => "zip.udon.avatar-bone-immobilizer";

        public override string DisplayName => "Avatar Bone Immobilizer";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("add dummy sibling Bone and VRC Rotation Constraint to lock target bone", ctx =>
                {
                    foreach (var pluginComponent in ctx.AvatarRootObject
                                 .GetComponentsInChildren<ImmobilizeBones>())
                    {
                        if (pluginComponent == null)
                        {
                            Debug.Log("Skip: ImmobilizeBones plugin component not found");
                            continue;
                        }

                        var entries = new List<(ImmobilizeBones.BoneEntry entry, Transform dummyBone)>();
                        foreach (var entry in pluginComponent.targetBones)
                        {
                            var reference = entry.targetBone;
                            if (reference == null) continue;
                            var targetObject = reference.Get(pluginComponent);
                            if (targetObject == null)
                            {
                                Debug.Log($"Skip: ImmobilizeBones target GameObject {reference.referencePath} not found");
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
                        }

                        if (entries.Count == 0) continue;


                        var boneRotations = new List<(string, Quaternion)>();
                        switch (pluginComponent.rotationSource)
                        {
                            case ImmobilizeBones.RotationSource.PerBoneEuler:
                                foreach (var (entry, dummy) in entries)
                                {
                                    boneRotations.Add((GetRelativePath(ctx.AvatarRootTransform, dummy),
                                        Quaternion.Euler(entry.euler)));
                                }

                                break;
                            case ImmobilizeBones.RotationSource.FromAnimationClip:
                                if (pluginComponent.clip == null)
                                {
                                    break;
                                }

                                var targets = SampleRotationFromClip.SampleRotations(
                                    ctx.AvatarRootObject,
                                    entries.Select((tuple, _) => tuple.entry.targetBone.Get(pluginComponent).transform)
                                        .ToList(),
                                    pluginComponent.clip,
                                    pluginComponent.clipFrame
                                );

                                boneRotations = entries.Zip(targets,
                                    ((tuple, quaternion) => (GetRelativePath(ctx.AvatarRootTransform, tuple.dummyBone),
                                        quaternion))).ToList();
                                break;
                        }

                        if (pluginComponent.rotationSource != ImmobilizeBones.RotationSource.UseCurrent)
                        {
                            var controller = CreateRotateAnimationAndAnimatorController.CreateAnimatorController(boneRotations,
                                $"ImmobilizeBones_{pluginComponent.gameObject.name}_{pluginComponent.GetInstanceID()}");
                            var mergeAnimator = pluginComponent.gameObject.AddComponent<ModularAvatarMergeAnimator>();
                            mergeAnimator.animator = controller;
                            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                            mergeAnimator.deleteAttachedAnimator = false;
                            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
                            mergeAnimator.matchAvatarWriteDefaults = true;
                            mergeAnimator.layerPriority = 0;
                            mergeAnimator.mergeAnimatorMode = MergeAnimatorMode.Append;
                        }

                        Object.DestroyImmediate(pluginComponent);
                    }
                });
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