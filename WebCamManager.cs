using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class WebCamManager : MonoBehaviour
{
    public RawImage cameraFeed;
    public AspectRatioFitter aspectRatioFitter;

    // Modelden tespit cagirabilmek icin
    public DamageDetector damageDetector;

    // Kamera Degistirme ve Fotograf Cekme Ozellikleri
    public Button switchCameraButton;
    public Button captureButton;

    private WebCamTexture webCamTexture;
    private WebCamDevice[] devices;
    private int currentCameraIndex = 0;
    
    // Yeni Durum: Tarama mi yapiyoruz yoksa canli akis mi?
    private bool isScanning = false;

    // --- YENI ZOOM DEGISKENLERI ---
    private float currentZoom = 1f;
    private float minZoom = 1f;
    private float maxZoom = 4f;
    private float initialPinchDistance = 0f;
    private float initialZoom = 1f;

    void AutoLayout()
    {
        // Buton - Sag Ust Kose (Kamera Degistir)
        if (switchCameraButton != null)
        {
            RectTransform rt = switchCameraButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-50, -50);
            rt.sizeDelta = new Vector2(300, 100);

            TMP_Text t = switchCameraButton.GetComponentInChildren<TMP_Text>(); 
            if (t != null) { t.text = "Kamerayi Cevir"; t.fontSize = 28; }      
        }

        // Buton - Alt Orta (Fotograf Cek)
        if (captureButton != null)
        {
            RectTransform rt = captureButton.GetComponent<RectTransform>();     
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 150);
            rt.sizeDelta = new Vector2(400, 120);

            TMP_Text t = captureButton.GetComponentInChildren<TMP_Text>();      
            if (t != null) { t.text = "Fotograf Cek"; t.fontSize = 32; }        
        }
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        AutoLayout();

        devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("Cihazda kamera bulunamadi!");
            return;
        }

        // Telefonda otomatik olarak Arka Kamerayı (Back-Facing Camera) bul ve seç
        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                currentCameraIndex = i;
                break;
            }
        }

        StartCamera(currentCameraIndex);

        if (switchCameraButton != null)
            switchCameraButton.onClick.AddListener(SwitchCamera);
    }

    void StartCamera(int index)
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }

#if UNITY_ANDROID || UNITY_IOS
        // Android telefonlarda arka kamera için Full HD yüksek kalite
        webCamTexture = new WebCamTexture(devices[index].name, 1920, 1080, 30);   
#else
        // Windows masaüstünde CPU darboğazı ve kasma olmaması için akıcı 720p
        webCamTexture = new WebCamTexture(devices[index].name, 1280, 720, 30);   
