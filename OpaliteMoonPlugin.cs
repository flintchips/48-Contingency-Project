using System.Reflection;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using GameNetcodeStuff;
using Object = UnityEngine.Object;
using BepInEx;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace OpaliteMoonMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OpaliteMoonPlugin : BaseUnityPlugin
    {
        
        public const string PluginGuid = "com.flintchips.OpaliteMoonMod";
        public const string PluginName = "Opalite";
        public const string PluginVersion = "0.1.0";

        private void Awake()
        {
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

        public void FinishOpening()
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
                BoxCollider apparatusCollider = prop.GetComponent<BoxCollider>();
                if (apparatusCollider != null)
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
                if (holder != null)
                {
                    if (holder.currentlyHeldObjectServer == prop || holder.isHoldingObject)
                    {
                        if (holder.ItemSlots != null)
                        {
                            for (int i = 0; i < holder.ItemSlots.Length; i++)
                            {
                                if (holder.ItemSlots[i] == prop)
                                    holder.ItemSlots[i] = null;
                            }
                        }
                        holder.currentlyHeldObjectServer = null;
                        holder.isHoldingObject = false;
                        holder.twoHanded = false;
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
            if (dockTransform != null)
                prop.parentObject = dockTransform;
            else if (parentNetObj != null)
                prop.parentObject = parentNetObj.transform;
            prop.isHeld = false;
            prop.isHeldByEnemy = false;
            prop.playerHeldBy = null;
            prop.hasHitGround = true;
            prop.grabbable = false;
            prop.grabbableToEnemies = false;
            prop.fallTime = 1f;
            var rb = prop.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
            }
            apparatus.transform.localPosition = Vector3.zero;
            apparatus.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
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
            float propogationSpeed = 10f;
 
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
                    null);
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
    
    public class ControlRoomNightVision : MonoBehaviour
    {
        public ApparatusDockHandler apparatusDockHandler;
        public PlayerControllerB playerController;
        public List<Light> dayLights;
        public GameObject skyAndFogGlobalVolume;
        
        [Header("Dark settings")]
        public bool zeroAmbient = true;
        public Color ambientInside = Color.black;
        private bool savedNvEnabled;
        private float savedNvIntensity;
        private bool cachedNv;
        private bool cachedAmbient;
        private AmbientMode savedAmbientMode;
        private Color savedAmbientSky, savedAmbientEquator, savedAmbientGround, savedAmbientLight;
        private float savedAmbientIntensity;
        private bool cachedVolume;
        private bool savedVolumeActive;
        private readonly Dictionary<Light, LightSave> savedLights = new Dictionary<Light, LightSave>();
        private Coroutine pinRoutine;
        
        private struct LightSave
        {
            public bool activeSelf;
            public bool enabled;
            public float intensity;
        }
        
        private void Awake()
        {
            playerController = GetComponent<PlayerControllerB>();
        }

        
        // added to player when they enter by control room teleport and removed the same way
        private void Start()
        {
            if (playerController == null)
                playerController = GetComponent<PlayerControllerB>();
            if (apparatusDockHandler == null)
                apparatusDockHandler = FindAnyObjectByType<ApparatusDockHandler>();
            if (playerController == null || apparatusDockHandler == null)
            {
                Debug.LogError("[ControlRoomNightVision] Missing player or ApparatusDockHandler. DESTROYING.");
                Destroy(this);
                return;
            }
            // dayLights usually assigned by ControlRoomTeleport 
            if (dayLights == null)
                dayLights = new List<Light>();
            TryResolveSkyVolume();
            CacheAll();
            pinRoutine = StartCoroutine(PinDarkEndOfFrame());
            Debug.Log($"[ControlRoomNightVision] Active. lights={dayLights.Count} volume={(skyAndFogGlobalVolume != null ? skyAndFogGlobalVolume.name : "null")}");
        }

        private void OnDestroy()
        {
            if (pinRoutine != null)
            {
                StopCoroutine(pinRoutine);
                pinRoutine = null;
            }
            RestoreAll();
            Debug.Log("[ControlRoomNightVision] DESTROYED - Restored Sun Lights, SkyAndFogGlobalVolume, Ambient and NightVision.");
        }
        
        private IEnumerator PinDarkEndOfFrame()
        {
            var wait = new WaitForEndOfFrame();
            while (enabled)
            {
                ApplyDark();
                ApplyNightVision();
                yield return wait;
            }
        }
        
        private void TryResolveSkyVolume()
        {
            if (skyAndFogGlobalVolume != null) return;
        
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            // use name to find it
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "Sky and Fog Global Volume")
                {
                    if (all[i].gameObject.scene.IsValid())
                    {
                        skyAndFogGlobalVolume = all[i].gameObject;
                        return;
                    }
                }
            }
            // search siblings
            if (dayLights != null && dayLights.Count > 0 && dayLights[0] != null)
            {
                Transform t = dayLights[0].transform;
                while (t.parent != null) t = t.parent;
                var found = t.GetComponentsInChildren<Transform>(true);
                foreach (var c in found)
                {
                    if (c.name == "Sky and Fog Global Volume")
                    {
                        skyAndFogGlobalVolume = c.gameObject;
                        return;
                    }
                }
            }
            Debug.LogWarning("[ControlRoomNightVision] Sky and Fog Global Volume not found.");
        }
        
        private void CacheAll()
        {
            foreach (var light in dayLights)
            {
                if (light == null || savedLights.ContainsKey(light)) continue;
                savedLights[light] = new LightSave
                {
                    activeSelf = light.gameObject.activeSelf,
                    enabled = light.enabled,
                    intensity = light.intensity
                };
            }
            
            if (skyAndFogGlobalVolume != null && !cachedVolume)
            {
                cachedVolume = true;
                savedVolumeActive = skyAndFogGlobalVolume.activeSelf;
            }
            
            if (zeroAmbient && !cachedAmbient)
            {
                cachedAmbient = true;
                savedAmbientMode = RenderSettings.ambientMode;
                savedAmbientSky = RenderSettings.ambientSkyColor;
                savedAmbientEquator = RenderSettings.ambientEquatorColor;
                savedAmbientGround = RenderSettings.ambientGroundColor;
                savedAmbientLight = RenderSettings.ambientLight;
                savedAmbientIntensity = RenderSettings.ambientIntensity;
            }
            
            if (playerController != null && playerController.nightVision != null && !cachedNv)
            {
                cachedNv = true;
                savedNvEnabled = playerController.nightVision.enabled;
                savedNvIntensity = playerController.nightVision.intensity;
            }
        }
        
        private void ApplyDark()
        {
            if (dayLights != null)
            {
                for (int i = 0; i < dayLights.Count; i++)
                {
                    Light light = dayLights[i];
                    if (light == null) continue;
                    if (light.gameObject.activeSelf)
                        light.gameObject.SetActive(false);
                    light.enabled = false;
                    light.intensity = 0f;
                }
            }
   
            if (skyAndFogGlobalVolume != null && skyAndFogGlobalVolume.activeSelf)
                skyAndFogGlobalVolume.SetActive(false);
  
            if (zeroAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientInside;
                RenderSettings.ambientSkyColor = ambientInside;
                RenderSettings.ambientEquatorColor = ambientInside;
                RenderSettings.ambientGroundColor = ambientInside;
                RenderSettings.ambientIntensity = 0f;
            }
        }
        
        public void LateUpdate()
        {
            ApplyNightVision();
        }
        
        private void ApplyNightVision()
        {
            if (playerController == null || playerController.nightVision == null) return;
            if (apparatusDockHandler == null) return;
            
            if (!apparatusDockHandler.isPowered)
            {
                playerController.nightVision.enabled = false;
                return;
            }
            
            if (!playerController.nightVision.enabled)
            {
                playerController.nightVision.enabled = true;
                playerController.nightVision.intensity = 0f;
            }
            if (playerController.nightVision.intensity < 220f)
                playerController.nightVision.intensity += 50f * Time.deltaTime;
        }
        
        private void RestoreAll()
        {
            foreach (var kv in savedLights)
            {
                Light light = kv.Key;
                if (light == null) continue;
                LightSave s = kv.Value;
                light.intensity = s.intensity;
                light.enabled = s.enabled;
                light.gameObject.SetActive(s.activeSelf);
            }
            savedLights.Clear();
            if (cachedVolume && skyAndFogGlobalVolume != null)
            {
                skyAndFogGlobalVolume.SetActive(savedVolumeActive);
                cachedVolume = false;
            }
            if (cachedAmbient)
            {
                RenderSettings.ambientMode = savedAmbientMode;
                RenderSettings.ambientSkyColor = savedAmbientSky;
                RenderSettings.ambientEquatorColor = savedAmbientEquator;
                RenderSettings.ambientGroundColor = savedAmbientGround;
                RenderSettings.ambientLight = savedAmbientLight;
                RenderSettings.ambientIntensity = savedAmbientIntensity;
                cachedAmbient = false;
            }
            if (cachedNv && playerController != null && playerController.nightVision != null)
            {
                playerController.nightVision.enabled = savedNvEnabled;
                playerController.nightVision.intensity = savedNvIntensity;
                cachedNv = false;
            }
        }
    }

    enum DockingInteractions
    {
        Early,
        Late,
        Cancel
    }
}

