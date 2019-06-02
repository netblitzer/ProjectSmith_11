using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour {

    // The root component that all other components will attach to. This is by default the first component
    //  that was loaded in to the forge.
    public Component rootComponent { get; private set; }

    public string weaponName;

    public Weapon ( ) {
        this.rootComponent = null;
        this.weaponName = "New Weapon";
    }

    public void SetRootComponent(Component _newRoot) {
        this.rootComponent = _newRoot;
    }
}
