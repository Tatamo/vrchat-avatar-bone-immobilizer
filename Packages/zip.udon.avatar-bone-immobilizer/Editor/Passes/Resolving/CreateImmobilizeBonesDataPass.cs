#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using Tatamo.AvatarBoneImmobilizer.Components;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using UnityEditor;
using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Passses.Resolving
{
    public static class CreateImmobilizeBonesDataPass
    {
        public static void Run(Transform avatarRootTransform, IEnumerable<ImmobilizeBones> components)
        {
            foreach (var component in components)
            {
                if (component == null) continue;

                var domainObject = component.gameObject.AddComponent<ImmobilizeBonesData>();
                domainObject.parameterName =
                    component.parameterName != "" ? component.parameterName : "ImmobilizeBones";
                domainObject.immobilizeWhenParamTrue = component.immobilizeWhenParamTrue;

                List<(AvatarObjectReference reference, Transform targetTransform, Vector3 euler)> list = new();
                foreach (var entry in component.targetBones)
                {
                    if (entry == null) continue;
                    var reference = entry.targetBone;
                    if (reference == null) continue;
                    var targetObject = reference.Get(component);
                    if (targetObject == null) continue;

                    list.Add((reference, targetObject.transform, entry.euler));
                }

                switch (component.rotationSource)
                {
                    case ImmobilizeBones.RotationSource.UseCurrent:
                        foreach (var (reference, targetTransform, _) in list)
                        {
                            domainObject.targets.Add(new ImmobilizeBonesData.BoneRotation()
                            {
                                reference = reference, rotation = Quaternion.Euler(targetTransform.localEulerAngles)
                            });
                        }

                        break;
                    case ImmobilizeBones.RotationSource.PerBoneEuler:
                        foreach (var (reference, _, euler) in list)
                        {
                            domainObject.targets.Add(new ImmobilizeBonesData.BoneRotation()
                                { reference = reference, rotation = Quaternion.Euler(euler) });
                        }

                        break;
                    case ImmobilizeBones.RotationSource.FromAnimationClip:
                        if (component.clip == null)
                            throw new Exception("Avatar Bone Immobilizer: animation clip is not set");
                        if (list.Count == 0) break;

                        AnimationMode.StartAnimationMode();
                        try
                        {
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(avatarRootTransform.gameObject, component.clip,
                                component.clipFrame / component.clip.frameRate);
                            AnimationMode.EndSampling();

                            foreach (var (reference, targetTransform, _) in list)
                            {
                                domainObject.targets.Add(new ImmobilizeBonesData.BoneRotation()
                                    { reference = reference, rotation = targetTransform.localRotation });
                            }
                        }
                        finally
                        {
                            AnimationMode.StopAnimationMode();
                        }

                        break;
                }
            }
        }
    }
}