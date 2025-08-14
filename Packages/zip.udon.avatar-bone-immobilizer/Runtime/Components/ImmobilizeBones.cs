#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDKBase;

namespace Tatamo.AvatarBoneImmobilizer.Components
{
    [AddComponentMenu("Tatamo/AvatarBoneImmobilizer/ImmobilizeBones")]
    public class ImmobilizeBones : MonoBehaviour, IEditorOnly
    {
        public enum RotationSource
        {
            UseCurrent,
            PerBoneEuler,
            FromAnimationClip
        }

        [System.Serializable]
        public class BoneEntry
        {
            public AvatarObjectReference? targetBone;
            public Vector3 euler;
        }

        public RotationSource rotationSource = RotationSource.UseCurrent;

        public AnimationClip? clip;
        public int clipFrame = 0;

        public string parameterName = "";
        public bool immobilizeWhenParamTrue = true;
        
        public List<BoneEntry> targetBones = new();
    }
}