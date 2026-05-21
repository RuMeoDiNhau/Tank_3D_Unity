using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Tanks.Complete
{
    public class TankHealth : NetworkBehaviour
    {
        public float m_StartingHealth = 100f;               
        public Slider m_Slider;                             
        public Image m_FillImage;                           
        public Color m_FullHealthColor = Color.green;    
        public Color m_ZeroHealthColor = Color.red;      
        public GameObject m_ExplosionPrefab;                
        [HideInInspector] public bool m_HasShield;          
        
        private AudioSource m_ExplosionAudio;               
        private ParticleSystem m_ExplosionParticles;        
        private float m_LocalHealth; // Dùng cho chế độ Offline
        private bool m_Dead;                                
        private float m_ShieldValue;                        
        private bool m_IsInvincible;                        

        // BÍ QUYẾT ĐỒNG BỘ: Biến mạng thay thế cho m_CurrentHealth ở chế độ Online
        public NetworkVariable<float> m_NetworkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private void Awake ()
        {
            m_ExplosionParticles = Instantiate (m_ExplosionPrefab).GetComponent<ParticleSystem> ();
            m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource> ();
            m_ExplosionParticles.gameObject.SetActive (false);
            m_Slider.maxValue = m_StartingHealth;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if(m_ExplosionParticles != null)
                Destroy(m_ExplosionParticles.gameObject);
        }

        private void OnEnable()
        {
            m_LocalHealth = m_StartingHealth;
            m_Dead = false;
            m_HasShield = false;
            m_ShieldValue = 0;
            m_IsInvincible = false;

            SetHealthUI(m_StartingHealth);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_NetworkHealth.Value = m_StartingHealth;
            }
            
            // Đăng ký sự kiện lắng nghe biến mạng thay đổi
            m_NetworkHealth.OnValueChanged += OnHealthChanged;
            SetHealthUI(m_NetworkHealth.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_NetworkHealth.OnValueChanged -= OnHealthChanged;
        }

        // Hàm này tự động chạy trên mọi máy tính (Client/Host) khi Server trừ máu
        private void OnHealthChanged(float previousValue, float newValue)
        {
            SetHealthUI(newValue);
            
            if (newValue <= 0f && !m_Dead)
            {
                OnDeath();
            }
        }

        // Giao diện chung cho xử lý sát thương (Lai giữa Offline & Online)
        public void TakeDamage (float amount)
        {
            bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (isOnline)
            {
                // ONLINE: Chỉ Server được quyền trừ biến mạng
                if (!IsServer) return;

                if (!m_IsInvincible)
                {
                    m_NetworkHealth.Value -= amount * (1 - m_ShieldValue);
                }
            }
            else
            {
                // OFFLINE: Xử lý biến Local như cũ
                if (!m_IsInvincible)
                {
                    m_LocalHealth -= amount * (1 - m_ShieldValue);
                    SetHealthUI(m_LocalHealth);

                    if (m_LocalHealth <= 0f && !m_Dead)
                    {
                        OnDeath();
                    }
                }
            }
        }

        public void IncreaseHealth(float amount)
        {
            bool isOnline = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

            if (isOnline)
            {
                // ONLINE: Chỉ Server có quyền cộng máu
                if (!IsServer) return;

                if (m_NetworkHealth.Value + amount <= m_StartingHealth)
                {
                    m_NetworkHealth.Value += amount;
                }
                else
                {
                    m_NetworkHealth.Value = m_StartingHealth;
                }
            }
            else
            {
                // OFFLINE
                if (m_LocalHealth + amount <= m_StartingHealth)
                {
                    m_LocalHealth += amount;
                }
                else
                {
                    m_LocalHealth = m_StartingHealth;
                }

                SetHealthUI(m_LocalHealth);
            }
        }

        public void ToggleShield (float shieldAmount)
        {
            m_HasShield = !m_HasShield;
            if (m_HasShield)
            {
                m_ShieldValue = shieldAmount;
            }
            else
            {
                m_ShieldValue = 0;
            }
        }

        public void ToggleInvincibility()
        {
            m_IsInvincible = !m_IsInvincible;
        }

        // Truyền giá trị vào trực tiếp để thanh máu cập nhật đúng lúc
        private void SetHealthUI (float currentHealth)
        {
            m_Slider.value = currentHealth;
            m_FillImage.color = Color.Lerp (m_ZeroHealthColor, m_FullHealthColor, currentHealth / m_StartingHealth);
        }

        private void OnDeath ()
        {
            m_Dead = true;

            m_ExplosionParticles.transform.position = transform.position;
            m_ExplosionParticles.gameObject.SetActive (true);
            m_ExplosionParticles.Play ();
            m_ExplosionAudio.Play();

            gameObject.SetActive (false);
        }
    }
}