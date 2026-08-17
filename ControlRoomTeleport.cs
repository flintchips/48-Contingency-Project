using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpaliteMoonMod;

public class ControlRoomTeleport : NetworkBehaviour
{
    // basically a copy of ControlRoomTeleport but it does its own thing
    
    public bool isEntranceToControlRoom;

    public Transform entrancePoint;

    public int entranceId;

    public StartOfRound playersManager;

    public AudioClip customFirstTimeAudio;

    public int audioReverbPreset = -1;

    public ControlRoomTeleport exitScript;

    public AudioSource entrancePointAudio;

    public AudioClip[] doorAudios;

    public InteractTrigger triggerScript;

    private float checkForEnemiesInterval;

    private bool enemyNearLastCheck;

    private bool gotExitPoint;

    private bool checkedForFirstTime;

    public float timeAtLastUse;

    public Animator thisEntranceAnimator;

    private bool exitPointDoesntExist;

    private bool playingCreakAudio;

    public List<Light> dayLights;
    
    private void Awake()
    {
        playersManager = UnityEngine.Object.FindObjectOfType<StartOfRound>();
        triggerScript = base.gameObject.GetComponent<InteractTrigger>();
        checkForEnemiesInterval = 10f;
    }
    
    private void PlayCreakSFX()
    {
        AudioClip[] array = StartOfRound.Instance.creakOpenDoorMetal;
        int num = UnityEngine.Random.Range(0, array.Length);
        entrancePointAudio.clip = array[num];
        entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
        entrancePointAudio.Play();
        playingCreakAudio = true;
        if (exitScript != null && exitScript.thisEntranceAnimator != null)
        {
            array = StartOfRound.Instance.creakOpenDoorMetal;
            exitScript.entrancePointAudio.clip = array[num];
            exitScript.entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            exitScript.entrancePointAudio.Play();
        }
    }
    
    public void StartOpeningEntrance()
    {
        if (!GetDoorAnimators() || Time.realtimeSinceStartup - timeAtLastUse < 0.5f)
        {
            return;
        }
        if (exitScript == null && (exitPointDoesntExist || !FindExitPoint()))
        {
            exitPointDoesntExist = true;
            return;
        }
        thisEntranceAnimator.SetBool("Open", value: true);
        if (!playingCreakAudio)
        {
            PlayCreakSFX();
        }
        if (exitScript.thisEntranceAnimator != null)
        {
            exitScript.thisEntranceAnimator.SetBool("Open", value: true);
        }
        SyncStartOpeningDoorRpc();
    }
    
    [Rpc(SendTo.NotMe, RequireOwnership = false)]
    public void SyncStartOpeningDoorRpc()
    {
        if (!GetDoorAnimators())
        {
            return;
        }
        if (exitScript == null && (exitPointDoesntExist || !FindExitPoint()))
        {
            exitPointDoesntExist = true;
            return;
        }
        thisEntranceAnimator.SetBool("Open", value: true);
        OpaliteMoonPlugin.Log.LogDebug($"'{base.gameObject.name}' entrancePointAudio isPlaying: {entrancePointAudio.isPlaying}");
        if (!playingCreakAudio)
        {
            PlayCreakSFX();
        }
        if (exitScript.thisEntranceAnimator != null)
        {
            exitScript.thisEntranceAnimator.SetBool("Open", value: true);
        }
    }
    
    public void FinishOpeningEntrance(bool playShutAudio = true)
    {
        OpaliteMoonPlugin.Log.LogDebug($"Called finishopeningentrance ({playShutAudio})");
        if (entranceId != 0)
        {
            OpaliteMoonPlugin.Log.LogDebug("Id is not 0");
            return;
        }
        if (!GetDoorAnimators())
        {
            OpaliteMoonPlugin.Log.LogDebug("finishopeningentrance animator null");
            return;
        }
        if (!thisEntranceAnimator.GetBool("Open"))
        {
            if (playingCreakAudio)
            {
                entrancePointAudio.Stop();
                if (exitScript != null && exitScript.thisEntranceAnimator != null)
                {
                    exitScript.entrancePointAudio.Stop();
                }
            }
            OpaliteMoonPlugin.Log.LogDebug("Entrance teleport was not open; returning");
            return;
        }
        if (exitScript == null && (exitPointDoesntExist || !FindExitPoint()))
        {
            OpaliteMoonPlugin.Log.LogDebug("Couldn't find exit script");
            exitPointDoesntExist = true;
            OpaliteMoonPlugin.Log.LogDebug("End A");
            return;
        }
        thisEntranceAnimator.SetBool("Open", value: false);
        OpaliteMoonPlugin.Log.LogDebug("'" + base.gameObject.name + "' STOPPING entrancePointAudio");
        entrancePointAudio.Stop();
        playingCreakAudio = false;
        if (exitScript.thisEntranceAnimator != null)
        {
            exitScript.entrancePointAudio.Stop();
        }
        if (playShutAudio)
        {
            if (Time.realtimeSinceStartup - timeAtLastUse > 0.5f)
            {
                PlayAudioAtTeleportPositions();
            }
            SyncFinishOpeningEntranceRpc();
        }
        if (exitScript.thisEntranceAnimator != null)
        {
            exitScript.thisEntranceAnimator.SetBool("Open", value: false);
        }
    }
    
