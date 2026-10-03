using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using JLL.Components;
using UnityEngine.Events;
using Dawn;
using BepInEx.Logging;
using HarmonyLib;

namespace OpaliteMoonMod;

public class ControlRoomManager : NetworkBehaviour
{
    public static ControlRoomManager Instance;
    public ApparatusDockHandler dockHandler;
    public bool isRainingInside;
    
    public System.Random LockersRandom;
    public System.Random BasinRandom;
    public System.Random KyvidRandom;

    public AudioSource[] MiscAudios;
    public AudioClip[] miscClips;

    public GameObject[] controlRoomLights;
    public GameObject[] controlRoomLights2;
    public AudioSource MonitorAudio;
    public AudioClip monitorAlarmBeep;
    
    public AudioSource DoorAudio;
    public AudioClip doorOpeningSfx;
    
    public NetworkObject[] lockers;

    public List<PlayerControllerB> playersInsideControlRoom = new List<PlayerControllerB>();

    public ControlRoomTeleport controlRoomTeleport;

    public ControlRoomTeleport outdoorControlRoomTeleport;

    public EntranceTeleport controlRoomFireTeleport;

    public EntranceTeleport indoorFireTeleport;

    public Animator garageDoorAnimator;

    public bool hasBeenPowered;

    private bool isPoweredOld;

    private InteractTrigger controlRoomDoorTrigger;

    private InteractTrigger indoorDoorTrigger;

    private InteractTrigger controlRoomFireTrigger;

    private InteractTrigger indoorFireTrigger;
    
    private Renderer[] roomRenderers;

    public AudioSource AmbienceAudio;
    public AudioClip ambienceClip;
    public AudioSource DelugePumpAudio;
    public AudioClip delugePumpWhir;
    public AudioClip delugeFlow;

    public Animator reservoirWaterAnimator;

    public bool isDraining;
    private float drainTimer;

    private List<ItemSpawner> basinSpawners;
    
    private List<Renderer> renderers;

    private List<ItemSpawner> basinScrapSpawners = new List<ItemSpawner>();
    
    public Animator kyividAnimator;
    public int kyividState = 1;

    public ControlRoomSteamValve valve;

    public Animator PCAnimator;

    public ItemSpawner.WeightedItemRefrence[] customBasinScrapList;

    public void Awake()
    {
        renderers = GetActiveRenderers();
        //CullControlRoom(true);
        if (Instance == null) Instance = this;
        if (dockHandler == null) dockHandler = FindObjectOfType<ApparatusDockHandler>();
        SetExitReferences();
        
        AmbienceAudio.clip = ambienceClip;
        AmbienceAudio.Play();
        AmbienceAudio.loop = true;

        AmbienceAudio.volume = 0f;
    }

    public void StartUpPC()
    {
        PCAnimator.SetBool("On", true);
        if (MiscAudios.Length > 0 && MiscAudios[0] != null)
        {
            MiscAudios[0].volume = 0.8f;
            MiscAudios[0].pitch = 0.5f;
            StartCoroutine(StopAudioAfterFade(MiscAudios[0], 0.5f));
        }
        StartCoroutine(PCAudiosCoroutine());
    }

    public IEnumerator PCAudiosCoroutine()
    {
        MonitorAudio.volume = 1f;
            
        if (miscClips[1] != null)
        {
            MonitorAudio.PlayOneShot(miscClips[1]);
        }

        yield return new WaitForSeconds(1f);
        
        yield return null;
    }

    public IEnumerator StopAudioAfterFade(AudioSource audioSource, float fadeSeconds)
    {
        float startingVolume = audioSource.volume;
        float currentTime = 0;
        
        while (currentTime < fadeSeconds)
        {
            currentTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startingVolume, 0f, currentTime / fadeSeconds);
            yield return null; 
        }
        
