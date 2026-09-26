// ========================================================
// DENTIFY — ORTAK YARDIMCI METODLAR
// FormatPlate ve FormatDate gibi tekrarlanan metodların
// tek bir yerde tutulması. DRY prensibi.
// ========================================================

public static class DentifyUtils
{
    /// <summary>
    /// "34ABC1234" → "34 ABC 1234" şeklinde güzel plaka formatı.
    /// </summary>
    public static string FormatPlate(string plate)
    {
        if (string.IsNullOrEmpty(plate) || plate.Length < 5) return plate ?? "";

        // İlk 2 rakam (il kodu)
        string ilKodu = plate.Substring(0, 2);
        string rest = plate.Substring(2);

        // Harfleri ve rakamları ayır
        string harfler = "";
        string sonRakamlar = "";
        bool harfBitti = false;

        foreach (char c in rest)
        {
            if (!harfBitti && char.IsLetter(c))
            {
                harfler += c;
            }
            else
            {
                harfBitti = true;
                sonRakamlar += c;
            }
        }

        return $"{ilKodu} {harfler} {sonRakamlar}".Trim();
    }

    /// <summary>
    /// ISO 8601 tarih stringini "dd.MM.yyyy" formatına çevirir.
    /// </summary>
    public static string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "—";
        if (System.DateTime.TryParse(isoDate, out System.DateTime dt))
        {
            return dt.ToString("dd.MM.yyyy");
        }
        return isoDate;
    }

    /// <summary>
    /// Orijinal fotoğrafın üzerine AI maskesini ve bounding box kutularını basarak
    /// raporda gösterilecek "İşaretli / Maskeli Fotoğraf" oluşturur.
    /// </summary>
    public static UnityEngine.Texture2D CreateAnnotatedPhoto(UnityEngine.Texture2D photo, UnityEngine.Texture2D maskOverlay, System.Collections.Generic.List<DetectedDamage> damages)
    {
        if (photo == null) return null;

        int width = photo.width;
        int height = photo.height;

        UnityEngine.Texture2D annotated = new UnityEngine.Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false);

        UnityEngine.Color[] photoPixels;
        try
        {
            photoPixels = photo.GetPixels();
        }
        catch
        {
            // Eğer texture readable değilse RenderTexture üzerinden kopyala
            UnityEngine.RenderTexture rt = UnityEngine.RenderTexture.GetTemporary(width, height, 0, UnityEngine.RenderTextureFormat.ARGB32);
            UnityEngine.Graphics.Blit(photo, rt);
            UnityEngine.RenderTexture prev = UnityEngine.RenderTexture.active;
            UnityEngine.RenderTexture.active = rt;
            annotated.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
            annotated.Apply();
            UnityEngine.RenderTexture.active = prev;
            UnityEngine.RenderTexture.ReleaseTemporary(rt);
            photoPixels = annotated.GetPixels();
        }

        // 1. Mask overlay'ini ve kutuları sadece hasarlı alanlar üzerinde harmanla (Tam ekran 2 milyon piksel CPU döngüsünü engeller)
        if (damages != null && damages.Count > 0 && maskOverlay != null)
        {
            UnityEngine.Color[] maskPixels = maskOverlay.GetPixels();
            int maskW = maskOverlay.width;
            int maskH = maskOverlay.height;

            foreach (var damage in damages)
            {
                int bX1 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((damage.centerX - damage.width / 2f) / 1024f * width), 0, width - 1);
                int bY1 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((1f - (damage.centerY + damage.height / 2f) / 1024f) * height), 0, height - 1);
                int bX2 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((damage.centerX + damage.width / 2f) / 1024f * width), 0, width - 1);
                int bY2 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((1f - (damage.centerY - damage.height / 2f) / 1024f) * height), 0, height - 1);

                for (int y = bY1; y <= bY2; y++)
                {
                    int maskY = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.FloorToInt((float)y / height * maskH), 0, maskH - 1);
                    int rowOffset = y * width;

                    for (int x = bX1; x <= bX2; x++)
                    {
                        int maskX = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.FloorToInt((float)x / width * maskW), 0, maskW - 1);
                        UnityEngine.Color mc = maskPixels[maskY * maskW + maskX];

                        if (mc.a > 0.05f)
                        {
                            int pxIdx = rowOffset + x;
                            UnityEngine.Color pc = photoPixels[pxIdx];
                            float alpha = UnityEngine.Mathf.Clamp(mc.a * 1.4f, 0.50f, 0.70f);
                            photoPixels[pxIdx] = new UnityEngine.Color(
                                pc.r * (1f - alpha) + mc.r * alpha,
                                pc.g * (1f - alpha) + mc.g * alpha,
                                pc.b * (1f - alpha) + mc.b * alpha,
                                1f
                            );
                        }
                    }
                }
            }
        }

        // 2. Şık Çerçeve (Bounding Box) ve Sınıf/Doğruluk Etiket Rozetlerini Çiz
        if (damages != null && damages.Count > 0)
        {
            int thickness = UnityEngine.Mathf.Max(2, width / 450); // Zarif ve ince 2-3px çerçeve
            int fontScale = UnityEngine.Mathf.Max(1, width / 700);  // Fotoğraf çözünürlüğüne göre font ölçeği

            foreach (var damage in damages)
            {
                // Sınıfa ve doğruluk oranına göre modern neon renk seçimi
                UnityEngine.Color boxColor = damage.confidence >= 0.80f 
                    ? new UnityEngine.Color(1f, 0.2f, 0.2f, 1f)     // Parlak Kırmızı (Yüksek Güven / Yeni)
                    : new UnityEngine.Color(1f, 0.82f, 0.1f, 1f);   // Canlı Altın Sarısı

                // YOLO normalize koordinatlar → Piksel koordinatları
                int boxX1 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((damage.centerX - damage.width / 2f) / 1024f * width), 0, width - 1);
                int boxY1 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((1f - (damage.centerY + damage.height / 2f) / 1024f) * height), 0, height - 1);
                int boxX2 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((damage.centerX + damage.width / 2f) / 1024f * width), 0, width - 1);
                int boxY2 = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt((1f - (damage.centerY - damage.height / 2f) / 1024f) * height), 0, height - 1);

                // A. İnce ve Şık Dikdörtgen Çerçeve
                for (int y = boxY1; y <= boxY2; y++)
                {
                    for (int x = boxX1; x <= boxX2; x++)
                    {
                        bool isBorder = (x < boxX1 + thickness || x > boxX2 - thickness || y < boxY1 + thickness || y > boxY2 - thickness);
                        if (isBorder)
                        {
                            photoPixels[y * width + x] = boxColor;
                        }
                    }
                }

                // B. Sınıf Adı ve Yüzde Metnini Hazırla
                string cleanClassName = NormalizeTurkishToAscii(damage.className).ToUpperInvariant();
                string labelText = $"{cleanClassName} %{UnityEngine.Mathf.RoundToInt(damage.confidence * 100)}";

                // C. Etiket Arka Plan Rozeti (Pill Badge)
                int charW = 6 * fontScale;
                int charH = 7 * fontScale;
                int padX = 4 * fontScale;
                int padY = 3 * fontScale;
                int pillW = labelText.Length * charW + padX * 2;
                int pillH = charH + padY * 2;

                int pillX1 = boxX1;
                int pillY1 = UnityEngine.Mathf.Clamp(boxY2 - pillH, 0, height - pillH);
                int pillX2 = UnityEngine.Mathf.Clamp(pillX1 + pillW, 0, width - 1);
                int pillY2 = UnityEngine.Mathf.Clamp(boxY2, 0, height - 1);

                // Rozet arka planı doldur (Koyu lacivert-siyah ve kenarlık)
                UnityEngine.Color pillBg = new UnityEngine.Color(0.08f, 0.11f, 0.18f, 0.92f);
                for (int py = pillY1; py <= pillY2; py++)
                {
                    for (int px = pillX1; px <= pillX2; px++)
                    {
                        bool isPillBorder = (px == pillX1 || px == pillX2 || py == pillY1 || py == pillY2);
                        photoPixels[py * width + px] = isPillBorder ? boxColor : pillBg;
                    }
                }

                // D. Etiket Metnini Rozet İçine Yaz (Beyaz Renkli)
                DrawBitmapText(photoPixels, width, height, labelText, pillX1 + padX, pillY1 + padY, fontScale, UnityEngine.Color.white);
            }
        }

        annotated.SetPixels(photoPixels);
        annotated.Apply();
        return annotated;
    }

    /// <summary>
    /// Türkçe karakterleri dahili font için ASCII karşılıklarına çevirir.
    /// </summary>
    private static string NormalizeTurkishToAscii(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("ç", "c").Replace("Ç", "C")
            .Replace("ğ", "g").Replace("Ğ", "G")
            .Replace("ı", "i").Replace("İ", "I")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ş", "s").Replace("Ş", "S")
            .Replace("ü", "u").Replace("Ü", "U");
    }

    /// <summary>
    /// Fotoğraf piksel dizisi üzerine dahili 5x7 ASCII bitmap font ile metin yazar.
    /// </summary>
    private static void DrawBitmapText(UnityEngine.Color[] pixels, int texW, int texH, string text, int startX, int startY, int scale, UnityEngine.Color textColor)
    {
        int curX = startX;

        foreach (char c in text)
        {
            char upperC = char.ToUpperInvariant(c);
            if (Font5x7.TryGetValue(upperC, out byte[] glyph))
            {
                for (int col = 0; col < 5; col++)
                {
                    byte colData = glyph[col];
                    for (int row = 0; row < 7; row++)
                    {
                        bool isPixelOn = (colData & (1 << row)) != 0;
                        if (isPixelOn)
                        {
                            int px = curX + col * scale;
                            int py = startY + (6 - row) * scale;

                            for (int sy = 0; sy < scale; sy++)
                            {
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int targetX = px + sx;
                                    int targetY = py + sy;
                                    if (targetX >= 0 && targetX < texW && targetY >= 0 && targetY < texH)
                                    {
                                        pixels[targetY * texW + targetX] = textColor;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            curX += 6 * scale; // 5 piksel karakter + 1 piksel aralık
        }
    }

    // ========================================================
    // 5x7 STANDART ASCII BITMAP FONT TABLOSU
    // ========================================================
    private static readonly System.Collections.Generic.Dictionary<char, byte[]> Font5x7 = new System.Collections.Generic.Dictionary<char, byte[]>
    {
        {' ', new byte[]{0x00, 0x00, 0x00, 0x00, 0x00}},
        {'0', new byte[]{0x3E, 0x51, 0x49, 0x45, 0x3E}},
        {'1', new byte[]{0x00, 0x42, 0x7F, 0x40, 0x00}},
        {'2', new byte[]{0x42, 0x61, 0x51, 0x49, 0x46}},
        {'3', new byte[]{0x21, 0x41, 0x45, 0x4B, 0x31}},
        {'4', new byte[]{0x18, 0x14, 0x12, 0x7F, 0x10}},
        {'5', new byte[]{0x27, 0x45, 0x45, 0x45, 0x39}},
        {'6', new byte[]{0x3C, 0x4A, 0x49, 0x49, 0x30}},
        {'7', new byte[]{0x01, 0x71, 0x09, 0x05, 0x03}},
        {'8', new byte[]{0x36, 0x49, 0x49, 0x49, 0x36}},
        {'9', new byte[]{0x06, 0x49, 0x49, 0x29, 0x1E}},
        {'%', new byte[]{0x23, 0x13, 0x08, 0x64, 0x62}},
        {'[', new byte[]{0x00, 0x7F, 0x41, 0x41, 0x00}},
        {']', new byte[]{0x00, 0x41, 0x41, 0x7F, 0x00}},
        {'-', new byte[]{0x08, 0x08, 0x08, 0x08, 0x08}},
        {'/', new byte[]{0x20, 0x10, 0x08, 0x04, 0x02}},
        {':', new byte[]{0x00, 0x36, 0x36, 0x00, 0x00}},
        {'.', new byte[]{0x00, 0x60, 0x60, 0x00, 0x00}},
        {'A', new byte[]{0x7E, 0x11, 0x11, 0x11, 0x7E}},
        {'B', new byte[]{0x7F, 0x49, 0x49, 0x49, 0x36}},
        {'C', new byte[]{0x3E, 0x41, 0x41, 0x41, 0x22}},
        {'D', new byte[]{0x7F, 0x41, 0x41, 0x22, 0x1C}},
        {'E', new byte[]{0x7F, 0x49, 0x49, 0x49, 0x41}},
        {'F', new byte[]{0x7F, 0x09, 0x09, 0x09, 0x01}},
        {'G', new byte[]{0x3E, 0x41, 0x49, 0x49, 0x7A}},
        {'H', new byte[]{0x7F, 0x08, 0x08, 0x08, 0x7F}},
        {'I', new byte[]{0x00, 0x41, 0x7F, 0x41, 0x00}},
        {'J', new byte[]{0x20, 0x40, 0x41, 0x3F, 0x01}},
        {'K', new byte[]{0x7F, 0x08, 0x14, 0x22, 0x41}},
        {'L', new byte[]{0x7F, 0x40, 0x40, 0x40, 0x40}},
        {'M', new byte[]{0x7F, 0x02, 0x0C, 0x02, 0x7F}},
        {'N', new byte[]{0x7F, 0x04, 0x08, 0x10, 0x7F}},
        {'O', new byte[]{0x3E, 0x41, 0x41, 0x41, 0x3E}},
        {'P', new byte[]{0x7F, 0x09, 0x09, 0x09, 0x06}},
        {'Q', new byte[]{0x3E, 0x41, 0x51, 0x21, 0x5E}},
        {'R', new byte[]{0x7F, 0x09, 0x19, 0x29, 0x46}},
        {'S', new byte[]{0x46, 0x49, 0x49, 0x49, 0x31}},
        {'T', new byte[]{0x01, 0x01, 0x7F, 0x01, 0x01}},
        {'U', new byte[]{0x3F, 0x40, 0x40, 0x40, 0x3F}},
        {'V', new byte[]{0x1F, 0x20, 0x40, 0x20, 0x1F}},
        {'W', new byte[]{0x7F, 0x20, 0x18, 0x20, 0x7F}},
        {'X', new byte[]{0x63, 0x14, 0x08, 0x14, 0x63}},
        {'Y', new byte[]{0x07, 0x08, 0x70, 0x08, 0x07}},
        {'Z', new byte[]{0x61, 0x51, 0x49, 0x45, 0x43}}
    };
}
