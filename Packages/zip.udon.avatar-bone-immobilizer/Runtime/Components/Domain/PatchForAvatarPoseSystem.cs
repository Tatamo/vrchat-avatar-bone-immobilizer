#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Components.Domain
{
    [AddComponentMenu("")]
    public class PatchForAvatarPoseSystem: MonoBehaviour
    {
        [Serializable]
        public class FixTarget
        {
            public string name;
            public string path;
        }
        public List<FixTarget> targets = new();
        public string layerName;
        public string lockClipName;
        public string? unlockClipName;
        public string? disableClipName;
    }
}
