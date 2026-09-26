using UnityEngine;
using Unity.InferenceEngine;
using System;
using System.Collections.Generic;
using Unity.Collections;

public class DamageDetector : MonoBehaviour
{
    [Header("Model Settings")]
    public ModelAsset modelAsset;
    public DamageUI damageUI; 
    
    // Güvenilirlik (% kaç emin olduğunda çizsin - YOLOv8 standardı 0.25f)
    [Range(0f, 1f)] public float confidenceThreshold = 0.25f;

    // === CALLBACK SİSTEMİ ===
    // Dış sınıfların (GuidedCaptureManager vb.) tespit sonuçlarını alabilmesi için
    public event Action<List<DamageResult>> OnDamageDetected;
    private List<DamageResult> lastDetectedDamages = new List<DamageResult>();

    // Model calistirici (Backend - GPU uzerinde calisacak)
    private Worker worker;
    private Model runtimeModel;
    
    // Kameradan alinan goruntuyu cevirecegimiz tensor
    private Tensor<float> inputTensor;

    private const int IMAGE_SIZE = 1024;
    private int MASK_SIZE => IMAGE_SIZE / 4;
    private int MASK_AREA => MASK_SIZE * MASK_SIZE;

    // Maske Cizimleri Icin Texture2D
    private Texture2D maskOverlayTexture;
    private Color[] maskColors;

    // Titresim Icin Cok Sayida Olmasini Engelleme Suresi
    private float lastVibrationTime = 0f;

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
#if UNITY_ANDROID && !UNITY_EDITOR
        // Qualcomm Snapdragon (Mi 10 Pro) ve MediaTek cihazlarda GPU fence kilitlenmelerini
        // önleyen ve 8 çekirdekli Kryo işlemcide 20ms'de çalışan ARM NEON CPU Worker
        worker = new Worker(runtimeModel, BackendType.CPU);
#else
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
#endif
        inputTensor = new Tensor<float>(new TensorShape(1, 3, IMAGE_SIZE, IMAGE_SIZE));
        
        maskOverlayTexture = new Texture2D(MASK_SIZE, MASK_SIZE, TextureFormat.RGBA32, false);
        maskColors = new Color[MASK_AREA];

