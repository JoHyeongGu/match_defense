using UnityEngine;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using DG.Tweening;

namespace MatchDefense.Match
{
    public class MatchItem : MonoBehaviour
    {
        public int ItemType { get; private set; }
        public Vector3 BoardPos { get; set; }

        [Header("<color=yellow>Shape</color>")]
        [SerializeField] private MeshFilter[] models;
        [SerializeField] private MeshFilter model;

        [Header("<color=yellow>To Sphere</color>")]
        [SerializeField] private float mashDuration = 1f;
        [SerializeField] private float sphereRadius = 1f;
        [SerializeField, Range(0.1f, 1.0f)] private float flattenFactor = 0.6f;
        [SerializeField] private AnimationCurve mashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #region Unity
        private void Awake()
        {
            meshDatas = new MeshData[models.Length];
            for (int i = 0; i < models.Length; i++)
            {
                meshDatas[i] = new MeshData();
                PrewarmMeshData(i);
            }
        }

        private void OnDestroy()
        {
            if (meshDatas == null) return;
            foreach (var data in meshDatas)
            {
                if (data != null && data.dynamicMesh != null)
                {
                    Destroy(data.dynamicMesh);
                }
            }
        }
        #endregion

        #region Match Item
        public void ChangeMesh(int index)
        {
            if (index < 0 || index >= models.Length) return;
            if (ItemType == index && currentData != null) return;

            ItemType = index;
            for (int i = 0; i < models.Length; i++)
            {
                models[i].gameObject.SetActive(i == index);
            }
            model = models[index];
            currentData = meshDatas[index];
        }
        #endregion

        #region Interaction
        public void Hover(bool isHover)
        {
            transform.DOKill();
            if (isHover)
            {
                transform.DOMove(BoardPos + Vector3.up * 0.3f, 0.1f);
                transform.DOScale(Vector3.one * 1.15f, 0.1f);
            }
            else
            {
                transform.DOMove(BoardPos, 0.1f);
                transform.DOScale(Vector3.one, 0.1f);
            }
        }

        public void SwapTo(Vector3 targetPos, float duration, Action onComplete = null)
        {
            BoardPos = targetPos;
            transform.DOMove(targetPos, duration).SetEase(Ease.OutQuad).OnComplete(() => onComplete?.Invoke());
        }
        #endregion

        #region Effects
        public void MoveAndMerge(Vector3 targetPos, float duration, Action onComplete)
        {
            transform.DOMove(targetPos, duration).SetEase(Ease.InQuad).OnComplete(() => onComplete?.Invoke());
        }

        public void PlayMatchEffect(Action onComplete)
        {
            transform.DOKill();
            StopAllCoroutines();
            isMashing = false;

            Vector3 originalScale = transform.localScale;
            Sequence seq = DOTween.Sequence();

            seq.AppendCallback(() => StartCoroutine(SphereRoutine()));
            seq.Append(transform.DOScale(Vector3.zero, mashDuration).SetEase(Ease.InBack));

            seq.OnComplete(() =>
            {
                ResetMesh();
                transform.localScale = originalScale;
                onComplete?.Invoke();
            });
        }

        public void ResetMesh()
        {
            if (currentData != null && currentData.isInitialized)
            {
                currentData.dynamicMesh.vertices = currentData.origVerts;
                currentData.dynamicMesh.normals = currentData.origNorms;
                currentData.dynamicMesh.RecalculateBounds();
            }
        }
        #endregion

        #region Sphere Mesh
        private class MeshData
        {
            public Mesh dynamicMesh;
            public Vector3[] origVerts;
            public Vector3[] origNorms;
            public Vector3[] targetVerts;
            public Vector3[] targetNorms;
            public Vector3[] curVerts;
            public Vector3[] curNorms;
            public bool isInitialized;
        }

        private MeshData[] meshDatas;
        private MeshData currentData;
        private bool isMashing;

