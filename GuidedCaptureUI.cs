using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// ========================================================
// DENTIFY — GUIDED CAPTURE UI
// Rehberli çekim ekranı: Kamera + üstte açı bilgisi + ilerleme barı.
// 8 açılı sıralı çekim için kullanıcı arayüzü.
// ========================================================

public class GuidedCaptureUI : MonoBehaviour
{
    [Header("Kamera")]
    public WebCamManager webCamManager;
    public DamageDetector damageDetector;
    public GuidedCaptureManager guidedCaptureManager;
    public DamageUI damageUI;

    [Header("Üst Bilgi Paneli")]
    public TMP_Text phaseTitle;             // "TESLİM ÖNCESİ ÇEKİM" veya "TESLİM SONRASI ÇEKİM"
    public TMP_Text angleNameText;          // "Ön-Sağ Çeyrek"
    public TMP_Text guidanceText;           // "Sağ ön köşeye 45° açıyla konumlanın"
    public TMP_Text progressText;           // "3 / 8"
    public Slider progressBar;              // İlerleme çubuğu

    [Header("Plaka & Müşteri Bilgisi")]
    public TMP_Text sessionInfoText;        // "34 ABC 123 • Ahmet Yılmaz"

    [Header("Alt Butonlar")]
    public Button captureButton;            // Fotoğraf çek butonu
    public Button galleryButton;            // Galeriden / Diskten fotoğraf yükle (Test için)
    public Button cameraToggleButton;       // Kamerayı aç/kapat (Masaüstü için)
    public Button skipButton;               // Açıyı atla butonu
    public Button backButton;               // Ana menüye dön

    [Header("Çekim Sonucu Paneli")]
    public GameObject resultPanel;          // AI sonucu gösterim paneli
    public TMP_Text resultText;             // "2 Hasar Tespit Edildi!"
    public Button continueButton;           // Sonraki açıya geç butonu

    [Header("8 Açı Tamamlandı Paneli")]
    public GameObject completionPanel;      // Tüm açılar tamamlandı
    public TMP_Text completionSummaryText;  // Özet metin
    public Button extraCaptureButton;       // Ek detay çekim butonu
    public Button finishButton;             // Bitir butonu

    [Header("Referanslar")]
    public MainMenuUI mainMenuUI;
    public ComparisonUI comparisonUI;

    // Dahili durum
    private RentalSession currentSession;
    private CapturePhase currentPhase;
    private bool isShowingResult = false;

