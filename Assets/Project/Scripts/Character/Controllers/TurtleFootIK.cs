// ============================================================
// TurtleFootIK.cs
// ------------------------------------------------------------
// Ne işe yarar:
// Baby Caretta'nın 4 ayağının zemin yüksekliğine ve eğimine 
// göre hizalanmasını sağlar. Animation Rigging'in Target 
// objelerini yönetir.
// ============================================================
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Game.Character
{
    public class TurtleFootIK : MonoBehaviour
    {
        [System.Serializable]
        public class LegData
        {
            [Tooltip("Örn: On_Sol_Bacak")]
            public string legName;
            
            [Tooltip("Animation Rigging'deki Target objesi")]
            public Transform target;
            
            [Tooltip("Hiyerarşideki asıl ayak/yüzgeç ucu kemiği (Tip Bone)")]
            public Transform animatedFoot;
            
            [Tooltip("Bu bacağın IK constraint bileşeni")]
            public TwoBoneIKConstraint constraint;
            
            [Range(0, 1)] public float ikWeight = 1f;
        }

        [Header("IK Ayarları")]
        [SerializeField] private LegData[] legs = new LegData[4];
        
        [Header("Raycast (Zemin Okuma)")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float raycastUpOffset = 1.0f; // Ayağın ne kadar yukarısından ışın atılacak
        [SerializeField] private float raycastDownDistance = 2.0f; // Işın ne kadar aşağı gidecek
        [SerializeField] private float footHeightOffset = 0.05f; // Ayak modelinin kalınlık payı
        
        [Header("Yumuşatma")]
        [SerializeField] private float positionLerpSpeed = 15f;
        [SerializeField] private float rotationLerpSpeed = 15f;

                /// <summary>
        /// Predator sistemi (yengeç vb.) için rastgele bir bacağın gerçek kemik (Tip Bone)
        /// referansını döndürür. IK aktif olsun olmasın kullanılabilir - sadece hiyerarşideki
        /// referansı okur, IK constraint'e dokunmaz.
        /// </summary>
        public Transform GetRandomFootTransform()
        {
            if (legs == null || legs.Length == 0) return null;
            int index = Random.Range(0, legs.Length);
            return legs[index].animatedFoot;
        }

        /// <summary>
        /// Predator sisteminin "en yakın boş bacağı bul" mantığı için tüm bacak kemik
        /// referanslarını döndürür (null olanlar dahil - çağıran taraf null kontrolü yapmalı).
        /// </summary>
        public Transform[] GetAllFootTransforms()
        {
            if (legs == null) return new Transform[0];
            var result = new Transform[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                result[i] = legs[i] != null ? legs[i].animatedFoot : null;
            }
            return result;
        }

private void LateUpdate()
        {
            foreach (var leg in legs)
            {
                if (leg.ikWeight <= 0f)
                {
                    leg.constraint.weight = 0f;
                    continue;
                }

                // 1. Raycast'in başlangıç noktasını belirle: 
                // X ve Z animasyondan gelir (adım atabilmek için), Y ise karakterin gövdesinin üstünden başlar.
                Vector3 rayOrigin = new Vector3(
                    leg.animatedFoot.position.x, 
                    transform.position.y + raycastUpOffset, 
                    leg.animatedFoot.position.z
                );

                // 2. Aşağı doğru Raycast fırlat
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDownDistance, groundLayer))
                {
                    // 3. Pozisyonu Ayarla (X ve Z animasyondan, Y zeminden)
                    Vector3 targetPos = new Vector3(
                        leg.animatedFoot.position.x, 
                        hit.point.y + footHeightOffset, 
                        leg.animatedFoot.position.z
                    );
                    leg.target.position = Vector3.Lerp(leg.target.position, targetPos, Time.deltaTime * positionLerpSpeed);

                    // 4. Rotasyonu Ayarla (Zemin eğimine - hit.normal - göre hizala)
                    Quaternion targetRot = Quaternion.FromToRotation(transform.up, hit.normal) * leg.animatedFoot.rotation;
                    leg.target.rotation = Quaternion.Slerp(leg.target.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);

                    // IK ağırlığını uygula
                    leg.constraint.weight = Mathf.Lerp(leg.constraint.weight, leg.ikWeight, Time.deltaTime * positionLerpSpeed);
                }
                else
                {
                    // Eğer zemin bulunamazsa (örneğin kaplumbağa uçurumdan düşüyorsa) IK'yı yumuşakça kapat
                    leg.constraint.weight = Mathf.Lerp(leg.constraint.weight, 0f, Time.deltaTime * positionLerpSpeed);
                }
            }
        }
    }
}