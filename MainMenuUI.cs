using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ========================================================
// DENTIFY — MAIN MENU UI
// Ana ekran: 3 büyük kart butonu ile navigasyon.
// ========================================================

public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    public GameObject mainMenuPanel;
    public GameObject newRentalPanel;
    public GameObject activeRentalsPanel;
    public GameObject archivePanel;
    public GameObject guidedCapturePanel;
    public GameObject comparisonPanel;

    [Header("Ana Menü Butonları")]
    public Button newRentalButton;
    public Button returnVehicleButton;
    public Button archiveButton;
    public Button cameraToggleButton;       // Ana ekranda Kamerayı Aç / Kapat

    [Header("Kamera Referansı")]
    public WebCamManager webCamManager;

    [Header("Durum Bilgisi")]
    public TMP_Text activeCountText;    // "3 Araç Kirada" gibi

    void Start()
    {
        // Buton olaylarını bağla
        if (newRentalButton != null)
            newRentalButton.onClick.AddListener(OpenNewRental);

        if (returnVehicleButton != null)
            returnVehicleButton.onClick.AddListener(OpenActiveRentals);

        if (archiveButton != null)
            archiveButton.onClick.AddListener(OpenArchive);

        if (cameraToggleButton != null)
            cameraToggleButton.onClick.AddListener(ToggleCamera);

        // Başlangıçta ana menüyü göster, diğerlerini kapat
        ShowMainMenu();
    }

    public void ToggleCamera()
    {
        if (webCamManager != null)
        {
            webCamManager.ToggleCamera();
            UpdateCameraToggleUI(webCamManager.isCameraEnabled);
        }
    }

    private void UpdateCameraToggleUI(bool isEnabled)
    {
        if (cameraToggleButton != null)
        {
            TMP_Text t = cameraToggleButton.GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                t.text = isEnabled ? "KAMERA: AÇIK" : "KAMERA: KAPALI";
            }
            Image img = cameraToggleButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = isEnabled ? new Color(0.02f, 0.52f, 0.85f, 1f) : new Color(0.18f, 0.22f, 0.30f, 1f);
            }
        }
    }

    void OnEnable()
    {
        UpdateActiveCount();
    }

    // ========================================================
    // NAVİGASYON
    // ========================================================

    public void ShowMainMenu()
    {
        HideAllPanels();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        UpdateActiveCount();
    }

    public void OpenNewRental()
    {
        HideAllPanels();
        if (newRentalPanel != null) newRentalPanel.SetActive(true);
    }

    public void OpenActiveRentals()
    {
        HideAllPanels();
        if (activeRentalsPanel != null) activeRentalsPanel.SetActive(true);
    }

    public void OpenArchive()
    {
        HideAllPanels();
        if (archivePanel != null) archivePanel.SetActive(true);
    }

    public void OpenGuidedCapture()
    {
        HideAllPanels();
        if (guidedCapturePanel != null) guidedCapturePanel.SetActive(true);
    }

    public void OpenComparison()
    {
        HideAllPanels();
        if (comparisonPanel != null) comparisonPanel.SetActive(true);
    }

    // ========================================================
    // YARDIMCI
    // ========================================================

    private void HideAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (newRentalPanel != null) newRentalPanel.SetActive(false);
        if (activeRentalsPanel != null) activeRentalsPanel.SetActive(false);
        if (archivePanel != null) archivePanel.SetActive(false);
        if (guidedCapturePanel != null) guidedCapturePanel.SetActive(false);
        if (comparisonPanel != null) comparisonPanel.SetActive(false);

        // Ekstra Güvenlik: Canvas altındaki tüm mükerrer panelleri dinamik kapat
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            string[] panelNames = { "MainMenuPanel", "NewRentalPanel", "ActiveRentalsPanel", "ArchivePanel", "GuidedCapturePanel", "ComparisonPanel" };
            foreach (Transform child in canvas.transform)
            {
                foreach (string pName in panelNames)
                {
                    if (child.name.StartsWith(pName))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    private void UpdateActiveCount()
    {
        if (activeCountText != null && RentalSessionManager.Instance != null)
        {
            int activeCount = RentalSessionManager.Instance.GetActiveRentals().Count;
            int inProgressCount = RentalSessionManager.Instance.GetInProgressRentals().Count;

            if (activeCount > 0 || inProgressCount > 0)
            {
                activeCountText.text = $"{activeCount} Araç Kirada - {inProgressCount} İşlem Devam Ediyor";
            }
            else
            {
                activeCountText.text = "Henüz aktif kiralama yok.";
            }
        }
    }
}
