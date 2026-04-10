
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

namespace GamePlay.UI
{
    public static class GameObjectExtension
    {
        public static void SetActiveFast(this GameObject o, bool s)
        {
            if (o.activeSelf != s)
            {
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
}