#endif
        cameraFeed.texture = webCamTexture;
        webCamTexture.Play();

        AdjustCameraAspectAndRotation();
    }

    public bool isCameraEnabled = true;

    public void ToggleCamera()
    {
        isCameraEnabled = !isCameraEnabled;
        if (webCamTexture == null) return;
        if (isCameraEnabled)
        {
            webCamTexture.Play();
        }
        else
        {
            webCamTexture.Pause();
        }
    }

    public bool IsCameraPlaying()
    {
        return webCamTexture != null && isCameraEnabled && webCamTexture.isPlaying;
    }

    public void SwitchCamera()
    {
        if (devices.Length <= 1) return;

        currentCameraIndex = (currentCameraIndex + 1) % devices.Length;
        StartCamera(currentCameraIndex);
    }

    public void CaptureSnapshot()
    {
        if (webCamTexture == null) return;

        if (!isScanning)
        {
            // FOTOGRAF CEK (CANLI YAYINI DONDUR VE ANALIZ ET)
            Texture2D snap = TakeSnapshot();
            if (snap == null) return;

            webCamTexture.Pause(); // Kamerayi ekranda dondur
            isScanning = true;

            // Sadece cektigi an analize baslasin, FPS dusmesin / Isınmasin
            if (damageDetector != null)
                damageDetector.DetectDamage(snap);

            Destroy(snap); // Bellek sızıntısını önle

            if (captureButton != null)
            {
                TMP_Text t = captureButton.GetComponentInChildren<TMP_Text>();
                if (t != null) { t.text = "Yeni Tarama (Kameraya Don)"; }
            }


        }
        else
        {
            ResetCamera();
        }
    }

    // --- KAMERAYI SIFIRLAMA METODU ---
    private void ResetCamera()
    {
        // Galeri texture bellek sızıntısını önle:
        // Eğer cameraFeed'de WebCamTexture değil de galeri resmi varsa yok et
        if (cameraFeed != null && cameraFeed.texture != null && cameraFeed.texture != webCamTexture)
        {
            Destroy(cameraFeed.texture);
        }

        if (webCamTexture != null && !webCamTexture.isPlaying)
        {
            webCamTexture.Play();
        }

        isScanning = false;
        currentZoom = 1f; // Zoom sifirla
        cameraFeed.texture = webCamTexture; // GALERIDEN RESIM KALMASIN

        cameraFeed.rectTransform.localScale = new Vector3(
            Mathf.Sign(cameraFeed.rectTransform.localScale.x), 
            Mathf.Sign(cameraFeed.rectTransform.localScale.y), 1f);

        if (damageDetector != null && damageDetector.damageUI != null)
            damageDetector.damageUI.DrawDamages(new System.Collections.Generic.List<DamageResult>(), 1024, 1024, null);

        if (captureButton != null)
        {
            TMP_Text t = captureButton.GetComponentInChildren<TMP_Text>();
            if (t != null) { t.text = "Fotograf Cek"; }
        }
    }

    void AdjustCameraAspectAndRotation()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
            return;

        int videoRotationAngle = webCamTexture.videoRotationAngle;
        bool isFrontFacing = devices[currentCameraIndex].isFrontFacing;

        // UI Rotasyonunu Ayarla
        cameraFeed.rectTransform.localEulerAngles = new Vector3(0, 0, -videoRotationAngle);

        float videoAspect = (float)webCamTexture.width / webCamTexture.height;  
        bool verticallyRotated = (videoRotationAngle == 90 || videoRotationAngle == 270);

        if (verticallyRotated)
        {
            float flippedAspect = 1f / videoAspect;
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.aspectRatio = flippedAspect;
            }
        }
        else
        {
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.aspectRatio = videoAspect;
            }
        }
        
        // Cihazin yuzu veya arka kamerasina gore aynalama
        float scaleY = webCamTexture.videoVerticallyMirrored ? -1f : 1f;        
        float scaleX = isFrontFacing ? -1f : 1f;

        // Zoom ayarlarini tutarak dondurme ayarlari
        cameraFeed.rectTransform.localScale = new Vector3(currentZoom * scaleX, currentZoom * scaleY, 1f);
    }

    void Update()
    {
        if (webCamTexture != null && webCamTexture.didUpdateThisFrame && !isScanning)
        {
            AdjustCameraAspectAndRotation();
        }

        // --- PINCH TO ZOOM (PARMAKLA YAKINLASTIRMA) ---
        if (!isScanning && Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
            {
                initialPinchDistance = Vector2.Distance(touch1.position, touch2.position);
                initialZoom = currentZoom;
            }
            else if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
            {
                float currentPinchDistance = Vector2.Distance(touch1.position, touch2.position);
                if (Mathf.Approximately(initialPinchDistance, 0)) return;

                float factor = currentPinchDistance / initialPinchDistance;
                currentZoom = Mathf.Clamp(initialZoom * factor, minZoom, maxZoom);

                // Dijital zoom uygula (yon isaretini/aynayi koru)
                if (cameraFeed != null)
                {
                    float signX = cameraFeed.rectTransform.localScale.x < 0 ? -1f : 1f;
                    float signY = cameraFeed.rectTransform.localScale.y < 0 ? -1f : 1f;
                    cameraFeed.rectTransform.localScale = new Vector3(currentZoom * signX, currentZoom * signY, 1f);
                }
            }
        }
    }

    // --- EKRAN GORUNTUSUNU (RAPORU) GALERIYE KAYDET ---
    public void SaveReportToGallery()
    {
        if (!isScanning) return; // Sadece foto donuyorken kaydetilir.
        StartCoroutine(TakeScreenshotAndSave());
    }

    private IEnumerator TakeScreenshotAndSave()
    {
        yield return new WaitForEndOfFrame();

        string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = "Dentify_Rapor_" + timeStamp + ".png";

        ScreenCapture.CaptureScreenshot(fileName);
        Debug.Log("Rapor basariyla kaydedildi: " + fileName);

        if (captureButton != null)
        {
            TMP_Text t = captureButton.GetComponentInChildren<TMP_Text>();
            if (t != null) { t.text = "Galeriye Kaydedildi! (Don)"; }
        }
    }

    // --- GALERIDEN FOTOGRAF YUKLE (NATIVE GALLERY ILE) ---
    public void LoadFromGallery()
    {
        // 1. Kamerayi durdur, foto alinacaginin sinyalini ver
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Pause();
        }
        isScanning = true;
        currentZoom = 1f;

        // 2. Çapraz platform dosya/galeri seçiciyi aç (Windows .exe, Editör ve Android uyumlu)
        NativeFilePicker.PickImage((galleryTexture) =>
        {
            if (galleryTexture != null)
            {
                // 3. Ekrana Resmi Bas
                cameraFeed.texture = galleryTexture;
                cameraFeed.rectTransform.localEulerAngles = Vector3.zero;
                cameraFeed.rectTransform.localScale = Vector3.one;

                if (aspectRatioFitter != null)
                {
                    aspectRatioFitter.aspectRatio = (float)galleryTexture.width / galleryTexture.height;
                }

                // 4. YAPAY ZEKAYI TETIKLE! (Analizi Baslat)
                if (damageDetector != null)
                {
                    damageDetector.DetectDamage(galleryTexture);
                }

                // UI Ayari (Geri Don Butonu Eklendi)
                if (captureButton != null)
                {
                    TMP_Text t = captureButton.GetComponentInChildren<TMP_Text>();
                    if (t != null) { t.text = "Analiz Bitti (Kameraya Don)"; }
                }
            }
            else
            {
                // 5. Kullanici iptal ettiyse kamerayi geri ac!
                ResetCamera();
            }
        }, "Hasarlı Araç Fotoğrafı Seç");

        Debug.Log("Galeri İslemi Baslatildi.");
    }

    private void OnDestroy()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
    }

    // ========================================================
    // PUBLIC ACCESSOR METODLARI (Rehberli Çekim Desteği)
    // GuidedCaptureUI ve diğer manager sınıfları bu metodları kullanır.
    // ========================================================

    /// <summary>
    /// Aktif WebCamTexture referansını döndürür.
    /// </summary>
    public WebCamTexture GetWebCamTexture()
    {
        return webCamTexture;
    }

    /// <summary>
    /// Kameradan anlık snapshot alır (Texture2D kopyası).
    /// Xiaomi Mi 10, Samsung vb. yüksek çözünürlüklü sensörlerde GetPixels bellek donmasını
    /// önlemek için GPU üzerinden RenderTexture ile ultra hızlı ölçeklenir.
    /// </summary>
    public Texture2D TakeSnapshot()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying) return null;

        int targetW = Mathf.Clamp(webCamTexture.width, 320, 1280);
        int targetH = Mathf.Clamp(webCamTexture.height, 240, 720);
        if (webCamTexture.width > 0 && webCamTexture.height > 0)
        {
            float aspect = (float)webCamTexture.width / webCamTexture.height;
            targetH = Mathf.RoundToInt(targetW / aspect);
        }

        RenderTexture rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(webCamTexture, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D snap = new Texture2D(targetW, targetH, TextureFormat.RGB24, false);
        snap.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        snap.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return snap;
    }

    /// <summary>
    /// Kamerayı duraklatır (snapshot sonrası).
    /// </summary>
    public void PauseCamera()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Pause();
        }
    }

    private Texture2D freezeFrameTexture;

    /// <summary>
    /// Ekranın donmasını sağlamak için çekilen fotoğrafı ekrana sabitler.
    /// WebCamTexture durduğunda gri/beyaz olmasını kesin olarak önler.
    /// </summary>
    public void FreezeFrame(Texture2D snapshot)
    {
        UnfreezeFrame();

        if (snapshot != null && cameraFeed != null)
        {
            freezeFrameTexture = snapshot;
            cameraFeed.texture = snapshot;
            cameraFeed.rectTransform.localScale = Vector3.one;
            cameraFeed.rectTransform.localEulerAngles = Vector3.zero;

            if (aspectRatioFitter != null && snapshot.height > 0)
            {
                aspectRatioFitter.aspectRatio = (float)snapshot.width / snapshot.height;
            }
        }
    }

    /// <summary>
    /// Canlı kameraya dönüldüğünde dondurulmuş görüntüyü temizler.
    /// </summary>
    public void UnfreezeFrame()
    {
        if (freezeFrameTexture != null)
        {
            Destroy(freezeFrameTexture);
            freezeFrameTexture = null;
        }

        if (cameraFeed != null && webCamTexture != null)
        {
            cameraFeed.texture = webCamTexture;
        }
    }

    /// <summary>
    /// Kamerayı yeniden başlatır.
    /// </summary>
    public void ResumeCamera()
    {
        UnfreezeFrame();

        if (webCamTexture != null && isCameraEnabled && !webCamTexture.isPlaying)
        {
            webCamTexture.Play();
            cameraFeed.texture = webCamTexture;
        }
        isScanning = false;
    }

    /// <summary>
    /// Tarama durumunu dışarıdan ayarlamak için.
    /// </summary>
    public void SetScanning(bool scanning)
    {
        isScanning = scanning;
    }
}