#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class AnimationClipFixer : EditorWindow
{
    private GameObject referenceModel;
    private AnimationClip clipToFix;

    [MenuItem("Tools/VAT Animation Fixer")]
    public static void ShowWindow()
    {
        GetWindow<AnimationClipFixer>("VAT Anim Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Add Missing Transform Keys safely", EditorStyles.boldLabel);
        
        referenceModel = (GameObject)EditorGUILayout.ObjectField("Reference Model", referenceModel, typeof(GameObject), true);
        clipToFix = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", clipToFix, typeof(AnimationClip), false);

        if (GUILayout.Button("Fix Animation Clip"))
        {
            if (referenceModel == null || clipToFix == null) return;
            FixClipSafely(referenceModel, clipToFix);
        }
    }

    private void FixClipSafely(GameObject root, AnimationClip clip)
    {
        // Ctrl+Z 를 위한 Undo 등록
        Undo.RecordObject(clip, "Fix VAT Animation");

        Transform[] allTransforms = root.GetComponentsInChildren<Transform>();

        foreach (Transform t in allTransforms)
        {
            string path = AnimationUtility.CalculateTransformPath(t, root.transform);
            if (string.IsNullOrEmpty(path)) continue;

            // 1. Position 독립 검사 및 추가
            AddCurveIfMissing(clip, path, "m_LocalPosition.x", t.localPosition.x);
            AddCurveIfMissing(clip, path, "m_LocalPosition.y", t.localPosition.y);
            AddCurveIfMissing(clip, path, "m_LocalPosition.z", t.localPosition.z);

            // 2. Rotation 검사 (오일러 방식과 쿼터니언 방식 중 하나라도 있으면 패스)
            bool hasEulerX = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.x")) != null;
            bool hasQuatX = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x")) != null;

            if (!hasEulerX && !hasQuatX)
            {
                // 회전값이 아예 없는 뼈대에만 오일러 각도로 기본 포즈 추가
                Vector3 euler = t.localEulerAngles;
                AddCurveIfMissing(clip, path, "localEulerAnglesRaw.x", euler.x);
                AddCurveIfMissing(clip, path, "localEulerAnglesRaw.y", euler.y);
                AddCurveIfMissing(clip, path, "localEulerAnglesRaw.z", euler.z);
            }
        }
        
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>성공적으로 처리되었습니다!</color> 기존 키프레임은 유지되고 빈 곳만 채워졌습니다.");
    }

    private void AddCurveIfMissing(AnimationClip clip, string path, string propertyName, float value)
    {
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        if (AnimationUtility.GetEditorCurve(clip, binding) == null)
        {
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 0f, value));
        }
    }
}
#endif