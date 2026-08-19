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

        player.isInsideFactory = true;
        KyividManager.Instance.kyividDirectionalLighting.enabled = false;
    }
    
    public void LeaveControlRoom()
    {
        if (!PlayerExists()) return;
        
        player.isInsideFactory = false;
        TimeOfDay.Instance.effects[(int)TimeOfDay.Instance.currentLevelWeather].effectEnabled = true;
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