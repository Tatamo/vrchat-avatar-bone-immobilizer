using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using Tatamo.AvatarBoneImmobilizer.Components;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tatamo.AvatarBoneImmobilizer.Editor.Inspectors
{
    [CustomEditor(typeof(ImmobilizeBones))]
    public class ImmobilizeBonesEditor : UnityEditor.Editor

    {
        SerializedProperty _rotationSource, _clip, _clipFrame, _parameterName, _immobilizeWhenParamTrue, _targetBones,
            _enablePreview;

        ReorderableList _list;

        ImmobilizeBones TargetComponent => (ImmobilizeBones)target;

        void OnEnable()
        {
            _rotationSource = serializedObject.FindProperty(nameof(ImmobilizeBones.rotationSource));
            _clip = serializedObject.FindProperty(nameof(ImmobilizeBones.clip));
            _clipFrame = serializedObject.FindProperty(nameof(ImmobilizeBones.clipFrame));
            _parameterName = serializedObject.FindProperty(nameof(ImmobilizeBones.parameterName));
            _immobilizeWhenParamTrue = serializedObject.FindProperty(nameof(ImmobilizeBones.immobilizeWhenParamTrue));
            _targetBones = serializedObject.FindProperty(nameof(ImmobilizeBones.targetBones));
            _enablePreview = serializedObject.FindProperty(nameof(ImmobilizeBones.enablePreview));

            _list = new ReorderableList(serializedObject, _targetBones, draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            _list.drawHeaderCallback = r =>
            {
                EditorGUI.LabelField(r, "Target Bones");
                if (!r.Contains(Event.current.mousePosition)) return;
                switch (Event.current.type)
                {
                    case EventType.DragUpdated:
                        ;
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        Event.current.Use();
                        break;

                    case EventType.DragPerform:
                        DragAndDrop.AcceptDrag();
                        AddDroppedObjects(DragAndDrop.objectReferences);
                        Event.current.Use();
                        break;
                }
            };

            _list.elementHeightCallback = _ =>
            {
                float height = EditorGUIUtility.singleLineHeight + 6;
                if ((ImmobilizeBones.RotationSource)_rotationSource.enumValueIndex ==
                    ImmobilizeBones.RotationSource.PerBoneEuler)
                    height += EditorGUIUtility.singleLineHeight + 4;
                return height;
            };

            _list.drawElementCallback = (rect, index, _, _) =>
            {
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 80f;
                EditorGUI.PropertyField(new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight),
                    _targetBones.GetArrayElementAtIndex(index)
                        .FindPropertyRelative(nameof(ImmobilizeBones.BoneEntry.targetBone)),
                    new GUIContent($"Element {index}"));

                if ((ImmobilizeBones.RotationSource)_rotationSource.enumValueIndex ==
                    ImmobilizeBones.RotationSource.PerBoneEuler)
                {
                    var buttonWidth = 60f;
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 6, rect.width - buttonWidth - 6, EditorGUIUtility.singleLineHeight),
                        _targetBones.GetArrayElementAtIndex(index)
                            .FindPropertyRelative(nameof(ImmobilizeBones.BoneEntry.euler)), new GUIContent("Rotation"));
                    if (GUI.Button(new Rect(rect.x + rect.width - buttonWidth, rect.y + EditorGUIUtility.singleLineHeight + 6, buttonWidth, EditorGUIUtility.singleLineHeight), "Capture"))
                        CaptureEuler(index);
                }

                EditorGUIUtility.labelWidth = originalLabelWidth;
            };

            _list.onAddCallback = _ =>
            {
                Undo.RecordObject(target, "Add Bone Entry");
                _targetBones.arraySize++;
                serializedObject.ApplyModifiedProperties();
                Repaint();
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200;
            EditorGUILayout.PropertyField(_rotationSource, new GUIContent("Rotation Source"));
            EditorGUI.indentLevel++;
            if ((ImmobilizeBones.RotationSource)_rotationSource.enumValueIndex ==
                ImmobilizeBones.RotationSource.FromAnimationClip)
            {
                EditorGUILayout.PropertyField(_clip, new GUIContent("Animation Clip"));

                using (new EditorGUI.DisabledScope(_clip.objectReferenceValue == null))
                {
                    int maxFrame = 0;
                    if (_clip.objectReferenceValue is AnimationClip ac && ac.frameRate > 0f)
                    {
                        var fps = ac.frameRate;
                        maxFrame = Mathf.Max(0, Mathf.FloorToInt(ac.length * fps + 0.0001f));
                    }

                    var label = maxFrame > 0 ? $"Clip Frame (0..{maxFrame})" : "Clip Frame";
                    _clipFrame.intValue = EditorGUILayout.IntField(label, _clipFrame.intValue);
                    if (maxFrame > 0) _clipFrame.intValue = Mathf.Clamp(_clipFrame.intValue, 0, maxFrame);
                }
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Parameter Control", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_parameterName, new GUIContent("Parameter Name"));
            EditorGUILayout.PropertyField(_immobilizeWhenParamTrue, new GUIContent("Immobilize when param is true"));

            EditorGUILayout.Space(8);

            _list.DoLayoutList();

            if ((ImmobilizeBones.RotationSource)_rotationSource.enumValueIndex ==
                ImmobilizeBones.RotationSource.PerBoneEuler)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Capture All (Current Pose)", GUILayout.Width(220))) CaptureAllEuler();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_enablePreview, new GUIContent("Enable Preview"));

            DrawValidation();
            EditorGUIUtility.labelWidth = originalLabelWidth;
            serializedObject.ApplyModifiedProperties();
        }

        void AddDroppedObjects(Object[] objects)
        {
            Undo.RecordObject(TargetComponent, "Add Bones (Drag & Drop)");
            IEnumerable<Transform> ExtractTransforms()
            {
                foreach (var obj in objects)
                {
                    if (obj is GameObject gameObject) yield return gameObject.transform;
                    else if (obj is Component component) yield return component.transform;
                }
            }

            foreach (var transform in ExtractTransforms())
            {
                var target = new AvatarObjectReference();
                target.Set(transform.gameObject);

                var entry = new ImmobilizeBones.BoneEntry
                {
                    targetBone = target,
                    euler = transform.localEulerAngles
                };
                TargetComponent.targetBones.Add(entry);
            }

            EditorUtility.SetDirty(TargetComponent);
            serializedObject.Update();
        }

        public void CaptureEuler(int index)
        {
            Undo.RecordObject(TargetComponent, "Capture Euler");
            TargetComponent.CaptureEuler(index);
            EditorUtility.SetDirty(TargetComponent);
            serializedObject.Update();
        }

        public void CaptureAllEuler()
        {
            Undo.RecordObject(TargetComponent, "Capture All Euler");
            TargetComponent.CaptureAllEuler();
            EditorUtility.SetDirty(TargetComponent);
            serializedObject.Update();
        }

        void DrawValidation()
        {
            var messages = new List<string>();

            if (TargetComponent.rotationSource ==
                ImmobilizeBones.RotationSource.FromAnimationClip
                && TargetComponent.clip == null)
            {
                messages.Add("Animation Clip is not set.");
            }

            if (TargetComponent.parameterName == "")
            {
                messages.Add("Parameter Name is not set.");
            }

            if (messages.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(string.Join("\n", messages), MessageType.Warning);
            }
        }
    }
}