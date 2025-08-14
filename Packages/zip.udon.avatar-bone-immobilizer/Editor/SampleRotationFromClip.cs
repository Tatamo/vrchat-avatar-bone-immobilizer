#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Editor
{
    public static class SampleRotationFromClip
    {
        public static List<Quaternion> SampleRotations(
            GameObject avatarRoot,
            List<Transform> targets,
            AnimationClip clip,
            int frame)
        {
            var result = new List<Quaternion>(targets.Count);
            for (int i = 0; i < targets.Count; i++) result.Add(Quaternion.identity);

            if (avatarRoot == null || clip == null || targets.Count == 0) return result;

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(avatarRoot, clip, frame / clip.frameRate);
                AnimationMode.EndSampling();

                for (int i = 0; i < targets.Count; i++)
                {
                    var tr = targets[i];
                    if (tr != null) result[i] = tr.localRotation;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            return result;
        }
    }
}