using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Tanks.Complete
{
    public class NetworkUI : MonoBehaviour
    {
        [Header("Optional")]
        [SerializeField] private TMP_InputField m_AddressInput;
        [SerializeField] private ushort m_Port = 7777;
        [SerializeField] private GameObject m_RootToHide;
        [SerializeField] private GameUIHandler m_GameUIHandler;
        [SerializeField] private GameObject m_ModeSelectRootToHide;
        [SerializeField] private CameraControl m_CameraControl;

        private static NetworkManager Manager => NetworkManager.Singleton;
        private bool m_Subscribed;

        private void OnDisable()
        {
            UnsubscribeCallbacks();
        }

        private void OnDestroy()
        {
            UnsubscribeCallbacks();
        }

        public void StartHost()
        {
            if (Manager == null)
            {
                Debug.LogError("[NetworkUI] No NetworkManager.Singleton in scene.");
                return;
            }

            ApplyConnectionData();
            if (Manager.StartHost())
            {
                Debug.Log($"[NetworkUI] Host started. Address={GetAddressForLog()} Port={m_Port}");
                HideUI();
                SubscribeCallbacks();
                StartCoroutine(AttachCameraToLocalPlayer());
            }
            else
            {
                Debug.LogError("[NetworkUI] StartHost failed.");
            }
        }

        public void StartClient()
        {
            if (Manager == null)
            {
                Debug.LogError("[NetworkUI] No NetworkManager.Singleton in scene.");
                return;
            }

            ApplyConnectionData();
            if (Manager.StartClient())
            {
                Debug.Log($"[NetworkUI] Client started. Address={GetAddressForLog()} Port={m_Port}");
                HideUI();
                SubscribeCallbacks();
                StartCoroutine(AttachCameraToLocalPlayer());
            }
            else
            {
                Debug.LogError("[NetworkUI] StartClient failed.");
            }
        }

        public void StartServer()
        {
            if (Manager == null)
            {
                Debug.LogError("[NetworkUI] No NetworkManager.Singleton in scene.");
                return;
            }

            ApplyConnectionData();
            if (Manager.StartServer())
            {
                Debug.Log($"[NetworkUI] Server started. Address={GetAddressForLog()} Port={m_Port}");
                HideUI();
                SubscribeCallbacks();
            }
            else
            {
                Debug.LogError("[NetworkUI] StartServer failed.");
            }
        }

        private void ApplyConnectionData()
        {
            var transport = Manager != null ? Manager.GetComponent<UnityTransport>() : null;
            if (transport == null)
                return;

            string address = transport.ConnectionData.Address;
            if (m_AddressInput != null && !string.IsNullOrWhiteSpace(m_AddressInput.text))
                address = m_AddressInput.text.Trim();

            transport.SetConnectionData(address, m_Port);
        }

        private string GetAddressForLog()
        {
            var transport = Manager != null ? Manager.GetComponent<UnityTransport>() : null;
            if (transport == null)
                return "(no transport)";
            return transport.ConnectionData.Address;
        }

        private void HideUI()
        {
            if (m_GameUIHandler == null)
                m_GameUIHandler = FindAnyObjectByType<GameUIHandler>(FindObjectsInactive.Include);

            if (m_GameUIHandler != null)
            {
                m_GameUIHandler.CleanupMenuPreviews();
                m_GameUIHandler.SetStartMenuVisible(false);
            }
            else
            {
                CleanupMenuPreviewsFallback();
            }

            // Extra safety: even if the wrong GameUIHandler was assigned (or StartMenuRoot wasn't wired),
            // disable the common offline menu roots so they don't keep rendering preview cameras.
            HideOfflineMenuFallback();

            if (m_ModeSelectRootToHide != null)
                m_ModeSelectRootToHide.SetActive(false);

            if (m_RootToHide != null)
                m_RootToHide.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private static void CleanupMenuPreviewsFallback()
        {
            var slots = FindObjectsByType<StartMenuSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var slot in slots)
            {
                if (slot == null)
                    continue;

                if (slot.TankPreview != null)
                {
                    Destroy(slot.TankPreview);
                    slot.TankPreview = null;
                }
            }
        }

        private static void HideOfflineMenuFallback()
        {
            // These are the usual names in this project.
            var startMenu = GameObject.Find("StartMenu");
            if (startMenu != null)
                startMenu.SetActive(false);

            // Disable any cameras that might live under the menu hierarchy.
            var menuCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in menuCameras)
            {
                if (cam == null)
                    continue;

                // Only disable cameras that belong to the UI/menu layer objects, never the main camera rig.
                // (UI layer might be 5 by default but projects vary, so use name-based heuristics here.)
                var goName = cam.gameObject.name;
                if (goName.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    goName.IndexOf("Start", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cam.enabled = false;
                }
            }
        }

        public void ShowUI()
        {
            if (m_RootToHide != null)
                m_RootToHide.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void HideUIManual()
        {
            if (m_RootToHide != null)
                m_RootToHide.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void SubscribeCallbacks()
        {
            if (m_Subscribed || Manager == null)
                return;

            Manager.OnClientConnectedCallback += OnClientConnected;
            Manager.OnClientDisconnectCallback += OnClientDisconnected;
            m_Subscribed = true;
        }

        private void UnsubscribeCallbacks()
        {
            if (!m_Subscribed || Manager == null)
                return;

            Manager.OnClientConnectedCallback -= OnClientConnected;
            Manager.OnClientDisconnectCallback -= OnClientDisconnected;
            m_Subscribed = false;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (Manager == null)
                return;

            Debug.Log($"[NetworkUI] ClientConnected: {clientId}. LocalClientId={Manager.LocalClientId} IsServer={Manager.IsServer} IsHost={Manager.IsHost} IsClient={Manager.IsClient}");

            if (Manager.IsServer)
            {
                var hasClient = Manager.ConnectedClients != null && Manager.ConnectedClients.ContainsKey(clientId);
                var playerObj = hasClient ? Manager.ConnectedClients[clientId].PlayerObject : null;
                Debug.Log($"[NetworkUI] Server view: ConnectedClients={Manager.ConnectedClientsList?.Count ?? 0} HasClient={hasClient} PlayerObject={(playerObj != null ? playerObj.name : "(null)")}");
            }

            // When the local client connects (host or client), attach the camera to the local PlayerObject.
            if (clientId == Manager.LocalClientId)
                StartCoroutine(AttachCameraToLocalPlayer());
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (Manager == null)
                return;

            Debug.LogWarning($"[NetworkUI] ClientDisconnected: {clientId}. LocalClientId={Manager.LocalClientId}");
        }

        private System.Collections.IEnumerator AttachCameraToLocalPlayer()
        {
            if (m_CameraControl == null)
                m_CameraControl = FindAnyObjectByType<CameraControl>();

            if (m_CameraControl == null || Manager == null)
                yield break;

            float timeout = 5f;
            while (timeout > 0f)
            {
                var localClient = Manager.LocalClient;
                var playerObject = localClient != null ? localClient.PlayerObject : null;
                if (playerObject != null)
                {
                    m_CameraControl.m_Targets = new[] { playerObject.transform };
                    m_CameraControl.SetStartPositionAndSize();
                    yield break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            var playerPrefabName = Manager.NetworkConfig != null && Manager.NetworkConfig.PlayerPrefab != null
                ? Manager.NetworkConfig.PlayerPrefab.name
                : "(null)";
            Debug.LogWarning(
                $"[NetworkUI] Timed out waiting for local PlayerObject. Camera targets not set. " +
                $"Check NetworkManager 'Default Player Prefab' and Network Prefabs List. PlayerPrefab={playerPrefabName}");
        }
    }
}