        private void PrewarmMeshData(int index)
        {
            MeshData data = meshDatas[index];
            if (data.isInitialized) return;

            MeshFilter mf = models[index];
            data.dynamicMesh = Instantiate(mf.sharedMesh);
            data.dynamicMesh.MarkDynamic();
            mf.mesh = data.dynamicMesh;

            data.origVerts = data.dynamicMesh.vertices;
            data.origNorms = data.dynamicMesh.normals;

            int vertexCount = data.origVerts.Length;
            data.targetVerts = new Vector3[vertexCount];
            data.targetNorms = new Vector3[vertexCount];
            data.curVerts = new Vector3[vertexCount];
            data.curNorms = new Vector3[vertexCount];

            Vector3 center = data.dynamicMesh.bounds.center;
            float cy = center.y;

            Vector3 scaleRatio = new Vector3(
                transform.lossyScale.x / mf.transform.lossyScale.x,
                transform.lossyScale.y / mf.transform.lossyScale.y,
                transform.lossyScale.z / mf.transform.lossyScale.z
            );

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 origPos = data.origVerts[i];
                float dx = origPos.x - center.x;
                float dy = origPos.y - cy;
                float dz = origPos.z - center.z;

                float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                Vector3 dir = dist > 0.0001f ? new Vector3(dx / dist, dy / dist, dz / dist) : Vector3.up;

                data.targetVerts[i].x = center.x + dir.x * sphereRadius * scaleRatio.x;
                data.targetVerts[i].y = cy + (dir.y * sphereRadius * flattenFactor) * scaleRatio.y;
                data.targetVerts[i].z = center.z + dir.z * sphereRadius * scaleRatio.z;

                Vector3 targetNorm = new Vector3(dir.x, dir.y * flattenFactor, dir.z);
                float normDist = Mathf.Sqrt(targetNorm.x * targetNorm.x + targetNorm.y * targetNorm.y + targetNorm.z * targetNorm.z);

                data.targetNorms[i].x = normDist > 0.0001f ? targetNorm.x / normDist : 0f;
                data.targetNorms[i].y = normDist > 0.0001f ? targetNorm.y / normDist : 1f;
                data.targetNorms[i].z = normDist > 0.0001f ? targetNorm.z / normDist : 0f;
            }

            data.isInitialized = true;
        }

        public async void ToSphere()
        {
            if (isMashing) return;

            await Task.Delay(1000);
            StartCoroutine(SphereRoutine());
        }

        private IEnumerator SphereRoutine()
        {
            isMashing = true;

            float invDuration = 1f / mashDuration;
            float elapsed = 0f;
            int length = currentData.origVerts.Length;

            Vector3[] oV = currentData.origVerts;
            Vector3[] tV = currentData.targetVerts;
            Vector3[] cV = currentData.curVerts;
            Vector3[] oN = currentData.origNorms;
            Vector3[] tN = currentData.targetNorms;
            Vector3[] cN = currentData.curNorms;

            while (elapsed < mashDuration)
            {
                elapsed += Time.deltaTime;
                float t = mashCurve.Evaluate(elapsed * invDuration);

                for (int i = 0; i < length; i++)
                {
                    cV[i].x = oV[i].x + (tV[i].x - oV[i].x) * t;
                    cV[i].y = oV[i].y + (tV[i].y - oV[i].y) * t;
                    cV[i].z = oV[i].z + (tV[i].z - oV[i].z) * t;

                    cN[i].x = oN[i].x + (tN[i].x - oN[i].x) * t;
                    cN[i].y = oN[i].y + (tN[i].y - oN[i].y) * t;
                    cN[i].z = oN[i].z + (tN[i].z - oN[i].z) * t;
                }

                currentData.dynamicMesh.vertices = cV;
                currentData.dynamicMesh.normals = cN;

                yield return null;
            }

            currentData.dynamicMesh.vertices = tV;
            currentData.dynamicMesh.normals = tN;
            currentData.dynamicMesh.RecalculateBounds();

            isMashing = false;
        }
        #endregion
    }
}