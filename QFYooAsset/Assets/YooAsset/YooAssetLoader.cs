using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace YooAsset
{
    public class YooAssetLoader : MonoBehaviour
    {
        private static YooAssetLoader _instance;

        public static YooAssetLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("YooAssetLoader");
                    _instance = obj.AddComponent<YooAssetLoader>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        private void Awake()
        {
            LoadAssetSync<Object>("");
        }

        public static T LoadAssetSync<T>(string path) where T : Object
        {
            // var package = YooAssets.;
            // if (package == null)
            //     return null;

            if (string.IsNullOrEmpty(path))
                return null;

            var handle = YooAssets.LoadAssetSync(path);
            var obj = handle.AssetObject;
            if (obj != null)
                return obj as T;

            return null;
        }

        public static GameObject InstantiateSync(string path, Transform parent = null)
        {
            var obj = LoadAssetSync<GameObject>(path);
            if (obj == null)
                return null;

            if (parent)
            {
                return Instantiate(obj, parent);
            }

            return Instantiate(obj);
        }

        #region 启动协程

        public void StartCoroutineSafe(IEnumerator routine)
        {
            StartCoroutine(routine);
        }

        public void StopCoroutineSafe(IEnumerator routine)
        {
            StopCoroutine(routine);
        }

        #endregion
    }
}