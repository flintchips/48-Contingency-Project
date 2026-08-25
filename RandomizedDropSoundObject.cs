using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OpaliteMoonMod;

public class RandomizedDropSoundObject : PhysicsProp
{
    private int timesAudioDropped;
    public int playAfterTimesDropped = 0;
    public AudioClip rareDropSound;
    public float dropSoundChance = 0.11f;
    public override void PlayDropSFX()
    {
        if (itemProperties.dropSFX != null)
        {
            timesAudioDropped++;
            AudioClip sound = ((Random.value <= dropSoundChance) && rareDropSound != null && (timesAudioDropped > playAfterTimesDropped))? rareDropSound : itemProperties.dropSFX;
            AudioSource component = base.gameObject.GetComponent<AudioSource>();
            component.PlayOneShot(sound);
            WalkieTalkie.TransmitOneShotAudio(component, sound);
            if (base.IsOwner)
            {
                RoundManager.Instance.PlayAudibleNoise(base.transform.position, 8f, 0.5f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed, 941);
            }
        }
        hasHitGround = true;
    }
}