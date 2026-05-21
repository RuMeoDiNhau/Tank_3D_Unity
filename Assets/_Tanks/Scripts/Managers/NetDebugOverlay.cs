using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    public class NetDebugOverlay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_Text;

        private void Update()
        {
            if (m_Text == null)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                m_Text.text = "Net: no NetworkManager";
                return;
            }

            m_Text.text =
                $"Net: running={(nm.IsListening ? "yes" : "no")}  host={nm.IsHost}  server={nm.IsServer}  client={nm.IsClient}\n" +
                $"LocalClientId={nm.LocalClientId}  ConnectedClients={nm.ConnectedClientsList.Count}";
        }
    }
}

