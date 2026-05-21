using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    public class OnlineSpawnPoints : MonoBehaviour
    {
        [Tooltip("Spawn points used when running in online mode (Host/Client).")]
        [SerializeField] private Transform[] m_SpawnPoints;

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            if (m_SpawnPoints == null || m_SpawnPoints.Length == 0)
                return;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return;

            var player = client.PlayerObject;
            if (player == null)
                return;

            int index = (int)(clientId % (ulong)m_SpawnPoints.Length);
            var spawn = m_SpawnPoints[index];
            if (spawn == null)
                return;

            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
    }
}

