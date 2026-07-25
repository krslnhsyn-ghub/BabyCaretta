using UnityEngine;

namespace Game.Predators
{
    // Şu an sadece yengeç tipleri kullanılacak; martı/kuş/kayalık yaratığı ileride eklenecek.
    public enum PredatorType
    {
        SmallCrab,
        LargeCrab,
        LargeBird,
        SmallBird,
        RockCreature
    }

    /// <summary>
    /// Bir predator tipinin tüm ayarlanabilir verisi. Yeni bir predator eklemek (örn. kayalık
    /// yaratığı) yeni kod yazmak değil, bu ScriptableObject'ten yeni bir asset oluşturup
    /// değerleri/modeli değiştirmek anlamına gelir.
    /// </summary>
    [CreateAssetMenu(fileName = "PredatorData", menuName = "Baby Caretta/Predator Data")]
    public class PredatorData : ScriptableObject
    {
        [Header("Genel")]
        public PredatorType predatorType;

        [Header("Algılama / Yakalama Mesafeleri")]
        public float detectRange = 5f;
        public float grabRange = 1f;

        [Header("Yavaşlatma")]
        [Tooltip("Bu predator kaplumbağayı tutarken hız çarpanı (0-1). Stackable ise birden " +
                 "fazla predator'ın çarpanları çarpılarak birleşir (lineer).")]
        [Range(0.1f, 1f)]
        public float slowdownRatio = 0.5f;

        [Tooltip("Aynı anda bu türden birden fazla predator kaplumbağayı tutabilir mi " +
                 "(örn. küçük yengeçler stackable, büyük yengeç değil)")]
        public bool stackable = false;

        [Header("Kaçış (Escape) Ayarları")]
        [Tooltip("Çift basış arasındaki minimum süre (saniye) - bundan hızlı basılırsa başarısız sayılır")]
        public float escapeIntervalMin = 0.15f;
        [Tooltip("Çift basış arasındaki maksimum süre (saniye) - bundan yavaş basılırsa başarısız sayılır")]
        public float escapeIntervalMax = 0.6f;

        [Header("Yakalanma Sonucu")]
        [Tooltip("Kurtulamazsa kaç saniye sonra checkpoint'e dönülür")]
        public float captureDelay = 4f;

        [Header("İlk Karşılaşma")]
        [Tooltip("Bu predator ile ilk karşılaşmada küçük bir görsel ipucu (ok/parlama) gösterilsin mi")]
        public bool showFirstEncounterHint = true;
    }
}
