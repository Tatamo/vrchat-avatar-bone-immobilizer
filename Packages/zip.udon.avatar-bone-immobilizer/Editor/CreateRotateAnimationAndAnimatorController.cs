#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Editor
{
    public static class CreateRotateAnimationAndAnimatorController
    {
        public static AnimatorController CreateAnimatorController(List<(string, Quaternion)> boneRotations, string name)
        {
            var fx = new AnimatorController();
            var layer = new AnimatorControllerLayer
            {
                name = name,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
            };
            var stateMachine = new AnimatorStateMachine();
            var clip = CreateRotationClip(boneRotations, name);
            var state = new AnimatorState
            {
                motion = clip,
                name = name,
                // アバター全体で統一するのが望ましいのだが、MA Merge Animatorの処理を挟むのでここではあまり考えなくてよい
                writeDefaultValues = false
            };
            stateMachine.AddState(state, new Vector3(300, 0, 0));
            stateMachine.defaultState = state;
            layer.stateMachine = stateMachine;
            fx.AddLayer(layer);
            return fx;
        }

        private static AnimationClip CreateRotationClip(List<(string, Quaternion)> boneRotations, string name)
        {
            var clip = new AnimationClip { name = name };
            foreach (var (path, rotation) in boneRotations)
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"),
                    new AnimationCurve(new Keyframe(0f, rotation.x))
                );
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"),
                    new AnimationCurve(new Keyframe(0f, rotation.y))
                );
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"),
                    new AnimationCurve(new Keyframe(0f, rotation.z))
                );
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"),
                    new AnimationCurve(new Keyframe(0f, rotation.w))
                );
            }
            return clip;
        }
    }
}