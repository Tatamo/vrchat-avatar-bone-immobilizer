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

        public string parameterName = "ImmobilizeBones";
        public bool immobilizeWhenParamTrue = true;
        
        public List<BoneEntry> targetBones = new();

        public bool enablePreview = true;

        public void CaptureEuler(int index)
        {
            if (index < 0 || index >= targetBones.Count) return;

            var entry = targetBones[index];
            if (entry.targetBone == null) return;

            var targetObject = entry.targetBone.Get(this);
            if (targetObject == null) return;

            entry.euler = targetObject.transform.localEulerAngles;
        }

        public void CaptureAllEuler()
        {
            for (int i = 0; i < targetBones.Count; i++)
            {
                CaptureEuler(i);
            }
        }
    }
}