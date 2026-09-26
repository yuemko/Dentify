using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DamageUI : MonoBehaviour
{
    [Header("1. Gorsellestirme Alanlari")]
    [Tooltip("Kutularin icinde dolasacagi Container cercevesi.")]
    public RectTransform drawContainer;

    [Tooltip("Yapay zeka tarafindan uretilen KIRMIZI maske kaplamasi (RawImage)")]
    public RawImage maskDisplayImage;

    [Tooltip("Uzerinde yazi olan, bizim yaratigimiz kare cerceve Prefab'i")]    
    public GameObject boxPrefab;

    [Header("2. Profesyonel Panel (Istege Bagli - Sahneden Srukleyin)")]        
    [Tooltip("Cok yaklastiniz, Lutfen tarayin vs.. gibi akilli metin. (Text objesi)")]
    public TMP_Text smartAssistantText;

    [Tooltip("Dinamik 'Kac Cizik, Kac Gocuk buldu' raporu. (Text objesi)")]     
    public TMP_Text reportStatsText;

    [Tooltip("Kamera akış yöneticisi referansı (Katman hizalaması için)")]
    public WebCamManager webCamManager;

    private List<GameObject> activeBoxes = new List<GameObject>();

    void Start()
    {
        EnsureLayerAlignment();

        // 0. Ekranda Duran Beyaz Maske/Canvas Sorununu CoZ
        if (maskDisplayImage != null)
        {
            maskDisplayImage.color = Color.clear; // Tamamen seffaf (gorunmez) yap
        }

        // Otomatik Konumlandirma (Script Ayarliyor)
        if (smartAssistantText != null)
        {
            RectTransform rt = smartAssistantText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -100);
            rt.sizeDelta = new Vector2(900, 100);
            smartAssistantText.alignment = TextAlignmentOptions.Top; // TMP_Text karsiligi
            smartAssistantText.fontSize = 36;
        }

        if (reportStatsText != null)
        {
            RectTransform rt = reportStatsText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(50, -50);
            rt.sizeDelta = new Vector2(600, 300);
            reportStatsText.alignment = TextAlignmentOptions.TopLeft; // TMP_Text karsiligi
            reportStatsText.fontSize = 28;
        }
    }

    /// <summary>
    /// AI Maske ve Kutu katmanlarını çalışma anında CameraFeed'in tam alt nesnesi (child) yaparak
    /// piksel kaymalarını %100 önler.
    /// </summary>
    public void EnsureLayerAlignment()
    {
        if (webCamManager == null)
            webCamManager = Object.FindFirstObjectByType<WebCamManager>();

        if (webCamManager != null && webCamManager.cameraFeed != null)
        {
            Transform cameraFeedTransform = webCamManager.cameraFeed.transform;

            if (maskDisplayImage != null && maskDisplayImage.transform.parent != cameraFeedTransform)
            {
                maskDisplayImage.transform.SetParent(cameraFeedTransform, false);
                RectTransform mRt = maskDisplayImage.rectTransform;
                mRt.anchorMin = Vector2.zero;
                mRt.anchorMax = Vector2.one;
                mRt.anchoredPosition = Vector2.zero;
                mRt.sizeDelta = Vector2.zero;
                mRt.localScale = Vector3.one;
                mRt.localEulerAngles = Vector3.zero;
            }

            if (drawContainer != null && drawContainer.transform.parent != cameraFeedTransform)
            {
                drawContainer.transform.SetParent(cameraFeedTransform, false);
                drawContainer.anchorMin = Vector2.zero;
                drawContainer.anchorMax = Vector2.one;
                drawContainer.anchoredPosition = Vector2.zero;
                drawContainer.sizeDelta = Vector2.zero;
                drawContainer.localScale = Vector3.one;
                drawContainer.localEulerAngles = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Ekrana çizilmiş tüm kutuları, maskeyi ve asistan yazılarını sıfırlar.
    /// </summary>
    public void ClearAllDrawings()
    {
        foreach (var box in activeBoxes)
        {
            if (box != null) Destroy(box);
        }
        activeBoxes.Clear();

        if (maskDisplayImage != null)
        {
            maskDisplayImage.texture = null;
            maskDisplayImage.color = Color.clear;
        }

        if (smartAssistantText != null) smartAssistantText.text = "";
        if (reportStatsText != null) reportStatsText.text = "";
    }

    /// <summary>
    /// Ekrana kutulari, maskeyi ve dinamik yazilari oturtan asil method.
    /// </summary>
    public void DrawDamages(List<DamageResult> damages, int imageWidth, int imageHeight, Texture2D maskOverlay)
    {
        EnsureLayerAlignment();

        // 1. Maskeyi Ekrana Yapistir (Hasar yoksa ekranı beyazlatma, ŞEFFAF yap!)
        if (maskDisplayImage != null)
        {
            if (damages != null && damages.Count > 0 && maskOverlay != null)
            {
                maskDisplayImage.texture = maskOverlay;
                maskDisplayImage.color = Color.white;
            }
            else
            {
                maskDisplayImage.texture = null;
                maskDisplayImage.color = Color.clear;
            }
        }

        // 2. Eski Cizimleri Ekranda Sil
        foreach (var box in activeBoxes) Destroy(box);
        activeBoxes.Clear();

        if (damages == null || damages.Count == 0 || drawContainer == null || boxPrefab == null) return;

        float containerW = drawContainer.rect.width;
        float containerH = drawContainer.rect.height;

        bool isCameraTooClose = false;
        int detectedCount = damages.Count;

        // 3. Her Bir Hasari (Kutuyu) Ekleyip Ayarlama
        foreach (var damage in damages)
        {
            // Objeyi Uret ve Bizi Rahatsiz Etmesin Diye Merkeze (0.5) Al
            GameObject boxObj = Instantiate(boxPrefab, drawContainer);
            RectTransform rt = boxObj.GetComponent<RectTransform>();
            Color confidentColor = DamageConstants.HighConfidenceColor;
            Color unsureColor = DamageConstants.LowConfidenceColor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one; // Bazen abartili buyuklukte dogmasini %100 engeller.

            // Yapay zekadan (YOLO) Gelen Kutu Agirliklarini (0.01 gibi oranlar) Piksele (1080) Cikaralim
            float xPos = (damage.centerX / imageWidth) * containerW;
            float yPos = (1f - (damage.centerY / imageHeight)) * containerH; // Android & Unity Axis Cevirici
            float w = (damage.width / imageWidth) * containerW;
            float h = (damage.height / imageHeight) * containerH;

            // Yapay zeka ekrana o kadar buyuk bir 'kutu' cizdi ki aracin dibindeyiz! 
            // (%60'ini kapliyorsa geriye git diyelim)
            if ((w * h) > (containerW * containerH * 0.6f))
            {
                isCameraTooClose = true;
            }

            rt.anchoredPosition = new Vector2(xPos - containerW / 2f, yPos - containerH / 2f);
            rt.sizeDelta = new Vector2(w, h);

            // 4. Renk Kodlamasi
            Image boxImageColor = boxObj.GetComponent<Image>();
            if (boxImageColor != null)
            {
                boxImageColor.color = damage.confidence > 0.80f 
                    ? DamageConstants.HighConfidenceColor 
                    : DamageConstants.LowConfidenceColor;
            }

            // 5. Hasar etiket metni (DamageConstants'tan sınıf adını al)
            string damageName = DamageConstants.GetClassName(damage.classId);
            string labelToPrint = $"{damageName} %{Mathf.RoundToInt(damage.confidence * 100)}";

            // TMP_Text ve Legacy Normal Text denemesi
            TMP_Text tmpText = boxObj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null) tmpText.text = labelToPrint;
            else
            {
                Text defaultText = boxObj.GetComponentInChildren<Text>();
                if (defaultText != null) defaultText.text = labelToPrint;
            }

            activeBoxes.Add(boxObj);
        }

        // 6. Akilli Asistan Uyari Sistemini Calistir
        UpdateSmartAssistant(isCameraTooClose, detectedCount);
    }

    private void UpdateSmartAssistant(bool tooClose, int detectedCount)
    {
        // Rapor Ekranini Guncelle
        if (reportStatsText != null)
        {
            reportStatsText.text = $"SCAN RAPORU:\nTespit Edilen Hasar: {detectedCount} Adet";
        }

        // Asistan Metnini Guncelle
        if (smartAssistantText != null)
        {
            if (tooClose) {
                smartAssistantText.text = "Çok yaklaştınız! Lütfen 1-2 adım geri gidin.";
                smartAssistantText.color = Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.time * 2f, 1f));
            }
            else if (detectedCount == 0) {
                smartAssistantText.text = "Yüzey taranıyor... Kamerayı yavaşça hareket ettirin.";
                smartAssistantText.color = Color.white;
            }
            else {
                smartAssistantText.text = $"{detectedCount} Adet Hasar Tespit Edildi!";
                smartAssistantText.color = Color.green;
            }
        }
    }

    // ========================================================
    // KARŞILAŞTIRMA MODU — BEFORE/AFTER GÖRSELLEŞTİRME
    // ========================================================

    /// <summary>
    /// Karşılaştırma sonuçlarını renk kodlu olarak ekrana çizer.
    /// Yeşil = Eski hasar (zaten vardı)
    /// Kırmızı = Yeni hasar (müşteri döneminde oluşmuş)
    /// Beyaz şeffaf = Çözülen hasar (artık tespit edilmiyor)
    /// </summary>
    public void DrawComparisonDamages(AngleComparisonResult compResult, int imageWidth, int imageHeight)
    {
        // Eski kutuları temizle
        foreach (var box in activeBoxes) Destroy(box);
        activeBoxes.Clear();

        if (drawContainer == null || boxPrefab == null) return;

        float containerW = drawContainer.rect.width;
        float containerH = drawContainer.rect.height;

        // Renk tanımları (DamageConstants'tan)
        Color newDamageColor = DamageConstants.NewDamageColor;
        Color existingDamageColor = DamageConstants.ExistingDamageColor;
        Color resolvedDamageColor = DamageConstants.ResolvedDamageColor;

        // Yeni hasarları çiz (en üstte, kırmızı)
        foreach (var damage in compResult.newDamages)
        {
            DrawSingleDamageBox(damage, "[YENİ]", newDamageColor, imageWidth, imageHeight, containerW, containerH);
        }

        // Eski hasarları çiz (yeşil)
        foreach (var damage in compResult.existingDamages)
        {
            DrawSingleDamageBox(damage, "[ESKİ]", existingDamageColor, imageWidth, imageHeight, containerW, containerH);
        }

        // Çözülen hasarları çiz (beyaz şeffaf)
        foreach (var damage in compResult.resolvedDamages)
        {
            DrawSingleDamageBox(damage, "[ÇÖZ.]", resolvedDamageColor, imageWidth, imageHeight, containerW, containerH);
        }

        // Rapor istatistiklerini güncelle
        if (reportStatsText != null)
        {
            reportStatsText.text = $"KARŞILAŞTIRMA:\n" +
                $"Yeni: {compResult.newDamages.Count}\n" +
                $"Eski: {compResult.existingDamages.Count}\n" +
                $"Çözülen: {compResult.resolvedDamages.Count}";
        }
    }

    /// <summary>
    /// Tek bir hasar kutusunu belirtilen renk ve etiketle çizer.
    /// </summary>
    private void DrawSingleDamageBox(DetectedDamage damage, string statusLabel, Color boxColor,
        int imageWidth, int imageHeight, float containerW, float containerH)
    {
        GameObject boxObj = Instantiate(boxPrefab, drawContainer);
        RectTransform rt = boxObj.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;

        float xPos = (damage.centerX / imageWidth) * containerW;
        float yPos = (1f - (damage.centerY / imageHeight)) * containerH;
        float w = (damage.width / imageWidth) * containerW;
        float h = (damage.height / imageHeight) * containerH;

        rt.anchoredPosition = new Vector2(xPos - containerW / 2f, yPos - containerH / 2f);
        rt.sizeDelta = new Vector2(w, h);

        // Kutu rengini ayarla
        Image boxImageColor = boxObj.GetComponent<Image>();
        if (boxImageColor != null)
        {
            boxImageColor.color = boxColor;
        }

        // Etiket metni
        string labelToPrint = $"{statusLabel} {damage.className} %{Mathf.RoundToInt(damage.confidence * 100)}";

        TMP_Text tmpText = boxObj.GetComponentInChildren<TMP_Text>();
        if (tmpText != null) tmpText.text = labelToPrint;
        else
        {
            Text defaultText = boxObj.GetComponentInChildren<Text>();
            if (defaultText != null) defaultText.text = labelToPrint;
        }

        activeBoxes.Add(boxObj);
    }
}
