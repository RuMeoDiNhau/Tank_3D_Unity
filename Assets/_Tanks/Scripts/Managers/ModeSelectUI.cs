using UnityEngine;
using Unity.Netcode;

namespace Tanks.Complete
{
    public class ModeSelectUI : MonoBehaviour
    {
        [Header("UI Roots")]
        [SerializeField] private GameObject m_ModeSelectRoot;

        [Header("Offline")]
        [SerializeField] private GameUIHandler m_GameUIHandler;

        [Header("Online")]
        [SerializeField] private NetworkUI m_NetworkUI;

        public void ChooseOffline()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (m_ModeSelectRoot != null)
                m_ModeSelectRoot.SetActive(false);

            if (m_NetworkUI != null)
                m_NetworkUI.HideUIManual();

            if (m_GameUIHandler != null)
                m_GameUIHandler.SetStartMenuVisible(true);
        }

        public void ChooseOnline()
        {
            if (m_ModeSelectRoot != null)
                m_ModeSelectRoot.SetActive(false);

            if (m_GameUIHandler != null)
                m_GameUIHandler.SetStartMenuVisible(false);

            if (m_NetworkUI != null)
                m_NetworkUI.ShowUI();
        }

        public void BackToModeSelect()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (m_ModeSelectRoot != null)
                m_ModeSelectRoot.SetActive(true);

            if (m_GameUIHandler != null)
                m_GameUIHandler.SetStartMenuVisible(false);

            if (m_NetworkUI != null)
                m_NetworkUI.HideUIManual();
        }
    }
}
