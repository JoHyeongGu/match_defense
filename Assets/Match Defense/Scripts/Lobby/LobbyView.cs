using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MatchDefense.Lobby
{
    public class LobbyView : MonoBehaviour
    {
        [Serializable]
        public class StageData
        {
            public int index;
            public string name;
        }
        [SerializeField] private List<StageData> stageData = new();

        private PanelRenderer panelRenderer;

        private void OnEnable()
        {
            panelRenderer = GetComponent<PanelRenderer>();
            panelRenderer?.RegisterUIReloadCallback(OnReloadedCallback);
        }
        private void OnDisable()
        {
            panelRenderer?.UnregisterUIReloadCallback(OnReloadedCallback);
        }

        private void OnReloadedCallback(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            stageData.Reverse();
            ListView listView = root.Q("StageListView") as ListView;
            listView.itemsSource = stageData;
            listView.bindItem = (ele, index) =>
            {
                Button button = ele.Q("Button") as Button;
                button.text = stageData[index].name;
            };
        }
    }
}
