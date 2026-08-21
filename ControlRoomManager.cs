using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using JLL.Components;
using UnityEngine.Events;

namespace OpaliteMoonMod;

public class ControlRoomManager : NetworkBehaviour
{
    public static ControlRoomManager Instance;
    public ApparatusDockHandler dockHandler;
    public bool isRainingInside;
    
    public System.Random LockersRandom;
    public System.Random BasinRandom;
    public System.Random KyvidRandom;

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
    
    private List<Renderer> renderers;

    private List<ItemSpawner> basinScrapSpawners = new List<ItemSpawner>();
    
    public Animator kyividAnimator;
    public int kyividState = 1;

    public ControlRoomSteamValve valve;

    public Animator PCAnimator;

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
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void StartDelugeServerRpc()
    {
        OpaliteMoonPlugin.Log.LogDebug("Starting Deluge On Server");
        StartDelugeClientRpc();
    }
    
    [ClientRpc]
    public void StartDelugeClientRpc()
    {
        OpaliteMoonPlugin.Log.LogDebug("Starting Deluge On Client");
        StartCoroutine(DelugeFloodEvent());
        isDraining = true;
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

    private void SetExitReferences()
    {
        InitExitListeners();
    }

    // called from RoundManager.LoadNewLevel Prefix patch
    public void BeforeLoadNewLevel(int currentSeed)
    {
        if (!RoundManager.Instance.hasInitializedLevelRandomSeed) OpaliteMoonPlugin.Log.LogDebug("[ControlRoomManager.BeforeLoadNewLevel] wtf.");
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager.BeforeLoadNewLevel] setting up lockers with seed {currentSeed}.");
        KyvidRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 2352);
        kyividAnimator.SetInteger("state", kyividState);
        SetupLockersServerRpc();
        SetupBasinScrapServerRpc();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SetupBasinScrapServerRpc()
    {
        GameObject[] foundNodes = GameObject.FindGameObjectsWithTag("BasinScrapNode");
        BasinRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 393);
        int scrapSpawnCount = 10 + BasinRandom.Next(2);
        
        SetupBasinScrapClientRpc();
    }

    private ItemSpawner SpawnScrapAt(Vector3 postion)
    {
        GameObject spawnerGameObject = new GameObject("scrapSpawner");
        ItemSpawner spawner = spawnerGameObject.AddComponent<ItemSpawner>();
        spawner.enabled = false;
        spawner.spawnOnEnabled = true;
        spawner.SourcePool = SpawnPoolSource.LevelItems;
        spawner.spawnRotation = RotationType.RandomRotation;
        spawnerGameObject.transform.position = postion;
        return spawner;
    }
    
    [ClientRpc]
    private void SetupBasinScrapClientRpc()
    {

    }
    
    [ServerRpc(RequireOwnership = false)]
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
    
    [ClientRpc]
    private void SetupLockersClientRpc(bool[] lockerStates)
    {
        for (int i = 0; i < lockers.Length; i++)
        {
            lockers[i].gameObject.SetActive(lockerStates[i]);
        }
    }
    
    private IEnumerator DelugeFloodEvent() 
    {
        DelugePumpAudio.PlayOneShot(delugePumpWhir);
        DelugePumpAudio.clip = delugeFlow;
        DelugePumpAudio.Play();
        DelugePumpAudio.loop = true;
        
        foreach (GameObject obj in controlRoomLights)
        {
            obj.SetActive(false);
        }
        
        yield return new WaitForSeconds(1f);
        
        PCAnimator.SetBool("DamActive", true);
        
        isRainingInside = true;
        
        MonitorAudio.PlayOneShot(monitorAlarmBeep);
        
        foreach (GameObject obj in controlRoomLights2)
        {
            obj.SetActive(true);
        }

        BurstValve();
        
        yield return new WaitForSeconds(1f);
        
        drainTimer = 0f;
        reservoirWaterAnimator.SetBool("Drain", true);
        reservoirWaterAnimator.SetBool("Filled", false);
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
        DoorAudio.PlayOneShot(doorOpeningSfx);
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
    
    [ServerRpc(RequireOwnership = false)]
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
                drainTimer += Time.deltaTime / 50f; // 50 seconds
                if (drainTimer > 1)
                {
                    drainTimer = 1;
                    isRainingInside = false;
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
    
    [ClientRpc]
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

    private void SpawnScrapInBasin()
    {
        
    }

    private void PlayerEnterControlRoom(PlayerControllerB player)
    {
        playersInsideControlRoom.Add(player);
        if (player.IsClient)
        {
            //CullControlRoom(false);
            AmbienceAudio.volume = 1f;
        }
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Added {player.playerUsername} to Control Room");
    }
    
    private void PlayerExitControlRoom(PlayerControllerB player)
    {
        playersInsideControlRoom.Remove(player);
        if (player.IsClient)
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
    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            OpaliteMoonPlugin.Log.LogDebug("[PlayerDetector] Some kind of player objectt has collided here..");
            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            if (player != null && player.IsLocalPlayer)
            {
                if (triggerOnce && triggeredOnce) return;
                TriggerEnteredServerRpc(player.actualClientId);
                OpaliteMoonPlugin.Log.LogDebug($"Player {player.gameObject.name} has entered the trigger of {gameObject.name} [ON CLIENT]");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TriggerEnteredServerRpc(ulong playerID)
    {
        TriggerEnteredClientRpc(playerID);
    }
    
    [ClientRpc]
    public void TriggerEnteredClientRpc(ulong playerID)
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
        OpaliteMoonPlugin.Log.LogDebug($"Player {enteredPlayer.gameObject.name} has entered the trigger of {gameObject.name} [ALL CLIENTS]");
        OnEnterTriggerEventsAllClients.Invoke();
    }
}