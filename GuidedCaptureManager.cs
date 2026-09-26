using UnityEngine;
using System.Collections.Generic;

// ========================================================
// DENTIFY — GUIDED CAPTURE MANAGER
// 8 açılı sıralı çekim yöneticisi.
// Kullanıcıyı sırayla her açıdan fotoğraf çekmeye yönlendirir.
// ========================================================

public class GuidedCaptureManager : MonoBehaviour
{
    public static GuidedCaptureManager Instance { get; private set; }

    [Header("Bağlantılar")]
    public DamageDetector damageDetector;
    public WebCamManager webCamManager;

    // Şu anki çekim oturumu
    private RentalSession currentSession;
    private CapturePhase currentPhase;
    private List<CaptureAngle> captureAngles;
    private int currentAngleIndex = 0;
    private bool isCapturing = false;

    // Ek (serbest) çekim sayacı
    private int extraCaptureCount = 0;

    // === EVENTS ===
    public event System.Action<CaptureAngle, int, int> OnAngleChanged;          // (açı, mevcut, toplam)
    public event System.Action<CaptureResult> OnCaptureCompleted;               // Tek çekim tamamlandı
    public event System.Action<RentalSession, CapturePhase> OnAllAnglesCompleted; // 8 açı bitti
    public event System.Action<RentalSession, CapturePhase> OnSessionPhaseCompleted; // Faz tamamen bitti

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ========================================================
    // 8 STANDART AÇI TANIMLARI
    // ========================================================

    /// <summary>
    /// 8 standart çekim açısını oluşturur (saat yönünde).
    /// </summary>
    private List<CaptureAngle> CreateStandardAngles()
    {
        return new List<CaptureAngle>
        {
            new CaptureAngle(0, "on",       "Ön",              "Aracın ön tarafına geçin ve tam karşıdan çekin."),
            new CaptureAngle(1, "on_sag",   "Ön-Sağ Çeyrek",   "Sağ ön köşeye 45° açıyla konumlanın."),
            new CaptureAngle(2, "sag",      "Sağ Yan",         "Aracın sağ tarafına geçin ve yandan çekin."),
            new CaptureAngle(3, "arka_sag", "Arka-Sağ Çeyrek", "Sağ arka köşeye 45° açıyla konumlanın."),
            new CaptureAngle(4, "arka",     "Arka",            "Aracın arkasına geçin ve tam karşıdan çekin."),
            new CaptureAngle(5, "arka_sol", "Arka-Sol Çeyrek", "Sol arka köşeye 45° açıyla konumlanın."),
            new CaptureAngle(6, "sol",      "Sol Yan",         "Aracın sol tarafına geçin ve yandan çekin."),
            new CaptureAngle(7, "on_sol",   "Ön-Sol Çeyrek",   "Sol ön köşeye 45° açıyla konumlanın."),
            new CaptureAngle(8, "tavan",    "Tavan & Cam",     "Kamerayı yukarı kaldırarak tavan ve sunroof yüzeyini çekin.")
        };
    }

    // ========================================================
    // ÇEKİM BAŞLATMA / DURDURMA
    // ========================================================

    /// <summary>
    /// Rehberli çekim sürecini başlatır.
    /// </summary>
    public void StartGuidedCapture(RentalSession session, CapturePhase phase)
    {
        currentSession = session;
        currentPhase = phase;
        captureAngles = CreateStandardAngles();
        currentAngleIndex = 0;
        extraCaptureCount = 0;
        isCapturing = true;

        // Mevcut tamamlanmış çekimleri kontrol et (devam eden oturum için)
        DetectionsData existingData = RentalSessionManager.Instance.LoadDetections(session, phase);
        if (existingData != null)
        {
            foreach (var capture in existingData.captures)
            {
                CaptureAngle angle = captureAngles.Find(a => a.id == capture.angleId);
                if (angle != null)
                {
                    angle.isCompleted = true;
                }
            }

            // İlk tamamlanmamış açıyı bul
            int firstIncomplete = captureAngles.FindIndex(a => !a.isCompleted);
            if (firstIncomplete >= 0)
            {
                currentAngleIndex = firstIncomplete;
            }
        }

        // İlk açıyı bildir
        NotifyAngleChanged();

        Debug.Log($"Rehberli çekim başlatıldı: {session.plateNumber} - {phase}");
    }

