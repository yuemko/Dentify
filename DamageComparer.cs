using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ========================================================
// DENTIFY — DAMAGE COMPARER
// Teslim öncesi vs sonrası hasar karşılaştırma motoru.
// IoU tabanlı eşleştirme ile yeni/eski/çözülen hasarları tespit eder.
// ========================================================

public class DamageComparer : MonoBehaviour
{
    public static DamageComparer Instance { get; private set; }

    [Header("Karşılaştırma Eşikleri")]
    [Range(0.1f, 0.9f)]
    [Tooltip("Bu IoU değerinin üzerinde eşleşen hasarlar 'eski hasar' sayılır")]
    public float matchIoUThreshold = 0.3f;

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
    // ANA KARŞILAŞTIRMA
    // ========================================================

    /// <summary>
    /// Bir oturumun teslim öncesi ve sonrası tespitlerini karşılaştırır.
    /// Tüm açılar için fark analizi yapar.
    /// </summary>
    public ComparisonReport CompareSession(RentalSession session)
    {
        if (session == null)
        {
            Debug.LogError("CompareSession: Oturum null!");
            return null;
        }

        // Teslim öncesi ve sonrası verilerini yükle
        DetectionsData beforeData = RentalSessionManager.Instance.LoadDetections(
            session, CapturePhase.TeslimOncesi);
        DetectionsData afterData = RentalSessionManager.Instance.LoadDetections(
            session, CapturePhase.TeslimSonrasi);

        if (beforeData == null) beforeData = new DetectionsData();
        if (afterData == null) afterData = new DetectionsData();

        ComparisonReport report = new ComparisonReport
        {
            sessionId = session.sessionId,
            plateNumber = session.plateNumber,
            customerName = session.customerName,
            rentalDate = session.createdDate,
            returnDate = session.returnDate,
            reportDate = System.DateTime.Now.ToString("o"),
            angleResults = new List<AngleComparisonResult>()
        };

        int totalNew = 0;
        int totalExisting = 0;
        int totalResolved = 0;
        Dictionary<string, int> newByClass = new Dictionary<string, int>();

        // Her açı için karşılaştırma yap
        string[] standardAngleIds = { "on", "on_sag", "sag", "arka_sag", "arka", "arka_sol", "sol", "on_sol" };
        string[] standardAngleNames = { "Ön", "Ön-Sağ Çeyrek", "Sağ Yan", "Arka-Sağ Çeyrek", "Arka", "Arka-Sol Çeyrek", "Sol Yan", "Ön-Sol Çeyrek" };

        for (int i = 0; i < standardAngleIds.Length; i++)
        {
            string angleId = standardAngleIds[i];

            // Bu açıdaki öncesi ve sonrası çekimleri bul
            CaptureResult beforeCapture = beforeData.captures.Find(c => c.angleId == angleId);
            CaptureResult afterCapture = afterData.captures.Find(c => c.angleId == angleId);

            List<DetectedDamage> beforeDamages = beforeCapture != null ? beforeCapture.damages : new List<DetectedDamage>();
            List<DetectedDamage> afterDamages = afterCapture != null ? afterCapture.damages : new List<DetectedDamage>();

            AngleComparisonResult angleResult = CompareAngle(angleId, standardAngleNames[i], beforeDamages, afterDamages);
            report.angleResults.Add(angleResult);

            totalNew += angleResult.newDamages.Count;
            totalExisting += angleResult.existingDamages.Count;
            totalResolved += angleResult.resolvedDamages.Count;

            // Sınıf bazlı sayaç
            foreach (var damage in angleResult.newDamages)
            {
                if (newByClass.ContainsKey(damage.className))
                    newByClass[damage.className]++;
                else
                    newByClass[damage.className] = 1;
            }
        }

        // Ek çekimleri de karşılaştır (eşleşme yapılamaz, hepsi ek bilgi olarak eklenir)
        foreach (var afterCapture in afterData.captures)
        {
            if (afterCapture.angleId.StartsWith("extra_"))
            {
                AngleComparisonResult extraResult = new AngleComparisonResult
                {
                    angleId = afterCapture.angleId,
                    angleName = afterCapture.angleName,
                    newDamages = new List<DetectedDamage>(afterCapture.damages),
                    existingDamages = new List<DetectedDamage>(),
                    resolvedDamages = new List<DetectedDamage>()
                };
                report.angleResults.Add(extraResult);
                totalNew += afterCapture.damages.Count;

                foreach (var damage in afterCapture.damages)
                {
                    if (newByClass.ContainsKey(damage.className))
                        newByClass[damage.className]++;
                    else
                        newByClass[damage.className] = 1;
                }
            }
        }

        // Özet istatistikleri doldur
        report.totalNewDamages = totalNew;
        report.totalExistingDamages = totalExisting;
        report.totalResolvedDamages = totalResolved;

        report.newDamagesByClass = new List<ClassDamageCount>();
        foreach (var kvp in newByClass)
        {
            report.newDamagesByClass.Add(new ClassDamageCount(kvp.Key, kvp.Value));
        }

        Debug.Log($"Karşılaştırma tamamlandı: {totalNew} yeni, {totalExisting} eski, {totalResolved} çözülen hasar");
        return report;
    }

