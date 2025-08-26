#nullable enable

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor.Animations;
using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Components.Domain
{
    [AddComponentMenu("")]
    public class ImmobilizeBonesData: MonoBehaviour
    {
        [Serializable]
        public class BoneRotation
        {
            public AvatarObjectReference reference;
            public Quaternion rotation;
        }
        public List<BoneRotation> targets = new();

        public string parameterName;
        public bool immobilizeWhenParamTrue;

        public AnimatorController? controller;
        public AnimationClip? clipLocked;
        public AnimationClip? clipUnlocked;
    }
}