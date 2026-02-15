#nullable enable

using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Components.Domain
{
    [AddComponentMenu("")]
    public class GlobalDisableData : MonoBehaviour
    {
        public RuntimeAnimatorController? controller;
        public AnimationClip? disableClip;
    }
}
