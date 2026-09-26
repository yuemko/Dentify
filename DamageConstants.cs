// ========================================================
// DENTIFY — SABİT DEĞİŞKENLER
// Tüm projede kullanılan hasar sınıf isimleri ve renkler.
// Bu dosyayı tek kaynak olarak kullanın, başka yerde
// classNames dizisi oluşturmayın!
// ========================================================

using UnityEngine;

public static class DamageConstants
{
    /// <summary>
    /// 7 hasar sınıfının Türkçe isimleri.
    /// Model çıktısındaki classId ile indekslenir.
    /// </summary>
    public static readonly string[] ClassNames = 
    {
        "Çizik",            // 0
        "Göçük",            // 1
        "Çatlak / Yırtık",  // 2
        "Eksik Parça",      // 3
        "Kırık Far",        // 4
        "Delik",            // 5
        "Kırık Cam"         // 6
    };

    public static readonly int NumClasses = ClassNames.Length;

    /// <summary>
    /// classId'den sınıf adını güvenli şekilde döndürür.
    /// </summary>
    public static string GetClassName(int classId)
    {
        if (classId >= 0 && classId < ClassNames.Length)
            return ClassNames[classId];
        return "Bilinmeyen";
    }

    // Renk sabitleri
    public static readonly Color HighConfidenceColor = new Color(1f, 0f, 0f, 0.6f);   // Kırmızı (%85+)
    public static readonly Color LowConfidenceColor = new Color(1f, 1f, 0f, 0.4f);    // Sarı (belirsiz)
    public static readonly Color NewDamageColor = new Color(1f, 0.1f, 0.1f, 0.5f);     // Kırmızı (yeni)
    public static readonly Color ExistingDamageColor = new Color(0.1f, 0.9f, 0.2f, 0.35f); // Yeşil (eski)
    public static readonly Color ResolvedDamageColor = new Color(1f, 1f, 1f, 0.2f);    // Beyaz (çözülen)

    // Maske ağırlık sayısı (YOLOv8-Seg)
    public const int MaskWeightCount = 32;
}
