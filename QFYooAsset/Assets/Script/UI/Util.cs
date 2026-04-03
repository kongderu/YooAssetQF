
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Utils {
    public static class GameObjectExtension {
        public static void SetActiveFast(this GameObject o, bool s) {
            if (o.activeSelf != s) {
                o.SetActive(s);
            }
        }
    }
    public enum SceneID
    {
        Login = 0,
        Index = 1,
        Loading = 2,
        Game = 3
    }

    public static class Util {


        public static string basePageUrl = "Assets/Samples/YooAsset/2.3.18/Space Shooter/GameRes/UIPanel/";
        public static string pageSuffix = ".prefab";
        public static string GetAvatarUrl(int avatarId) {
            return "Assets/AB/Avatar/" + avatarId + ".png";
        }

        public static string GetEquipUrl(string equipRid) {
            return "Assets/AB/Equip/Items/" + equipRid + ".prefab";
        }
    }
}