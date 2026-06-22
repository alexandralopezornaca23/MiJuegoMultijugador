using UnityEngine;
using UnityEditor;

public class AnimationPathRemapper : EditorWindow
{
    private AnimationClip sourceClip;
    private string prefix = "Armature/";
    private string outputName = "RemappedClip";
    private string hipsBoneName = "Hips";

    private Transform targetHipsTransform; // Hips del modelo del jugador, para leer su rest pose

    private bool fixOffsetX = false;
    private bool fixOffsetY = true;
    private bool fixOffsetZ = false;

    [MenuItem("Tools/Animation Path Remapper")]
    public static void ShowWindow()
    {
        GetWindow<AnimationPathRemapper>("Animation Path Remapper");
    }

    private void OnGUI()
    {
        sourceClip = (AnimationClip)EditorGUILayout.ObjectField("Source Clip (de Mixamo)", sourceClip, typeof(AnimationClip), false);
        prefix = EditorGUILayout.TextField("Prefijo a añadir", prefix);
        hipsBoneName = EditorGUILayout.TextField("Nombre del hueso raíz (Hips)", hipsBoneName);
        outputName = EditorGUILayout.TextField("Nombre del nuevo clip", outputName);

        EditorGUILayout.Space();
        targetHipsTransform = (Transform)EditorGUILayout.ObjectField(
            "Hips del modelo (rest pose)", targetHipsTransform, typeof(Transform), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anclar offset del hueso raíz a la rest pose del modelo:");
        fixOffsetX = EditorGUILayout.Toggle("Eje X", fixOffsetX);
        fixOffsetY = EditorGUILayout.Toggle("Eje Y", fixOffsetY);
        fixOffsetZ = EditorGUILayout.Toggle("Eje Z", fixOffsetZ);

        EditorGUILayout.Space();

        if (GUILayout.Button("Remapear y guardar") && sourceClip != null)
        {
            RemapClip();
        }
    }

    private void RemapClip()
    {
        AnimationClip newClip = new AnimationClip();
        newClip.frameRate = sourceClip.frameRate;

        var bindings = AnimationUtility.GetCurveBindings(sourceClip);

        foreach (var binding in bindings)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);

            EditorCurveBinding newBinding = binding;
            newBinding.path = string.IsNullOrEmpty(binding.path)
                ? prefix.TrimEnd('/')
                : prefix + binding.path;

            bool isHipsPosition =
                binding.path == hipsBoneName &&
                binding.propertyName.StartsWith("m_LocalPosition");

            if (isHipsPosition && targetHipsTransform != null && curve.length > 0)
            {
                bool fixThisAxis =
                    (binding.propertyName.EndsWith(".x") && fixOffsetX) ||
                    (binding.propertyName.EndsWith(".y") && fixOffsetY) ||
                    (binding.propertyName.EndsWith(".z") && fixOffsetZ);

                if (fixThisAxis)
                {
                    float targetValue = 0f;
                    if (binding.propertyName.EndsWith(".x")) targetValue = targetHipsTransform.localPosition.x;
                    else if (binding.propertyName.EndsWith(".y")) targetValue = targetHipsTransform.localPosition.y;
                    else if (binding.propertyName.EndsWith(".z")) targetValue = targetHipsTransform.localPosition.z;

                    float originalFirstValue = curve.keys[0].value;
                    float delta = targetValue - originalFirstValue;

                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].value += delta;
                    }
                    curve = new AnimationCurve(keys);
                }
            }

            AnimationUtility.SetEditorCurve(newClip, newBinding, curve);
        }

        var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(sourceClip);
        foreach (var binding in objBindings)
        {
            var keyframes = AnimationUtility.GetObjectReferenceCurve(sourceClip, binding);
            EditorCurveBinding newBinding = binding;
            newBinding.path = string.IsNullOrEmpty(binding.path)
                ? prefix.TrimEnd('/')
                : prefix + binding.path;
            AnimationUtility.SetObjectReferenceCurve(newClip, newBinding, keyframes);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(sourceClip);
        AnimationUtility.SetAnimationClipSettings(newClip, settings);

        string path = $"Assets/{outputName}.anim";
        AssetDatabase.CreateAsset(newClip, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Clip remapeado guardado en: {path}. Delta aplicado según rest pose de {targetHipsTransform?.name}");
    }
}