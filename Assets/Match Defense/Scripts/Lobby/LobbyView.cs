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

        private void OnEnable() =>
            GetComponent<PanelRenderer>()?.RegisterUIReloadCallback(OnReloadedCallback);
        private void OnDisable() =>
            GetComponent<PanelRenderer>()?.UnregisterUIReloadCallback(OnReloadedCallback);

        private void OnReloadedCallback(PanelRenderer panelRenderer, VisualElement root, int version)
        {
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
