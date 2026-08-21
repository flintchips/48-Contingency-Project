using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace OpaliteMoonMod;

public class ApparatusDockHandler : NetworkBehaviour
{
    // hi
    public bool isPowered;

    public RoundManager roundManager;

    public Animator thisDockAnimator;

    public AudioSource dockingPointAudio;

    private Coroutine connectAnimation;
    
    private Coroutine roomPowerAnimation;
    
    private Coroutine roomFlickerAnimation;

    public AudioClip[] dockingAudios;

    public Transform apparatusPoint;

    private LungProp dockedApparatus;
    
    public Animator DockLightAnimator;

    public float timeAtLastUse;

    public GameObject[] poweredRoomObjects;
    
    public List<Animator> poweredRoomLightAnimators = new List<Animator>();

    private InteractTrigger triggerScript;

    private float playbackTime;

    private void Awake()
    {
        isPowered = false;
        triggerScript = base.gameObject.GetComponent<InteractTrigger>();
        foreach (GameObject obj in poweredRoomObjects)
        {
            var animator = obj.GetComponent<Animator>();
            if (animator != null)
            {
                poweredRoomLightAnimators.Add(animator);
                animator.SetBool("On", false);
            }
        }
    }

    private void Start()
    {
        roundManager = FindObjectOfType<RoundManager>();
    }

    public void FinishOpening() // interacting with dock stuff
    {
        if (!GetDockAnimators())
            return;

        thisDockAnimator.SetBool("Open", false);

        NetworkObject apparatus = GetApparatusFromInteractingPlayer();
        if (apparatus == null)
        {
            CancelOpening();
            return;
        }

        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (localPlayer != null && localPlayer.currentlyHeldObjectServer != null)
        {
            NetworkObject parentNetObj = GetApparatusParentNetworkObject();
            if (parentNetObj == null)
                parentNetObj = this.NetworkObject;
            if (parentNetObj != null)
            {
                localPlayer.DiscardHeldObject(
                    true,
                    parentNetObj,
                    Vector3.zero,
                    matchRotationOfParent: true
                );
            }
            else
            {
                localPlayer.DiscardHeldObject();
            }
        }

        PlaceApparatusServerRpc(new NetworkObjectReference(apparatus));
    }

    public void CancelOpening() // interacting with dock stuff
    {
        if (!GetDockAnimators())
        {
            return;
        }

        thisDockAnimator.SetBool("Open", value: false);
        SyncCancelOpeningRpc();
    }

    [Rpc(SendTo.NotMe, RequireOwnership = false)]
    public void SyncCancelOpeningRpc()
    {
        if (!GetDockAnimators())
        {
            return;
        }

        thisDockAnimator.SetBool("Open", value: false);
    }

    private NetworkObject? GetApparatusFromInteractingPlayer()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        if (player == null || !player.isHoldingObject || player.currentlyHeldObjectServer == null)
            return null;

        GrabbableObject held = player.currentlyHeldObjectServer;
        if (held is not LungProp) return null;

