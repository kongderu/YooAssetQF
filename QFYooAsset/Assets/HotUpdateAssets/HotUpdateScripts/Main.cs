using UnityEngine;
using GamePlay.UI;
using System.Threading.Tasks; 

namespace GamePlay
{
    public static class Main
    {
        public static async Task Start()
        {
            await UIController.Instance.InitUI();
            UIController.Instance.ShowPage(new ShowPageInfo(UIPageType.UIHome, UILevelType.Main));
        }
    }
}