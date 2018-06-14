using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ForgerUI : MonoBehaviour {

    public ForgerManager manager;

    public Camera mainSceneCamera;
    
    // -----/ UI Saving Elements /-----

    public GameObject fileAlreadyExistsParent;

    public Text fileAlreadyExistsText;

    public GameObject loadFileMenuParent;

    public ScrollRect loadFileScrollObject;

    public GameObject loadFileOptionPrefab;

    public GameObject loadFileUnsavedChangesParent;

    private LoadOption lastLoadOptionClicked;


    // Use this for initialization
    void Start () {
        // Make sure all menus are closed.
        this.ToggleLoadMenu(false);

    }
	
	// Update is called once per frame
	void Update () {
		
	}


    public void ToggleLoadMenu (bool _toggle) {
        if (_toggle) {
            // Clear all the current options in the scroll menu.
            while (this.loadFileScrollObject.content.transform.childCount > 0) {
                Transform temp = this.loadFileScrollObject.content.transform.GetChild(0);
                temp.SetParent(null);
                GameObject.Destroy(temp.gameObject);
            }

            // Get all the loadable components.
            List<GameObject> loadOptionsAvailable = this.manager.GetLoadableComponentOptions(this.loadFileOptionPrefab);

            // If the list isn't null, we have options to load.
            if (loadOptionsAvailable != null) {
                // Get the scroll content transform.
                Transform loadScrollContent = this.loadFileScrollObject.content.transform;

                // How far down each option should be.
                int curHeight = 5;

                // Add each option to the transform.
                foreach (GameObject obj in loadOptionsAvailable) {
                    RectTransform objTransform = obj.transform as RectTransform;
                    objTransform.SetParent(loadScrollContent);
                    //objTransform.offsetMin = new Vector2(5, -65 - curHeight);
                    //objTransform.offsetMax = new Vector2(-5, -curHeight);
                    objTransform.localScale = new Vector3(1, 1, 1);
                    curHeight += 70;
                }
            }
        }

        this.loadFileMenuParent.SetActive(_toggle);
    }

    public void LoadOptionClicked (LoadOption _optionClicked) {
        this.lastLoadOptionClicked = _optionClicked;

        string status = this.manager.LoadSelectedComponent(_optionClicked.GetLoadFilePath());
        
        // Clear the last clicked option.
        this.lastLoadOptionClicked = null;

        // Make sure the continue load menu and the load menu is closed.
        this.ToggleLoadMenu(false);
    }
}
