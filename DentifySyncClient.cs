using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// ========================================================
// DENTIFY — MOBIL TO WEB SENKRONIZASYON ISTEMCISI
// Unity oturumunu, 8 açılı fotoğrafları ve hasar koordinatlarını
// tek bir paket olarak Web Paneline aktarır.
// ========================================================

public class DentifySyncClient : MonoBehaviour
{
    public static DentifySyncClient Instance { get; private set; }

    [Header("Sunucu Yapılandırması")]
    [Tooltip("Web Dashboard API URL'si")]
    public string serverUrl = "http://localhost:3000/api/sync";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Bir kiralama oturumunu, tüm fotoğraflarını ve raporunu web paneline senkronize eder.
    /// </summary>
    public void SyncSession(RentalSession session, ComparisonReport report, Action<bool, string> onComplete = null)
    {
        StartCoroutine(SendSyncRequest(session, report, onComplete));
    }

    private IEnumerator SendSyncRequest(RentalSession session, ComparisonReport report, Action<bool, string> onComplete)
    {
        if (session == null)
        {
            onComplete?.Invoke(false, "Oturum verisi boş.");
            yield break;
        }

        string sessionFolder = RentalSessionManager.Instance != null 
            ? RentalSessionManager.Instance.GetSessionPath(session) 
            : "";

        WebSyncPayload payload = new WebSyncPayload
        {
            id = session.sessionId,
            plateNumber = session.plateNumber,
            customerName = session.customerName,
            status = session.status.ToString().ToLower(),
            createdDate = session.createdDate,
            returnDate = session.returnDate,
            captures = new List<WebCaptureItem>()
        };

        if (report != null)
        {
            payload.report = new WebReportItem
            {
                totalNew = report.totalNewDamages,
                totalExisting = report.totalExistingDamages,
                totalResolved = report.totalResolvedDamages,
                reportDate = report.reportDate
            };
        }

        // Teslim Öncesi ve Teslim Sonrası çekimlerini yükle
        if (RentalSessionManager.Instance != null && !string.IsNullOrEmpty(sessionFolder))
        {
            DetectionsData beforeData = RentalSessionManager.Instance.LoadDetections(session, CapturePhase.TeslimOncesi);
            DetectionsData afterData = RentalSessionManager.Instance.LoadDetections(session, CapturePhase.TeslimSonrasi);

            AppendCaptures(payload.captures, beforeData, sessionFolder, "teslim_oncesi");
            AppendCaptures(payload.captures, afterData, sessionFolder, "teslim_sonrasi");
        }

        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[DentifySync] Web paneline aktarılıyor: {session.plateNumber} ({payload.captures.Count} çekim) -> {serverUrl}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[DentifySync] Aktarım Başarılı: {req.downloadHandler.text}");
                onComplete?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[DentifySync] Aktarım Hatası: {req.error}");
                onComplete?.Invoke(false, req.error);
            }
        }
    }

    private void AppendCaptures(List<WebCaptureItem> targetList, DetectionsData data, string sessionFolder, string phase)
    {
        if (data == null || data.captures == null) return;

        foreach (var cap in data.captures)
        {
            WebCaptureItem item = new WebCaptureItem
            {
                phase = phase,
                angleId = cap.angleId,
                angleName = cap.angleName,
                timestamp = cap.timestamp,
                damages = new List<WebDamageItem>()
            };

            // Hasarları kopyala
            if (cap.damages != null)
            {
                foreach (var d in cap.damages)
                {
                    item.damages.Add(new WebDamageItem
                    {
                        classId = d.classId,
                        className = d.className,
                        confidence = d.confidence,
                        centerX = d.centerX,
                        centerY = d.centerY,
                        width = d.width,
                        height = d.height
                    });
                }
            }

            // Fotoğrafı Base64 olarak oku (Öncelikli olarak marked_path, yoksa raw photo)
            string photoRelPath = $"{phase}/photos/{cap.photoFileName}";
            string markedRelPath = $"{phase}/photos/{Path.GetFileNameWithoutExtension(cap.photoFileName)}_marked.png";
            
            string fullMarkedPath = Path.Combine(sessionFolder, markedRelPath);
            string fullRawPath = Path.Combine(sessionFolder, photoRelPath);

            string fileToRead = File.Exists(fullMarkedPath) ? fullMarkedPath : (File.Exists(fullRawPath) ? fullRawPath : null);

            if (fileToRead != null)
            {
                try
                {
                    byte[] imgBytes = File.ReadAllBytes(fileToRead);
                    item.imageBase64 = Convert.ToBase64String(imgBytes);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DentifySync] Fotoğraf okunamadı: {fileToRead} - {e.Message}");
                }
            }

            targetList.Add(item);
        }
    }

    [System.Serializable]
    private class WebSyncPayload
    {
        public string id;
        public string plateNumber;
        public string customerName;
        public string status;
        public string createdDate;
        public string returnDate;
        public List<WebCaptureItem> captures;
        public WebReportItem report;
    }

    [System.Serializable]
    private class WebCaptureItem
    {
        public string phase;
        public string angleId;
        public string angleName;
        public string timestamp;
        public string imageBase64;
        public List<WebDamageItem> damages;
    }

    [System.Serializable]
    private class WebDamageItem
    {
        public int classId;
        public string className;
        public float confidence;
        public float centerX;
        public float centerY;
        public float width;
        public float height;
    }

    [System.Serializable]
    private class WebReportItem
    {
        public int totalNew;
        public int totalExisting;
        public int totalResolved;
        public string reportDate;
    }
}