        audioSource.volume = 0f;
        audioSource.Stop();
        audioSource.volume = startingVolume; 
    }
    
    public IEnumerator StartAudioWithFade(AudioSource audioSource, float fadeSeconds)
    {
        float targetVolume = audioSource.volume;
        float currentTime = 0;
    
        audioSource.volume = 0f;
        audioSource.Play();
    
        while (currentTime < fadeSeconds)
        {
            currentTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, currentTime / fadeSeconds);
            yield return null; 
        }
    
        // Ensure it ends exactly at the target volume
        audioSource.volume = targetVolume;
    }
    
    //[ServerRpc(RequireOwnership = false)]
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void StartDelugeServerRpc()
    {
        OpaliteMoonPlugin.Log.LogDebug("Starting Deluge On Server");
        StartDelugeClientRpc();
    }
    
    //[ClientRpc]
    [Rpc(SendTo.ClientsAndHost)]
    public void StartDelugeClientRpc()
    {
        OpaliteMoonPlugin.Log.LogDebug("Starting Deluge On Client");
        StartCoroutine(DelugeFloodEvent());
        isDraining = true;
    }

    //[ServerRpc(RequireOwnership = false)]
    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void OnBeginPowerServerRpc()
    {
        OnBeginPowerClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void OnBeginPowerClientRpc()
    {
        AmbienceAudio.Stop();
        StartCoroutine(BigDoorOpen());
    }
    
    [Rpc(SendTo.ClientsAndHost)]
    public void OnEndPowerClientRpc()
    {
        if (PCAnimator != null) PCAnimator.SetBool("On", false);

        if (garageDoorAnimator != null)
        {
            if (garageDoorAnimator.GetBool("Open"))
            {
                // FIX: Check if custom array audio exists first. If missing, drop back to doorOpeningSfx safely
                if (DoorAudio != null && miscClips != null && miscClips.Length > 2 && miscClips[2] != null)
                {
                    DoorAudio.Stop();
                    DoorAudio.PlayOneShot(miscClips[2], 1f); 
                }
                else if (DoorAudio != null && doorOpeningSfx != null)
                {
                    DoorAudio.Stop();
                    DoorAudio.PlayOneShot(doorOpeningSfx, 1f); // Safe default layout backup sound
                }
            }
            
            garageDoorAnimator.SetBool("Open", false);
        }
    }

    private void SetExitReferences()
    {
        InitExitListeners();
    }

    public void BeforeLoadNewLevel(int currentSeed)
    {
        if (!RoundManager.Instance.hasInitializedLevelRandomSeed) OpaliteMoonPlugin.Log.LogDebug("[ControlRoomManager.BeforeLoadNewLevel] wtf.");
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager.BeforeLoadNewLevel] setting up lockers with seed {currentSeed}.");
        KyvidRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 2352);
        kyividAnimator.SetInteger("state", kyividState);
        SetupLockersServerRpc();
        SetupBasinScrap();
    }

    private void SetupBasinScrap()
    {
        Debug.Log("[ControlRoomManager] >>> SetupBasinScrap STARTED <<<");

        GameObject parent = GameObject.Find("BasinScrapSpawns");
        if (parent == null)
        {
            Debug.LogError("[ControlRoomManager] Could not find BasinScrapSpawns parent");
            return;
        }

        Debug.Log("[ControlRoomManager] Step 1: Found BasinScrapSpawns GameObject.");

        GameObject[] foundNodes = parent.GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && t.name.StartsWith("ScrapNode"))
            .Select(t => t.gameObject)
            .ToArray();

        Debug.Log($"[ControlRoomManager] Step 2: Found {foundNodes.Length} ScrapNodes.");

        if (foundNodes.Length == 0)
        {
            Debug.LogError("[ControlRoomManager] BasinScrapSpawns has no children starting with 'ScrapNode'");
            return;
        }

        int scrapSeed = StartOfRound.Instance != null ? StartOfRound.Instance.randomMapSeed + 393 : 393;
        BasinRandom = new System.Random(scrapSeed);

        if (StartOfRound.Instance.currentLevel == null)
        {
            Debug.LogError("[ControlRoomManager] !!! CRITICAL ERROR: targetLevel passed into SetupBasinScrap is NULL !!!");
            return;
        }
        if (StartOfRound.Instance.currentLevel.spawnableScrap == null)
        {
            Debug.LogError("[ControlRoomManager] !!! CRITICAL ERROR: targetLevel.spawnableScrap is NULL !!!");
            return;
        }
        Debug.Log($"[ControlRoomManager] Step 3: targetLevel spawnableScrap count is {StartOfRound.Instance.currentLevel.spawnableScrap.Count}");

        int scrapSpawnCount = 10 + BasinRandom.Next(4);
        List<JLL.Components.ItemSpawner.WeightedItemRefrence> highValueScrapList = new();

        if (StartOfRound.Instance.currentLevel != null && StartOfRound.Instance.currentLevel.spawnableScrap != null)
        {
            foreach (var itemRarity in StartOfRound.Instance.currentLevel.spawnableScrap)
            {
                if (itemRarity == null) { Debug.Log("[ControlRoomManager] Warning: itemRarity entry is null"); continue; }
                if (itemRarity.spawnableItem == null) { Debug.Log("[ControlRoomManager] Warning: spawnableItem inside entry is null"); continue; }


                if (itemRarity.spawnableItem.minValue >= 70)
                {
                    highValueScrapList.Add(new JLL.Components.ItemSpawner.WeightedItemRefrence() { Item = itemRarity.spawnableItem, Weight = itemRarity.rarity, FindRegisteredItem = false });
                }
            }

            if (highValueScrapList.Count == 0)
            {
                Debug.Log("[ControlRoomManager] No items found with minValue >= 70, checking maxValue...");
                foreach (var itemRarity in StartOfRound.Instance.currentLevel.spawnableScrap)
                {
                    if (itemRarity == null || itemRarity.spawnableItem == null) continue;

                    if (itemRarity.spawnableItem.maxValue >= 70)
                    {
                        highValueScrapList.Add(new JLL.Components.ItemSpawner.WeightedItemRefrence() { Item = itemRarity.spawnableItem, Weight = itemRarity.rarity, FindRegisteredItem = false });
                    }
                }
            }
        }

        var customListArray = highValueScrapList.ToArray();
        Debug.Log($"[ControlRoomManager] Step 4: customListArray generated. Built list contains {customListArray.Length} item variants.");

        if (customListArray == null || customListArray.Length == 0)
        {
            Debug.LogWarning("[ControlRoomManager] customListArray for scrap spawners is empty!");
            return;
        }

        Debug.Log($"[ControlRoomManager] Step 5: Preparing to loop and spawn {scrapSpawnCount} templates.");
        int successfullyCreatedSpawners = 0;

        for (int i = 0; i < scrapSpawnCount; i++)
        {
            int selectedNode = BasinRandom.Next(0, foundNodes.Length);
            if (foundNodes[selectedNode] == null) continue;

            JLL.Components.ItemSpawner spawner = new GameObject("BasinItemSpawner").AddComponent<JLL.Components.ItemSpawner>();
            if(spawner == null) Debug.LogError("[ControlRoomManager] Could not find BasinItemSpawner");
            Vector2 randomOffset = GetRandomPointInCircleForBasin(5f);
            spawner.transform.position = foundNodes[selectedNode].transform.position + new Vector3(randomOffset.x, 5, randomOffset.y);

            int attempts = 0;
            while (!Physics.Raycast(spawner.transform.position, -Vector3.up, out var hitInfo, 80f, 268437761, QueryTriggerInteraction.Ignore) && attempts < 10)
            {
                randomOffset = GetRandomPointInCircleForBasin(5f);
                spawner.transform.position = foundNodes[selectedNode].transform.position + new Vector3(randomOffset.x, 5, randomOffset.y);
                attempts++;
            }

            spawner.enabled = false;
            spawner.spawnOnEnabled = true;

            if (SpawnPoolSource.LevelItems != null)
            {
                spawner.SourcePool = SpawnPoolSource.LevelItems;
            }
            else
            {
                Debug.LogError($"[ControlRoomManager] Spawner loop index {i}: SpawnPoolSource.LevelItems is NULL! Items will not instantiate.");
            }
            spawner.CustomList = customListArray;
            spawner.spawnRotation = RotationType.RandomRotation;
            spawner.transform.localEulerAngles = new Vector3(0, BasinRandom.Next(0, 360), 0);

            basinScrapSpawners.Add(spawner);

            spawner.gameObject.SetActive(true);
            successfullyCreatedSpawners++;
        }

        Debug.Log($"[ControlRoomManager] >>> SetupBasinScrap FINISHED <<< Successfully queued {successfullyCreatedSpawners} spawners.");
    }

    public Vector2 GetRandomPointInCircleForBasin(float radius)
    {
        float randomAngle = (float)(BasinRandom.NextDouble() * 2 * Mathf.PI);
        
        float randomRadius = (float)Math.Sqrt(BasinRandom.NextDouble()) * radius;
        
        float x = randomRadius * Mathf.Cos(randomAngle);
        float y = randomRadius * Mathf.Sin(randomAngle);
        
        return new Vector2(x, y);
    }
    
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SetupLockersServerRpc()
    {
        LockersRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 214);
        bool[] lockerStates = new bool[lockers.Length];
        for (int i = 0; i < lockers.Length; i++)
        {
            if (LockersRandom.Next() % 4 != 0) lockerStates[i] = true;
        }
        SetupLockersClientRpc(lockerStates);
    }
    
    //[ClientRpc]
    [Rpc(SendTo.ClientsAndHost)]
    private void SetupLockersClientRpc(bool[] lockerStates)
    {
        for (int i = 0; i < lockers.Length; i++)
        {
            lockers[i].gameObject.SetActive(lockerStates[i]);
        }
    }
    
    private IEnumerator DelugeFloodEvent() 
{
    if (DelugePumpAudio != null)
    {
        if (delugePumpWhir != null) DelugePumpAudio.PlayOneShot(delugePumpWhir);
        DelugePumpAudio.clip = delugeFlow;
        DelugePumpAudio.Play();
        DelugePumpAudio.loop = true;
    }
    
    if (controlRoomLights != null)
    {
        foreach (GameObject obj in controlRoomLights)
        {
            if (obj != null) obj.SetActive(false);
        }
    }
    
    if (miscClips != null && miscClips.Length > 0 && miscClips[0] != null && AmbienceAudio != null)
    {
        AmbienceAudio.clip = miscClips[0];
        AmbienceAudio.Play();

        if (MiscAudios != null && MiscAudios.Length > 1 && MiscAudios[1] != null)
        {
            MiscAudios[1].clip = miscClips[0];
            MiscAudios[1].volume = 1f;
            StartCoroutine(StartAudioWithFade(MiscAudios[1], 0.5f));
        }
    }
    
    if (PCAnimator != null) PCAnimator.SetBool("DamActive", true);
    
    yield return new WaitForSeconds(1f);
    
    isRainingInside = true;
    
    if (MonitorAudio != null && monitorAlarmBeep != null)
        MonitorAudio.PlayOneShot(monitorAlarmBeep);
    
    if (controlRoomLights2 != null)
    {
        foreach (GameObject obj in controlRoomLights2)
        {
            if (obj != null) obj.SetActive(true);
        }
    }

    BurstValve(); 
    
    yield return new WaitForSeconds(1f);

    if (basinScrapSpawners != null)
    {
        foreach (ItemSpawner spawner in basinScrapSpawners)
        {
            if (spawner == null) continue;
            Debug.Log("[ControlRoomManager] enabling scrap spawner.");
            spawner.enabled = true;
            if (spawner.gameObject != null) spawner.gameObject.SetActive(true);
        }
    }
    else
    {
        Debug.LogWarning("[ControlRoomManager] basinScrapSpawners is null on this client!");
    }
    
    // Wait a brief frame sequence block to allow network items to settle on the floor
    yield return new WaitForSeconds(0.6f);
    
    bool dogDisabled = OpaliteMoonPlugin.SoppingZedDogEnabled != null && !OpaliteMoonPlugin.SoppingZedDogEnabled.Value;
    bool replaceWithRegularScrap = OpaliteMoonPlugin.ReplaceWithRegularScrap != null && OpaliteMoonPlugin.ReplaceWithRegularScrap.Value;
    bool shouldBeMuddy = dogDisabled || replaceWithRegularScrap;

    if (shouldBeMuddy && basinScrapSpawners != null)
    {
        foreach (ItemSpawner spawner in basinScrapSpawners)
        {
            if (spawner == null) continue;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Collider[] hitColliders = Physics.OverlapSphere(spawner.transform.position, 4f);
                bool foundItemThisSpawner = false;

                foreach (var col in hitColliders)
                {
                    GrabbableObject scrapItem = col.GetComponentInParent<GrabbableObject>();
                    if (scrapItem == null) continue;

                    if (col.gameObject.GetComponentInParent<PlayerControllerB>() != null) continue;

                    foundItemThisSpawner = true;

                    if (scrapItem.GetComponent<MudVariantApplier>() == null)
                    {
                        scrapItem.gameObject.AddComponent<MudVariantApplier>();
                        Debug.Log($"[ControlRoomManager] Successfully converted basin item {scrapItem.itemProperties.itemName} to Muddy Variant via OverlapSphere sweep.");
                    }
                }

                if (foundItemThisSpawner) break; 
                yield return new WaitForSeconds(0.1f); 
            }
        }
    }

    var itemKey = NamespacedKey<DawnItemInfo>.From("opalite_moon", "sopping_zed_dog");
    Item soppyzedProperties = null;
    
    if (LethalContent.Items != null && LethalContent.Items.ContainsKey(itemKey))
    {
        var lookupResult = LethalContent.Items[itemKey];
        if (lookupResult != null) soppyzedProperties = lookupResult.Item;
    }

    if (soppyzedProperties != null)
    {
        foreach (var grabbableObject in FindObjectsByType<GrabbableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (grabbableObject == null || string.IsNullOrEmpty(grabbableObject.name) || !grabbableObject.name.Contains("SoppingZed")) continue;
            
            if (BasinRandom != null) grabbableObject.floorYRot = BasinRandom.Next(360);
            
            if (grabbableObject.itemProperties != soppyzedProperties)
                grabbableObject.itemProperties = soppyzedProperties;
        }
    }
    
    drainTimer = 0f;
    
    if (reservoirWaterAnimator != null)
    {
        reservoirWaterAnimator.SetBool("Drain", true);
        reservoirWaterAnimator.SetBool("Filled", false);
    }
    
    if (HUDManager.Instance != null)
        HUDManager.Instance.DisplayTip("???", "The dam's flood gate has opened.");
    
    yield return null;
}

    public void BurstValve()
    {
        valve.valveHasBurst = true;
        valve.BurstValve();
    }
    
    private IEnumerator BigDoorOpen() 
    {
        yield return new WaitForSeconds(3f);
        
        garageDoorAnimator.SetBool("Open", true);
        DoorAudio.clip = doorOpeningSfx;
        DoorAudio.Play();
        yield return new WaitForSeconds(2f);
        StartCoroutine(StopAudioAfterFade(DoorAudio, 0.5f));
    }
    
    private IEnumerator BigDoorClose() 
    {
        yield return new WaitForSeconds(1f);
        garageDoorAnimator.SetBool("Open", false);
    }
    
    public void InitExitListeners()
    {
        indoorDoorTrigger = controlRoomTeleport.GetComponent<InteractTrigger>();
        controlRoomDoorTrigger = outdoorControlRoomTeleport.GetComponent<InteractTrigger>();
        controlRoomDoorTrigger.onInteract.AddListener(OutdoorControlRoomDoorTeleportPlayer);
        indoorDoorTrigger.onInteract.AddListener(IndoorControlRoomDoorTeleportPlayer);
        
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] added event controlRoomTeleportAction to controlRoomFireTrigger.onInteract");
    }

    public void OutdoorControlRoomDoorTeleportPlayer(PlayerControllerB player)
    {
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Teleported from Outside -> ControlRoom. Teleport finished from local player {player.actualClientId}");
        UpdateControlRoomPresenceServerRpc(player.actualClientId, true);
    }
    
    public void IndoorControlRoomDoorTeleportPlayer(PlayerControllerB player)
    {
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Teleported from ControlRoom -> Outside. Teleport finished from local player {player.actualClientId}");
        UpdateControlRoomPresenceServerRpc(player.actualClientId, false);
    }
    
    //[ServerRpc(RequireOwnership = false)]
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void UpdateControlRoomPresenceServerRpc(ulong clientId, bool isEntering)
    {
        UpdateControlRoomPresenceClientRpc(clientId, isEntering);
    }
    
    public void LateUpdate()
    {
        hasBeenPowered = dockHandler.isPowered;
        if(!isPoweredOld && hasBeenPowered) OnBeginPowerServerRpc();
        isPoweredOld = dockHandler.isPowered;

        if (isDraining)
        {
            if (drainTimer < 1)
            {
                drainTimer += Time.deltaTime / 42f; // 50 seconds
                if (drainTimer > 1)
                {
                    drainTimer = 1;
                    isRainingInside = false;
                    StartCoroutine(StopAudioAfterFade(AmbienceAudio, 0.5f));
                    StartCoroutine(StopAudioAfterFade(MiscAudios[1], 0.5f));
                }
                
                reservoirWaterAnimator.SetFloat("Time", drainTimer);
            }
        }

        if (kyividAnimator != null)
        {
            kyividAnimator.SetFloat("timeOfDay", TimeOfDay.Instance.normalizedTimeOfDay);
        }

        if (StartOfRound.Instance.localPlayerController.isInsideFactory && isRainingInside)
        {
            TimeOfDay.Instance.effects[(int)LevelWeatherType.Rainy].effectEnabled = true;
        }
        else
        {
            if (StartOfRound.Instance.localPlayerController.isInsideFactory)
            {
                TimeOfDay.Instance.effects[(int)LevelWeatherType.Rainy].effectEnabled = false;
            }
            else
            {
                TimeOfDay.Instance.effects[(int)LevelWeatherType.Rainy].effectEnabled = (TimeOfDay.Instance.currentLevelWeather ==  LevelWeatherType.Rainy);
            }
        }
    }
    
    //[ClientRpc]
    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateControlRoomPresenceClientRpc(ulong clientId, bool isEntering)
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
                PlayerEnterControlRoom(targetPlayer);
                addon.EnterControlRoom();
            }
        }
        else
        {
            if (playersInsideControlRoom.Contains(targetPlayer))
            {
                PlayerExitControlRoom(targetPlayer);
                addon.LeaveControlRoom();
            }
        }
    }
    
    private void PlayerEnterControlRoom(PlayerControllerB player)
    {
        playersInsideControlRoom.Add(player);
        if (player == StartOfRound.Instance.localPlayerController)
        {
            //CullControlRoom(false);
            AmbienceAudio.volume = 1f;
        }
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Added {player.playerUsername} to Control Room");
    }
    
    private void PlayerExitControlRoom(PlayerControllerB player)
    {
        playersInsideControlRoom.Remove(player);
        if (player == StartOfRound.Instance.localPlayerController)
        {
            //CullControlRoom(true);
            AmbienceAudio.volume = 0f;
        }
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Removed {player.playerUsername} from Control Room");
    }

    public List<Renderer> GetActiveRenderers()
    {
        // this doesnt work right
        List<Renderer> allRenderers = gameObject.GetComponentsInChildren<Renderer>().ToList();
        List<Renderer> activeRenderers = new List<Renderer>();
        foreach (Renderer renderer in allRenderers)
        {
            if(renderer.enabled)
            {
                activeRenderers.Add(renderer);
            }
        }
        
        return activeRenderers;
    }

    public void CullControlRoom(bool cull)
    {
        // this doesnt work right
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = !cull;
        }
    }
}

