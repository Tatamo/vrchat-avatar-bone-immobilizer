#nullable enable

using nadena.dev.ndmf;
using Tatamo.AvatarBoneImmobilizer.Components;
using Tatamo.AvatarBoneImmobilizer.Components.Domain;
using Tatamo.AvatarBoneImmobilizer.Editor;
using Tatamo.AvatarBoneImmobilizer.Editor.Passses.Generating;
using Tatamo.AvatarBoneImmobilizer.Editor.Passses.Resolving;
using Tatamo.AvatarBoneImmobilizer.Editor.Passses.Transforming;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(BoneImmobilizerPlugin))]

namespace Tatamo.AvatarBoneImmobilizer.Editor
{
    public class BoneImmobilizerPlugin : Plugin<BoneImmobilizerPlugin>
    {
        public override string QualifiedName => "zip.udon.avatar-bone-immobilizer";

        public override string DisplayName => "Avatar Bone Immobilizer";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run("Bake component data",
                    ctx =>
                    {
                        CreateImmobilizeBonesDataPass.Run(ctx.AvatarRootTransform,
                            ctx.AvatarRootObject.GetComponentsInChildren<ImmobilizeBones>());
                    });
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Generate Animator for later use and MA Component",
                    ctx =>
                    {
                        GenerateAssetsPass.Run(ctx.AvatarRootTransform,
                            ctx.AvatarRootObject.GetComponentsInChildren<ImmobilizeBonesData>());
                    });
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Attach dummy bones and update animation clips", ctx =>
                {
                    ApplyChangesPass.Run(ctx.AvatarRootTransform,
                        ctx.AvatarRootObject.GetComponentsInChildren<ImmobilizeBonesData>());
                    foreach (var dataComponent in ctx.AvatarRootObject.GetComponentsInChildren<ImmobilizeBonesData>())
                    {
                        Object.DestroyImmediate(dataComponent);
                    }
                });
        }
    }
}