        return held.NetworkObject;
    }

    private bool LocalPlayerHoldingApparatus()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        if (player == null || !player.isHoldingObject)
            return false;
        return player.currentlyHeldObjectServer is LungProp;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void PlaceApparatusServerRpc(NetworkObjectReference apparatusRef)
    {
        if (isPowered) return;
        if (!apparatusRef.TryGet(out NetworkObject apparatus)) return;
        LungProp prop = apparatus.GetComponent<LungProp>();
        if (prop == null) return;
        
        NetworkObject parentNetObj = GetApparatusParentNetworkObject();
        if (parentNetObj == null)
        {
            OpaliteMoonPlugin.Log.LogDebug(("[ApparatusDock] apparatusPoint has no NetworkObject"));
            return;
        }
        isPowered = true;
        if (apparatus.IsSpawned && apparatus.OwnerClientId != NetworkManager.ServerClientId)
            apparatus.ChangeOwnership(NetworkManager.ServerClientId);
        DockApparatusLocal(prop, apparatus, stripHolder: true);
        PlaceApparatusClientRpc(apparatusRef);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaceApparatusClientRpc(NetworkObjectReference apparatusRef)
    {
        if (!apparatusRef.TryGet(out NetworkObject apparatus)) return;

        LungProp prop = apparatus.GetComponent<LungProp>();
        if (prop == null) return;

        isPowered = true;
        dockedApparatus = prop;

        DockApparatusLocal(prop, apparatus, stripHolder: true);

        if (GetDockAnimators())
        {
            thisDockAnimator.SetBool("Open", false);
            thisDockAnimator.SetBool("Powered", true);
        }

        if (triggerScript != null)
        {
            triggerScript.interactable = false;
            triggerScript.hoverTip = "[Locked]";
            triggerScript.disabledHoverTip = "[Locked]";
        }

        if (connectAnimation == null)
            connectAnimation = StartCoroutine(ConnectToMachinery());

        TurnOnRoomLights();
    }


    private void DockApparatusLocal(LungProp prop, NetworkObject apparatus, bool stripHolder)
    {
        if (prop == null || apparatus == null) return;

        NetworkObject parentNetObj = GetApparatusParentNetworkObject();
        Transform dockTransform = apparatusPoint != null ? apparatusPoint : parentNetObj != null ? parentNetObj.transform : null;

        if (stripHolder)
        {
            PlayerControllerB holder = prop.playerHeldBy;
            PlayerControllerB local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;

            if (holder == null && local != null && local.currentlyHeldObjectServer == prop)
                holder = local;

            if (holder != null && holder == local)
            {
                NetworkObject parentNet = GetApparatusParentNetworkObject();
                holder.DiscardHeldObject(true, parentNet, Vector3.zero, matchRotationOfParent: true);
            }
            
            if (holder != null)
            {
                holder.currentlyHeldObjectServer = null;
                holder.isHoldingObject = false;
                holder.twoHanded = false;
            }
        }
        
        

        if (parentNetObj != null)
        {
            if (apparatus.transform.parent != parentNetObj.transform)
                apparatus.TrySetParent(parentNetObj, worldPositionStays: false);
        }
        else if (dockTransform != null)
        {
            apparatus.transform.SetParent(dockTransform, worldPositionStays: false);
        }
        
        apparatus.transform.localPosition = Vector3.zero;
        apparatus.transform.localEulerAngles = new Vector3(0f, 180f, 0f);

        BoxCollider apparatusCollider = prop.GetComponent<BoxCollider>();
        if (apparatusCollider != null && triggerScript != null)
        {
            apparatusCollider.enabled = false;
            BoxCollider thisColliderAddition = triggerScript.gameObject.AddComponent<BoxCollider>();
            thisColliderAddition.center = triggerScript.transform.InverseTransformPoint(
                apparatusCollider.transform.TransformPoint(apparatusCollider.center));
            thisColliderAddition.size = triggerScript.transform.InverseTransformVector(
                apparatusCollider.transform.TransformVector(apparatusCollider.size));
            thisColliderAddition.size = new Vector3(
                Mathf.Abs(thisColliderAddition.size.x),
                Mathf.Abs(thisColliderAddition.size.y),
                Mathf.Abs(thisColliderAddition.size.z));
            thisColliderAddition.isTrigger = false;
        }

        if (dockTransform != null)
            prop.parentObject = dockTransform;
        else if (parentNetObj != null)
            prop.parentObject = parentNetObj.transform;

        prop.isHeld = false;
        prop.playerHeldBy = null;
        prop.hasHitGround = true;
        prop.grabbable = false;
        prop.grabbableToEnemies = false;
        prop.fallTime = 1f;
    }
    
    private NetworkObject GetApparatusParentNetworkObject()
    {
        if (apparatusPoint != null)
        {
            var net = apparatusPoint.GetComponent<NetworkObject>();
            if (net != null) return net;
            net = apparatusPoint.GetComponentInParent<NetworkObject>();
            if (net != null) return net;
        }
        return this.NetworkObject;
    }
    
    public void FlickerRoomLights()
    {
        if (roomFlickerAnimation == null)
            roomFlickerAnimation = StartCoroutine(FlickerPoweredLightsControlRoom());
    }
    
    public void TurnOnRoomLights()
    {
        if (roomPowerAnimation == null)
            roomPowerAnimation = StartCoroutine(RoomPowerAnimation());
    }

    private IEnumerator RoomPowerAnimation()
    {
        float[] individualDelays = new float[poweredRoomObjects.Length];
        float propogationSpeed = 7f;

        List<GameObject> sortedObjects = poweredRoomObjects.OrderBy(obj => Vector3.Distance(obj.transform.position, apparatusPoint.position)).ToList();
        
        int j = 0;
        float timeSpent = 0f;
        foreach (GameObject obj in sortedObjects)
        {
            float delay = Vector3.Distance(obj.transform.position, apparatusPoint.position) / propogationSpeed;
            float wait = delay - timeSpent;

            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
                timeSpent = delay;
            }
            
            obj.SetActive(true);
            var animator = obj.GetComponent<Animator>();
            if(animator != null)animator.SetBool("on", true);
        }
        yield return new WaitForSeconds(0.2f);
        FlickerRoomLights();
        yield return null;
    }
    
    private IEnumerator FlickerPoweredLightsControlRoom(bool flickerFlashlights = false, bool disableFlashlights = false)
    {
        OpaliteMoonPlugin.Log.LogDebug("Flickering Control Room lights");
        if (poweredRoomLightAnimators.Count > 0 && poweredRoomLightAnimators[0] != null)
        {
            int loopCount = 0;
            int b = 4;
            while (b > 0 && b != 0)
            {
                for (int j = loopCount; j < poweredRoomLightAnimators.Count / b; j++)
                {
                    loopCount++;
                    poweredRoomLightAnimators[j].SetTrigger("Flicker");
                }
                yield return new WaitForSeconds(0.05f);
                b--;
            }
        }
    }

    private IEnumerator ConnectToMachinery()
    {
        GameObject newSparkParticle = null;
        if (dockedApparatus != null && dockedApparatus.sparkParticle != null)
        {
            newSparkParticle = Instantiate(
                dockedApparatus.sparkParticle,
                dockedApparatus.transform.position,
                Quaternion.identity,
                dockedApparatus.transform);
        }
        
        dockingPointAudio.PlayOneShot(dockingAudios[0], 0.7f);
        yield return new WaitForSeconds(0.1f);
        if (newSparkParticle != null)
            newSparkParticle.SetActive(true);
        
        yield return new WaitForSeconds(0.3f); 
        
        if (DockLightAnimator != null)
            DockLightAnimator.SetBool("Light Begin", true);
        if (roundManager != null)
            roundManager.FlickerLights();
        
        yield return new WaitForSeconds(1f);
        
        if (newSparkParticle != null)
            Destroy(newSparkParticle, 2f); 
        connectAnimation = null;
        yield return null;
    }

    public void StartOpening()
    {
        if (isPowered || !LocalPlayerHoldingApparatus())
            return;
        if (!GetDockAnimators() || Time.realtimeSinceStartup - timeAtLastUse < 0.5f)
            return;
        timeAtLastUse = Time.realtimeSinceStartup;
        thisDockAnimator.SetBool("Open", true);
        SyncStartOpeningRpc();
    }
    [Rpc(SendTo.NotMe, RequireOwnership = false)]
    public void SyncStartOpeningRpc()
    {
        if (!GetDockAnimators()) return;
        thisDockAnimator.SetBool("Open", true);
    }

    private bool GetDockAnimators()
    {
        return thisDockAnimator != null;
    }

    private void LateUpdate()
    {
        if (triggerScript == null)
            return;
        if (!isPowered)
        {
            thisDockAnimator.SetBool("Powered", value: false);
        }
        bool canDock = LocalPlayerHoldingApparatus() && !isPowered;
        triggerScript.interactable = canDock;
        triggerScript.hoverTip = canDock ? "Insert Apparatus : [LMB]" : "";
        triggerScript.disabledHoverTip = isPowered ? "[Locked]" : "[Requires Apparatus]";
    }
}

public enum DockingInteractions
{
    Early,
    Late,
    Cancel
}