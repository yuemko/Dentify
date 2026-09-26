using UnityEngine;
using System.Collections.Generic;
using System.IO;

// ========================================================
// DENTIFY — REPORT GENERATOR
// JSON karşılaştırma raporu ve görsel çıktı üretimi.
// ========================================================

public class ReportGenerator : MonoBehaviour
{
    public static ReportGenerator Instance { get; private set; }

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
    // KARŞILAŞTIRMA RAPORU ÜRET VE KAYDET
    // ========================================================

    /// <summary>
    /// Oturumun tam karşılaştırma raporunu üretir, kaydeder ve döndürür.
    /// </summary>
    public ComparisonReport GenerateAndSaveReport(RentalSession session)
    {
        if (session == null)
        {
            Debug.LogError("GenerateAndSaveReport: Oturum null!");
            return null;
        }

        // Karşılaştırmayı yap
        ComparisonReport report = DamageComparer.Instance.CompareSession(session);
        if (report == null)
        {
            Debug.LogError("GenerateAndSaveReport: Karşılaştırma başarısız!");
            return null;
        }

        // Raporu kaydet
        RentalSessionManager.Instance.SaveComparisonReport(session, report);

        // NOT: Oturum durumu GuidedCaptureManager.FinishCurrentPhase() tarafından güncelleniyor.
        // Burada tekrar SetSessionStatus çağırmıyoruz (çift disk I/O önlenir).

        Debug.Log($"Rapor oluşturuldu ve kaydedildi: {session.plateNumber}");
        return report;
    }

    // ========================================================
    // RAPOR ÖZETİ METNİ OLUŞTURMA
    // ========================================================

    /// <summary>
    /// Rapor özetini okunabilir metin olarak döndürür (UI'da gösterilmek için).
    /// </summary>
    public string GenerateSummaryText(ComparisonReport report)
    {
        if (report == null) return "Rapor bulunamadı.";

        string text = "";
        text += $"═══════════════════════════════\n";
        text += $"  DENTIFY HASAR RAPORU\n";
        text += $"═══════════════════════════════\n\n";
        text += $"Plaka: {report.plateNumber}\n";
        text += $"Müşteri: {report.customerName}\n";
        text += $"Kiralama: {FormatDate(report.rentalDate)}\n";
        text += $"Teslim: {FormatDate(report.returnDate)}\n";
        text += $"Rapor: {FormatDate(report.reportDate)}\n\n";

        text += $"-------------------------------\n";
        text += $"  ÖZET\n";
        text += $"-------------------------------\n";
        text += $"[YENİ] Yeni Hasar: {report.totalNewDamages} Adet\n";
        text += $"[ESKİ] Eski Hasar: {report.totalExistingDamages} Adet\n";
        text += $"[ÇÖZÜLEN] Çözülen: {report.totalResolvedDamages} Adet\n\n";

        if (report.totalNewDamages > 0)
        {
            text += $"  YENİ HASAR DETAYI:\n";
            foreach (var cls in report.newDamagesByClass)
            {
                text += $"    - {cls.className}: {cls.count} adet\n";
            }
            text += "\n";
        }

        text += $"───────────────────────────────\n";
        text += $"  AÇI BAZLI DETAY\n";
        text += $"───────────────────────────────\n";

        foreach (var angle in report.angleResults)
        {
            if (angle.newDamages.Count == 0 && angle.existingDamages.Count == 0)
                continue; // Boş açıları atla

            text += $"\n[{angle.angleName}]:\n";

            foreach (var d in angle.newDamages)
            {
                text += $"  [YENİ] {d.className} (%{Mathf.RoundToInt(d.confidence * 100)})\n";
            }

            foreach (var d in angle.existingDamages)
            {
                text += $"  [ESKİ] {d.className} (%{Mathf.RoundToInt(d.confidence * 100)})\n";
            }

            foreach (var d in angle.resolvedDamages)
            {
                text += $"  [ÇÖZÜLEN] {d.className}\n";
            }
        }

        return text;
    }

    /// <summary>
    /// Kısa özet metni (listelemede kullanılır).
    /// </summary>
    public string GenerateShortSummary(ComparisonReport report)
    {
        if (report == null) return "Rapor yok";

        if (report.totalNewDamages == 0)
        {
            return $"Yeni hasar tespit edilmedi ({report.totalExistingDamages} eski hasar)";
        }

        return $"{report.totalNewDamages} Yeni Hasar! ({report.totalExistingDamages} eski)";
    }

