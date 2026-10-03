using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using System.Reflection;

namespace OpaliteMoonMod;

public class ApparatusDockHandler : NetworkBehaviour
{
    // hi
    
    // btw this script was based off v80's EnteranceTeleport script when I first started making it which might sound crazy
    
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

    public string removeAppTip = "Remove Apparatus : [LMB]";

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

    public void FinishOpening()
    {
        if (!GetDockAnimators())
            return;

        // Check if the door is still busy performing an opening animation loop
        AnimatorStateInfo stateInfo = thisDockAnimator.GetCurrentAnimatorStateInfo(0);
        
        // Assuming your open animation state is named "Open" or similar.
        // If it's still playing and hasn't reached full completion (1.0f), cancel out early to prevent snapping.
        if (stateInfo.IsName("Open") && stateInfo.normalizedTime < 1.0f)
        {
            OpaliteMoonPlugin.Log.LogDebug("[ApparatusDockHandler] Waiting for opening animation to finish before allowing interaction completion.");
            CancelOpening();
            return;
        }

        thisDockAnimator.SetBool("Open", false);

        bool configAllowsRemoval = OpaliteMoonPlugin.CanRemoveDockedApparatus != null && OpaliteMoonPlugin.CanRemoveDockedApparatus.Value;
        
        if (isPowered && configAllowsRemoval)
        {
            if (LocalPlayerHoldingNothing())
            {
                if (dockedApparatus != null)
                {
                    StartCoroutine(RemoveFromMachinery(dockedApparatus.NetworkObject));
                    return; 
                }
                else
                {
                    OpaliteMoonPlugin.Log.LogError("[ApparatusDockHandler] isPowered is true but dockedApparatus reference is null!");
                }
            }
            CancelOpening();
            return;
        }

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
            if (parentNetObj == null) parentNetObj = this.NetworkObject;
            if (parentNetObj != null)
            {
                localPlayer.DiscardHeldObject(
                    true, parentNetObj,
                    Vector3.zero, true
                );
            }
            else
            {
                localPlayer.DiscardHeldObject();
            }
        }