        Debug.Log("YOLOv8-Seg Modeli Basariyla Yuklendi!");
    }

    public void DetectDamage(Texture cameraTexture)
    {
        if (worker == null || cameraTexture == null)
            return;

        TextureConverter.ToTensor(cameraTexture, inputTensor);
        worker.Schedule(inputTensor);

        Tensor<float> output0 = worker.PeekOutput("output0") as Tensor<float>; 
        Tensor<float> output1 = worker.PeekOutput("output1") as Tensor<float>; 

        output0.CompleteAllPendingOperations();
        output1.CompleteAllPendingOperations();

        ProcessOutputs(output0, output1);

        // Tensor bellek sızıntısını önle — her frame sonrası serbest bırak
        output0.Dispose();
        output1.Dispose();
    }

    private void ProcessOutputs(Tensor<float> boxesAndWeights, Tensor<float> maskPrototypes)
    {
        var data = boxesAndWeights.DownloadToArray();
        var protoData = maskPrototypes.DownloadToArray();

        int grid1 = IMAGE_SIZE / 8;
        int grid2 = IMAGE_SIZE / 16;
        int grid3 = IMAGE_SIZE / 32;
        int numElements = (grid1 * grid1) + (grid2 * grid2) + (grid3 * grid3);     
        int numClasses = DamageConstants.NumClasses;
        int maskWeightCount = DamageConstants.MaskWeightCount;
        int attributes = 4 + numClasses + maskWeightCount;
        
        List<DamageResult> detectedDamages = new List<DamageResult>();

        for (int i = 0; i < numElements; i++)
        {
            float maxScore = 0f;
            int classId = -1;

            for (int c = 0; c < numClasses; c++)
            {
                float score = data[(4 + c) * numElements + i];
                if (score > maxScore)
                {
                    maxScore = score;
                    classId = c;
                }
            }

            if (maxScore >= confidenceThreshold)
            {
                DamageResult damage = new DamageResult();
                damage.classId = classId;
                damage.confidence = maxScore;
                
                damage.centerX = data[0 * numElements + i];
                damage.centerY = data[1 * numElements + i];
                damage.width = data[2 * numElements + i];
                damage.height = data[3 * numElements + i];
                
                damage.maskWeights = new float[maskWeightCount];
                for(int m = 0; m < maskWeightCount; m++)
                {
                    damage.maskWeights[m] = data[(attributes - maskWeightCount + m) * numElements + i];
                }

                detectedDamages.Add(damage);
            }
        }

        List<DamageResult> finalDamages = ApplyNMS(detectedDamages, 0.4f);

        CreateMaskTexture(finalDamages, protoData);



        // Sonuçları sakla ve event'i tetikle
        lastDetectedDamages = finalDamages;
        OnDamageDetected?.Invoke(finalDamages);

        if (damageUI != null) {
            damageUI.DrawDamages(finalDamages, IMAGE_SIZE, IMAGE_SIZE, maskOverlayTexture);
        }
    }

    private void CreateMaskTexture(List<DamageResult> damages, float[] protoData)
    {
        if (damages == null || damages.Count == 0 || protoData == null)
        {
            System.Array.Clear(maskColors, 0, maskColors.Length);
            maskOverlayTexture.SetPixels(maskColors);
            maskOverlayTexture.Apply();
            return;
        }

        System.Array.Clear(maskColors, 0, maskColors.Length);

        Color confidentColor = DamageConstants.HighConfidenceColor;
        Color unsureColor = DamageConstants.LowConfidenceColor;
        int maskWeightCount = DamageConstants.MaskWeightCount;

        int limitDamages = Mathf.Min(damages.Count, 8); // En önemli ilk 8 hasar

        for (int d = 0; d < limitDamages; d++)
        {
            var damage = damages[d];
            if (damage.maskWeights == null || damage.maskWeights.Length < maskWeightCount) continue;

            Color drawColor = damage.confidence > 0.85f ? confidentColor : unsureColor;

            int startX = Mathf.Clamp(Mathf.FloorToInt((damage.centerX - damage.width * 0.5f) / 4f), 0, MASK_SIZE - 1);
            int startY = Mathf.Clamp(Mathf.FloorToInt((damage.centerY - damage.height * 0.5f) / 4f), 0, MASK_SIZE - 1);
            int endX = Mathf.Clamp(Mathf.CeilToInt((damage.centerX + damage.width * 0.5f) / 4f), 0, MASK_SIZE - 1);
            int endY = Mathf.Clamp(Mathf.CeilToInt((damage.centerY + damage.height * 0.5f) / 4f), 0, MASK_SIZE - 1);

            float[] weights = damage.maskWeights;

            for (int y = startY; y <= endY; y++)
            {
                int rowOffset = y * MASK_SIZE;
                int flippedRow = (MASK_SIZE - 1 - y) * MASK_SIZE;

                for (int x = startX; x <= endX; x++)
                {
                    int pixelIdx = rowOffset + x;
                    float maskVal = 0f;

                    for (int c = 0; c < maskWeightCount; c++)
                    {
                        maskVal += weights[c] * protoData[c * MASK_AREA + pixelIdx];
                    }

                    // Sigmoid(maskVal) > 0.5f  <===>  maskVal > 0f (Mathf.Exp CPU darboğazını tamamen sıfırlar)
                    if (maskVal > 0f)
                    {
                        maskColors[flippedRow + x] = drawColor;
                    }
                }
            }
        }

        maskOverlayTexture.SetPixels(maskColors);
        maskOverlayTexture.Apply();
    }

    private List<DamageResult> ApplyNMS(List<DamageResult> boxes, float iouThreshold)
    {
        boxes.Sort((a, b) => b.confidence.CompareTo(a.confidence));
        List<DamageResult> results = new List<DamageResult>();

        while (boxes.Count > 0)
        {
            DamageResult bestBox = boxes[0];
            results.Add(bestBox);
            boxes.RemoveAt(0);

            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                // Class-Aware NMS: Yalnızca AYNI sınıftaki kutular birbirini eler.
                // Farklı sınıftaki hasarlar (örn: Göçük içindeki Çizik veya Çatlak) birbirini ezmez.
                bool isSameClass = bestBox.classId == boxes[i].classId;
                float iou = CalculateIoU(bestBox, boxes[i]);

                if (isSameClass && iou > iouThreshold)
                {
                    boxes.RemoveAt(i);
                }
                else if (!isSameClass && iou > 0.85f) // Aşırı yüksek çakışmada kopya kutuyu engelle
                {
                    boxes.RemoveAt(i);
                }
            }
        }
        return results;
    }

    private float CalculateIoU(DamageResult boxA, DamageResult boxB)
    {
        float aX1 = boxA.centerX - boxA.width / 2;
        float aY1 = boxA.centerY - boxA.height / 2;
        float aX2 = boxA.centerX + boxA.width / 2;
        float aY2 = boxA.centerY + boxA.height / 2;

        float bX1 = boxB.centerX - boxB.width / 2;
        float bY1 = boxB.centerY - boxB.height / 2;
        float bX2 = boxB.centerX + boxB.width / 2;
        float bY2 = boxB.centerY + boxB.height / 2;

        float interX1 = Mathf.Max(aX1, bX1);
        float interY1 = Mathf.Max(aY1, bY1);
        float interX2 = Mathf.Min(aX2, bX2);
        float interY2 = Mathf.Min(aY2, bY2);

        float interArea = Mathf.Max(0, interX2 - interX1) * Mathf.Max(0, interY2 - interY1);

        float areaA = boxA.width * boxA.height;
        float areaB = boxB.width * boxB.height;
        float unionArea = areaA + areaB - interArea;

        return interArea / unionArea;
    }

    // === DIŞ ERİŞİM METODLARI ===

    /// <summary>
    /// Son tespit sonuçlarını döndürür.
    /// </summary>
    public List<DamageResult> GetLastDetectedDamages()
    {
        return lastDetectedDamages;
    }

    /// <summary>
    /// Maske overlay texture'ının kopyasını döndürür (kayıt için).
    /// </summary>
    public Texture2D GetMaskTextureCopy()
    {
        if (maskOverlayTexture == null) return null;

        Texture2D copy = new Texture2D(maskOverlayTexture.width, maskOverlayTexture.height, TextureFormat.RGBA32, false);
        copy.SetPixels(maskOverlayTexture.GetPixels());
        copy.Apply();
        return copy;
    }

    /// <summary>
    /// IMAGE_SIZE değerini dış sınıflara açar.
    /// </summary>
    public int GetImageSize()
    {
        return IMAGE_SIZE;
    }

    private void OnDestroy()
    {
        inputTensor?.Dispose();
        worker?.Dispose();
        if (maskOverlayTexture != null) Destroy(maskOverlayTexture);
    }
}

