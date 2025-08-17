#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Tatamo.AvatarBoneImmobilizer.Editor
{
    public static class CreateRotateAnimationAndAnimatorController
    {
        public static AnimatorController CreateAnimatorController(List<string> targetPaths,
            List<(string, Quaternion)> boneRotations, string name,
            string parameterName, bool immobilizeWhenParamTrue)
        {
            var fx = new AnimatorController();
            fx.AddParameter(new AnimatorControllerParameter
            {
                name = parameterName != "" ? parameterName : "ImmobilizeBones",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = immobilizeWhenParamTrue
            });
            var layer = new AnimatorControllerLayer
            {
                name = name,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
            };
            var stateMachine = new AnimatorStateMachine();
            var clip = CreateRotationClip(boneRotations, name + "_Immobilize");
            var emptyClip = new AnimationClip { name = name + "_Release" };
            foreach (var originalBonePath in targetPaths)
            {
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(originalBonePath, typeof(VRCRotationConstraint), "IsActive"),
                    new AnimationCurve(new Keyframe(0f, 1f))
                );
                AnimationUtility.SetEditorCurve(emptyClip,
                    EditorCurveBinding.FloatCurve(originalBonePath, typeof(VRCRotationConstraint), "IsActive"),
                    new AnimationCurve(new Keyframe(0f, 0f))
                );
            }

            // writeDefaultの値はアバター全体で統一するのが望ましいのだが、MA Merge Animatorの処理を挟むのでここではあまり考えなくてよい
            var immobilizeState = new AnimatorState
            {
                motion = clip,
                name = name + "_Immobilize",
                writeDefaultValues = false
            };
            var releaseState = new AnimatorState
            {
                motion = emptyClip,
                name = name + "_Release",
                writeDefaultValues = false
            };
            stateMachine.AddState(immobilizeState, new Vector3(300, 0, 0));
            stateMachine.AddState(releaseState, new Vector3(300, 100, 0));
            stateMachine.defaultState = immobilizeState;
            var toReleaseTransition = immobilizeState.AddTransition(releaseState);
            toReleaseTransition.hasExitTime = false;
            toReleaseTransition.duration = 0;
            var toImmobileTransition = releaseState.AddTransition(immobilizeState);
            toImmobileTransition.hasExitTime = false;
            toImmobileTransition.duration = 0;
            toReleaseTransition.AddCondition(
                immobilizeWhenParamTrue ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0f, parameterName);
            toImmobileTransition.AddCondition(
                immobilizeWhenParamTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterName);
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