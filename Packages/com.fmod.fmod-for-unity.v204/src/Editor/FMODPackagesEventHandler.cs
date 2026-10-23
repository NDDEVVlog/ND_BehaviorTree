using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;

namespace FMOD.Editor
{
    public class FMODPackagesEventHandler
    {
        // You must use '[InitializeOnLoadMethod]' or '[InitializeOnLoad]' to subscribe to this event.
        [InitializeOnLoadMethod]
        static void SubscribeToEvent()
        {
            // This causes the method to be invoked after the Editor registers the new list of packages.
            Events.registeredPackages += RegisteredPackagesEventHandler;
        }

        static void RegisteredPackagesEventHandler(PackageRegistrationEventArgs packageRegistrationEventArgs)
        {
            // Code executed here can safely assume that the Editor has finished compiling the new list of packages

            foreach (var addedPackage in packageRegistrationEventArgs.added)
            {
                if (addedPackage.displayName.Contains("FMOD for Unity"))
                {
                    UnityEngine.Debug.Log($"Adding {addedPackage.displayName}");
                    // First time install, nothing to do here (?)
                    return;
                }
            }

            for (int i = 0; i <= packageRegistrationEventArgs.changedFrom.Count; i++)
            {
                var oldPackage = packageRegistrationEventArgs.changedFrom[i];
                var newPackage = packageRegistrationEventArgs.changedTo[i];

                if (oldPackage.displayName.Contains("FMOD for Unity"))
                {
                    // Our old libs do not get unloaded
                    // prompt user to restart Unity?
                    if (EditorUtility.DisplayDialog("Updating FMOD for Unity", $"You are attempting to update FMOD for Unity:\n\nFrom: {oldPackage.version}\nTo: {newPackage.version}\n\nThis requires restarting the editor, do you wish to continue?", "OK", "Cancel"))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            EditorApplication.OpenProject(Environment.CurrentDirectory);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }
    }
}
