using UnityEngine;
using System.Collections;
namespace OpaliteMoonMod;

public class KyividManager : MonoBehaviour
{
    public Light kyividDirectionalLighting;
    public Transform lightTarget;
    public static KyividManager Instance;
    
    
    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    public void Update()
    {
        if (lightTarget.transform != null)
        {
            kyividDirectionalLighting.transform.LookAt(lightTarget.transform);
            if (StartOfRound.Instance.localPlayerController.isInsideFactory)
            {
                kyividDirectionalLighting.enabled = false;
            }
        }
    }
}