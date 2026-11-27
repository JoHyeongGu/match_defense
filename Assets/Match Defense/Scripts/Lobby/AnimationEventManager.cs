using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using MatchDefense.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MatchDefense
{
    [Serializable]
    public class CameraShotPreset
    {
        [Tooltip("Unique shot ID called from Animation Events (e.g., intro_wide, oven_zoom)")]
        public string shotId = "shot_1";
        [Tooltip("If enabled, uses local coordinates relative to this STAGE Object")]
        public bool useLocalSpace = true;
        public Vector3 position;
        public Vector3 eulerAngles;

        [Header("Transition & Lens")]
        [Tooltip("0 = instant snap; > 0 = smooth transition duration in seconds")]
        [Min(0f)] public float duration = 1.0f;
        public Ease ease = Ease.InOutCubic;
        public bool overrideFov = false;
        public float fieldOfView = 60f;
    }

    public class AnimationEventManager : MonoBehaviour
    {
        [Header("Target Camera")]
        [SerializeField] private Camera targetCamera;

        [Header("Camera Shot Presets")]
        [SerializeField] private List<CameraShotPreset> cameraShots = new List<CameraShotPreset>();

        private Vector3 _originCamPos;
        private Quaternion _originCamRot;
        private float _originCamFov = 60f;
        private bool _hasSavedOrigin;

        private Tween _moveTween;
        private Tween _rotTween;
        private Tween _fovTween;
        private Tween _shakeTween;

        public IReadOnlyList<CameraShotPreset> CameraShots => cameraShots;

        private Camera ResolveCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null && !_hasSavedOrigin)
            {
                _originCamPos = targetCamera.transform.position;
                _originCamRot = targetCamera.transform.rotation;
                _originCamFov = targetCamera.fieldOfView;
                _hasSavedOrigin = true;
            }
            return targetCamera;
        }

        private void Awake()
        {
            ResolveCamera();
        }

        private void OnDisable()
        {
            KillCameraTweens();
        }

        private void KillCameraTweens()
        {
            _moveTween?.Kill();
            _rotTween?.Kill();
            _fovTween?.Kill();
            _shakeTween?.Kill();
        }

        #region Camera Animation Events
        public void MoveCamera(string shotId)
        {
            CameraShotPreset shot = FindShot(shotId);
            if (shot == null)
            {
                Debug.LogWarning($"[AnimationEventManager] Camera shot '{shotId}' not found on {name}.");
                return;
            }
            ApplyShot(shot, forceInstant: false);
        }

        public void SnapCamera(string shotId)
        {
            CameraShotPreset shot = FindShot(shotId);
            if (shot == null)
            {
                Debug.LogWarning($"[AnimationEventManager] Camera shot '{shotId}' not found on {name}.");
                return;
            }
            ApplyShot(shot, forceInstant: true);
        }

        public void MoveCameraByIndex(int index)
        {
            if (cameraShots == null || index < 0 || index >= cameraShots.Count) return;
            ApplyShot(cameraShots[index], forceInstant: false);
        }

        public void SnapCameraByIndex(int index)
        {
            if (cameraShots == null || index < 0 || index >= cameraShots.Count) return;
            ApplyShot(cameraShots[index], forceInstant: true);
        }

        public void SetCameraFov(float fieldOfView)
        {
            Camera cam = ResolveCamera();
            if (cam == null) return;
            _fovTween?.Kill();
            _fovTween = cam.DOFieldOfView(fieldOfView, 0.4f).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        public void ShakeCamera(float strength)
        {
            Camera cam = ResolveCamera();
            if (cam == null) return;
            _shakeTween?.Kill(true);
            float s = strength > 0f ? strength : 0.25f;
            _shakeTween = cam.transform.DOShakePosition(0.3f, s, vibrato: 18, randomness: 90f, fadeOut: true).SetUpdate(true);
        }

        public void ResetCamera(float duration)
        {
            Camera cam = ResolveCamera();
            if (cam == null || !_hasSavedOrigin) return;

            KillCameraTweens();
            if (duration <= 0.001f)
            {
                cam.transform.SetPositionAndRotation(_originCamPos, _originCamRot);
                cam.fieldOfView = _originCamFov;
                return;
            }

            _moveTween = cam.transform.DOMove(_originCamPos, duration).SetEase(Ease.InOutCubic).SetUpdate(true);
            _rotTween = cam.transform.DORotateQuaternion(_originCamRot, duration).SetEase(Ease.InOutCubic).SetUpdate(true);
            _fovTween = cam.DOFieldOfView(_originCamFov, duration).SetEase(Ease.InOutCubic).SetUpdate(true);
        }

        public void ApplyShot(CameraShotPreset shot, bool forceInstant)
        {
            Camera cam = ResolveCamera();
            if (cam == null || shot == null) return;

            Vector3 worldPos = shot.useLocalSpace
                ? transform.TransformPoint(shot.position)
                : shot.position;
            Quaternion worldRot = shot.useLocalSpace
                ? transform.rotation * Quaternion.Euler(shot.eulerAngles)
                : Quaternion.Euler(shot.eulerAngles);

            KillCameraTweens();

            if (forceInstant || shot.duration <= 0.001f)
            {
                cam.transform.SetPositionAndRotation(worldPos, worldRot);
                if (shot.overrideFov) cam.fieldOfView = shot.fieldOfView;
                return;
            }

            _moveTween = cam.transform.DOMove(worldPos, shot.duration).SetEase(shot.ease).SetUpdate(true);
            _rotTween = cam.transform.DORotateQuaternion(worldRot, shot.duration).SetEase(shot.ease).SetUpdate(true);
            if (shot.overrideFov)
                _fovTween = cam.DOFieldOfView(shot.fieldOfView, shot.duration).SetEase(shot.ease).SetUpdate(true);
        }

        private CameraShotPreset FindShot(string shotId)
        {
            if (string.IsNullOrEmpty(shotId) || cameraShots == null) return null;
            for (int i = 0; i < cameraShots.Count; i++)
            {
                if (cameraShots[i] != null && string.Equals(cameraShots[i].shotId, shotId, StringComparison.OrdinalIgnoreCase))
                    return cameraShots[i];
            }
            return null;
        }
        #endregion

        #region Sound & Haptic Animation Events
        public void PlaySFX(AudioClip clip)
        {
            SoundManager.Instance?.PlaySFX(clip, 1f, 1f);
        }

        public void PlaySFXByName(string sfxTypeName)
        {
            if (Enum.TryParse(sfxTypeName, true, out SfxType type))
                SoundManager.Instance?.PlaySFX(type);
        }

        public void PlayBGM(AudioClip newBgm)
        {
            SoundManager.Instance?.PlayBGM(newBgm, 0.4f);
        }

        public void SwitchBGMImmediate(AudioClip newBgm)
        {
            SoundManager.Instance?.PlayBGMImmediate(newBgm, 0.4f);
        }

        public void StopBGM()
        {
            SoundManager.Instance?.StopBGM();
        }

        public void FadeOutBGM(float duration)
        {
            SoundManager.Instance?.FadeOutBGM(duration);
        }

        public void PauseBGM() => SoundManager.Instance?.PauseBGM();
        public void ResumeBGM() => SoundManager.Instance?.ResumeBGM();

        public void TriggerHaptic()
        {
            CoinFlyEffect.TriggerLightHaptic();
        }
        #endregion

#if UNITY_EDITOR
        public void CaptureCameraAsNewShot(Camera cam, bool useLocalSpace)
        {
            if (cam == null) return;
            Undo.RecordObject(this, "Capture Camera Shot");
            var shot = new CameraShotPreset
            {
                shotId = $"shot_{cameraShots.Count + 1}",
                useLocalSpace = useLocalSpace,
                position = useLocalSpace ? transform.InverseTransformPoint(cam.transform.position) : cam.transform.position,
                eulerAngles = useLocalSpace
                    ? (Quaternion.Inverse(transform.rotation) * cam.transform.rotation).eulerAngles
                    : cam.transform.eulerAngles,
                duration = 1.0f,
                ease = Ease.InOutCubic,
                overrideFov = true,
                fieldOfView = cam.fieldOfView
            };
            cameraShots.Add(shot);
            EditorUtility.SetDirty(this);
        }

        public void OverwriteShotFromCamera(int index, Camera cam)
        {
            if (cam == null || index < 0 || index >= cameraShots.Count) return;
            Undo.RecordObject(this, "Overwrite Camera Shot");
            var shot = cameraShots[index];
            shot.position = shot.useLocalSpace ? transform.InverseTransformPoint(cam.transform.position) : cam.transform.position;
            shot.eulerAngles = shot.useLocalSpace
                ? (Quaternion.Inverse(transform.rotation) * cam.transform.rotation).eulerAngles
                : cam.transform.eulerAngles;
            shot.fieldOfView = cam.fieldOfView;
            EditorUtility.SetDirty(this);
        }

        public void PreviewShotInEditor(int index)
        {
            if (index < 0 || index >= cameraShots.Count) return;
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            var shot = cameraShots[index];
            Undo.RecordObject(cam.transform, "Preview Camera Shot");
            Vector3 worldPos = shot.useLocalSpace ? transform.TransformPoint(shot.position) : shot.position;
            Quaternion worldRot = shot.useLocalSpace
                ? transform.rotation * Quaternion.Euler(shot.eulerAngles)
                : Quaternion.Euler(shot.eulerAngles);
            cam.transform.SetPositionAndRotation(worldPos, worldRot);
            if (shot.overrideFov) cam.fieldOfView = shot.fieldOfView;
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(AnimationEventManager))]
    public class AnimationEventManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            AnimationEventManager mgr = (AnimationEventManager)target;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Camera Shot Capture & Preview", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Scene View (Local)"))
            {
                if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                    mgr.CaptureCameraAsNewShot(SceneView.lastActiveSceneView.camera, useLocalSpace: true);
            }
            if (GUILayout.Button("Capture Main Camera (Local)"))
            {
                if (Camera.main != null)
                    mgr.CaptureCameraAsNewShot(Camera.main, useLocalSpace: true);
            }
            EditorGUILayout.EndHorizontal();

            if (mgr.CameraShots != null && mgr.CameraShots.Count > 0)
            {
                EditorGUILayout.Space(4);
                for (int i = 0; i < mgr.CameraShots.Count; i++)
                {
                    var shot = mgr.CameraShots[i];
                    if (shot == null) continue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"[{i}] {shot.shotId}", GUILayout.Width(140));
                    if (GUILayout.Button("Preview (MainCam)"))
                    {
                        mgr.PreviewShotInEditor(i);
                    }
                    if (GUILayout.Button("Overwrite from Scene View"))
                    {
                        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                            mgr.OverwriteShotFromCamera(i, SceneView.lastActiveSceneView.camera);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
#endif
}