    /// <summary>
    /// Rehberli çekimi durdurur (iptal).
    /// </summary>
    public void StopGuidedCapture()
    {
        isCapturing = false;
        currentSession = null;
        Debug.Log("Rehberli çekim durduruldu.");
    }

    // ========================================================
    // ÇEKİM İŞLEMLERİ
    // ========================================================

    /// <summary>
    /// Mevcut açıdaki fotoğrafı çeker ve AI analizini başlatır.
    /// WebCamManager'dan alınan snapshot ile çalışır.
    /// </summary>
    public void CaptureCurrentAngle(Texture2D snapshot)
    {
        if (!isCapturing || currentSession == null || snapshot == null) return;

        CaptureAngle currentAngle = GetCurrentAngle();
        if (currentAngle == null) return;

        // AI analizini çalıştır
        damageDetector.DetectDamage(snapshot);

        // Sonuçları al
        List<DamageResult> damageResults = damageDetector.GetLastDetectedDamages();
        Texture2D maskCopy = damageDetector.GetMaskTextureCopy();

        // CaptureResult oluştur
        string prefix = (currentAngle.index + 1).ToString("D2"); // "01", "02", ...
        CaptureResult result = new CaptureResult
        {
            angleId = currentAngle.id,
            angleName = currentAngle.displayName,
            photoFileName = $"{prefix}_{currentAngle.id}.png",
            maskFileName = $"{prefix}_{currentAngle.id}_mask.png",
            timestamp = System.DateTime.Now.ToString("o"),
            damages = new List<DetectedDamage>()
        };

        // DamageResult → DetectedDamage dönüşümü
        if (damageResults != null)
        {
            foreach (var dr in damageResults)
            {
                result.damages.Add(DetectedDamage.FromDamageResult(dr));
            }
        }

        // Dosyaya kaydet
        RentalSessionManager.Instance.SaveCaptureResult(
            currentSession, currentPhase, result, snapshot, maskCopy);

        // maskCopy kopyasını temizle (snapshot ekran gösterimi bittiğinde UnfreezeFrame ile temizlenir)
        if (maskCopy != null) Destroy(maskCopy);

        // Açıyı tamamlandı olarak işaretle
        currentAngle.isCompleted = true;

        // Event tetikle
        OnCaptureCompleted?.Invoke(result);

        Debug.Log($"Açı çekildi: {currentAngle.displayName} - {result.damages.Count} hasar tespit edildi");
    }

    /// <summary>
    /// Mevcut açıyı atlar (opsiyonel).
    /// </summary>
    public void SkipCurrentAngle()
    {
        if (!isCapturing) return;

        MoveToNextAngle();
    }

    /// <summary>
    /// Serbest ek çekim yapar (8 ana açı tamamlandıktan sonra).
    /// </summary>
    public void CaptureExtra(Texture2D snapshot)
    {
        if (!isCapturing || currentSession == null || snapshot == null) return;

        extraCaptureCount++;

        // AI analizini çalıştır
        damageDetector.DetectDamage(snapshot);

        List<DamageResult> damageResults = damageDetector.GetLastDetectedDamages();
        Texture2D maskCopy = damageDetector.GetMaskTextureCopy();

        string extraId = $"extra_{extraCaptureCount:D2}";

        CaptureResult result = new CaptureResult
        {
            angleId = extraId,
            angleName = $"Ek Detay #{extraCaptureCount}",
            photoFileName = $"{extraId}.png",
            maskFileName = $"{extraId}_mask.png",
            timestamp = System.DateTime.Now.ToString("o"),
            damages = new List<DetectedDamage>()
        };

        if (damageResults != null)
        {
            foreach (var dr in damageResults)
            {
                result.damages.Add(DetectedDamage.FromDamageResult(dr));
            }
        }

        RentalSessionManager.Instance.SaveCaptureResult(
            currentSession, currentPhase, result, snapshot, maskCopy);

        // maskCopy kopyasını temizle
        if (maskCopy != null) Destroy(maskCopy);

        OnCaptureCompleted?.Invoke(result);

        Debug.Log($"Ek çekim kaydedildi: {extraId} - {result.damages.Count} hasar");
    }

