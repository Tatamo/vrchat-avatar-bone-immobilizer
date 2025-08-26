#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Passses.Generating
{
    public static class GenerateAssetsPass
    {
        public static void Run(Transform avatarRootTransform, IEnumerable<ImmobilizeBonesData> domainObjects)
        {
            foreach (var data in domainObjects)
            {
                if (data == null) continue;

                var name = $"ImmobilizeBones_{data.gameObject.name}_{data.GetInstanceID()}";
                // この時点では空のアニメーションクリップを作成しておく
                data.clipLocked = new AnimationClip { name = name + "_Immobilize" };
                data.clipUnlocked = new AnimationClip { name = name + "_Release" };

                var controller = new AnimatorController();
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = data.parameterName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = data.immobilizeWhenParamTrue
                });
                var layer = new AnimatorControllerLayer
                {
                    name = name,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    defaultWeight = 1f,
                };
                var stateMachine = new AnimatorStateMachine();

                // writeDefaultの値はアバター全体で統一するのが望ましいのだが、MA Merge Animatorの処理を挟むのでここではあまり考えなくてよい
                var immobilizeState = new AnimatorState
                {
                    motion = data.clipLocked,
                    name = name + "_Immobilize",
                    writeDefaultValues = false
                };
                var releaseState = new AnimatorState
                {
                    motion = data.clipUnlocked,
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
                    data.immobilizeWhenParamTrue ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0f,
                    data.parameterName);
                toImmobileTransition.AddCondition(
                    data.immobilizeWhenParamTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f,
                    data.parameterName);
                layer.stateMachine = stateMachine;
                controller.AddLayer(layer);

                data.controller = controller;

                var mergeAnimator = data.gameObject.AddComponent<ModularAvatarMergeAnimator>();
                mergeAnimator.animator = data.controller;
                mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                mergeAnimator.deleteAttachedAnimator = false;
                mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
                mergeAnimator.matchAvatarWriteDefaults = true;
                mergeAnimator.layerPriority = 0;
                mergeAnimator.mergeAnimatorMode = MergeAnimatorMode.Append;
            }
        }
    }
}