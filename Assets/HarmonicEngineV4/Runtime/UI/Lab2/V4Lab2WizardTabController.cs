using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Top tab bar for the Lab2 setup wizard categories.</summary>
    public sealed class V4Lab2WizardTabController : MonoBehaviour
    {
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] tabPages;
        [SerializeField] private int defaultTabIndex;

        private int _activeTab = -1;

        public int ActiveTab => _activeTab;

        private void Awake()
        {
            AutoDiscoverIfNeeded();
            if (tabButtons == null || tabPages == null || tabPages.Length == 0)
            {
                return;
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.AddListener(() => SelectTab(index));
                }
            }

            SelectTab(Mathf.Clamp(defaultTabIndex, 0, Mathf.Max(0, tabPages.Length - 1)));
        }

        public void Bind(Button[] buttons, GameObject[] pages, int defaultIndex = 0)
        {
            tabButtons = buttons;
            tabPages = pages;
            defaultTabIndex = defaultIndex;
            _activeTab = -1;
            SelectTab(defaultTabIndex);
        }

        public void SelectTab(int index)
        {
            AutoDiscoverIfNeeded();
            if (tabPages == null || tabPages.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, tabPages.Length - 1);
            _activeTab = index;

            for (int i = 0; i < tabPages.Length; i++)
            {
                if (tabPages[i] != null)
                {
                    tabPages[i].SetActive(i == index);
                }
            }

            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    if (tabButtons[i] != null)
                    {
                        V4Lab2UITheme.ApplyTabButton(tabButtons[i], i == index);
                    }
                }
            }
        }

        private void AutoDiscoverIfNeeded()
        {
            if (tabPages != null && tabPages.Length > 0 && tabButtons != null && tabButtons.Length > 0)
            {
                return;
            }

            Transform bar = transform.Find("TabBar");
            Transform content = transform.parent != null ? transform.parent.Find("TabContentArea") : null;
            if (bar == null || content == null)
            {
                return;
            }

            if (tabButtons == null || tabButtons.Length == 0)
            {
                tabButtons = bar.GetComponentsInChildren<Button>(true);
            }

            if (tabPages == null || tabPages.Length == 0)
            {
                tabPages = new GameObject[content.childCount];
                for (int i = 0; i < content.childCount; i++)
                {
                    tabPages[i] = content.GetChild(i).gameObject;
                }
            }
        }
    }
}
