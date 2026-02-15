# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Avatar Bone Immobilizer is a Unity NDMF plugin for VRChat that immobilizes humanoid bone joints, preventing them from being affected by locomotion animations or VR tracking. It works by creating dummy sibling bones with VRCRotationConstraints at build time, bypassing VRC Animator Tracking Control state changes.

Primary documentation (README.md) is in Japanese.

## Build & Development

This is a Unity UPM package (`zip.udon.avatar-bone-immobilizer`). There are no CLI build commands, linters, or automated tests. Development is done entirely within Unity Editor 2022.3+.

**Dependencies** (VPM): VRChat Avatars SDK `^3.7.0`, NDMF `>=1.8.0`, Modular Avatar `>=1.13.0`

**Assembly definitions:**
- `Tatamo.AvatarBoneImmobilizer` — Runtime assembly (components only)
- `Tatamo.AvatarBoneImmobilizer.Editor` — Editor assembly (plugin, passes, inspector, preview)

## Architecture

### NDMF Plugin Pipeline (`BoneImmobilizerPlugin`)

The plugin registers passes across NDMF build phases:

1. **Resolving** — `CreateImmobilizeBonesDataPass`: Converts user-facing `ImmobilizeBones` components into `ImmobilizeBonesData` domain objects, resolving rotation sources (current pose, per-bone euler, animation clip sampling). Removes the original components.
2. **Generating** (before Modular Avatar) — `GenerateAssetsPass`: Creates AnimatorController and AnimationClips for locked/unlocked bone states, attaches MA Merge Animator.
3. **Transforming** (before MA) — `RebaseTargetBonesToPatchAvatarPoseSystemPass` + `ApplyChangesPass`: Creates dummy sibling bones, attaches VRCRotationConstraints, handles AvatarPoseSystem compatibility by rebasing bone targets.
4. **Transforming** (after MA) — `RebaseAnimationsPathToPatchAvatarPoseSystemPass`: Fixes animation paths post-MA processing for AvatarPoseSystem compatibility.

### Key Components

- **`ImmobilizeBones`** (Runtime) — User-facing MonoBehaviour. Holds rotation source config (`UseCurrent`/`PerBoneEuler`/`FromAnimationClip`), target bones list, parameter name for toggle control.
- **`ImmobilizeBonesData`** (Runtime) — Domain model created during build. Holds resolved bone rotations and generated animation references. Not user-facing.
- **`PatchForAvatarPoseSystem`** (Runtime) — Marker component for post-MA AvatarPoseSystem path correction.
- **`ImmobilizeBonesPreview`** (Editor) — NDMF preview integration for real-time scene visualization.
- **`ImmobilizeBonesEditor`** (Editor) — Custom inspector with ReorderableList, drag-and-drop bone adding, capture buttons.

### Namespace Structure

- `Tatamo.AvatarBoneImmobilizer.Components` — User-facing runtime components
- `Tatamo.AvatarBoneImmobilizer.Components.Domain` — Build-time domain models
- `Tatamo.AvatarBoneImmobilizer.Editor` — Plugin entry point
- `Tatamo.AvatarBoneImmobilizer.Editor.Passses.*` — Build passes (note: `Passses` has triple-s typo in namespace)
- `Tatamo.AvatarBoneImmobilizer.Editor.Preview` — Preview system
- `Tatamo.AvatarBoneImmobilizer.Editor.Inspectors` — Custom editors

## Code Conventions

- `#nullable enable` is used throughout
- Comments explaining complex logic are written in Japanese
