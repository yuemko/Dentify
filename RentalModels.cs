using System.Collections.Generic;

// ========================================================
// DENTIFY — RENTAL VERI MODELLERI
// Tüm serializable veri sınıfları tek dosyada toplanmıştır.
// ========================================================

/// <summary>
/// Kiralama oturumunun hangi aşamada olduğunu belirtir.
/// </summary>
public enum RentalStatus
{
    TeslimOncesiDevam,      // Teslim öncesi çekim devam ediyor
    TeslimBekleniyor,       // Teslim öncesi tamamlandı, araç müşteride
    TeslimSonrasiDevam,     // Teslim sonrası çekim devam ediyor
    Tamamlandi              // Her iki çekim de tamamlandı, rapor üretildi
}

/// <summary>
/// Kamera çekim aşaması: Teslim öncesi mi, sonrası mı?
/// </summary>
public enum CapturePhase
{
    TeslimOncesi,
    TeslimSonrasi
}

// ========================================================
// ANA OTURUM VERİSİ
// ========================================================

/// <summary>
/// Tek bir kiralama oturumunun tüm meta verisi.
/// persistentDataPath/rentals/ altındaki klasörle eşleşir.
/// </summary>
[System.Serializable]
public class RentalSession
{
    public string sessionId;        // Benzersiz GUID
    public string plateNumber;      // "34ABC123"
    public string customerName;     // "Ahmet Yılmaz"
    public string createdDate;      // ISO 8601 formatı
    public string returnDate;       // Teslim alım tarihi (sonradan doldurulur)
    public RentalStatus status;     // Durumu
    public string folderName;       // Klasör adı: [Plaka]_[Musteri]_[Tarih]
}

/// <summary>
/// Tüm oturumların ana indeks dosyası (rentals.json).
/// </summary>
[System.Serializable]
public class RentalIndex
{
    public List<RentalSession> sessions = new List<RentalSession>();
}

// ========================================================
// ÇEKİM AÇISI TANIMI
// ========================================================

/// <summary>
/// Rehberli çekimdeki 8 açıdan birini tanımlar.
/// </summary>
[System.Serializable]
public class CaptureAngle
{
    public int index;               // 0-7
    public string id;               // "on", "on_sag", "sag", ...
    public string displayName;      // "Ön", "Ön-Sağ Çeyrek", ...
    public string guidanceText;     // Kullanıcıya gösterilecek rehber metin
    public bool isCompleted;        // Bu açı çekildi mi?

    public CaptureAngle(int index, string id, string displayName, string guidanceText)
    {
        this.index = index;
        this.id = id;
        this.displayName = displayName;
        this.guidanceText = guidanceText;
        this.isCompleted = false;
    }
}

// ========================================================
// TEK BİR ÇEKİMİN SONUCU
// ========================================================

/// <summary>
/// Bir açıdan çekilen fotoğrafın AI analiz sonucu.
/// detections.json dosyasındaki her bir kayıt buna karşılık gelir.
/// </summary>
[System.Serializable]
public class CaptureResult
{
    public string angleId;              // "on", "sag", "extra_01" vs.
    public string angleName;            // "Ön", "Sağ Yan" vs.
    public string photoFileName;        // "01_on.png"
    public string maskFileName;         // "01_on_mask.png"
    public string timestamp;            // ISO 8601
    public List<DetectedDamage> damages = new List<DetectedDamage>();
}

/// <summary>
/// Bir açının tüm çekim sonuçlarını tutan dosya yapısı (detections.json).
/// </summary>
[System.Serializable]
public class DetectionsData
{
    public string phase;        // "teslim_oncesi" veya "teslim_sonrasi"
    public string sessionId;
    public List<CaptureResult> captures = new List<CaptureResult>();
}

// ========================================================
// TEK BİR HASAR
// ========================================================

/// <summary>
/// AI tarafından tespit edilen tek bir hasar.
/// DamageResult sınıfının JSON-serialize edilebilir, genişletilmiş versiyonu.
/// </summary>
[System.Serializable]
public class DetectedDamage
{
    public int classId;
    public string className;            // "Çizik", "Göçük" vs.
    public float confidence;
    public float centerX;
    public float centerY;
    public float width;
    public float height;
    [System.NonSerialized]
    public float[] maskWeights;         // 32 ağırlık (bellekte tutulur, JSON'a yazılmaz)

    /// <summary>
    /// Mevcut DamageResult nesnesinden DetectedDamage'a dönüştürme.
    /// </summary>
    public static DetectedDamage FromDamageResult(DamageResult result)
    {
        DetectedDamage dd = new DetectedDamage();
        dd.classId = result.classId;
        dd.className = DamageConstants.GetClassName(result.classId);
        dd.confidence = result.confidence;
        dd.centerX = result.centerX;
        dd.centerY = result.centerY;
        dd.width = result.width;
        dd.height = result.height;
        dd.maskWeights = result.maskWeights;
        return dd;
    }
}

// ========================================================
// FARK ANALİZİ SONUCU
// ========================================================

/// <summary>
/// Tek bir açı için teslim öncesi vs sonrası karşılaştırma sonucu.
/// </summary>
[System.Serializable]
public class AngleComparisonResult
{
    public string angleId;
    public string angleName;
    public List<DetectedDamage> existingDamages = new List<DetectedDamage>();    // Zaten vardı (🟢)
    public List<DetectedDamage> newDamages = new List<DetectedDamage>();         // YENİ HASAR (🔴)
    public List<DetectedDamage> resolvedDamages = new List<DetectedDamage>();    // Artık yok (tamir?)
}

/// <summary>
/// Tüm oturumun karşılaştırma raporu (comparison_report.json).
/// </summary>
[System.Serializable]
public class ComparisonReport
{
    public string sessionId;
    public string plateNumber;
    public string customerName;
    public string rentalDate;
    public string returnDate;
    public string reportDate;

    // Özet istatistikler
    public int totalNewDamages;
    public int totalExistingDamages;
    public int totalResolvedDamages;

    // Sınıf bazlı yeni hasar sayıları
    public List<ClassDamageCount> newDamagesByClass = new List<ClassDamageCount>();

    // Her açının detaylı sonucu
    public List<AngleComparisonResult> angleResults = new List<AngleComparisonResult>();
}

/// <summary>
/// Sınıf bazlı hasar sayısı (rapor özeti için).
/// </summary>
[System.Serializable]
public class ClassDamageCount
{
    public string className;
    public int count;

    public ClassDamageCount(string className, int count)
    {
        this.className = className;
        this.count = count;
    }
}