    [Rpc(SendTo.NotMe, RequireOwnership = false)]
    public void SyncFinishOpeningEntranceRpc()
    {
        if (entranceId != 0 || !GetDoorAnimators())
        {
            return;
        }

        if (exitScript == null && (exitPointDoesntExist || !FindExitPoint()))
        {
            exitPointDoesntExist = true;
            return;
        }

        thisEntranceAnimator.SetBool("Open", value: false);
        entrancePointAudio.Stop();
        playingCreakAudio = false;
        if (exitScript.thisEntranceAnimator != null)
        {
            exitScript.entrancePointAudio.Stop();
        }

        if (Time.realtimeSinceStartup - timeAtLastUse > 0.5f)
        {
            PlayAudioAtTeleportPositions();
        }

        if (exitScript.thisEntranceAnimator != null)
        {
            exitScript.thisEntranceAnimator.SetBool("Open", value: false);
        }
    }
    
    private bool GetDoorAnimators()
    {
        return thisEntranceAnimator != null;
    }
    
    public bool FindExitPoint()
    {
        ControlRoomTeleport[] array = UnityEngine.Object.FindObjectsOfType<ControlRoomTeleport>();
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].isEntranceToControlRoom != isEntranceToControlRoom && array[i].entranceId == entranceId)
            {
                exitScript = array[i];
            }
        }
        if (exitScript == null)
        {
            return false;
        }
        return true;
    }
    
    public void TeleportPlayer()
	{
		bool blocked = false;
		if (!FindExitPoint())
		{
			blocked = true;
		}
		if (blocked)
		{
			HUDManager.Instance.DisplayTip("???", "The entrance appears to be blocked.");
			return;
		}
        
        var localPlayer = GameNetworkManager.Instance.localPlayerController;
        Transform thisPlayerBody = localPlayer.thisPlayerBody;
        
        localPlayer.TeleportPlayer(exitScript.entrancePoint.position);
        localPlayer.isInElevator = false;
        localPlayer.isInHangarShipRoom = false;
        thisPlayerBody.eulerAngles = new Vector3(thisPlayerBody.eulerAngles.x, exitScript.entrancePoint.eulerAngles.y, thisPlayerBody.eulerAngles.z);
        
        localPlayer.isInsideFactory = isEntranceToControlRoom;

        for (int i = 0; i < localPlayer.ItemSlots.Length; i++)
        {
            if (localPlayer.ItemSlots[i] != null)
                localPlayer.ItemSlots[i].isInFactory = isEntranceToControlRoom;
        }
        if (localPlayer.ItemOnlySlot != null)
            localPlayer.ItemOnlySlot.isInFactory = isEntranceToControlRoom;
        
		FinishOpeningEntrance(playShutAudio: false);
        
		SetAudioPreset((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        
		if (!checkedForFirstTime && isEntranceToControlRoom)
		{
			checkedForFirstTime = true;
			StartCoroutine(playMusicOnDelay()); // control room music i made
		}
        
		timeAtLastUse = Time.realtimeSinceStartup;
		TeleportPlayerServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        GameNetworkManager.Instance.localPlayerController.isInsideFactory = isEntranceToControlRoom;
	}
    
    private IEnumerator playMusicOnDelay()
	{
		yield return new WaitForSeconds(0.6f);

		HUDManager.Instance.UIAudio.PlayOneShot(customFirstTimeAudio);
	}

	[ServerRpc(RequireOwnership = false)]
	public void TeleportPlayerServerRpc(int playerObj)
	{
		TeleportPlayerClientRpc(playerObj);
	}

	[ClientRpc]
	public void TeleportPlayerClientRpc(int playerObj)
	{
		if (playersManager.allPlayerScripts[playerObj] == GameNetworkManager.Instance.localPlayerController)
		{
			return;
		}
        
        if (!FindExitPoint()) return;
        
		playersManager.allPlayerScripts[playerObj].TeleportPlayer(exitScript.entrancePoint.position, withRotation: true, exitScript.entrancePoint.eulerAngles.y);
		playersManager.allPlayerScripts[playerObj].isInElevator = false;
		playersManager.allPlayerScripts[playerObj].isInHangarShipRoom = false;
        
        var player = playersManager.allPlayerScripts[playerObj];
        if (player == null || player == GameNetworkManager.Instance.localPlayerController)
            return;
        
        player.isInsideFactory = isEntranceToControlRoom;
        
        for (int i = 0; i < player.ItemSlots.Length; i++)
        {
            if (player.ItemSlots[i] != null)
                player.ItemSlots[i].isInFactory = isEntranceToControlRoom;
        }
        if (player.ItemOnlySlot != null)
            player.ItemOnlySlot.isInFactory = isEntranceToControlRoom;
        
		FinishOpeningEntrance(playShutAudio: false);
		playersManager.allPlayerScripts[playerObj].isInsideFactory = isEntranceToControlRoom;

		if (GameNetworkManager.Instance.localPlayerController.isPlayerDead && playersManager.allPlayerScripts[playerObj] == GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript)
		{
			SetAudioPreset(playerObj);
		}
		else
		{
			PlayAudioAtTeleportPositions();
		}

		timeAtLastUse = Time.realtimeSinceStartup;
	}
    
    private void SetAudioPreset(int playerObj)
    {
        if (audioReverbPreset < 0) return;
        {
            UnityEngine.Object.FindObjectOfType<AudioReverbPresets>().audioPresets[audioReverbPreset].ChangeAudioReverbForPlayer(StartOfRound.Instance.allPlayerScripts[playerObj]);
            var presets = UnityEngine.Object.FindObjectOfType<AudioReverbPresets>();
            if (presets == null || presets.audioPresets == null ||
                audioReverbPreset >= presets.audioPresets.Length) return;
            var player = StartOfRound.Instance.allPlayerScripts[playerObj];
            presets.audioPresets[audioReverbPreset].ChangeAudioReverbForPlayer(player);
        }
        
        if (entrancePointAudio != null)
            PlayAudioAtTeleportPositions();
    }
    
    public void PlayAudioAtTeleportPositions()
    {
        if (StartOfRound.Instance.testRoom != null)
        {
            return;
        }
        if (entranceId == 0)
        {
            AudioClip[] shutDoorMetal = StartOfRound.Instance.shutDoorMetal;
            AudioClip[] array = StartOfRound.Instance.shutDoorMetal;
            
            if (isEntranceToControlRoom)
            {
                entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                entrancePointAudio.PlayOneShot(shutDoorMetal[UnityEngine.Random.Range(0, shutDoorMetal.Length)]);
                exitScript.entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                exitScript.entrancePointAudio.PlayOneShot(array[UnityEngine.Random.Range(0, array.Length)]);
            }
            else
            {
                entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                entrancePointAudio.PlayOneShot(array[UnityEngine.Random.Range(0, array.Length)]);
                exitScript.entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                exitScript.entrancePointAudio.PlayOneShot(shutDoorMetal[UnityEngine.Random.Range(0, shutDoorMetal.Length)]);
            }
        }
        else if (doorAudios.Length != 0)
        {
            entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            entrancePointAudio.PlayOneShot(doorAudios[UnityEngine.Random.Range(0, doorAudios.Length)]);
            exitScript.entrancePointAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            exitScript.entrancePointAudio.PlayOneShot(doorAudios[UnityEngine.Random.Range(0, doorAudios.Length)]);
        }
    }
    
    private void Update()
    {
        if (triggerScript == null || !isEntranceToControlRoom)
        {
            return;
        }
        if (checkForEnemiesInterval <= 0f)
        {
            if (!gotExitPoint)
            {
                if (FindExitPoint())
                {
                    gotExitPoint = true;
                }
                return;
            }
            checkForEnemiesInterval = 1f;
            bool flag = false;
            for (int i = 0; i < RoundManager.Instance.SpawnedEnemies.Count; i++)
            {
                if (Vector3.Distance(RoundManager.Instance.SpawnedEnemies[i].transform.position, exitScript.entrancePoint.transform.position) < 7.7f && !RoundManager.Instance.SpawnedEnemies[i].isEnemyDead)
                {
                    flag = true;
                    break;
                }
            }
            if (flag && !enemyNearLastCheck)
            {
                enemyNearLastCheck = true;
                triggerScript.hoverTip = "[Near activity detected!]";
            }
            else if (enemyNearLastCheck)
            {
                enemyNearLastCheck = false;
                triggerScript.hoverTip = "Enter: [LMB]";
            }
        }
        else
        {
            checkForEnemiesInterval -= Time.deltaTime;
        }
    }
}