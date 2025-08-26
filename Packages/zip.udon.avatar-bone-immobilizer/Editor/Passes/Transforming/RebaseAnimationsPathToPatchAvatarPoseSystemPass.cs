#nullable enable

using System.Collections.Generic;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Passses.Transforming
{
    public static class RebaseAnimationsPathToPatchAvatarPoseSystemPass
    {
        /**
         * AvatarPoseSystemがアバターに適用されている場合の競合を回避する：Modular Avatar適用後の後処理（想定APSバージョン: 4.21）
         *
         * APSによって作られるクローンのArmature構造はもともとのArmatureと全く同じ名前をしている。
         * そのためか、Modular Avatarのアニメーションマージ機能が誤って適用されてしまい、
         * クローンBoneを操作したいアニメーションが元々のBoneのpathで上書きされる。
         * ここではそのようなアニメーションをMA適用後のFXレイヤーの中から見つけ出し、適切なpathで再度上書きする。
         */
        public static void Run(Transform avatarRootTransform, IEnumerable<PatchForAvatarPoseSystem> components)
        {
            foreach (var patch in components)
            {
                if (patch == null) continue;
                var descriptor = avatarRootTransform.gameObject.GetComponent<VRCAvatarDescriptor>();
                foreach (var fx in descriptor.baseAnimationLayers)
                {
                    if (fx.type != VRCAvatarDescriptor.AnimLayerType.FX) continue;
                    if (fx.animatorController == null) continue;
                    var controller = (AnimatorController)fx.animatorController;
                    foreach (var layer in controller.layers)
                    {
                        if (layer.name != patch.layerName) continue;
                        foreach (var childState in layer.stateMachine.states)
                        {
                            if (childState.state.motion is AnimationClip clip)
                            {
                                if (clip.name.Contains(patch.lockClipName))
                                {
                                    foreach (var target in patch.targets)
                                    {
                                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                                        {
                                            if (binding.path !=
                                                $"{target.path}/{target.name}_Const/{target.name}") continue;
                                            var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                            var newBinding = new EditorCurveBinding
                                            {
                                                path = target.path,
                                                type = binding.type,
                                                propertyName = binding.propertyName,
                                            };
                                            AnimationUtility.SetEditorCurve(clip, binding, null);
                                            AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                                            EditorUtility.SetDirty(clip);
                                            AssetDatabase.SaveAssets();
                                        }
                                    }
                                }

                                if (clip.name.Contains(patch.unlockClipName))
                                {
                                    foreach (var target in patch.targets)
                                    {
                                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                                        {
                                            if (binding.path !=
                                                $"{target.path}/{target.name}_Const/{target.name}") continue;
                                            var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                            var newBinding = new EditorCurveBinding
                                            {
                                                path = target.path,
                                                type = binding.type,
                                                propertyName = binding.propertyName,
                                            };
                                            AnimationUtility.SetEditorCurve(clip, binding, null);
                                            AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                                            EditorUtility.SetDirty(clip);
                                            AssetDatabase.SaveAssets();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}