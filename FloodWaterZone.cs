using GameNetcodeStuff;
using UnityEngine;
namespace OpaliteMoonMod;

public class FloodWaterZone : MonoBehaviour
{
    // edit of Jacobs JBlowerFan class but for the push of the flood water
    // thank you Jacob from Jacob's Lethal Libraries
    // i love you
    //public float forceMultiplier = 1f;
    public float vehicleForceMultiplier = 6f;
    public Transform sourcePos;
    
    public void OnTriggerStay(Collider other)
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        if (other.gameObject == player.gameObject && !player.inVehicleAnimation && !player.inSpecialInteractAnimation)
        {
            player.externalForceAutoFade += CalcPushForce(player.transform.position);
            player.fallValue = -1f;
            player.fallValueUncapped = -1f;
        }
        else if (other.gameObject.TryGetComponent(out VehicleController vehicle) && vehicle.IsOwner)
        {
            vehicle.mainRigidbody.AddForce(CalcPushForce(vehicle.transform.position) * vehicleForceMultiplier, ForceMode.Impulse);
        }
    }
    
    public Vector3 CalcPushForce(Vector3 pos)
    { 
        
        //float sideDistance = Mathf.Abs(sourcePos.InverseTransformPoint(pos).z);
        return sourcePos.forward * 0.5f;
    }
}