        PlaceApparatusServerRpc(new NetworkObjectReference(apparatus));
    }


    public void CancelOpening() 
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
        if (player == null || !player.isHoldingObject) return false;
        
        return player.currentlyHeldObjectServer is LungProp;
    }
    
    private bool LocalPlayerHoldingNothing()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        if (player == null || player.isHoldingObject) return false;
        
        return !player.isHoldingObject;
    }

    private bool CanGrabFromMachinery()
    {
        if (!isPowered || !LocalPlayerHoldingNothing()) return false;
        if (!(OpaliteMoonPlugin.CanRemoveDockedApparatus != null &&
              OpaliteMoonPlugin.CanRemoveDockedApparatus.Value)) return false;
        return true;
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
        
        if (apparatus.IsSpawned && apparatus.OwnerClientId != NetworkManager.ServerClientId) apparatus.ChangeOwnership(NetworkManager.ServerClientId);
        DockApparatusLocal(prop, apparatus, true);
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

        DockApparatusLocal(prop, apparatus, true);

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

        if (connectAnimation == null) connectAnimation = StartCoroutine(ConnectToMachinery());

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
            if (apparatus.transform.parent != parentNetObj.transform) apparatus.TrySetParent(parentNetObj, worldPositionStays: false);
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
            thisColliderAddition.center = triggerScript.transform.InverseTransformPoint(apparatusCollider.transform.TransformPoint(apparatusCollider.center));
            thisColliderAddition.size = triggerScript.transform.InverseTransformVector(apparatusCollider.transform.TransformVector(apparatusCollider.size));
            thisColliderAddition.size = new Vector3(Mathf.Abs(thisColliderAddition.size.x), Mathf.Abs(thisColliderAddition.size.y), Mathf.Abs(thisColliderAddition.size.z));
            thisColliderAddition.isTrigger = false;
        }

        if (dockTransform != null) prop.parentObject = dockTransform;
        else if (parentNetObj != null) prop.parentObject = parentNetObj.transform;

        prop.isHeld = false;
        prop.playerHeldBy = null;
        prop.hasHitGround = true;
        prop.grabbable = false;
        prop.grabbableToEnemies = false;
        prop.fallTime = 1f;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RemoveApparatusServerRpc(NetworkObjectReference apparatusRef, RpcParams rpcParams = default)
    {
        if (!isPowered) return;
        if (!apparatusRef.TryGet(out NetworkObject apparatus)) return;

        LungProp prop = apparatus.GetComponent<LungProp>();
        if (prop == null) return;

        isPowered = false;
        
        // Remove parenting on the server via Netcode infrastructure
        if (apparatus.transform.parent != null)
        {
            apparatus.TryRemoveParent(worldPositionStays: true);
        }

        // FIX: Extract the sender client's ID safely from the RpcParams struct metadata
        ulong interactingClientId = rpcParams.Receive.SenderClientId;
        
        if (apparatus.IsSpawned)
        {
            apparatus.ChangeOwnership(interactingClientId);
        }

        RemoveApparatusClientRpc(apparatusRef);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RemoveApparatusClientRpc(NetworkObjectReference apparatusRef)
    {
        if (!apparatusRef.TryGet(out NetworkObject apparatus)) return;

        LungProp prop = apparatus.GetComponent<LungProp>();
        if (prop == null) return;

        isPowered = false;
        dockedApparatus = null;

        if (GetDockAnimators())
        {
            // FIX: Force state updates clearly so the animator transitions to its shut/unpowered sequence
            thisDockAnimator.SetBool("Open", false);
            thisDockAnimator.SetBool("Powered", false);
            
            // OPTIONAL: If your door animator relies on a trigger for sudden closure, uncomment the line below:
            // thisDockAnimator.SetTrigger("SlamDoor"); 
        }

        if (triggerScript != null)
        {
            BoxCollider[] currentColliders = triggerScript.gameObject.GetComponents<BoxCollider>();
            foreach (BoxCollider col in currentColliders)
            {
                if (!col.isTrigger)
                {
                    Destroy(col);
                }
            }
            triggerScript.interactable = true;
        }

        if (connectAnimation != null) { StopCoroutine(connectAnimation); connectAnimation = null; }
        if (roomPowerAnimation != null) { StopCoroutine(roomPowerAnimation); roomPowerAnimation = null; }
        if (roomFlickerAnimation != null) { StopCoroutine(roomFlickerAnimation); roomFlickerAnimation = null; }

        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        bool shouldWield = localPlayer != null && LocalPlayerHoldingNothing() && 
                           Vector3.Distance(localPlayer.transform.position, transform.position) < 5f;

        UndockAndGrabApparatusLocal(prop, apparatus, shouldWield);
        
        // FIX: Safely check array indexing before playing the sound effect
        if (dockingPointAudio != null && dockingAudios != null && dockingAudios.Length > 0)
        {
            // Assuming index 0 is your placement sound, or another index for removal slam
            dockingPointAudio.PlayOneShot(dockingAudios[0], 0.7f); 
        }

        foreach (GameObject obj in poweredRoomObjects)
        {
            obj.SetActive(false);
            var animator = obj.GetComponent<Animator>();
            if (animator != null) animator.SetBool("on", false);
        }
    }
    private void UndockAndGrabApparatusLocal(LungProp prop, NetworkObject apparatus, bool holderWield)
    {
        if (prop == null || apparatus == null) return;

        // Restore collider so the item can be targeted / physics can interact again
        BoxCollider apparatusCollider = prop.GetComponent<BoxCollider>();
        if (apparatusCollider != null)
        {
            apparatusCollider.enabled = true;
        }

        // Always restore grabbable state first so the item is never left permanently locked
        prop.grabbable = true;
        prop.grabbableToEnemies = true;
        prop.isPocketed = false;
        prop.fallTime = 0f;

        if (holderWield && LocalPlayerHoldingNothing())
        {
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer != null)
            {
                prop.transform.SetParent(localPlayer.localItemHolder, worldPositionStays: false);
                prop.transform.localPosition = Vector3.zero;

                prop.parentObject = localPlayer.localItemHolder;
                prop.isHeld = true;
                prop.playerHeldBy = localPlayer;
                prop.hasHitGround = false;

                try
                {
                    MethodInfo grabObjectServerRpc = typeof(PlayerControllerB).GetMethod(
                        "GrabObjectServerRpc",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (grabObjectServerRpc != null)
                    {
                        NetworkObjectReference netObjRef = new NetworkObjectReference(apparatus);
                        grabObjectServerRpc.Invoke(localPlayer, new object[] { netObjRef });
                        prop.GrabItemOnClient();
                    }
                    else
                    {
                        OpaliteMoonPlugin.Log.LogError("Could not find GrabObjectServerRpc via Reflection.");
                    }
                }
                catch (System.Exception ex)
                {
                    OpaliteMoonPlugin.Log.LogError($"Failed to force reflect grab layout: {ex}");
                }
            }
        }
        else
        {
            // Drop to floor as a normal free apparatus — fully unparented and pickable
            if (apparatus.IsSpawned && apparatus.transform.parent != null)
            {
                apparatus.TryRemoveParent(worldPositionStays: true);
            }
            else
            {
                prop.transform.SetParent(null, worldPositionStays: true);
            }

            prop.parentObject = null;
            prop.isHeld = false;
            prop.playerHeldBy = null;
            prop.hasHitGround = true;
            prop.grabbable = true;
            prop.grabbableToEnemies = true;
            prop.isPocketed = false;
            prop.fallTime = 1f;

            // Nudge slightly so it does not clip into the dock and becomes unreachable
            prop.transform.position += Vector3.up * 0.15f;
            prop.transform.position += transform.forward * 0.25f;
        }
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
        if (roomFlickerAnimation == null) roomFlickerAnimation = StartCoroutine(FlickerPoweredLightsControlRoom());
    }
    
    public void TurnOnRoomLights()
    {
        if (roomPowerAnimation == null) roomPowerAnimation = StartCoroutine(RoomPowerAnimation());
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
            newSparkParticle = Instantiate(dockedApparatus.sparkParticle, dockedApparatus.transform.position, Quaternion.identity, dockedApparatus.transform);
        }
        
        dockingPointAudio.PlayOneShot(dockingAudios[0], 0.7f);
        
        yield return new WaitForSeconds(0.1f);
        
        if (newSparkParticle != null) newSparkParticle.SetActive(true);
        
        yield return new WaitForSeconds(0.3f); 
        
        if (roundManager != null) roundManager.FlickerLights();
        
        yield return new WaitForSeconds(1f);
        
        if (newSparkParticle != null) Destroy(newSparkParticle, 2f); 
        connectAnimation = null;
        
        yield return null;
    }

    private IEnumerator RemoveFromMachinery(NetworkObject apparatus)
    {
        Debug.Log("[ApparatusDockHandler] RemoveFromMachinery CALLED");
    
        if (apparatus != null)
        {
            RemoveApparatusServerRpc(new NetworkObjectReference(apparatus));
        }
    
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
            thisDockAnimator.SetBool("Powered", false);
        }

        // Query current animation state frames
        AnimatorStateInfo stateInfo = thisDockAnimator.GetCurrentAnimatorStateInfo(0);
        bool isAnimating = (stateInfo.IsName("Open") && stateInfo.normalizedTime < 1.0f) || 
                           (stateInfo.IsName("Close") && stateInfo.normalizedTime < 1.0f);

        bool configAllowsRemoval = OpaliteMoonPlugin.CanRemoveDockedApparatus != null && OpaliteMoonPlugin.CanRemoveDockedApparatus.Value;
        bool canDock = LocalPlayerHoldingApparatus() && !isPowered;
        bool canRemove = isPowered && configAllowsRemoval && LocalPlayerHoldingNothing();
    
        // Lock out interactable status completely if the animator is transitioning/animating
        if (isAnimating)
        {
            triggerScript.interactable = false;
            triggerScript.hoverTip = "";
            triggerScript.disabledHoverTip = "[Busy]";
            return;
        }

        triggerScript.interactable = canDock || canRemove;

        if (canDock)
        {
            triggerScript.hoverTip = "Insert Apparatus : [LMB]";
        }
        else if (canRemove)
        {
            triggerScript.hoverTip = "Remove Apparatus : [LMB]";
        }
        else
        {
            triggerScript.hoverTip = "";
        }

        if (isPowered && configAllowsRemoval)
        {
            if (!LocalPlayerHoldingNothing())
            {
                triggerScript.disabledHoverTip = "[Hands Full]";
            }
            else
            {
                triggerScript.disabledHoverTip = "Remove Apparatus : [LMB]";
            }
        }
        else if (isPowered && !configAllowsRemoval)
        {
            triggerScript.disabledHoverTip = "[Locked]";
        }
        else
        {
            triggerScript.disabledHoverTip = "[Requires Apparatus]";
        }
    }
}

public enum DockingInteractions
{
    Early,
    Late,
    Cancel
}