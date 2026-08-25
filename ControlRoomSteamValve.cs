using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OpaliteMoonMod;

// copy of SteamValveHazard but how i like
public class ControlRoomSteamValve : MonoBehaviour
{
    public float valveCrackTime;

	public float valveBurstTime;

	public bool valveHasBurst;

	private bool valveHasCracked;

	private bool valveHasBeenRepaired;

	public InteractTrigger triggerScript;

	[Header("Fog")]
	public Animator fogAnimator;

	public Animator valveAnimator;

	public float fogSizeMultiplier;

	public float currentFogSize;

	[Header("Other Effects")]
	public ParticleSystem valveSteamParticle;

	public AudioClip[] pipeFlowingSFX;

	public AudioClip valveTwistSFX;

	public AudioClip valveBurstSFX;

	public AudioClip valveCrackSFX;

	public AudioClip steamBlowSFX;

	public AudioSource valveAudio;

	public GameObject spawnParticleWhenCracked;

	public ControlRoomSteamValveFixInteraction fixInteract;

	private const string steamHUDWarningText = "VISIBILITY LOW!\n\nSteam leak detected in area";

	private void Start()
	{
		if (pipeFlowingSFX.Length != 0)
		{
			valveAudio.pitch = Random.Range(0.85f, 1.1f);
			valveAudio.clip = pipeFlowingSFX[Random.Range(0, pipeFlowingSFX.Length)];
			valveAudio.Play();
		}
	}

	private void Update()
	{
		if (StartOfRound.Instance.allPlayersDead || NetworkManager.Singleton == null || !GameNetworkManager.Instance.gameHasStarted)
		{
			return;
		}
		
		if (valveHasBeenRepaired)
		{
			currentFogSize = Mathf.Clamp(currentFogSize - Time.deltaTime / 4f, 0.01f, 1f * fogSizeMultiplier);
			fogAnimator.SetFloat("time", currentFogSize);
		}
		else if (!valveHasCracked && valveCrackTime > 0f && TimeOfDay.Instance.normalizedTimeOfDay > valveCrackTime)
		{
			valveHasCracked = true;
			CrackValve();
		}
		else if (valveHasBurst && fogAnimator != null)
		{
			currentFogSize = Mathf.Clamp(currentFogSize + Time.deltaTime / 12f, 0f, 1f * fogSizeMultiplier);
			fogAnimator.SetFloat("time", currentFogSize);
			if (Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, valveAudio.transform.position) < 10f)
			{
				HUDManager.Instance.increaseHelmetCondensation = true;
				HUDManager.Instance.DisplayStatusEffect("VISIBILITY LOW!\n\nSteam leak detected in area");
			}
		}
	}

	private void CrackValve()
	{
		valveAudio.PlayOneShot(valveCrackSFX);
		WalkieTalkie.TransmitOneShotAudio(valveAudio, valveCrackSFX);
		if (valveSteamParticle != null)
		{
			ParticleSystem.MainModule main = valveSteamParticle.main;
			main.loop = false;
			valveSteamParticle.Play();
		}
	}

	public void BurstValve()
	{
		if (spawnParticleWhenCracked != null)
		{
			Instantiate(spawnParticleWhenCracked, base.transform.position, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
			Destroy(base.gameObject);
			return;
		}
		ParticleSystem.MainModule main = valveSteamParticle.main;
		main.loop = true;
		valveSteamParticle.Play();
		valveAudio.clip = steamBlowSFX;
		valveAudio.Play();
		valveAudio.PlayOneShot(valveBurstSFX);
		WalkieTalkie.TransmitOneShotAudio(valveAudio, valveBurstSFX);
		if (triggerScript != null)
		{
			triggerScript.interactable = true;
		}
	}

	public void FixValveLocalClient()
	{
		if (valveHasBurst && !valveHasBeenRepaired)
		{
			valveSteamParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
			valveAudio.clip = pipeFlowingSFX[Random.Range(0, pipeFlowingSFX.Length)];
			valveAudio.Play();
			valveAudio.PlayOneShot(valveTwistSFX, 1f);
			WalkieTalkie.TransmitOneShotAudio(valveAudio, valveTwistSFX);
			valveAnimator.SetTrigger("TwistValve");
			valveHasBeenRepaired = true;
			triggerScript.interactable = false;
		}
	}

	public void FixValve()
	{
		fixInteract.FixValve();
	}
}

public class ControlRoomSteamValveFixInteraction : NetworkBehaviour
{
	public ControlRoomSteamValve steamValveMain;

	public void FixValve()
	{
		steamValveMain.FixValveLocalClient();
		if (base.IsServer)
		{
			FixValveClientRpc();
		}
		else
		{
			FixValveServerRpc();
		}
	}

	//[ServerRpc(RequireOwnership = false)]
	[Rpc(SendTo.Server, RequireOwnership = false)]
	public void FixValveServerRpc()
	{
		FixValveClientRpc();
	}

	//[ClientRpc]
	[Rpc(SendTo.ClientsAndHost)]
	public void FixValveClientRpc()
	{
		steamValveMain.FixValveLocalClient();
	}
}