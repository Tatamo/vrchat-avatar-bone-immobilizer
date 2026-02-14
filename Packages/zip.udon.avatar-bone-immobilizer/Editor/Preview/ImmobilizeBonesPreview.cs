#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using Tatamo.AvatarBoneImmobilizer.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Preview
{
    internal class ImmobilizePreviewData
    {
        public GameObject AvatarRoot = null!;
        public Dictionary<Transform, Quaternion> BoneRotations = new();
    }

    internal class ImmobilizeBonesPreview : IRenderFilter
    {
        private static readonly TogglablePreviewNode EnableNode = TogglablePreviewNode.Create(
            () => "Bone Immobilizer",
            qualifiedName: "zip.udon.avatar-bone-immobilizer/BoneImmobilizerPreview",
            true
        );

        [InitializeOnLoadMethod]
        private static void StaticInit()
        {
        }

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield return EnableNode;
        }

        public bool IsEnabled(ComputeContext context)
        {
            return context.Observe(EnableNode.IsEnabled);
        }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext ctx)
        {
            var results = new List<RenderGroup>();

            foreach (var root in ctx.GetAvatarRoots())
            {
                if (ctx.ActiveInHierarchy(root) is false) continue;

                // Skip nested avatars
                if (ctx.GetAvatarRoot(root?.transform?.parent?.gameObject) != null) continue;

                var components = ctx.GetComponentsInChildren<ImmobilizeBones>(root, true);
                if (components.Length == 0) continue;

                var boneRotations = new Dictionary<Transform, Quaternion>();

                foreach (var component in components)
                {
                    if (component == null) continue;
                    if (!ctx.ActiveAndEnabled(component)) continue;
                    if (!ctx.Observe(component, c => c.enablePreview)) continue;

                    ResolveRotations(ctx, root.transform, component, boneRotations);
                }

                if (boneRotations.Count == 0) continue;

                var renderers = new HashSet<Renderer>();
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (renderer is not MeshRenderer and not SkinnedMeshRenderer) continue;
                    renderers.Add(renderer);
                }

                if (renderers.Count == 0) continue;

                results.Add(RenderGroup.For(renderers).WithData(new ImmobilizePreviewData
                {
                    AvatarRoot = root,
                    BoneRotations = boneRotations
                }));
            }

            return results.ToImmutableList();
        }

        private static void ResolveRotations(ComputeContext ctx, Transform avatarRootTransform,
            ImmobilizeBones component, Dictionary<Transform, Quaternion> boneRotations)
        {
            var rotationSource = ctx.Observe(component, c => c.rotationSource);
            ctx.Observe(component, c => c.clip);
            ctx.Observe(component, c => c.clipFrame);
            ctx.Observe(component,
                c => c.targetBones.Select(e => (
                    path: e?.targetBone?.referencePath ?? "",
                    euler: e?.euler ?? Vector3.zero
                )).ToList(),
                (a, b) => a.Count == b.Count && a.SequenceEqual(b));

            var list = new List<(Transform targetTransform, Vector3 euler)>();
            foreach (var entry in component.targetBones)
            {
                if (entry?.targetBone == null) continue;
                var targetObject = entry.targetBone.Get(component);
                if (targetObject == null) continue;
                list.Add((targetObject.transform, entry.euler));
            }

            if (list.Count == 0) return;

            switch (rotationSource)
            {
                case ImmobilizeBones.RotationSource.UseCurrent:
                    foreach (var (targetTransform, _) in list)
                    {
                        boneRotations[targetTransform] = Quaternion.Euler(targetTransform.localEulerAngles);
                    }

                    break;

                case ImmobilizeBones.RotationSource.PerBoneEuler:
                    foreach (var (targetTransform, euler) in list)
                    {
                        boneRotations[targetTransform] = Quaternion.Euler(euler);
                    }

                    break;

                case ImmobilizeBones.RotationSource.FromAnimationClip:
                    if (component.clip == null) break;

                    AnimationMode.StartAnimationMode();
                    try
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(avatarRootTransform.gameObject, component.clip,
                            component.clipFrame / component.clip.frameRate);
                        AnimationMode.EndSampling();

                        foreach (var (targetTransform, _) in list)
                        {
                            boneRotations[targetTransform] = targetTransform.localRotation;
                        }
                    }
                    finally
                    {
                        AnimationMode.StopAnimationMode();
                    }

                    break;
            }
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            return Task.FromResult<IRenderFilterNode>(
                new ImmobilizeBonesPreviewNode(context, group, proxyPairs));
        }
    }

    internal class ImmobilizeBonesPreviewNode : IRenderFilterNode
    {
        private readonly HashSet<Transform> _knownProxies = new();

        private readonly GameObject? SourceAvatarRoot;
        private readonly GameObject VirtualAvatarRoot;

        // Map from original bones to shadow bones (all bones in the hierarchy)
        private readonly Dictionary<Transform, Transform> _shadowBoneMap;

        // Rotation overrides for immobilized bones only
        private readonly Dictionary<Transform, Quaternion> _rotationOverrides;

        public ImmobilizeBonesPreviewNode(ComputeContext context, RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs)
        {
            var proxyPairList = proxyPairs.ToList();

            var data = group.GetData<ImmobilizePreviewData>();
            SourceAvatarRoot = data.AvatarRoot;
            _rotationOverrides = data.BoneRotations;

            var bonesSet = GetSourceBonesSet(context, proxyPairList);
            var bones = bonesSet.OrderBy(k => k.gameObject.name).ToArray();

            var scene = NDMFPreviewSceneManager.GetPreviewScene();
            var priorScene = SceneManager.GetActiveScene();

            try
            {
                SceneManager.SetActiveScene(scene);
                VirtualAvatarRoot = new GameObject(SourceAvatarRoot.name + " [BoneImmobilizer]");
                _shadowBoneMap = CreateShadowBones(bones);
            }
            finally
            {
                SceneManager.SetActiveScene(priorScene);
            }

            SyncBoneStates();
        }

        private static HashSet<Transform> GetSourceBonesSet(ComputeContext context,
            List<(Renderer, Renderer)> proxyPairs)
        {
            var bonesSet = new HashSet<Transform>();
            foreach (var (_, r) in proxyPairs)
            {
                if (r == null) continue;

                var rootBone = context.Observe(r, r_ => (r_ as SkinnedMeshRenderer)?.rootBone) ?? r.transform;
                bonesSet.Add(rootBone);

                var smr = r as SkinnedMeshRenderer;
                if (smr == null) continue;

                foreach (var b in context.Observe(smr, smr_ => smr_.bones, Enumerable.SequenceEqual))
                {
                    if (b != null) bonesSet.Add(b);
                }
            }

            return bonesSet;
        }

        private Dictionary<Transform, Transform> CreateShadowBones(Transform[] srcBones)
        {
            var srcToDst = new Dictionary<Transform, Transform>();

            for (var i = 0; i < srcBones.Length; i++) GetShadowBone(srcBones[i]);

            return srcToDst;

            Transform? GetShadowBone(Transform? srcBone)
            {
                if (srcBone == null) return null;
                if (srcToDst.TryGetValue(srcBone, out var dstBone)) return dstBone;

                var newBone = new GameObject(srcBone.name);
                ObjectRegistry.RegisterReplacedObject(srcBone.gameObject, newBone.gameObject);
                newBone.transform.SetParent(GetShadowBone(srcBone.parent) ?? VirtualAvatarRoot.transform);
                newBone.transform.localPosition = srcBone.localPosition;
                newBone.transform.localRotation = srcBone.localRotation;
                newBone.transform.localScale = srcBone.localScale;

                srcToDst[srcBone] = newBone.transform;

                return newBone.transform;
            }
        }

        private void SyncBoneStates()
        {
            // Copy local transforms from original to shadow
            foreach (var (src, dst) in _shadowBoneMap)
            {
                if (src == null || dst == null) continue;
                dst.localPosition = src.localPosition;
                dst.localRotation = src.localRotation;
                dst.localScale = src.localScale;
            }

            // Override rotations for immobilized bones
            foreach (var (bone, rotation) in _rotationOverrides)
            {
                if (bone == null) continue;
                if (_shadowBoneMap.TryGetValue(bone, out var shadowBone) && shadowBone != null)
                {
                    shadowBone.localRotation = rotation;
                }
            }
        }

        public RenderAspects WhatChanged => RenderAspects.Shapes;

        public void OnFrameGroup()
        {
            SyncBoneStates();
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (proxy == null) return;

            var curParent = proxy.transform.parent ?? original.transform.parent;
            if (curParent != null && _shadowBoneMap.TryGetValue(curParent, out var newRoot))
            {
                _knownProxies.Add(proxy.transform);
                proxy.transform.SetParent(newRoot, false);
            }

            var smr = proxy as SkinnedMeshRenderer;
            if (smr == null) return;

            var rootBone = _shadowBoneMap.TryGetValue(smr.rootBone, out var newRootBone)
                ? newRootBone
                : smr.rootBone;
            smr.rootBone = rootBone;
            smr.bones = smr.bones
                .Select(b => b == null ? null : _shadowBoneMap.GetValueOrDefault(b, b))
                .ToArray();
        }

        public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context, RenderAspects updatedAspects)
        {
            if (SourceAvatarRoot == null) return Task.FromResult<IRenderFilterNode>(null!);

            _knownProxies.RemoveWhere(p => p == null);

            var proxyPairList = proxyPairs.ToList();

            if (!GetSourceBonesSet(context, proxyPairList).SetEquals(_shadowBoneMap.Keys))
                return Task.FromResult<IRenderFilterNode>(null!);

            return Task.FromResult<IRenderFilterNode>(this);
        }

        public void Dispose()
        {
            foreach (var proxy in _knownProxies)
            {
                if (proxy != null && proxy.IsChildOf(VirtualAvatarRoot.transform))
                {
                    proxy.transform.SetParent(null, false);
                }
            }

            Object.DestroyImmediate(VirtualAvatarRoot);
        }
    }
}