    void Start()
    {
        // Buton bağlantıları
        if (captureButton != null)
            captureButton.onClick.AddListener(OnCapturePressed);

        if (galleryButton != null)
            galleryButton.onClick.AddListener(OnGalleryPressed);

        if (cameraToggleButton != null)
            cameraToggleButton.onClick.AddListener(OnCameraTogglePressed);

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipPressed);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinuePressed);

        if (extraCaptureButton != null)
            extraCaptureButton.onClick.AddListener(OnExtraCapturePressed);

        if (finishButton != null)
            finishButton.onClick.AddListener(OnFinishPressed);

        // Event dinleyicileri
        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.OnAngleChanged += OnAngleChanged;
            guidedCaptureManager.OnCaptureCompleted += OnCaptureCompleted;
            guidedCaptureManager.OnAllAnglesCompleted += OnAllAnglesCompleted;
        }

        // Başlangıçta panelleri gizle
        HideResultPanel();
        HideCompletionPanel();
    }

    void OnDestroy()
    {
        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.OnAngleChanged -= OnAngleChanged;
            guidedCaptureManager.OnCaptureCompleted -= OnCaptureCompleted;
            guidedCaptureManager.OnAllAnglesCompleted -= OnAllAnglesCompleted;
        }
    }

    // ========================================================
    // DIŞ ERİŞİM — ÇEKİMİ BAŞLAT
    // ========================================================

    /// <summary>
    /// Rehberli çekimi başlatır. NewRentalUI veya ActiveRentalsUI tarafından çağrılır.
    /// </summary>
    public void StartCapture(RentalSession session, CapturePhase phase)
    {
        currentSession = session;
        currentPhase = phase;
        isShowingResult = false;

        // Ana menü panellerini gizle ve sadece çekim panelini aktif yap
        if (mainMenuUI != null)
        {
            mainMenuUI.OpenGuidedCapture();
        }

        // Kamerayı başlat/canlandır
        if (webCamManager != null)
        {
            webCamManager.ResumeCamera();
        }

        // Çizimleri ve eski maskeleri temizle
        if (damageUI != null) damageUI.ClearAllDrawings();

        // Capture butonunu her zaman normal moda geri bağla
        // (Ek çekim sonrası listener OnExtraCaptureSnap'te kalma sorununu önler)
        if (captureButton != null)
        {
            captureButton.onClick.RemoveAllListeners();
            captureButton.onClick.AddListener(OnCapturePressed);
        }

        // UI güncelle
        UpdatePhaseTitle();
        UpdateSessionInfo();
        HideResultPanel();
        HideCompletionPanel();
        ShowCaptureControls(true);

        // GuidedCaptureManager'ı başlat
        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.StartGuidedCapture(session, phase);
        }
    }

    // ========================================================
    // BUTON OLAYLARI
    // ========================================================

    private void OnCapturePressed()
    {
        if (isShowingResult) return;
        if (webCamManager == null) return;

        // WebCamManager'ın public accessor'ını kullan
        Texture2D snapshot = webCamManager.TakeSnapshot();
        if (snapshot == null) return;

        // Ekranın gri olmasını önlemek için çekilen fotoğrafı ekrana sabitle
        webCamManager.FreezeFrame(snapshot);
        webCamManager.PauseCamera();

        // Çekimi yap
        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.CaptureCurrentAngle(snapshot);
        }
    }

    private void OnGalleryPressed()
    {
        if (isShowingResult) return;

        NativeFilePicker.PickImage((tex) =>
        {
            if (tex != null)
            {
                ProcessGalleryImage(tex);
            }
        }, "Hasarlı Araç Fotoğrafı Seç");
    }

    private void ProcessGalleryImage(Texture2D galleryTexture)
    {
        if (webCamManager != null)
        {
            webCamManager.FreezeFrame(galleryTexture);
            webCamManager.PauseCamera();
        }

        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.CaptureCurrentAngle(galleryTexture);
        }
    }

    private void OnCameraTogglePressed()
    {
        if (webCamManager != null)
        {
            webCamManager.ToggleCamera();
            bool isPlaying = webCamManager.IsCameraPlaying();
            if (cameraToggleButton != null)
            {
                TMP_Text txt = cameraToggleButton.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    txt.text = isPlaying ? "KAMERA KAPAT" : "KAMERA AÇ";
                }
            }
        }
    }

    private void OnSkipPressed()
    {
        if (damageUI != null) damageUI.ClearAllDrawings();

        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.SkipCurrentAngle();
            // MoveToNextAngle OnAngleChanged event'ini tetikleyecek
        }
    }

    private void OnBackPressed()
    {
        if (damageUI != null) damageUI.ClearAllDrawings();

        // Çekimi durdur
        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.StopGuidedCapture();
        }

        // Kamerayı geri başlat
        if (webCamManager != null)
            webCamManager.ResumeCamera();

        if (mainMenuUI != null)
            mainMenuUI.ShowMainMenu();
    }

    private void OnContinuePressed()
    {
        if (damageUI != null) damageUI.ClearAllDrawings();

        HideResultPanel();
        if (webCamManager != null)
            webCamManager.ResumeCamera();
        ShowCaptureControls(true);

        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.MoveToNextAngle();
        }
    }

    private void OnExtraCapturePressed()
    {
        HideCompletionPanel();
        if (webCamManager != null)
            webCamManager.ResumeCamera();
        ShowCaptureControls(true);

        // Serbest çekim moduna geç
        if (angleNameText != null) angleNameText.text = "Ek Detay Çekimi";
        if (guidanceText != null) guidanceText.text = "İstediğiniz bölgenin detay fotoğrafını çekin.";

        // Capture butonunu ek çekim için ayarla
        if (captureButton != null)
        {
            captureButton.onClick.RemoveAllListeners();
            captureButton.onClick.AddListener(OnExtraCaptureSnap);
        }
    }

    private void OnExtraCaptureSnap()
    {
        if (webCamManager == null) return;

        Texture2D snapshot = webCamManager.TakeSnapshot();
        if (snapshot == null) return;

        webCamManager.PauseCamera();

        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.CaptureExtra(snapshot);
        }
    }

    private void OnFinishPressed()
    {
        // Fazı tamamla
        if (guidedCaptureManager != null)
        {
            guidedCaptureManager.FinishCurrentPhase();
        }

        // Teslim sonrası ise karşılaştırma ekranına git
        if (currentPhase == CapturePhase.TeslimSonrasi)
        {
            // Rapor üret ve göster
            if (comparisonUI != null && currentSession != null)
            {
                ComparisonReport report = ReportGenerator.Instance.GenerateAndSaveReport(currentSession);
                if (report != null)
                {
                    comparisonUI.ShowReport(currentSession, report);
                }
            }

            if (mainMenuUI != null)
                mainMenuUI.OpenComparison();
        }
        else
        {
            // Teslim öncesi tamamlandı → ana menüye dön
            if (mainMenuUI != null)
                mainMenuUI.ShowMainMenu();
        }
    }

    // ========================================================
    // EVENT HANDLER'LAR
    // ========================================================

    private void OnAngleChanged(CaptureAngle angle, int current, int total)
    {
        if (damageUI != null) damageUI.ClearAllDrawings();

        if (angleNameText != null) angleNameText.text = angle.displayName;
        if (guidanceText != null) guidanceText.text = angle.guidanceText;
        if (progressText != null) progressText.text = $"{current} / {total}";
        if (progressBar != null)
        {
            progressBar.maxValue = total;
            progressBar.value = current - 1; // Mevcut açı henüz çekilmedi
        }
    }

    private void OnCaptureCompleted(CaptureResult result)
    {
        isShowingResult = true;
        ShowCaptureControls(false);
        ShowResultPanel(result);

        // İlerleme çubuğunu güncelle
        if (progressBar != null && guidedCaptureManager != null)
        {
            progressBar.value = guidedCaptureManager.GetCompletedAngles();
        }
    }

    private void OnAllAnglesCompleted(RentalSession session, CapturePhase phase)
    {
        HideResultPanel();
        ShowCaptureControls(false);
        if (webCamManager != null)
            webCamManager.ResumeCamera();
        ShowCompletionPanel();
    }

    // ========================================================
    // UI GÜNCELLEME
    // ========================================================

    private void UpdatePhaseTitle()
    {
        if (phaseTitle == null) return;

        if (currentPhase == CapturePhase.TeslimOncesi)
        {
            phaseTitle.text = "TESLİM ÖNCESİ ÇEKİM";
        }
        else
        {
            phaseTitle.text = "TESLİM SONRASI ÇEKİM";
        }
    }

    private void UpdateSessionInfo()
    {
        if (sessionInfoText == null || currentSession == null) return;
        sessionInfoText.text = $"{currentSession.plateNumber} - {currentSession.customerName}";
    }

    private void ShowResultPanel(CaptureResult result)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);

            if (resultText != null)
            {
                if (result.damages.Count == 0)
                {
                    resultText.text = $"{result.angleName}\nHasar tespit edilmedi.";
                }
                else
                {
                    string damageList = "";
                    foreach (var d in result.damages)
                    {
                        damageList += $"\n  - {d.className} (%{Mathf.RoundToInt(d.confidence * 100)})";
                    }
                    resultText.text = $"{result.angleName}\n{result.damages.Count} Hasar Tespit Edildi:{damageList}";
                }
            }
        }
    }

    private void HideResultPanel()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        isShowingResult = false;
    }

    private void ShowCompletionPanel()
    {
        if (completionPanel != null)
        {
            completionPanel.SetActive(true);

            if (completionSummaryText != null && currentSession != null)
            {
                string summary = ReportGenerator.Instance.GeneratePhaseSummary(currentSession, currentPhase);
                completionSummaryText.text = summary;
            }

            // Bitir buton metnini ayarla
            if (finishButton != null)
            {
                TMP_Text btnText = finishButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    if (currentPhase == CapturePhase.TeslimSonrasi)
                        btnText.text = "Raporu Oluştur";
                    else
                        btnText.text = "Teslim Öncesini Kaydet";
                }
            }
        }
    }

    private void HideCompletionPanel()
    {
        if (completionPanel != null) completionPanel.SetActive(false);
    }

    private void ShowCaptureControls(bool show)
    {
        if (captureButton != null) captureButton.gameObject.SetActive(show);
        if (skipButton != null) skipButton.gameObject.SetActive(show);
        if (galleryButton != null) galleryButton.gameObject.SetActive(show);
    }
}
