using UnityEngine;

[System.Serializable]
public class DamageResult
{
    public int classId;
    public float confidence;
    public float centerX;
    public float centerY;
    public float width;
    public float height;
    public float[] maskWeights;
}