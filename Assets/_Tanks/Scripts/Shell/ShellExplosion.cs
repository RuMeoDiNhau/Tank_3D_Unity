using UnityEngine;
using Unity.Netcode;

namespace Tanks.Complete
{
    // Bắt buộc phải kế thừa NetworkBehaviour để xài được ClientRpc
    public class ShellExplosion : NetworkBehaviour
    {
        public LayerMask m_TankMask;                        
        public ParticleSystem m_ExplosionParticles;         
        public AudioSource m_ExplosionAudio;                
        [HideInInspector] public float m_MaxLifeTime = 2f;  
        [HideInInspector] public float m_MaxDamage = 100f;                    
        [HideInInspector] public float m_ExplosionForce = 50f;                
        [HideInInspector] public float m_ExplosionRadius = 5f;                

        private void Awake()
        {
            if (m_ExplosionAudio == null)
            {
                if (m_ExplosionParticles != null)
                    m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource>();

                if (m_ExplosionAudio == null)
                    m_ExplosionAudio = GetComponent<AudioSource>();
            }
        }

        private void Start ()
        {
            // Thay vì dùng lệnh Destroy() ngay lập tức (gây lỗi mạng), ta hẹn giờ để dọn dẹp
            Invoke(nameof(DestroyShellTimeout), m_MaxLifeTime);
        }

        // Hàm dọn dẹp đạn nếu bay quá lâu mà không trúng ai
        private void DestroyShellTimeout()
        {
            bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isOnline)
            {
                // Đang Online: Chỉ Server mới được quyền xóa
                if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
            }
            else
            {
                // Đang Offline: Xóa bình thường
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter (Collider other)
        {
            // Kiểm tra xem mạng có đang bật không
            bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            // Nếu đang chơi Online và máy này KHÔNG PHẢI là Server -> Không cho phép tính sát thương
            if (isOnline && !IsServer)
                return;

            // --- ĐOẠN NÀY DÀNH CHO CẢ OFFLINE VÀ SERVER (Tính sát thương) ---
            Collider[] colliders = Physics.OverlapSphere (transform.position, m_ExplosionRadius, m_TankMask);
            for (int i = 0; i < colliders.Length; i++)
            {
                Rigidbody targetRigidbody = colliders[i].GetComponent<Rigidbody> ();
                if (!targetRigidbody)
                    continue;

                TankMovement targetMovement = targetRigidbody.GetComponent<TankMovement>();
                if (targetMovement != null)
                    targetMovement.AddExplosionForce(m_ExplosionForce, transform.position, m_ExplosionRadius);

                TankHealth targetHealth = targetRigidbody.GetComponent<TankHealth> ();
                if (!targetHealth)
                    continue;

                float damage = CalculateDamage (targetRigidbody.position);
                targetHealth.TakeDamage (damage);
            }

            // --- XỬ LÝ HIỆU ỨNG NỔ ---
            // 1. Chạy hiệu ứng ngay trên máy hiện tại (Máy Offline hoặc máy Server)
            PlayExplosionEffects();

            // 2. Nếu đang Online, báo cho các máy Client khác cũng nổ theo
            if (isOnline)
            {
                PlayExplosionClientRpc();
                
                // Hẹn giờ dọn rác chuẩn Netcode
                Invoke(nameof(DespawnShellNetwork), m_ExplosionParticles.main.duration);
            }
            else
            {
                // Hẹn giờ dọn rác chuẩn Offline
                Destroy(gameObject, m_ExplosionParticles.main.duration);
            }
        }

        // Hàm tàng hình vỏ đạn và bật tia lửa
        private void PlayExplosionEffects()
        {
            m_ExplosionParticles.Play();

            if (m_ExplosionAudio != null)
                m_ExplosionAudio.Play();

            // Giấu vỏ đạn đi thay vì tách ra (tránh lỗi Re-parenting)
            MeshRenderer mesh = GetComponentInChildren<MeshRenderer>();
            if (mesh != null) mesh.enabled = false;

            // Tắt va chạm để không trừ máu 2 lần
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Dừng hẳn viên đạn lại giữa không trung
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        // Loa phóng thanh gọi tất cả các máy trạm (Client) cùng chạy hiệu ứng
        [ClientRpc]
        private void PlayExplosionClientRpc()
        {
            // Tránh việc Server bị chạy hiệu ứng 2 lần
            if (IsServer) return; 

            PlayExplosionEffects();
        }

        // Lệnh dọn dẹp dành riêng cho Multiplayer
        private void DespawnShellNetwork()
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private float CalculateDamage (Vector3 targetPosition)
        {
            Vector3 explosionToTarget = targetPosition - transform.position;
            float explosionDistance = explosionToTarget.magnitude;
            float relativeDistance = (m_ExplosionRadius - explosionDistance) / m_ExplosionRadius;
            float damage = relativeDistance * m_MaxDamage;
            return Mathf.Max (0f, damage);
        }
    }
}