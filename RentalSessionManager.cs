using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// ========================================================
// DENTIFY — RENTAL SESSION MANAGER
// Tüm kiralama oturumlarının CRUD işlemleri ve dosya I/O.
// Singleton pattern ile tek bir instance çalışır.
// ========================================================

public class RentalSessionManager : MonoBehaviour
{
    public static RentalSessionManager Instance { get; private set; }

    private RentalIndex rentalIndex = new RentalIndex();
    private string rentalsRootPath;     // persistentDataPath/rentals/
    private string indexFilePath;       // persistentDataPath/rentals.json

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        rentalsRootPath = Path.Combine(Application.persistentDataPath, "rentals");
        indexFilePath = Path.Combine(Application.persistentDataPath, "rentals.json");

        // Rentals kök klasörünü oluştur
        if (!Directory.Exists(rentalsRootPath))
        {
            Directory.CreateDirectory(rentalsRootPath);
        }

        LoadIndex();
        Debug.Log($"RentalSessionManager başlatıldı. {rentalIndex.sessions.Count} oturum yüklendi.");
    }

    // ========================================================
    // OTURUM OLUŞTURMA
    // ========================================================

    /// <summary>
    /// Yeni bir kiralama oturumu oluşturur ve klasör yapısını kurar.
    /// </summary>
    public RentalSession CreateNewSession(string plateNumber, string customerName)
    {
        // Plaka temizle (boşluk, tire vs. kaldır, büyük harf yap)
        plateNumber = CleanPlateNumber(plateNumber);

        // Müşteri adını temizle (Türkçe karakter sorunlarını önle)
        string cleanCustomerName = CleanCustomerName(customerName);

        string dateStr = System.DateTime.Now.ToString("yyyyMMdd");
        string folderName = $"{plateNumber}_{cleanCustomerName}_{dateStr}";

        RentalSession session = new RentalSession
        {
            sessionId = System.Guid.NewGuid().ToString(),
            plateNumber = plateNumber,
            customerName = customerName,    // Orijinal adı sakla
            createdDate = System.DateTime.Now.ToString("o"),  // ISO 8601
            returnDate = "",
            status = RentalStatus.TeslimOncesiDevam,
            folderName = folderName
        };

        // Klasör yapısını oluştur
        string sessionPath = GetSessionPath(session);
        CreateSessionDirectories(sessionPath);

        // Session.json dosyasını yaz
        string sessionJsonPath = Path.Combine(sessionPath, "session.json");
        File.WriteAllText(sessionJsonPath, JsonUtility.ToJson(session, true));

        // Ana indekse ekle
        rentalIndex.sessions.Add(session);
        SaveIndex();

        Debug.Log($"Yeni oturum oluşturuldu: {folderName}");
        return session;
    }

    /// <summary>
    /// Oturum klasörü altında teslim_oncesi ve teslim_sonrasi alt dizinlerini oluşturur.
    /// </summary>
    private void CreateSessionDirectories(string sessionPath)
    {
        // Ana klasör
        Directory.CreateDirectory(sessionPath);

        // Teslim öncesi
        Directory.CreateDirectory(Path.Combine(sessionPath, "teslim_oncesi"));
        Directory.CreateDirectory(Path.Combine(sessionPath, "teslim_oncesi", "photos"));
        Directory.CreateDirectory(Path.Combine(sessionPath, "teslim_oncesi", "masks"));

        // Teslim sonrası
        Directory.CreateDirectory(Path.Combine(sessionPath, "teslim_sonrasi"));
        Directory.CreateDirectory(Path.Combine(sessionPath, "teslim_sonrasi", "photos"));
        Directory.CreateDirectory(Path.Combine(sessionPath, "teslim_sonrasi", "masks"));
    }

    // ========================================================
    // ÇEKİM SONUCU KAYDETME
    // ========================================================

    /// <summary>
    /// Bir çekim sonucunu (fotoğraf + maske + tespit verileri) ilgili oturuma kaydeder.
    /// </summary>
    public void SaveCaptureResult(RentalSession session, CapturePhase phase, CaptureResult result,
                                   Texture2D photo, Texture2D maskOverlay)
    {
        string phaseFolderName = phase == CapturePhase.TeslimOncesi ? "teslim_oncesi" : "teslim_sonrasi";
        string sessionPath = GetSessionPath(session);
        string phasePath = Path.Combine(sessionPath, phaseFolderName);

        // 1. Fotoğrafı kaydet
        string photoPath = Path.Combine(phasePath, "photos", result.photoFileName);
        if (photo != null)
        {
            byte[] photoBytes = photo.EncodeToPNG();
            File.WriteAllBytes(photoPath, photoBytes);
        }

        // 2. Maske overlay'ini kaydet
        if (maskOverlay != null && !string.IsNullOrEmpty(result.maskFileName))
        {
            string maskPath = Path.Combine(phasePath, "masks", result.maskFileName);
            byte[] maskBytes = maskOverlay.EncodeToPNG();
            File.WriteAllBytes(maskPath, maskBytes);
        }

        // 3. AI İşaretli (Maskeli + Çerçeveli) Kompozit Fotoğrafı Oluştur ve Kaydet
        if (photo != null)
        {
            Texture2D markedTex = DentifyUtils.CreateAnnotatedPhoto(photo, maskOverlay, result.damages);
            if (markedTex != null)
            {
                string markedFileName = Path.GetFileNameWithoutExtension(result.photoFileName) + "_marked.png";
                string markedPath = Path.Combine(phasePath, "photos", markedFileName);
                byte[] markedBytes = markedTex.EncodeToPNG();
                File.WriteAllBytes(markedPath, markedBytes);
                Object.Destroy(markedTex);
            }
        }

        // 3. Detections JSON'ını güncelle
        string detectionsPath = Path.Combine(phasePath, "detections.json");
        DetectionsData detectionsData;

        if (File.Exists(detectionsPath))
        {
            string existingJson = File.ReadAllText(detectionsPath);
            detectionsData = JsonUtility.FromJson<DetectionsData>(existingJson);
        }
        else
        {
            detectionsData = new DetectionsData
            {
                phase = phaseFolderName,
                sessionId = session.sessionId
            };
        }

        // Aynı açı ID'si varsa güncelle, yoksa ekle
        int existingIndex = detectionsData.captures.FindIndex(c => c.angleId == result.angleId);
        if (existingIndex >= 0)
        {
            detectionsData.captures[existingIndex] = result;
        }
        else
        {
            detectionsData.captures.Add(result);
        }

        File.WriteAllText(detectionsPath, JsonUtility.ToJson(detectionsData, true));

        Debug.Log($"Çekim kaydedildi: {result.angleId} ({phaseFolderName}) - {result.damages.Count} hasar");
    }

    // ========================================================
    // OTURUM DURUMU YÖNETİMİ
    // ========================================================

    /// <summary>
    /// Oturum durumunu günceller.
    /// </summary>
    public void SetSessionStatus(string sessionId, RentalStatus newStatus)
    {
        RentalSession session = GetSession(sessionId);
        if (session == null) return;

        session.status = newStatus;

        // Teslim sonrası başlıyorsa teslim tarihini kaydet
        if (newStatus == RentalStatus.TeslimSonrasiDevam)
        {
            session.returnDate = System.DateTime.Now.ToString("o");
        }

        // session.json'ı güncelle
        string sessionJsonPath = Path.Combine(GetSessionPath(session), "session.json");
        File.WriteAllText(sessionJsonPath, JsonUtility.ToJson(session, true));

        SaveIndex();
        Debug.Log($"Oturum durumu güncellendi: {session.plateNumber} → {newStatus}");
    }

    // ========================================================
    // SORGULAMA
    // ========================================================

    /// <summary>
    /// Tüm oturumları döndürür.
    /// </summary>
    public List<RentalSession> GetAllSessions()
    {
        return new List<RentalSession>(rentalIndex.sessions);
    }

    /// <summary>
    /// Durumu "TeslimBekleniyor" olan (müşteri aracı kullanıyor) oturumları döndürür.
    /// </summary>
    public List<RentalSession> GetActiveRentals()
    {
        return rentalIndex.sessions
            .Where(s => s.status == RentalStatus.TeslimBekleniyor)
            .OrderByDescending(s => s.createdDate)
            .ToList();
    }

    /// <summary>
    /// Tamamlanmış oturumları döndürür (arşiv).
    /// </summary>
    public List<RentalSession> GetCompletedRentals()
    {
        return rentalIndex.sessions
            .Where(s => s.status == RentalStatus.Tamamlandi)
            .OrderByDescending(s => s.returnDate)
            .ToList();
    }

    /// <summary>
    /// Devam eden (çekim aşamasındaki) oturumları döndürür.
    /// </summary>
    public List<RentalSession> GetInProgressRentals()
    {
        return rentalIndex.sessions
            .Where(s => s.status == RentalStatus.TeslimOncesiDevam || s.status == RentalStatus.TeslimSonrasiDevam)
            .OrderByDescending(s => s.createdDate)
            .ToList();
    }

    /// <summary>
    /// ID ile tek bir oturum döndürür.
    /// </summary>
    public RentalSession GetSession(string sessionId)
    {
        return rentalIndex.sessions.Find(s => s.sessionId == sessionId);
    }

    /// <summary>
    /// Plaka veya müşteri adıyla esnek arama yapar.
    /// </summary>
    public List<RentalSession> SearchByPlate(string query)
    {
        if (string.IsNullOrEmpty(query)) return rentalIndex.sessions;

        string cleanQuery = CleanPlateNumber(query);
        string lowerQuery = query.ToLowerInvariant();

        return rentalIndex.sessions
            .Where(s => (cleanQuery.Length > 0 && CleanPlateNumber(s.plateNumber).Contains(cleanQuery)) ||
                        (s.customerName != null && s.customerName.ToLowerInvariant().Contains(lowerQuery)))
            .OrderByDescending(s => s.createdDate)
            .ToList();
    }

    /// <summary>
    /// Bir kiralama oturumunu indeksten ve disk üzerindeki tüm dosyalarıyla siler.
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        RentalSession session = GetSession(sessionId);
        if (session == null) return false;

        try
        {
            // Disk üzerindeki oturum klasörünü ve resimleri sil
            string sessionFolder = GetSessionPath(session);
            if (Directory.Exists(sessionFolder))
            {
                Directory.Delete(sessionFolder, true);
            }

            // Dizinden çıkar ve index.json'ı güncelle (GUID bazlı güvenli silme)
            rentalIndex.sessions.RemoveAll(s => s.sessionId == session.sessionId);
            SaveIndex();

            Debug.Log($"Oturum başarıyla silindi: {session.plateNumber} ({session.sessionId})");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Oturum silinirken hata oluştu: {ex.Message}");
            return false;
        }
    }

    // ========================================================
    // TESPİT VERİLERİNİ YÜKLEME
    // ========================================================

    /// <summary>
    /// Belirli bir faz için detections.json'dan çekim sonuçlarını yükler.
    /// </summary>
    public DetectionsData LoadDetections(RentalSession session, CapturePhase phase)
    {
        string phaseFolderName = phase == CapturePhase.TeslimOncesi ? "teslim_oncesi" : "teslim_sonrasi";
        string detectionsPath = Path.Combine(GetSessionPath(session), phaseFolderName, "detections.json");

        if (File.Exists(detectionsPath))
        {
            string json = File.ReadAllText(detectionsPath);
            return JsonUtility.FromJson<DetectionsData>(json);
        }

        return null;
    }

    /// <summary>
    /// Belirli bir oturum ve fazın fotoğrafını yükler.
    /// </summary>
    public Texture2D LoadPhoto(RentalSession session, CapturePhase phase, string photoFileName)
    {
        string phaseFolderName = phase == CapturePhase.TeslimOncesi ? "teslim_oncesi" : "teslim_sonrasi";
        string photoPath = Path.Combine(GetSessionPath(session), phaseFolderName, "photos", photoFileName);

        if (File.Exists(photoPath))
        {
            byte[] bytes = File.ReadAllBytes(photoPath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            return tex;
        }

        return null;
    }

    // ========================================================
    // KARŞILAŞTIRMA RAPORUNU KAYDETME
    // ========================================================

    /// <summary>
    /// Karşılaştırma raporunu oturum klasörüne kaydeder.
    /// </summary>
    public void SaveComparisonReport(RentalSession session, ComparisonReport report)
    {
        string reportPath = Path.Combine(GetSessionPath(session), "comparison_report.json");
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        Debug.Log($"Karşılaştırma raporu kaydedildi: {session.plateNumber}");
    }

    /// <summary>
    /// Kayıtlı karşılaştırma raporunu yükler.
    /// </summary>
    public ComparisonReport LoadComparisonReport(RentalSession session)
    {
        string reportPath = Path.Combine(GetSessionPath(session), "comparison_report.json");
        if (File.Exists(reportPath))
        {
            string json = File.ReadAllText(reportPath);
            return JsonUtility.FromJson<ComparisonReport>(json);
        }
        return null;
    }

    // ========================================================
    // YARDIMCI METODLAR
    // ========================================================

    /// <summary>
    /// Oturum klasörünün tam yolunu döndürür.
    /// </summary>
    public string GetSessionPath(RentalSession session)
    {
        return Path.Combine(rentalsRootPath, session.folderName);
    }

    /// <summary>
    /// Plaka numarasını temizler: boşluk/tire kaldırır, büyük harfe çevirir.
    /// </summary>
    private string CleanPlateNumber(string plate)
    {
        if (string.IsNullOrEmpty(plate)) return "";
        return Regex.Replace(plate.ToUpperInvariant(), @"[\s\-]", "");
    }

    /// <summary>
    /// Müşteri adını dosya sistemi için temizler (Türkçe karakter → ASCII).
    /// Orijinal isim RentalSession.customerName'de korunur.
    /// </summary>
    private string CleanCustomerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Misafir";

        // Türkçe karakter dönüşümü (klasör adı için)
        name = name.Replace("ç", "c").Replace("Ç", "C")
                   .Replace("ğ", "g").Replace("Ğ", "G")
                   .Replace("ı", "i").Replace("İ", "I")
                   .Replace("ö", "o").Replace("Ö", "O")
                   .Replace("ş", "s").Replace("Ş", "S")
                   .Replace("ü", "u").Replace("Ü", "U");

        // Boşlukları kaldır, sadece harf ve rakam bırak
        name = Regex.Replace(name, @"[^a-zA-Z0-9]", "");

        return string.IsNullOrEmpty(name) ? "Misafir" : name;
    }

    /// <summary>
    /// Belirli bir faz için tamamlanan çekim sayısını döndürür.
    /// </summary>
    public int GetCompletedCaptureCount(RentalSession session, CapturePhase phase)
    {
        DetectionsData data = LoadDetections(session, phase);
        return data != null ? data.captures.Count : 0;
    }

    // ========================================================
    // DOSYA I/O (İNDEKS)
    // ========================================================

    private void SaveIndex()
    {
        string json = JsonUtility.ToJson(rentalIndex, true);
        File.WriteAllText(indexFilePath, json);
    }

    private void LoadIndex()
    {
        if (File.Exists(indexFilePath))
        {
            string json = File.ReadAllText(indexFilePath);
            rentalIndex = JsonUtility.FromJson<RentalIndex>(json);

            if (rentalIndex == null)
            {
                rentalIndex = new RentalIndex();
            }
            Debug.Log($"[Dentify] Indeks dosyasından {rentalIndex.sessions.Count} oturum yüklendi: {indexFilePath}");
        }
        else
        {
            Debug.Log($"[Dentify] Indeks dosyası henüz yok ({indexFilePath}). Yeni kiralama başlatabilirsiniz.");
        }
    }
}
