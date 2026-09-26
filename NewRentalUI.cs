using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

// ========================================================
// DENTIFY — NEW RENTAL UI
// Yeni kiralama: Plaka ve müşteri bilgisi giriş formu.
// ========================================================

public class NewRentalUI : MonoBehaviour
{
    [Header("Giriş Alanları")]
    public TMP_InputField plateInput;
    public TMP_InputField customerNameInput;

    [Header("Butonlar")]
    public Button startCaptureButton;
    public Button backButton;

    [Header("Bildirimler")]
    public TMP_Text validationText;         // Hata/uyarı mesajı

    [Header("Referanslar")]
    public MainMenuUI mainMenuUI;
    public GuidedCaptureManager guidedCaptureManager;
    public GuidedCaptureUI guidedCaptureUI;

    void Start()
    {
        if (startCaptureButton != null)
            startCaptureButton.onClick.AddListener(OnStartCapture);

        if (backButton != null)
            backButton.onClick.AddListener(OnBack);

        // Plaka girişinde otomatik büyük harf
        if (plateInput != null)
        {
            plateInput.onValueChanged.AddListener(OnPlateInputChanged);
            plateInput.characterLimit = 10;
        }

        if (customerNameInput != null)
        {
            customerNameInput.characterLimit = 50;
        }

        ClearValidation();
    }

    void OnEnable()
    {
        // Panel her açıldığında formu temizle
        ClearForm();
    }

    // ========================================================
    // FORM İŞLEMLERİ
    // ========================================================

    private void OnStartCapture()
    {
        // Validasyon
        string plate = plateInput != null ? plateInput.text.Trim().ToUpperInvariant() : "";
        string customer = customerNameInput != null ? customerNameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(plate))
        {
            ShowValidation("Lütfen plaka numarasını girin!");
            return;
        }

        if (!IsValidPlate(plate))
        {
            ShowValidation("Geçersiz plaka formatı! Örnek: 34ABC1234");
            return;
        }

        if (string.IsNullOrEmpty(customer))
        {
            ShowValidation("Lütfen müşteri adını girin!");
            return;
        }

        // Oturum oluştur
        RentalSession session = RentalSessionManager.Instance.CreateNewSession(plate, customer);

        if (session == null)
        {
            ShowValidation("Oturum oluşturulamadı!");
            return;
        }

        Debug.Log($"Yeni oturum oluşturuldu: {session.plateNumber} - {session.customerName}");

        // Rehberli çekime geç
        if (guidedCaptureUI != null)
        {
            guidedCaptureUI.StartCapture(session, CapturePhase.TeslimOncesi);
        }

        if (mainMenuUI != null)
        {
            mainMenuUI.OpenGuidedCapture();
        }
    }

    private void OnBack()
    {
        if (mainMenuUI != null)
            mainMenuUI.ShowMainMenu();
    }

    // ========================================================
    // VALİDASYON
    // ========================================================

    /// <summary>
    /// Türkiye plaka formatı kontrolü (basit).
    /// Format: [2 rakam][1-3 harf][1-4 rakam] veya benzer varyasyonlar.
    /// </summary>
    private bool IsValidPlate(string plate)
    {
        // Boşluk ve tireleri temizle
        string clean = Regex.Replace(plate.ToUpperInvariant(), @"[\s\-]", "");

        // En az 5, en fazla 9 karakter
        if (clean.Length < 5 || clean.Length > 9) return false;

        // İlk 2 karakter rakam (il kodu)
        if (!char.IsDigit(clean[0]) || !char.IsDigit(clean[1])) return false;

        // Geri kalanında en az 1 harf ve 1 rakam olmalı
        bool hasLetter = false;
        bool hasDigit = false;
        for (int i = 2; i < clean.Length; i++)
        {
            if (char.IsLetter(clean[i])) hasLetter = true;
            if (char.IsDigit(clean[i])) hasDigit = true;
        }

        return hasLetter && hasDigit;
    }

    private void OnPlateInputChanged(string value)
    {
        // Otomatik büyük harf
        if (plateInput != null && value != value.ToUpperInvariant())
        {
            plateInput.text = value.ToUpperInvariant();
            plateInput.caretPosition = plateInput.text.Length;
        }
        ClearValidation();
    }

    private void ShowValidation(string message)
    {
        if (validationText != null)
        {
            validationText.text = message;
            validationText.gameObject.SetActive(true);
        }
    }

    private void ClearValidation()
    {
        if (validationText != null)
        {
            validationText.text = "";
            validationText.gameObject.SetActive(false);
        }
    }

    private void ClearForm()
    {
        if (plateInput != null) plateInput.text = "";
        if (customerNameInput != null) customerNameInput.text = "";
        ClearValidation();
    }
}
