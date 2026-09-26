using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ========================================================
// DENTIFY — COMPARISON UI
// Teslim öncesi vs sonrası karşılaştırma rapor ekranı.
// Before/after yan yana görüntüleme ve yeni hasar vurgulama.
// ========================================================

public class ComparisonUI : MonoBehaviour
{
    [Header("Rapor Başlığı")]
    public TMP_Text reportTitleText;        // "34 ABC 123 • HASAR RAPORU"
    public TMP_Text reportDateText;         // "06.08.2026 17:30"

    [Header("Özet Panel")]
    public TMP_Text newDamageCountText;     // Büyük "3" sayısı
    public TMP_Text newDamageLabel;         // "Yeni Hasar"
    public TMP_Text existingDamageCountText;
    public TMP_Text resolvedDamageCountText;
    public TMP_Text classSummaryText;       // "Çizik: 2, Göçük: 1"

    [Header("Açı Detay Listesi")]
    public Transform angleContentArea;      // ScrollView > Content
    public GameObject angleDetailPrefab;    // Her açı için satır prefab'ı

    [Header("Tam Rapor Metni")]
    public TMP_Text fullReportText;         // Detaylı metin rapor

    [Header("Butonlar")]
    public Button backButton;
    public Button saveReportButton;         // Raporu galeriye kaydet
    public Button shareButton;              // Paylaş (opsiyonel)

    [Header("Referanslar")]
    public MainMenuUI mainMenuUI;

