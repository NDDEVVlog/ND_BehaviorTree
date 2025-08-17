using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ND_BehaviorTree.Editor;
using UnityEditor;
using UnityEngine.UIElements;
using System.Reflection;

public class PopCatEditor : ND_NodeEditor
{
    private Texture2D popCatOpenMouth;
    private Texture2D popCatCloseMouth;
    private AudioClip popSound;

    public PopCatEditor(ND_BehaviorTree.Node node, UnityEditor.SerializedObject BTObject, UnityEditor.Experimental.GraphView.GraphView graphView, string styleSheetPath)
        : base(node, BTObject, graphView, styleSheetPath)
    {   
        iconImage.style.display = DisplayStyle.Flex; // Ensure the icon image is visible
        iconImage = this.Q<Image>("icon-image");
        popCatOpenMouth = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ND_BehaviorTree/NDBT/Icons/POPCat1.png");
        popCatCloseMouth = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ND_BehaviorTree/NDBT/Icons/POPCat.png");
        popSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ND_BehaviorTree/DEMO/TestZone1/AudioClip/pop-cat-original-meme_3ObdYkj.mp3");

        iconImage.image = popCatCloseMouth;

        iconImage.RegisterCallback<MouseEnterEvent>(evt =>
        {   
            iconImage.image = popCatOpenMouth;
            if (popSound != null)
            {
                PlayAudioClip(popSound);
            }
            else
            {
                Debug.LogWarning("Pop sound clip not found at 'Assets/ND_BehaviorTree/NDBT/Sounds/PopSound.wav'.");
            }
        });

        iconImage.RegisterCallback<MouseLeaveEvent>(evt =>
        {   
        
            iconImage.image = popCatCloseMouth;
            
        });
    }

    private void PlayAudioClip(AudioClip clip)
    {
        // Try reflection to access UnityEditor.AudioUtil.PlayClip
        var audioUtilType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
        if (audioUtilType != null)
        {
            // Try multiple possible signatures for PlayClip
            MethodInfo playClipMethod = audioUtilType.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip) }, null)
                ?? audioUtilType.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? audioUtilType.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

            if (playClipMethod != null)
            {
                try
                {
                    // Handle different method signatures
                    if (playClipMethod.GetParameters().Length == 1)
                    {
                        playClipMethod.Invoke(null, new object[] { clip });
                    }
                    else
                    {
                        playClipMethod.Invoke(null, new object[] { clip, 0, false });
                    }
                    return; // Success, exit the method
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to play audio clip via reflection: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("Could not find AudioUtil.PlayClip or PlayPreviewClip method via reflection.");
            }
        }
        else
        {
            Debug.LogWarning("Could not find UnityEditor.AudioUtil type via reflection.");
        }

        // Fallback: Use AudioSource in the Editor
        GameObject tempGameObject = new GameObject("TempAudio");
        AudioSource audioSource = tempGameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
        Object.DestroyImmediate(tempGameObject, true); // Destroy immediately in Editor
    }
}