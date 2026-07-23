using UnityEngine;

using KLib;

public class DevShell : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var volumeManager = new VolumeManager();
        var isMuted = volumeManager.IsMuted();
        Debug.Log($"is muted: {isMuted}");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
