using System.Reflection;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using GameNetcodeStuff;
using Object = UnityEngine.Object;
using BepInEx;

namespace OpaliteMoonMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OpaliteMoonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.flintchips.48Contingency";
        public const string PluginName = "48contingency";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("OpaliteMod"))
                {
                    Debug.Log("[48contingency] Redirecting OpaliteMod to 48contingency!");
                    return Assembly.GetExecutingAssembly(); 
                }
                return null;
            };
            
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            Debug.Log("48contingency has loaded!");
        }
    }

    public class ControlRoomManager : NetworkBehaviour
    {
        public ApparatusDockHandler dockHandler;
        public SpawnSyncedObject[] storageShelfSpawns;
        public List<PlayerControllerB> playersInsideControlRoom = new List<PlayerControllerB>();
        public ControlRoomTeleport controlRoomTeleport, outdoorControlRoomTeleport;
        public EntranceTeleport controlRoomFireTeleport, indoorFireTeleport;

        public Animator garageDoorAnimator;

        public bool hasBeenPowered;
        private bool isPoweredOld;
        
        private InteractTrigger controlRoomDoorTrigger, indoorDoorTrigger;
        private InteractTrigger controlRoomFireTrigger, indoorFireTrigger;

        public void Awake()
        {
            if(dockHandler == null) dockHandler = FindObjectOfType<ApparatusDockHandler>(); // dont be stupid and use more than 1
            // fire exit ref is so i can get add a listener to the indoor fire exit for
            // telling when the player exits the fire exit into the control room instead of outside
            SetFireExitReferences();
            SpawnLockersAndRandomProps();
        }

        public void LateUpdate()
        {
            hasBeenPowered = dockHandler.isPowered;
            if(!isPoweredOld && hasBeenPowered) OnBeginPowerServerRpc();
            isPoweredOld = dockHandler.isPowered;
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void OnBeginPowerServerRpc()
        {
            OnBeginPowerClientRpc();
        }
        
        [ClientRpc]
        public void OnBeginPowerClientRpc()
        {
            StartCoroutine(BigDoorOpen());
        }
        
        [ClientRpc]
        public void OnEndPowerClientRpc()
        {
            StartCoroutine(BigDoorClose());
        }

        private void SetFireExitReferences()
        {
            StartCoroutine(WaitForIndoorFireTeleportToSpawn());
        }

        private IEnumerator WaitForIndoorFireTeleportToSpawn() // idk when indoor fire exit spawns 
        {
            float startTime = Time.timeSinceLevelLoad;
        
            //while (controlRoomFireTeleport != null && controlRoomFireTeleport.exitScript == null && Time.timeSinceLevelLoad - startTime < 15f)
            //{
            //    yield return new WaitForSeconds(1f);
            //}
            yield return new WaitForSeconds(1f);
            InitFireExitListeners();
        }
        
        private void SpawnLockersAndRandomProps()
        {
            StartCoroutine(WaitForLevelSeed());
        }

        private IEnumerator WaitForLevelSeed()
        {
            float startTime = Time.timeSinceLevelLoad;
            
            while (!RoundManager.Instance.hasInitializedLevelRandomSeed && Time.timeSinceLevelLoad - startTime < 15f)
            {
                yield return new WaitForSeconds(1f);
            }
            SetUpLockerSpawnsServerRpc();
        }
        
        private IEnumerator BigDoorOpen() 
        {
            yield return new WaitForSeconds(3f);
            garageDoorAnimator.SetBool("Open", true);
        }
        
        private IEnumerator BigDoorClose() 
        {
            yield return new WaitForSeconds(1f);
            garageDoorAnimator.SetBool("Open", false);
        }
        
        public void InitFireExitListeners()
        {
            //if(controlRoomFireTeleport == null || controlRoomFireTeleport.exitScript == null) Debug.LogError("[ControlRoomManager] Indoor fire exit or control or teleport or is null");

            //if (indoorFireTeleport == null && controlRoomFireTeleport)
            //{
            //    indoorFireTeleport = controlRoomFireTeleport.exitScript;
            //}
            
            //controlRoomFireTrigger = controlRoomFireTeleport.GetComponent<InteractTrigger>();
            //indoorFireTrigger = indoorFireTeleport.GetComponent<InteractTrigger>();
            indoorDoorTrigger = controlRoomTeleport.GetComponent<InteractTrigger>();
            controlRoomDoorTrigger = outdoorControlRoomTeleport.GetComponent<InteractTrigger>();
            
            //controlRoomFireTrigger.onInteract.AddListener(ControlRoomFireTeleportPlayer);
            //indoorFireTrigger.onInteract.AddListener(IndoorFireTeleportPlayer);
            //indoorFireTeleport.audioReverbPreset = 2;
            
            controlRoomDoorTrigger.onInteract.AddListener(OutdoorControlRoomDoorTeleportPlayer);
            indoorDoorTrigger.onInteract.AddListener(IndoorControlRoomDoorTeleportPlayer);
            
            Debug.Log($"[ControlRoomManager] added event controlRoomTeleportAction to controlRoomFireTrigger.onInteract");
            //Debug.Log($"[ControlRoomManager] added event indoorTeleportAction to indoorFireTrigger.onInteract");
        }
        
        [ServerRpc (RequireOwnership = false)]
        private void SetUpLockerSpawnsServerRpc()
        {
            int seed = StartOfRound.Instance.randomMapSeed;
            float spawnChance = 0.5f;
            System.Random random = new System.Random(seed);
            bool[] activeLockers = new bool[storageShelfSpawns.Length];
            for (int i = 0; i < activeLockers.Length; i++)
            {
                if (random.NextDouble() <= spawnChance)
                {
                    activeLockers[i] = true;
                }
            }

            SetUpLockerSpawnsClientRpc(activeLockers);
        }
        
        [ClientRpc]
        private void SetUpLockerSpawnsClientRpc(bool[] activeLockers)
        {
            for (int i = 0; i < activeLockers.Length; i++)
            {
                bool spawn = activeLockers[i];
                if (spawn)
                {
                    Instantiate(storageShelfSpawns[i].gameObject, storageShelfSpawns[i].transform);
                }
            }
        }

        public void IndoorFireTeleportPlayer(PlayerControllerB player)
        {
            Debug.Log($"[ControlRoomManager] Teleported from Factory -> ControlRoom. Teleport finished from local player {player.actualClientId}");
            UpdateControlRoomPresenceServerRpc(player.actualClientId, true, true);
        }
        
        public void ControlRoomFireTeleportPlayer(PlayerControllerB player)
        {
            Debug.Log($"[ControlRoomManager] Teleported from ControlRoom -> Factory. Teleport finished from local player {player.actualClientId}");
            UpdateControlRoomPresenceServerRpc(player.actualClientId, false, true);
        }
        
        public void OutdoorControlRoomDoorTeleportPlayer(PlayerControllerB player)
        {
            Debug.Log($"[ControlRoomManager] Teleported from Outside -> ControlRoom. Teleport finished from local player {player.actualClientId}");
            UpdateControlRoomPresenceServerRpc(player.actualClientId, true, true);
        }
        
        public void IndoorControlRoomDoorTeleportPlayer(PlayerControllerB player)
        {
            Debug.Log($"[ControlRoomManager] Teleported from ControlRoom -> Outside. Teleport finished from local player {player.actualClientId}");
            UpdateControlRoomPresenceServerRpc(player.actualClientId, false, false);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void UpdateControlRoomPresenceServerRpc(ulong clientId, bool isEntering, bool toFactory)
        {
            UpdateControlRoomPresenceClientRpc(clientId, isEntering, toFactory);
        }
        
        [ClientRpc]
        private void UpdateControlRoomPresenceClientRpc(ulong clientId, bool isEntering, bool inFactory)
        {
            PlayerControllerB targetPlayer = StartOfRound.Instance.allPlayerScripts.FirstOrDefault(p => p.actualClientId == clientId);

            if (targetPlayer == null) return;
            
            ControlRoomPlayerManager addon = targetPlayer.GetComponent<ControlRoomPlayerManager>();
            if (addon == null)
            {
                addon = targetPlayer.gameObject.AddComponent<ControlRoomPlayerManager>();
            }
            
            addon.controlRoom = this;

            if (isEntering)
            {
                if (!playersInsideControlRoom.Contains(targetPlayer))
                {
                    playersInsideControlRoom.Add(targetPlayer);
                    addon.EnterControlRoom(inFactory);
                    Debug.Log($"[ControlRoomManager] Added {targetPlayer.playerUsername} to Control Room Synced");
                }
            }
            else
            {
                if (playersInsideControlRoom.Contains(targetPlayer))
                {
                    playersInsideControlRoom.Remove(targetPlayer);
                    addon.LeaveControlRoom(inFactory);
     
                    Debug.Log($"[ControlRoomManager] Removed {targetPlayer.playerUsername} from Control Room Synced");
                }
            }
        }
    }

    public class ControlRoomPlayerManager : MonoBehaviour
    {
        public PlayerControllerB player;
        public ControlRoomManager controlRoom;
        public bool inFactory;
    
        public void Awake()
        {
            player = GetComponent<PlayerControllerB>();
            if(player == null) 
            {
                Destroy(this); 
            }
        }

        public void LateUpdate()
        {
            
        }
    
        public void EnterControlRoom(bool inFactory)
        {
            if(player == null) 
                player = GetComponent<PlayerControllerB>();
            
            if(player == null) 
            {
                Destroy(this);
                return;
            }

            player.isInsideFactory = true;
        }
    
        public void LeaveControlRoom(bool inFactory)
        {
            if (!inFactory)
            {
                
            }
        }

        public void OnDestroy()
        {
            Debug.Log("[ControlRoomPlayerManager] I have been destroyed");
        }
    }

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

            if (prop.playerHeldBy == null || prop.playerHeldBy.currentlyHeldObjectServer != prop)
                return;

            NetworkObject parentNetObj = GetApparatusParentNetworkObject();
            if (parentNetObj == null)
            {
                Debug.LogError("[ApparatusDock] apparatusPoint has no NetworkObject!");
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
            Transform dockTransform = apparatusPoint != null ? apparatusPoint :
                parentNetObj != null ? parentNetObj.transform : null;

            if (stripHolder)
            {
                PlayerControllerB holder = prop.playerHeldBy;
                PlayerControllerB local = GameNetworkManager.Instance != null
                    ? GameNetworkManager.Instance.localPlayerController
                    : null;

                if (holder == null && local != null && local.currentlyHeldObjectServer == prop)
                    holder = local;

                if (holder != null)
                {
                    if (holder.currentlyHeldObjectServer == prop || holder.isHoldingObject)
                    {
                        holder.currentlyHeldObjectServer = null;
                        holder.isHoldingObject = false;
                        holder.twoHanded = false;
                        prop.DiscardItemOnClient();
                        holder.playerBodyAnimator.SetBool("cancelHolding", true);
                        holder.playerBodyAnimator.SetTrigger("Throw");
                    }
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
            if (apparatusPoint == null) return null;
            var net = apparatusPoint.GetComponent<NetworkObject>();
            if (net != null) return net;
            return apparatusPoint.GetComponentInParent<NetworkObject>();
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
            Debug.Log("Flickering Control Room lights");
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
                newSparkParticle = Object.Instantiate(
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
                Object.Destroy(newSparkParticle, 2f); 
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
            
            
            triggerScript.hoverTip = canDock ? "[Insert Apparatus]" : "";
            triggerScript.disabledHoverTip = isPowered ? "[Locked]" : "[Requires Apparatus]";
        }
    }
    // end class

    public enum DockingInteractions
    {
        Early,
        Late,
        Cancel
    }
}

