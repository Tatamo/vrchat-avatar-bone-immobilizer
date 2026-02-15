#nullable enable


using System.Collections.Generic;
using System.Linq;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Passses.Transforming
{
    public static class ApplyChangesPass
    {
        /// <summary>
        /// 各コンポーネントのボーンエントリ情報。ダミーボーンとConstraint内でのSourceインデックスを保持する。
        /// </summary>
        private class BoneEntryInfo
        {
            public ImmobilizeBonesData data = null!;
            public ImmobilizeBonesData.BoneRotation entry = null!;
            public Transform dummyBone = null!;
            public int sourceIndex;
        }

        public static void Run(Transform avatarRootTransform, IEnumerable<ImmobilizeBonesData> domainObjects,
            GlobalDisableData? globalDisableData)
        {
            var dataList = domainObjects.Where(d => d != null).ToList();
            if (dataList.Count == 0) return;

            // 全ボーン参照を収集し、referencePath をキーにグループ化
            var boneGroups =
                new Dictionary<string, List<(ImmobilizeBonesData data, ImmobilizeBonesData.BoneRotation entry)>>();

            foreach (var data in dataList)
            {
                foreach (var entry in data.targets)
                {
                    var path = entry.reference.referencePath;
                    if (!boneGroups.ContainsKey(path))
                    {
                        boneGroups[path] = new List<(ImmobilizeBonesData, ImmobilizeBonesData.BoneRotation)>();
                    }

                    boneGroups[path].Add((data, entry));
                }
            }

            // 各一意ボーンに対してダミーボーンとConstraintを作成
            var boneEntryInfos = new Dictionary<string, List<BoneEntryInfo>>();

            foreach (var (referencePath, entries) in boneGroups)
            {
                // 最初のエントリからターゲットオブジェクトを取得
                Transform? target = null;
                foreach (var (data, entry) in entries)
                {
                    var targetObject = entry.reference.Get(data);
                    if (targetObject != null)
                    {
                        target = targetObject.transform;
                        break;
                    }
                }

                if (target == null)
                {
                    Debug.Log(
                        $"Skip: ImmobilizeBones target GameObject {referencePath} not found");
                    continue;
                }

                var parent = target.parent;
                if (parent == null)
                {
                    Debug.Log(
                        $"Skip: ImmobilizeBones target GameObject {referencePath} has no parent transform (cannot create sibling dummy bone)");
                    continue;
                }

                // 各コンポーネントに対してダミーボーンを作成
                var dummyBones =
                    new List<(Transform dummy, ImmobilizeBonesData data, ImmobilizeBonesData.BoneRotation entry)>();
                foreach (var (data, entry) in entries)
                {
                    var dummy = CreateDummyBone(parent, target, target.name);
                    dummyBones.Add((dummy, data, entry));
                }

                // 単一のVRCRotationConstraintを作成、全ダミーボーンをSourceとして追加
                var isShared = entries.Count > 1;
                var sources = new VRCConstraintSourceKeyableList(dummyBones.Count);
                for (int i = 0; i < dummyBones.Count; i++)
                {
                    // 共有ボーンの場合はweight=0（アニメーションで制御）、非共有ボーンはweight=1
                    sources[i] = new VRCConstraintSource(dummyBones[i].dummy, isShared ? 0f : 1f);
                }

                var constraint = target.gameObject.AddComponent<VRCRotationConstraint>();
                constraint.Sources = sources;
                // ActivateConstraintでオフセットをキャプチャしてからIsActive=falseに設定する。
                // グローバル無効化レイヤーがIsActive=0を常時再生し、
                // 個別コンポーネントのONアニメーションがIsActive=1に上書きする。
                constraint.ActivateConstraint();
                constraint.IsActive = false;

                var infos = new List<BoneEntryInfo>();
                for (int i = 0; i < dummyBones.Count; i++)
                {
                    infos.Add(new BoneEntryInfo
                    {
                        data = dummyBones[i].data,
                        entry = dummyBones[i].entry,
                        dummyBone = dummyBones[i].dummy,
                        sourceIndex = i
                    });
                }

                boneEntryInfos[referencePath] = infos;
                Debug.Log($"targetPath: {GetRelativePath(avatarRootTransform, target)}");
            }

            // アニメーションクリップを設定

            // グローバル無効化クリップ: 全ConstraintのIsActiveを0にセットするアニメーションを、他のアニメーションクリップより前に設定
            if (globalDisableData?.disableClip != null)
            {
                foreach (var (referencePath, infos) in boneEntryInfos)
                {
                    var targetObject = infos[0].entry.reference.Get(infos[0].data);
                    if (targetObject == null) continue;
                    var targetPath = GetRelativePath(avatarRootTransform, targetObject.transform);

                    AnimationUtility.SetEditorCurve(globalDisableData.disableClip,
                        EditorCurveBinding.FloatCurve(targetPath, typeof(VRCRotationConstraint), "IsActive"),
                        new AnimationCurve(new Keyframe(0f, 0f))
                    );
                }
            }

            // 各コンポーネントのアニメーションクリップを設定
            foreach (var data in dataList)
            {
                if (data.clipLocked == null || data.clipUnlocked == null) continue;

                var myEntries = new List<(string referencePath, BoneEntryInfo info)>();
                foreach (var (referencePath, infos) in boneEntryInfos)
                {
                    foreach (var info in infos)
                    {
                        if (info.data == data)
                        {
                            myEntries.Add((referencePath, info));
                        }
                    }
                }

                foreach (var (referencePath, info) in myEntries)
                {
                    var dummyPath = GetRelativePath(avatarRootTransform, info.dummyBone);
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(dummyPath, typeof(Transform), "m_LocalRotation.x"),
                        new AnimationCurve(new Keyframe(0f, info.entry.rotation.x))
                    );
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(dummyPath, typeof(Transform), "m_LocalRotation.y"),
                        new AnimationCurve(new Keyframe(0f, info.entry.rotation.y))
                    );
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(dummyPath, typeof(Transform), "m_LocalRotation.z"),
                        new AnimationCurve(new Keyframe(0f, info.entry.rotation.z))
                    );
                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(dummyPath, typeof(Transform), "m_LocalRotation.w"),
                        new AnimationCurve(new Keyframe(0f, info.entry.rotation.w))
                    );
                    
                    var targetObject = info.entry.reference.Get(info.data);
                    if (targetObject == null) continue;
                    var targetPath = GetRelativePath(avatarRootTransform, targetObject.transform);

                    AnimationUtility.SetEditorCurve(data.clipLocked,
                        EditorCurveBinding.FloatCurve(targetPath, typeof(VRCRotationConstraint), "IsActive"),
                        new AnimationCurve(new Keyframe(0f, 1f))
                    );

                    // 共有ボーンの場合: Source Weightをアニメーションで制御
                    var allInfos = boneEntryInfos[referencePath];
                    if (allInfos.Count > 1)
                    {
                        foreach (var otherInfo in allInfos)
                        {
                            var weight = otherInfo.data == data ? 1f : 0f;
                            AnimationUtility.SetEditorCurve(data.clipLocked,
                                EditorCurveBinding.FloatCurve(targetPath, typeof(VRCRotationConstraint),
                                    $"Sources.source{otherInfo.sourceIndex}.Weight"),
                                new AnimationCurve(new Keyframe(0f, weight))
                            );
                        }
                    }
                }
            }
        }

        private static Transform CreateDummyBone(Transform parent, Transform source, string baseName)
        {
            string boneName = MakeUniqueChildName(parent, $"{baseName}.ImmobilizeSource");

            var gameObject = new GameObject(boneName);
            var transform = gameObject.transform;
            transform.SetParent(parent, worldPositionStays: false);

            transform.localPosition = source.localPosition;
            transform.localRotation = source.localRotation;
            transform.localScale = source.localScale;

            return transform;
        }

        private static Transform? FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
            }

            return null;
        }

        private static string MakeUniqueChildName(Transform parent, string baseName)
        {
            if (FindChildByName(parent, baseName) == null) return baseName;
            int i = 1;
            while (FindChildByName(parent, $"{baseName} ({i})") != null) i++;
            return $"{baseName} ({i})";
        }

        private static string GetRelativePath(Transform root, Transform t)
        {
            if (t == root) return "";
            var stack = new Stack<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }

            return string.Join("/", stack.ToArray());
        }
    }
}