using System;
using System.IO;
using UnityEngine;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct OpenFileName
{
    public int structSize;
    public IntPtr hwnd;
    public IntPtr hinst;
    public string filter;
    public string custFilter;
    public int nMaxCustFilter;
    public int nFilterIndex;
    public string file;
    public int maxFile;
    public string fileTitle;
    public int maxFileTitle;
    public string initialDir;
    public string title;
    public int flags;
    public short fileOffset;
    public short fileExt;
    public string defExt;
    public IntPtr custData;
    public IntPtr hook;
    public string templateName;
    public IntPtr reservedPtr;
    public int reservedInt;
    public int flagsEx;
}
#endif

public static class NativeFilePicker
{
#if UNITY_STANDALONE_WIN
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    public static string OpenWindowsFile(string title)
    {
        OpenFileName ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = "Resim Dosyaları (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0Tüm Dosyalar (*.*)\0*.*\0\0";
        ofn.file = new string(new char[512]);
        ofn.maxFile = ofn.file.Length;
        ofn.fileTitle = new string(new char[128]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        ofn.title = title;
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008;

        if (GetOpenFileName(ofn))
        {
            int nullIdx = ofn.file.IndexOf('\0');
            if (nullIdx >= 0)
            {
                return ofn.file.Substring(0, nullIdx).Trim();
            }
            return ofn.file.Trim();
        }
        return null;
    }
#endif

    public static void PickImage(Action<Texture2D> onImageLoaded, string dialogTitle = "Fotoğraf Seç")
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(dialogTitle, "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                onImageLoaded?.Invoke(tex);
            }
        }
#elif UNITY_STANDALONE_WIN
        string winPath = OpenWindowsFile(dialogTitle);
        if (!string.IsNullOrEmpty(winPath) && File.Exists(winPath))
        {
            byte[] bytes = File.ReadAllBytes(winPath);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
            {
                onImageLoaded?.Invoke(tex);
            }
        }
#elif UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                Texture2D galleryTexture = NativeGallery.LoadImageAtPath(path, 1024, false);
                if (galleryTexture != null)
                {
                    onImageLoaded?.Invoke(galleryTexture);
                }
            }
        });
#else
        Debug.LogWarning("Bu platformda dosya seçici desteklenmiyor.");
#endif
    }
}