    // Dahili
    private RentalSession currentSession;
    private ComparisonReport currentReport;

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);

        if (saveReportButton != null)
            saveReportButton.onClick.AddListener(OnSaveReport);
    }

    // ========================================================
    // RAPORU GÖSTER
    // ========================================================

    /// <summary>
    /// Karşılaştırma raporunu ekranda gösterir.
    /// </summary>
    public void ShowReport(RentalSession session, ComparisonReport report)
    {
        currentSession = session;
        currentReport = report;

        if (report == null)
        {
            if (fullReportText != null)
                fullReportText.text = "Rapor oluşturulamadı.";
            return;
        }

        UpdateHeader(session, report);
        UpdateSummaryPanel(report);
        UpdateAngleDetails(report);
        UpdateFullReport(report);
    }

    // ========================================================
    // UI GÜNCELLEME
    // ========================================================

    private void UpdateHeader(RentalSession session, ComparisonReport report)
    {
        if (reportTitleText != null)
        {
            reportTitleText.text = $"{session.plateNumber} - HASAR RAPORU";
        }

        if (reportDateText != null)
        {
            if (System.DateTime.TryParse(report.reportDate, out System.DateTime dt))
            {
                reportDateText.text = dt.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }

    private void UpdateSummaryPanel(ComparisonReport report)
    {
        // Büyük yeni hasar sayısı
        if (newDamageCountText != null)
        {
            newDamageCountText.text = report.totalNewDamages.ToString();

            // 0 ise yeşil, değilse kırmızı
            newDamageCountText.color = report.totalNewDamages > 0
                ? new Color(1f, 0.3f, 0.3f) // Kırmızı
                : new Color(0.3f, 1f, 0.3f); // Yeşil
        }

        if (newDamageLabel != null)
        {
            newDamageLabel.text = report.totalNewDamages == 0
                ? "Yeni Hasar Yok!"
                : "Yeni Hasar Tespit Edildi";
        }

        if (existingDamageCountText != null)
        {
            existingDamageCountText.text = $"{report.totalExistingDamages} Eski Hasar";
        }

        if (resolvedDamageCountText != null)
        {
            resolvedDamageCountText.text = $"{report.totalResolvedDamages} Çözülen";
        }

        // Sınıf bazlı özet
        if (classSummaryText != null)
        {
            if (report.newDamagesByClass.Count > 0)
            {
                string summary = "Yeni Hasar Türleri:\n";
                foreach (var cls in report.newDamagesByClass)
                {
                    summary += $"  - {cls.className}: {cls.count} adet\n";
                }
                classSummaryText.text = summary;
            }
            else
            {
                classSummaryText.text = "Müşteri döneminde yeni hasar oluşmamıştır.";
            }
        }
    }

    private void UpdateAngleDetails(ComparisonReport report)
    {
        if (angleContentArea == null || angleDetailPrefab == null) return;

        // Eski satırları temizle
        foreach (Transform child in angleContentArea)
        {
            Destroy(child.gameObject);
        }

        // Her açı için kart oluştur
        foreach (var angleResult in report.angleResults)
        {
            // Boş açıları atla
            if (angleResult.newDamages.Count == 0 &&
                angleResult.existingDamages.Count == 0 &&
                angleResult.resolvedDamages.Count == 0)
            {
                continue;
            }

            GameObject item = Instantiate(angleDetailPrefab, angleContentArea);

            // Açı adı
            TMP_Text angleNameText = FindChildText(item, "AngleNameText");
            if (angleNameText != null)
            {
                string icon = angleResult.newDamages.Count > 0 ? "<color=#FF4D4D>● </color>" : "<color=#22C55E>● </color>";
                angleNameText.text = $"{icon}{angleResult.angleName}";
                angleNameText.richText = true;
            }

            // Açı özeti — her hasar ayrı satırda ve renk kodlu
            TMP_Text angleSummaryText = FindChildText(item, "AngleSummaryText");
            if (angleSummaryText != null)
            {
                string summary = "";
                int totalDamages = angleResult.newDamages.Count + angleResult.existingDamages.Count + angleResult.resolvedDamages.Count;

                foreach (var d in angleResult.newDamages)
                {
                    summary += $"<color=#FF4444>● [YENİ]</color> {d.className} <color=#AAAAAA>(%{Mathf.RoundToInt(d.confidence * 100)})</color>\n";
                }
                foreach (var d in angleResult.existingDamages)
                {
                    summary += $"<color=#66BB6A>● [ESKİ]</color> {d.className} <color=#AAAAAA>(%{Mathf.RoundToInt(d.confidence * 100)})</color>\n";
                }
                foreach (var d in angleResult.resolvedDamages)
                {
                    summary += $"<color=#42A5F5>● [ÇÖZÜLEN]</color> {d.className}\n";
                }

                angleSummaryText.text = summary.TrimEnd('\n');
                angleSummaryText.richText = true;

                // Çok fazla hasar varsa kartın yüksekliğini dinamik artır
                LayoutElement le = item.GetComponent<LayoutElement>();
                if (le != null && totalDamages > 3)
                {
                    le.preferredHeight = 120 + (totalDamages - 3) * 24;
                }
            }

            // Durum rengi (Modern koyu lacivert kart tonu)
            Image bgImage = item.GetComponent<Image>();
            if (bgImage != null)
            {
                if (angleResult.newDamages.Count > 0)
                {
                    bgImage.color = new Color(0.18f, 0.12f, 0.16f, 0.95f); // Koyu lacivert-kırmızı ton
                }
                else
                {
                    bgImage.color = new Color(0.09f, 0.15f, 0.22f, 0.95f); // Koyu lacivert kart
                }
            }
        }
    }

    private void UpdateFullReport(ComparisonReport report)
    {
        if (fullReportText != null)
        {
            fullReportText.text = ReportGenerator.Instance.GenerateSummaryText(report);
        }
    }

    // ========================================================
    // BUTON İŞLEMLERİ
    // ========================================================

    private void OnBack()
    {
        if (mainMenuUI != null)
            mainMenuUI.ShowMainMenu();
    }

    private void OnSaveReport()
    {
        if (currentReport == null || currentSession == null) return;

        // 1. Ekran görüntüsü al ve kaydet
        StartCoroutine(SaveReportScreenshot());

        // 2. HTML / PDF Raporu üret ve otomatik aç
        if (ReportGenerator.Instance != null)
        {
            string htmlPath = ReportGenerator.Instance.ExportReportToHTML(currentSession, currentReport);
            if (!string.IsNullOrEmpty(htmlPath))
            {
                Application.OpenURL("file://" + htmlPath);
                Debug.Log($"PDF/HTML Raporu tarayıcıda açılıyor: {htmlPath}");
            }
        }

        // 3. Web Paneline Otomatik Senkronize Et (Varsa)
        if (DentifySyncClient.Instance != null)
        {
            DentifySyncClient.Instance.SyncSession(currentSession, currentReport, (success, msg) => {
                if (success) Debug.Log("[Dentify] Oturum web paneline otomatik aktarıldı.");
                else Debug.LogWarning("[Dentify] Web paneli çevrimdışı, yerel kayıt korundu.");
            });
        }
    }

    private System.Collections.IEnumerator SaveReportScreenshot()
    {
        yield return new WaitForEndOfFrame();

        Texture2D screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();

        string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"Dentify_{currentSession.plateNumber}_{timeStamp}.png";

#if UNITY_ANDROID || UNITY_IOS
        // Android telefonda doğrudan Fotoğraf Galerisine (Dentify Albümüne) kaydet
        NativeGallery.SaveImageToGallery(screenTex, "Dentify", fileName, (success, path) => {
            if (success) Debug.Log($"[Dentify] Rapor galerisine kaydedildi: {path}");
        });
#else
        // Windows'ta Resimler klasörüne veya yerel dizine kaydet
        byte[] bytes = screenTex.EncodeToPNG();
        string picDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
        if (System.IO.Directory.Exists(picDir))
        {
            string fullPath = System.IO.Path.Combine(picDir, fileName);
            System.IO.File.WriteAllBytes(fullPath, bytes);
            Debug.Log($"[Dentify] Rapor Resimler klasörüne kaydedildi: {fullPath}");
        }
#endif

        Destroy(screenTex);

        // Kullanıcıya bildir
        if (saveReportButton != null)
        {
            TMP_Text btnText = saveReportButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                string originalText = btnText.text;
                btnText.text = "Galeriye Kaydedildi!";

                yield return new WaitForSeconds(2.5f);
                btnText.text = originalText;
            }
        }
    }

    // ========================================================
    // YARDIMCI
    // ========================================================

    private TMP_Text FindChildText(GameObject parent, string childName)
    {
        TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t.gameObject.name == childName) return t;
        }
        return null;
    }
}
