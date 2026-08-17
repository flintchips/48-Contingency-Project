using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using JLL.Components;

namespace OpaliteMoonMod;

public class ControlRoomManager : NetworkBehaviour
{
    public ApparatusDockHandler dockHandler;

    public System.Random LockersRandom;
    public System.Random BasinRandom;
    public System.Random KyvidRandom;
    
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

    private List<ItemSpawner> basinScrapSpawners = new List<ItemSpawner>();


    public Animator kyividAnimator;
    public int kyividState = 1;

    public void Awake()
    {
        if (dockHandler == null) dockHandler = FindObjectOfType<ApparatusDockHandler>();
        SetExitReferences();
        
        /*
        // for culling when ur not in the room
        roomRenderers = roomRenderers = GetComponentsInChildren<Renderer>(true);
        if (!playersInsideControlRoom.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            CullControlRoom(true);
        }*/
        
        AmbienceAudio.clip = ambienceClip;
        AmbienceAudio.Play();
        AmbienceAudio.loop = true;

        AmbienceAudio.volume = 0f;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void StartDelugeServerRpc()
    {
        OpaliteMoonPlugin.Log.LogDebug("Starting Deluge On Server");
        StartDelugeClientRpc();
    }
    
    [ClientRpc()]
    public void StartDelugeClientRpc()
    {
        OpaliteMoonPlugin.Log.LogDebug("Starting Deluge On Client");
        Debug.Log("starting deluge");
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
        reservoirWaterAnimator.SetBool("Drain", true);
        reservoirWaterAnimator.SetBool("Filled", false);
        DelugePumpAudio.PlayOneShot(delugePumpWhir);
        DelugePumpAudio.clip = delugeFlow;
        DelugePumpAudio.Play();
        DelugePumpAudio.loop = true;
        drainTimer = 0f;
        yield return null;
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
                drainTimer += Time.deltaTime / 30f; // 30 seconds
                if (drainTimer > 1)
                {
                    drainTimer = 1;
                }
                
                reservoirWaterAnimator.SetFloat("Time", drainTimer);
                //Debug.Log($"[ControlRoomManager] Draining: {drainTimer}");
            }
        }

        if (kyividAnimator != null)
        {
            //Debug.Log(TimeOfDay.Instance.normalizedTimeOfDay);
            kyividAnimator.SetFloat("timeOfDay", TimeOfDay.Instance.normalizedTimeOfDay);
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
            AmbienceAudio.volume = 1f;
        }
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Added {player.playerUsername} to Control Room");
    }
    
    private void PlayerExitControlRoom(PlayerControllerB player)
    {
        playersInsideControlRoom.Remove(player);
        if (player.IsClient)
        {
            AmbienceAudio.volume = 0f;
        }
        OpaliteMoonPlugin.Log.LogDebug($"[ControlRoomManager] Removed {player.playerUsername} from Control Room");
    }

    /*private void CullControlRoom(bool cull)
    {
        foreach (Renderer renderer in roomRenderers)
        {
            if (renderer != null)
            {
                int layerIndex = renderer.gameObject.layer;
                if(LayerMask.LayerToName(layerIndex) != "Scan Node")
                    renderer.enabled = !cull;
                else
                {
                    renderer.enabled = false;
                }
            }
        }
    }*/
}