    /// <summary>
    /// Sonraki açıya geçer. Çağıran: UI'daki "Devam" butonu veya otomatik geçiş.
    /// </summary>
    public void MoveToNextAngle()
    {
        if (!isCapturing) return;

        currentAngleIndex++;

        if (currentAngleIndex >= captureAngles.Count)
        {
            // Tüm 8 açı tamamlandı
            OnAllAnglesCompleted?.Invoke(currentSession, currentPhase);
            Debug.Log("Tüm 8 açı tamamlandı!");
            return;
        }

        // Zaten tamamlanmış açıları atla
        while (currentAngleIndex < captureAngles.Count && captureAngles[currentAngleIndex].isCompleted)
        {
            currentAngleIndex++;
        }

        if (currentAngleIndex >= captureAngles.Count)
        {
            OnAllAnglesCompleted?.Invoke(currentSession, currentPhase);
            return;
        }

        NotifyAngleChanged();
    }

    /// <summary>
    /// Fazı tamamlar (ek çekimler de dahil).
    /// Oturum durumunu günceller.
    /// </summary>
    public void FinishCurrentPhase()
    {
        if (currentSession == null) return;

        if (currentPhase == CapturePhase.TeslimOncesi)
        {
            RentalSessionManager.Instance.SetSessionStatus(
                currentSession.sessionId, RentalStatus.TeslimBekleniyor);
        }
        else if (currentPhase == CapturePhase.TeslimSonrasi)
        {
            RentalSessionManager.Instance.SetSessionStatus(
                currentSession.sessionId, RentalStatus.Tamamlandi);
        }

        OnSessionPhaseCompleted?.Invoke(currentSession, currentPhase);
        isCapturing = false;

        Debug.Log($"Faz tamamlandı: {currentSession.plateNumber} - {currentPhase}");
    }

    // ========================================================
    // DURUM SORGULAMA
    // ========================================================

    public CaptureAngle GetCurrentAngle()
    {
        if (captureAngles == null || currentAngleIndex >= captureAngles.Count)
            return null;
        return captureAngles[currentAngleIndex];
    }

    public int GetCurrentAngleIndex()
    {
        return currentAngleIndex;
    }

    public int GetTotalAngles()
    {
        return captureAngles != null ? captureAngles.Count : 8;
    }

    public int GetCompletedAngles()
    {
        if (captureAngles == null) return 0;
        int count = 0;
        foreach (var a in captureAngles)
        {
            if (a.isCompleted) count++;
        }
        return count;
    }

    public bool IsCapturing()
    {
        return isCapturing;
    }

    public RentalSession GetCurrentSession()
    {
        return currentSession;
    }

    public CapturePhase GetCurrentPhase()
    {
        return currentPhase;
    }

    /// <summary>
    /// Toplam tespit edilen hasar sayısını (tüm açılar) döndürür.
    /// </summary>
    public int GetTotalDamageCount()
    {
        if (currentSession == null) return 0;

        DetectionsData data = RentalSessionManager.Instance.LoadDetections(currentSession, currentPhase);
        if (data == null) return 0;

        int total = 0;
        foreach (var capture in data.captures)
        {
            total += capture.damages.Count;
        }
        return total;
    }

    // ========================================================
    // YARDIMCI
    // ========================================================

    private void NotifyAngleChanged()
    {
        CaptureAngle angle = GetCurrentAngle();
        if (angle != null)
        {
            OnAngleChanged?.Invoke(angle, currentAngleIndex + 1, captureAngles.Count);
        }
    }
}
