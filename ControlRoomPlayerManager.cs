using GameNetcodeStuff;
using UnityEngine;

namespace OpaliteMoonMod;

public class ControlRoomPlayerManager : MonoBehaviour
{
    public PlayerControllerB player;
    public ControlRoomManager controlRoom;
    
    public void Awake()
    {
        PlayerExists();
    }
    
    public void EnterControlRoom()
    {
        if (!PlayerExists()) return;
        if (player != StartOfRound.Instance.localPlayerController) return;
        
        player.isInsideFactory = true;
        if (KyividManager.Instance != null && KyividManager.Instance.kyividDirectionalLighting != null)
            KyividManager.Instance.kyividDirectionalLighting.enabled = false;
    }
    
    public void LeaveControlRoom()
    {
        if (!PlayerExists()) return;
        if (player != StartOfRound.Instance.localPlayerController) return;
        
        player.isInsideFactory = false;
        if((int)TimeOfDay.Instance.currentLevelWeather >= 0)
            TimeOfDay.Instance.effects[(int)TimeOfDay.Instance.currentLevelWeather].effectEnabled = true;
        if (KyividManager.Instance != null && KyividManager.Instance.kyividDirectionalLighting != null)
            KyividManager.Instance.kyividDirectionalLighting.enabled = true;
    }

    private bool PlayerExists()
    {
        if(player == null) 
            player = GetComponent<PlayerControllerB>();
            
        if(player == null) 
        {
            Destroy(this);
            return false;
        }
        return true;
    }
}