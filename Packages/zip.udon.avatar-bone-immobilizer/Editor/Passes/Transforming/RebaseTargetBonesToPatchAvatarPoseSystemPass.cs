#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using UnityEditor.Animations;
using UnityEngine;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Passses.Transforming
{
    public static class RebaseTargetBonesToPatchAvatarPoseSystemPass
    {
        /**
         * AvatarPoseSystemがアバターに適用されている場合の競合を回避する（想定APSバージョン: 4.21）
         *
         * APSのクローンBone - APSの_Const Bone - 元々のBone という構造が作られ、元々のBoneはこの時点ではBone Proxyがアタッチされている。
         * この構造を特定して、AvatarBoneImmobilizerが操作する対象を元々のBoneからAPSのBoneに移し替える。
         */
        public static void Run(Transform avatarRootTransform, IEnumerable<ImmobilizeBonesData> components,
            GlobalDisableData? globalDisableData)
        {
            bool anyPatched = false;
            // APS検出時のパッチターゲットを全コンポーネント分まとめて収集する（グローバル無効化クリップ用）
            var allPatchTargets = new List<PatchForAvatarPoseSystem.FixTarget>();

            foreach (var data in components)
            {
                if (data == null) continue;
                List<PatchForAvatarPoseSystem.FixTarget> patchTargets = new();
                foreach (var entry in data.targets)
                {
                    if (entry == null) continue;
                    var gameObject = entry.reference.Get(data);
                    if (gameObject == null) continue;

                    var boneProxies = gameObject.GetComponents<ModularAvatarBoneProxy>();
                    if (boneProxies == null || boneProxies.Length == 0) continue;
                    foreach (var boneProxy in boneProxies)
                    {
                        if (boneProxy.target == null) continue;
                        if (boneProxy.target.name != gameObject.name + "_Const") continue;
                        if (boneProxy.target.parent == null) continue;
                        if (boneProxy.target.parent.name != gameObject.name) continue;

                        var newReference = new AvatarObjectReference();
                        newReference.Set(boneProxy.target.parent.gameObject);
                        entry.reference = newReference;
                        patchTargets.Add(new PatchForAvatarPoseSystem.FixTarget()
                        {
                            name = gameObject.name,
                            path = newReference.referencePath
                        });
                        break;
                    }
                }

                if (patchTargets.Count > 0)
                {
                    anyPatched = true;
                    var patchComponent = data.gameObject.AddComponent<PatchForAvatarPoseSystem>();
                    patchComponent.lockClipName = data.clipLocked!.name;
                    // OFFクリップは空なのでパッチ不要
                    patchComponent.unlockClipName = null;
                    var controller = (AnimatorController)data.controller!;
                    patchComponent.layerName = controller.layers[0].name;
                    patchComponent.targets = patchTargets;
                    allPatchTargets.AddRange(patchTargets);
                }
            }

            // グローバル無効化クリップ用のパッチ
            if (anyPatched && globalDisableData != null)
            {
                var patchComponent = globalDisableData.gameObject.AddComponent<PatchForAvatarPoseSystem>();
                patchComponent.lockClipName = "";
                patchComponent.unlockClipName = null;
                patchComponent.disableClipName = globalDisableData.disableClip!.name;
                var controller = (AnimatorController)globalDisableData.controller!;
                patchComponent.layerName = controller.layers[0].name;
                // 重複を除いたターゲットリストを作成
                var uniqueTargets = new Dictionary<string, PatchForAvatarPoseSystem.FixTarget>();
                foreach (var target in allPatchTargets)
                {
                    uniqueTargets[target.path] = target;
                }
                patchComponent.targets = new List<PatchForAvatarPoseSystem.FixTarget>(uniqueTargets.Values);
            }
        }
    }
}