    // ========================================================
    // TEK AÇI KARŞILAŞTIRMASI
    // ========================================================

    /// <summary>
    /// Tek bir açı için öncesi/sonrası hasarları IoU ile karşılaştırır.
    /// </summary>
    public AngleComparisonResult CompareAngle(string angleId, string angleName,
        List<DetectedDamage> beforeDamages, List<DetectedDamage> afterDamages)
    {
        AngleComparisonResult result = new AngleComparisonResult
        {
            angleId = angleId,
            angleName = angleName,
            existingDamages = new List<DetectedDamage>(),
            newDamages = new List<DetectedDamage>(),
            resolvedDamages = new List<DetectedDamage>()
        };

        // Eşleşme takibi
        HashSet<int> matchedBeforeIndices = new HashSet<int>();
        HashSet<int> matchedAfterIndices = new HashSet<int>();

        // Her sonrası hasarını, her öncesi hasarıyla karşılaştır
        for (int a = 0; a < afterDamages.Count; a++)
        {
            float bestIoU = 0f;
            int bestBeforeIndex = -1;

            for (int b = 0; b < beforeDamages.Count; b++)
            {
                if (matchedBeforeIndices.Contains(b)) continue; // Zaten eşleşmiş

                float iou = CalculateIoU(afterDamages[a], beforeDamages[b]);

                // Aynı sınıf olma bonusu: IoU eşiğini %10 düşür
                bool sameClass = afterDamages[a].classId == beforeDamages[b].classId;
                float effectiveThreshold = sameClass ? matchIoUThreshold * 0.9f : matchIoUThreshold;

                if (iou > bestIoU && iou >= effectiveThreshold)
                {
                    bestIoU = iou;
                    bestBeforeIndex = b;
                }
            }

            if (bestBeforeIndex >= 0)
            {
                // Eşleşme bulundu → Eski hasar (zaten vardı)
                result.existingDamages.Add(afterDamages[a]);
                matchedBeforeIndices.Add(bestBeforeIndex);
                matchedAfterIndices.Add(a);
            }
            else
            {
                // Eşleşme yok → YENİ HASAR (müşteri döneminde oluşmuş)
                result.newDamages.Add(afterDamages[a]);
            }
        }

        // Öncesinde var ama sonrasında eşleşmemiş → Çözülen hasar
        for (int b = 0; b < beforeDamages.Count; b++)
        {
            if (!matchedBeforeIndices.Contains(b))
            {
                result.resolvedDamages.Add(beforeDamages[b]);
            }
        }

        return result;
    }

    // ========================================================
    // IoU HESAPLAMA (BOUNDING BOX)
    // ========================================================

    /// <summary>
    /// İki hasar bounding box'ının Intersection over Union değerini hesaplar.
    /// </summary>
    private float CalculateIoU(DetectedDamage a, DetectedDamage b)
    {
        float aX1 = a.centerX - a.width / 2f;
        float aY1 = a.centerY - a.height / 2f;
        float aX2 = a.centerX + a.width / 2f;
        float aY2 = a.centerY + a.height / 2f;

        float bX1 = b.centerX - b.width / 2f;
        float bY1 = b.centerY - b.height / 2f;
        float bX2 = b.centerX + b.width / 2f;
        float bY2 = b.centerY + b.height / 2f;

        float interX1 = Mathf.Max(aX1, bX1);
        float interY1 = Mathf.Max(aY1, bY1);
        float interX2 = Mathf.Min(aX2, bX2);
        float interY2 = Mathf.Min(aY2, bY2);

        float interArea = Mathf.Max(0, interX2 - interX1) * Mathf.Max(0, interY2 - interY1);

        float areaA = a.width * a.height;
        float areaB = b.width * b.height;
        float unionArea = areaA + areaB - interArea;

        if (unionArea <= 0) return 0f;
        return interArea / unionArea;
    }
}