public class PlayerDetector : NetworkBehaviour
{
    public bool triggerOnce = true;
    private bool triggeredOnce;
    public UnityEvent OnEnterTriggerEventsAllClients;
    public UnityEvent OnEnterTriggerEventsThisClient;
    public void OnTriggerEnter(Collider other)
    {
        
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        Debug.Log("[PlayerDetector] something has collided at least...");
        if (other.gameObject == player.gameObject)
        {
            if (triggerOnce && triggeredOnce) return;
            TriggerEnteredServerRpc(player.actualClientId);
            Debug.Log($"[PlayerDetector] Player {player.gameObject.name} has entered the trigger of {gameObject.name} [ON CLIENT]");
        }
    }

    [Rpc(SendTo.Server,  RequireOwnership = false)]
    public void TriggerEnteredServerRpc(ulong playerID)
    {
        TriggerEnteredClientRpc(playerID);
    }
    
    [Rpc(SendTo.ClientsAndHost)]
    protected virtual void TriggerEnteredClientRpc(ulong playerID)
    {
        PlayerControllerB enteredPlayer = null;
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if(player.actualClientId == playerID) 
            {
                enteredPlayer = player;
                break;
            }
        }

        if (enteredPlayer == null) 
        {
            OpaliteMoonPlugin.Log.LogWarning($"Could not find player with ID {playerID}");
            return; 
        }

        triggeredOnce = true;
        Debug.Log($"Player {enteredPlayer.gameObject.name} has entered the trigger of {gameObject.name} [ALL CLIENTS]");
        OpaliteMoonPlugin.Log.LogDebug($"Player {enteredPlayer.gameObject.name} has entered the trigger of {gameObject.name} [ALL CLIENTS]");
        OnEnterTriggerEventsAllClients.Invoke();
        if (StartOfRound.Instance.localPlayerController == enteredPlayer)
        {
            OnEnterTriggerEventsThisClient.Invoke();
        }
    }
}