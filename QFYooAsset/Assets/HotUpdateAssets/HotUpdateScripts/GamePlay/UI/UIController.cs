using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using System.Linq;
using System;
using Cysharp.Threading.Tasks;
using YooAsset;
namespace GamePlay.UI
{
    public enum UIPageType
    {
        HomepageUI = 0,
        GameMainUI,
        WarningAlert,
        LoadingUI,
        UIHome,
        UIBattle
    }

    public enum UILevelType
    {
        Prepare = 0,
        Main,
        UIPage,
        Popup,
        Alart,
        Debug
    }

    public class UIController : PersistentMonoSingleton<UIController>, IController
    {
        public Transform[] levles;
        private Dictionary<UIPageType, GameObject> pagesDict = new Dictionary<UIPageType, GameObject>();

        private Dictionary<UILevelType, LinkedList<UIPageType>> pagesGroup =
            new Dictionary<UILevelType, LinkedList<UIPageType>>();

        private void Awake()
        {
            levles = new Transform[Enum.GetValues(typeof(UILevelType)).Length];
            levles[0] = GameObject.Find("Prepare").transform;
            levles[1] = GameObject.Find("Main").transform;
            levles[2] = GameObject.Find("UIPage").transform;
            levles[3] = GameObject.Find("Popup").transform;
            levles[4] = GameObject.Find("Alart").transform;
            levles[5] = GameObject.Find("Debug").transform;
            base.Awake();

        }

        public async UniTask InitUI()
        {

            foreach (UILevelType value in Enum.GetValues(typeof(UILevelType)))
            {
                pagesGroup[value] = new LinkedList<UIPageType>();
            }

        }

        public void HidePage(UIPageType pageType)
        {
            if (!pagesDict.ContainsKey(pageType))
            {
                Debug.Log("Not Exist Page " + pageType);
                return;
            }

            pagesDict[pageType].SetActiveFast(false);
        }

        public void ShowPage(ShowPageInfo info)
        {
            ShowPageAsync(info).Forget();
        }

        public async UniTask<bool> ShowPageAsync(ShowPageInfo info)
        {
            if (info.closeOther)
            {
                foreach (var kv in pagesDict)
                {
                    kv.Value.SetActiveFast(false);
                }
            }
            if (pagesDict.ContainsKey(info.pageType) && pagesGroup[info.levelType].Contains(info.pageType))
            {

                pagesDict[info.pageType].SetActiveFast(true);
                SetPageInfo(info);
            }
            else if (pagesDict.ContainsKey(info.pageType) && !pagesGroup[info.levelType].Contains(info.pageType))
            {

                pagesDict[info.pageType].transform.SetParent(levles[(int)info.levelType], false);
                pagesDict[info.pageType].SetActiveFast(true);
                pagesGroup[GetGroupByPageType(info.pageType)].Remove(info.pageType);
                pagesGroup[info.levelType].AddLast(info.pageType);
                SetPageInfo(info);
            }
            else
            {
                //string pageUrl = GetPageUrlByType(info.pageType);
                var obj = await this.GetSystem<IAddressableSystem>().LoadAssetAsync<GameObject>(info.pageType.ToString());
                if (obj.Status == EOperationStatus.Succeed)
                {
                    GameObject page = Instantiate(obj.Result);
                    page.transform.SetParent(levles[(int)info.levelType], false);
                    pagesDict[info.pageType] = page;
                    pagesGroup[info.levelType].AddLast(info.pageType);
                    SetPageInfo(info);
                    return true;
                }
                else
                {
                    Debug.LogError($"Load {info.pageType} Page Failed");
                    return false;
                }
            }

            return true;
        }

        private void SetPageInfo(ShowPageInfo e)
        {
            UIPenal penal = pagesDict[e.pageType].GetComponent<UIPenal>();
            if (e.data != null && penal != null)
            {
                penal.InitData(e.data);
            }

            pagesDict[e.pageType].transform.SetAsLastSibling();
        }

        private UILevelType GetGroupByPageType(UIPageType type)
        {
            foreach (var kv in pagesGroup)
            {
                if (kv.Value.Contains(type))
                {
                    return kv.Key;
                }
            }

            return UILevelType.Main;
        }

        public void HidePageByLevel(UILevelType levelType)
        {
            foreach (var kv in pagesGroup[levelType])
            {
                if (pagesDict.ContainsKey(kv))
                {
                    pagesDict[kv].SetActiveFast(false);
                }
            }
        }
        public void DestoryPageByLevel(UILevelType levelType)
        {
            foreach (var kv in pagesGroup[levelType])
            {
                if (pagesDict.ContainsKey(kv))
                {
                    Destroy(pagesDict[kv]);
                    pagesDict.Remove(kv);
                    pagesGroup[levelType].Remove(kv);
                }
            }
        }

        // 获取对应页面
        private GameObject GetPage(UIPageType type)
        {
            if (pagesDict.ContainsKey(type))
            {
                return pagesDict[type];
            }

            return null;
        }

        //销毁对应页面
        private void DestoryPage(UIPageType type)
        {
            if (!pagesDict.ContainsKey(type))
            {
                Debug.LogError("not exist page");
                return;
            }

            Destroy(pagesDict[type]);
            pagesDict.Remove(type);
        }

        private void OnDestroy()
        {
            foreach (var kv in pagesDict)
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value);
                }
            }

            pagesDict.Clear();
        }

        public IArchitecture GetArchitecture()
        {
            return GameSystemEventRegister.Interface;
        }
    }
}