    // ========================================================
    // TESLİM ÖNCESİ ÖZET METNİ
    // ========================================================

    /// <summary>
    /// Teslim öncesi çekim sonrası tek faz için özet metin oluşturur.
    /// </summary>
    public string GeneratePhaseSummary(RentalSession session, CapturePhase phase)
    {
        DetectionsData data = RentalSessionManager.Instance.LoadDetections(session, phase);
        if (data == null) return "Veri bulunamadı.";

        int totalDamages = 0;
        Dictionary<string, int> classCounts = new Dictionary<string, int>();

        foreach (var capture in data.captures)
        {
            totalDamages += capture.damages.Count;
            foreach (var d in capture.damages)
            {
                if (classCounts.ContainsKey(d.className))
                    classCounts[d.className]++;
                else
                    classCounts[d.className] = 1;
            }
        }

        string phaseText = phase == CapturePhase.TeslimOncesi ? "TESLİM ÖNCESİ" : "TESLİM SONRASI";
        string text = $"{phaseText} ÖZET\n";
        text += $"Çekim Sayısı: {data.captures.Count}\n";
        text += $"Toplam Hasar: {totalDamages} Adet\n";

        if (classCounts.Count > 0)
        {
            text += "\nHasar Dağılımı:\n";
            foreach (var kvp in classCounts)
            {
                text += $"  - {kvp.Key}: {kvp.Value}\n";
            }
        }
        else
        {
            text += "\nHasar tespit edilmedi.\n";
        }

        return text;
    }

    // ========================================================
    // YARDIMCI
    // ========================================================

    /// <summary>
    /// ISO 8601 tarih stringini okunabilir formata çevirir.
    /// </summary>
    private string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "—";

