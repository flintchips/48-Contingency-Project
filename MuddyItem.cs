using UnityEngine;

namespace OpaliteMoonMod;

public class MuddyItem : MonoBehaviour
{
    public GrabbableObject baseGrabbableObject;
    
    public float muddy = 1;

    public static MuddyItem Create(GrabbableObject grabbableObject)
    {
        MuddyItem muddyComponent = grabbableObject.gameObject.AddComponent<MuddyItem>();
    
        muddyComponent.baseGrabbableObject = grabbableObject;
    
        return muddyComponent;
    }

    public void Start()
    {
        if (baseGrabbableObject != null)
        {
            if (muddy >= 0)
            {
                Debug.Log("[MUDDY ITEM] HI IM A MUDDY "+ baseGrabbableObject.transform.name);
                ScanNodeProperties scanNode = baseGrabbableObject.GetComponentInChildren<ScanNodeProperties>();
                if (scanNode != null)
                {
                    // Add "Muddy " to the header text if it isn't already added
                    if (!scanNode.headerText.StartsWith("Muddy "))
                    {
                        scanNode.headerText = "Muddy " + scanNode.headerText;
                    }
                }
            }
        }
    }

}