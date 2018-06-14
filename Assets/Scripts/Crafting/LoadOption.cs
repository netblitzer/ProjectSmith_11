using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class LoadOption : MonoBehaviour {

    // The UI text of the component's file name.
    public Text componentNameText;

    // The UI text for the component's last edited date.
    public Text componentSaveDateText;

    // The file path for the component.
    private string filePath;

    // The proper name of the component.
    private string compName;
    
    // The DateTime of the file for the component.
    private System.DateTime lastSaveDate;

    /// <summary>
    /// Sets the load option's information. This includes the file path, the component's name,
    /// and when the component was last edited.
    /// </summary>
    /// <param name="_path">The file path for this load option.</param>
    /// <returns></returns>
    public bool SetFilePath (string _path) {
        // See if the file first actually exists. If it doesn't, we should return.
        if (!File.Exists(_path))
            return false;

        // Otherwise we can read in parts of the file.
        this.filePath = _path;

        // Get the last save date.
        this.lastSaveDate = File.GetLastWriteTime(_path);

        // Create the binary stream.
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Open(_path, FileMode.Open);

        // Create the componentData and load it in.
        ComponentData cData = (ComponentData) bf.Deserialize(file);
        this.compName = cData.name;

        // Close the file.
        file.Close();

        // Display the information.
        this.componentNameText.text = this.compName;
        // 12:12:12 AM, 12/12/12
        this.componentSaveDateText.text = this.lastSaveDate.ToLongTimeString() + ", " + this.lastSaveDate.ToShortDateString();

        return true;
    }

    // Gets the file path of this load option.
    public string GetLoadFilePath () {
        return this.filePath;
    }

    public void LoadOptionClicked () {
        // Find the UI object in the scene.
        DesignerUI dui = FindObjectOfType<DesignerUI>();
        ForgerUI fui = FindObjectOfType<ForgerUI>();
        // Tell it that this option was clicked.
        if (dui != null)
            dui.LoadOptionClicked(this);
        else if (fui != null)
            fui.LoadOptionClicked(this);
    }
}