        if (System.DateTime.TryParse(isoDate, out System.DateTime dt))
        {
            return dt.ToString("dd.MM.yyyy HH:mm");
        }
        return isoDate;
    }

    // ========================================================
    // HTML / PDF RAPOR DIŞA AKTARMA
    // ========================================================

    /// <summary>
    /// Karşılaştırma raporunu şık bir HTML / PDF olarak kaydeder ve yolunu döndürür.
    /// </summary>
    public string ExportReportToHTML(RentalSession session, ComparisonReport report)
    {
        if (session == null || report == null) return null;

        string sessionFolder = RentalSessionManager.Instance.GetSessionPath(session);
        string htmlPath = Path.Combine(sessionFolder, $"Hasar_Raporu_{session.plateNumber}.html");

        string html = $@"<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Dentify Hasar Raporu - {report.plateNumber}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0f172a; color: #f8fafc; margin: 0; padding: 20px; }}
        .container {{ max-width: 900px; margin: 0 auto; background: #1e293b; border-radius: 12px; padding: 30px; box-shadow: 0 10px 25px rgba(0,0,0,0.5); border: 1px solid #334155; }}
        .header {{ display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #334155; padding-bottom: 20px; margin-bottom: 25px; }}
        .brand {{ font-size: 32px; font-weight: 800; color: #38bdf8; letter-spacing: 2px; }}
        .badge {{ background: #0284c7; color: white; padding: 6px 12px; border-radius: 20px; font-size: 14px; font-weight: 600; }}
        .info-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; background: #0f172a; padding: 20px; border-radius: 8px; margin-bottom: 25px; }}
        .info-item span {{ color: #94a3b8; font-size: 13px; display: block; }}
        .info-item strong {{ color: #f8fafc; font-size: 16px; }}
        .stats-grid {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 15px; margin-bottom: 30px; }}
        .stat-card {{ padding: 20px; border-radius: 10px; text-align: center; }}
        .stat-new {{ background: rgba(239, 68, 68, 0.15); border: 1px solid #ef4444; color: #fca5a5; }}
        .stat-exist {{ background: rgba(148, 163, 184, 0.15); border: 1px solid #64748b; color: #cbd5e1; }}
        .stat-resolved {{ background: rgba(34, 197, 94, 0.15); border: 1px solid #22c55e; color: #86efac; }}
        .stat-val {{ font-size: 36px; font-weight: 800; margin-bottom: 5px; }}
        .angle-title {{ font-size: 20px; font-weight: 700; color: #38bdf8; margin-top: 25px; margin-bottom: 12px; border-left: 4px solid #38bdf8; padding-left: 10px; }}
        .damage-tag {{ display: inline-block; padding: 6px 12px; border-radius: 6px; font-size: 13px; font-weight: 600; margin-right: 6px; margin-bottom: 6px; }}
        .tag-new {{ background: #ef4444; color: white; }}
        .tag-exist {{ background: #64748b; color: white; }}
        .tag-resolved {{ background: #22c55e; color: white; }}
        .footer {{ margin-top: 40px; text-align: center; font-size: 13px; color: #64748b; border-top: 1px solid #334155; padding-top: 20px; }}
        @media print {{ body {{ background: white; color: black; }} .container {{ box-shadow: none; border: none; background: white; }} }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div>
                <div class=""brand"">DENTIFY</div>
                <div style=""color: #94a3b8; font-size: 14px;"">Yapay Zeka Destekli Araç Hasar Tespit Raporu</div>
            </div>
            <div class=""badge"">KOSGEB / Teknokent Raporu</div>
        </div>

        <div class=""info-grid"">
            <div class=""info-item""><span>PLAKA</span><strong>{report.plateNumber}</strong></div>
            <div class=""info-item""><span>MÜŞTERİ ADI SOYADI</span><strong>{report.customerName}</strong></div>
            <div class=""info-item""><span>TESLİM ÖNCESİ ÇEKİM TARİHİ</span><strong>{FormatDate(report.rentalDate)}</strong></div>
            <div class=""info-item""><span>TESLİM SONRASI ÇEKİM TARİHİ</span><strong>{FormatDate(report.returnDate)}</strong></div>
        </div>

        <div class=""stats-grid"">
            <div class=""stat-card stat-new"">
                <div class=""stat-val"">{report.totalNewDamages}</div>
                <div>YENİ HASAR</div>
            </div>
            <div class=""stat-card stat-exist"">
                <div class=""stat-val"">{report.totalExistingDamages}</div>
                <div>ESKİ HASAR</div>
            </div>
            <div class=""stat-card stat-resolved"">
                <div class=""stat-val"">{report.totalResolvedDamages}</div>
                <div>ÇÖZÜLEN / ONARILAN</div>
            </div>
        </div>

        <h3 style=""color: #f8fafc; border-bottom: 1px solid #334155; padding-bottom: 10px;"">AÇI BAZLI DETAYLI HASAR LİSTESİ</h3>
";

        DetectionsData beforeData = RentalSessionManager.Instance.LoadDetections(session, CapturePhase.TeslimOncesi);
        DetectionsData afterData = RentalSessionManager.Instance.LoadDetections(session, CapturePhase.TeslimSonrasi);

        foreach (var angle in report.angleResults)
        {
            if (angle.newDamages.Count == 0 && angle.existingDamages.Count == 0 && angle.resolvedDamages.Count == 0)
                continue;

            html += $@"<div style=""background: #0f172a; border-radius: 10px; padding: 20px; margin-bottom: 20px; border: 1px solid #334155;"">";
            html += $@"<div class=""angle-title"" style=""margin-top:0;"">{angle.angleName}</div>";

            html += $@"<div style=""margin-bottom: 12px;"">";
            foreach (var d in angle.newDamages)
            {
                html += $@"<span class=""damage-tag tag-new"">[YENİ HASAR] {d.className} (%{Mathf.RoundToInt(d.confidence * 100)})</span>";
            }
            foreach (var d in angle.existingDamages)
            {
                html += $@"<span class=""damage-tag tag-exist"">[ESKİ HASAR] {d.className} (%{Mathf.RoundToInt(d.confidence * 100)})</span>";
            }
            foreach (var d in angle.resolvedDamages)
            {
                html += $@"<span class=""damage-tag tag-resolved"">[GİDERİLEN HASAR] {d.className}</span>";
            }
            html += $@"</div>";

            // Fotoğraf galerisini ekle (Öncelikli olarak AI İşaretli/Maskeli fotoğraf gösterilir)
            CaptureResult beforeCap = beforeData?.captures?.Find(c => c.angleId == angle.angleId || c.angleName == angle.angleName);
            CaptureResult afterCap = afterData?.captures?.Find(c => c.angleId == angle.angleId || c.angleName == angle.angleName);

            string beforeMarkedRel = beforeCap != null ? $"teslim_oncesi/photos/{Path.GetFileNameWithoutExtension(beforeCap.photoFileName)}_marked.png" : null;
            string beforeRawRel = beforeCap != null ? $"teslim_oncesi/photos/{beforeCap.photoFileName}" : null;
            string beforeImgPath = (beforeMarkedRel != null && File.Exists(Path.Combine(sessionFolder, beforeMarkedRel))) ? beforeMarkedRel : beforeRawRel;

            string afterMarkedRel = afterCap != null ? $"teslim_sonrasi/photos/{Path.GetFileNameWithoutExtension(afterCap.photoFileName)}_marked.png" : null;
            string afterRawRel = afterCap != null ? $"teslim_sonrasi/photos/{afterCap.photoFileName}" : null;
            string afterImgPath = (afterMarkedRel != null && File.Exists(Path.Combine(sessionFolder, afterMarkedRel))) ? afterMarkedRel : afterRawRel;

            if (beforeImgPath != null || afterImgPath != null)
            {
                html += $@"<div style=""display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 15px; margin-top: 15px;"">";
                if (beforeImgPath != null)
                {
                    string labelExtra = beforeImgPath.Contains("_marked") ? " (YAPAY ZEKA TESPİT HARİTASI)" : "";
                    html += $@"<div style=""background: #1e293b; padding: 10px; border-radius: 8px; border: 1px solid #334155; text-align: center;"">
                        <div style=""color: #94a3b8; font-size: 13px; font-weight: bold; margin-bottom: 8px;"">📸 TESLİM ÖNCESİ FOTOĞRAF{labelExtra}</div>
                        <img src=""{beforeImgPath}"" style=""max-width: 100%; height: auto; border-radius: 6px; border: 1px solid #475569;"" />
                    </div>";
                }
                if (afterImgPath != null)
                {
                    string labelExtra = afterImgPath.Contains("_marked") ? " (YAPAY ZEKA TESPİT HARİTASI)" : "";
                    html += $@"<div style=""background: #1e293b; padding: 10px; border-radius: 8px; border: 1px solid #334155; text-align: center;"">
                        <div style=""color: #38bdf8; font-size: 13px; font-weight: bold; margin-bottom: 8px;"">📸 TESLİM SONRASI FOTOĞRAF{labelExtra}</div>
                        <img src=""{afterImgPath}"" style=""max-width: 100%; height: auto; border-radius: 6px; border: 1px solid #475569;"" />
                    </div>";
                }
                html += $@"</div>";
            }

            html += $@"</div>";
        }

        html += $@"
        <!-- İMZA VE ONAY ALANI -->
        <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-top: 40px; padding-top: 20px; border-top: 2px dashed #334155;"">
            <div style=""background: #0f172a; padding: 15px; border-radius: 8px; text-align: center; border: 1px solid #334155;"">
                <div style=""color: #94a3b8; font-size: 13px; font-weight: bold; margin-bottom: 35px;"">KİRALAYAN FİRMA ONAYI / İMZA</div>
                <div style=""color: #64748b; font-size: 12px;"">Dentify Yapay Zeka Onaylı</div>
            </div>
            <div style=""background: #0f172a; padding: 15px; border-radius: 8px; text-align: center; border: 1px solid #334155;"">
                <div style=""color: #94a3b8; font-size: 13px; font-weight: bold; margin-bottom: 35px;"">MÜŞTERİ / TESLİM ALAN İMZA</div>
                <div style=""color: #64748b; font-size: 12px;"">{report.customerName}</div>
            </div>
        </div>

        <div class=""footer"">
            Dentify Edge AI Araç Hasar Tespit Sistemi &bull; Bu rapor yapay zeka tarafından otomatik oluşturulmuştur.
        </div>
    </div>
</body>
</html>";

        File.WriteAllText(htmlPath, html);
        Debug.Log($"HTML/PDF Raporu kaydedildi: {htmlPath}");
        return htmlPath;
    }
}
