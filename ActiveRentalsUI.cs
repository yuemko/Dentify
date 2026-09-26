using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// ========================================================
// DENTIFY — ACTIVE RENTALS UI
// Teslim bekleyen (müşteride olan) araçların listesi.
// Buradan "Teslim Sonrası Çekimi Başlat" tetiklenir.
// ========================================================

public class ActiveRentalsUI : MonoBehaviour
{
    [Header("UI Elemanları")]
    public Transform contentArea;           // ScrollView > Content
    public GameObject rentalItemPrefab;     // Liste satırı prefab'ı
    public TMP_InputField searchInput;      // Plaka ile arama
    public TMP_Text emptyStateText;        // "Aktif kiralama yok" mesajı
    public Button backButton;

    [Header("Referanslar")]
    public MainMenuUI mainMenuUI;
    public GuidedCaptureUI guidedCaptureUI;

    private List<RentalSession> displayedSessions = new List<RentalSession>();

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);

        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchChanged);
    }

    void OnEnable()
    {
        if (searchInput != null) searchInput.text = "";
        RefreshList();
    }

    // ========================================================
    // LİSTE YÖNETİMİ
    // ========================================================

    /// <summary>
    /// Listeyi aktif kiralamaları gösterecek şekilde yeniler.
    /// </summary>
    public void RefreshList()
    {
        if (RentalSessionManager.Instance == null) return;

        List<RentalSession> allSessions = RentalSessionManager.Instance.GetAllSessions();
        displayedSessions.Clear();

        // Tamamlanmamış tüm oturumları göster (TeslimBekleniyor, TeslimOncesiDevam, TeslimSonrasiDevam)
        foreach (var s in allSessions)
        {
            if (s.status != RentalStatus.Tamamlandi && !displayedSessions.Contains(s))
            {
                displayedSessions.Add(s);
            }
        }

        RenderList(displayedSessions);
    }

    /// <summary>
    /// Filtrelenmiş listeyi render eder.
    /// </summary>
    private void RenderList(List<RentalSession> sessions)
    {
        Debug.Log($"[ActiveRentalsUI] RenderList çağrıldı. Gösterilecek oturum sayısı: {sessions.Count}");

        // Eski satırları temizle
        if (contentArea != null)
        {
            foreach (Transform child in contentArea)
            {
                Destroy(child.gameObject);
            }
        }
        else
        {
            Debug.LogError("[ActiveRentalsUI] contentArea NULL! Dentify > Setup Scene çalıştırın.");
        }

        // Prefab kontrolü & otomatik yükleme fallback
        if (rentalItemPrefab == null)
        {
            #if UNITY_EDITOR
            rentalItemPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RentalItemPrefab.prefab");
            #endif
        }

        if (rentalItemPrefab == null)
        {
            Debug.LogError("[ActiveRentalsUI] rentalItemPrefab NULL!");
        }

        // Boş durum kontrolü
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(sessions.Count == 0);
            emptyStateText.text = "Aktif kiralama bulunamadı.\nYeni kiralama başlatmak için ana menüye dönün.";
        }

        if (contentArea == null || rentalItemPrefab == null) return;

        // Her oturum için satır oluştur
        foreach (var session in sessions)
        {
            GameObject item = Instantiate(rentalItemPrefab, contentArea);

            // Plaka metni
            TMP_Text plateText = FindChildText(item, "PlateText");
            if (plateText != null)
            {
                plateText.text = FormatPlate(session.plateNumber);
            }

            // Müşteri adı
            TMP_Text customerText = FindChildText(item, "CustomerText");
            if (customerText != null)
            {
                customerText.text = session.customerName;
            }

            // Tarih
            TMP_Text dateText = FindChildText(item, "DateText");
            if (dateText != null)
            {
                dateText.text = FormatDate(session.createdDate);
            }

            // Durum badge'i
            TMP_Text statusText = FindChildText(item, "StatusText");
            if (statusText != null)
            {
                statusText.text = GetStatusText(session.status);
            }

            // Teslim Al / Çekime Devam Et butonu
            Transform actionBtnTransform = item.transform.Find("ButtonContainer/ActionButton");
            if (actionBtnTransform == null) actionBtnTransform = item.transform.Find("ActionButton");
            if (actionBtnTransform != null)
            {
                Button actionButton = actionBtnTransform.GetComponent<Button>();
                if (actionButton != null)
                {
                    string sid = session.sessionId;
                    actionButton.onClick.AddListener(() => OnStartReturn(sid));

                    TMP_Text btnText = actionButton.GetComponentInChildren<TMP_Text>();
                    if (btnText != null)
                    {
                        if (session.status == RentalStatus.TeslimSonrasiDevam)
                            btnText.text = "Çekime Devam Et";
                        else
                            btnText.text = "Teslim Al";
                    }
                }
            }

            // Sil butonu (2 Adımlı Güvenli Onay)
            Transform delBtnTransform = item.transform.Find("ButtonContainer/DeleteButton");
            if (delBtnTransform != null)
            {
                Button delBtn = delBtnTransform.GetComponent<Button>();
                TMP_Text delText = delBtn != null ? delBtn.GetComponentInChildren<TMP_Text>() : null;
                Image delImg = delBtn != null ? delBtn.GetComponent<Image>() : null;

                if (delBtn != null)
                {
                    string sid = session.sessionId;
                    bool isPendingConfirm = false;

                    delBtn.onClick.AddListener(() =>
                    {
                        if (!isPendingConfirm)
                        {
                            isPendingConfirm = true;
                            if (delText != null) delText.text = "Emin msn?";
                            if (delImg != null) delImg.color = new Color(0.95f, 0.15f, 0.15f, 1f);
                            StartCoroutine(ResetDeleteButton(delText, delImg, () => isPendingConfirm = false));
                        }
                        else
                        {
                            OnDeleteSession(sid);
                        }
                    });
                }
            }
        }
    }

    // ========================================================
    // İŞLEMLER
    // ========================================================

    private System.Collections.IEnumerator ResetDeleteButton(TMP_Text text, Image img, System.Action resetCallback)
    {
        yield return new WaitForSeconds(3.0f);
        if (text != null) text.text = "Sil";
        if (img != null) img.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        resetCallback?.Invoke();
    }

    private void OnDeleteSession(string sessionId)
    {
        if (RentalSessionManager.Instance != null)
        {
            RentalSessionManager.Instance.DeleteSession(sessionId);
            RefreshList();
            if (mainMenuUI != null)
            {
                mainMenuUI.ShowMainMenu();
                mainMenuUI.OpenActiveRentals();
            }
        }
    }

    /// <summary>
    /// Teslim sonrası çekimi başlatır.
    /// </summary>
    private void OnStartReturn(string sessionId)
    {
        RentalSession session = RentalSessionManager.Instance.GetSession(sessionId);
        if (session == null) return;

        // Oturum durumunu güncelle
        if (session.status == RentalStatus.TeslimBekleniyor)
        {
            RentalSessionManager.Instance.SetSessionStatus(sessionId, RentalStatus.TeslimSonrasiDevam);
        }

        // Rehberli çekime geç
        if (guidedCaptureUI != null)
        {
            guidedCaptureUI.StartCapture(session, CapturePhase.TeslimSonrasi);
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

    private void OnSearchChanged(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            RefreshList();
            return;
        }

        List<RentalSession> filtered = RentalSessionManager.Instance.SearchByPlate(query);
        // Sadece aktif/devam edenleri filtrele (tamamlananlar arşive gider)
        filtered = filtered.FindAll(s => s.status != RentalStatus.Tamamlandi);

        RenderList(filtered);
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

    private string GetStatusText(RentalStatus status)
    {
        switch (status)
        {
            case RentalStatus.TeslimOncesiDevam: return "Çekim Devam";
            case RentalStatus.TeslimBekleniyor: return "Müşteride";
            case RentalStatus.TeslimSonrasiDevam: return "Teslim Çekimi";
            case RentalStatus.Tamamlandi: return "Tamamlandı";
            default: return "?";
        }
    }

    private string FormatPlate(string plate)
    {
        return DentifyUtils.FormatPlate(plate);
    }

    private string FormatDate(string isoDate)
    {
        return DentifyUtils.FormatDate(isoDate);
    }
}
