using System.Collections;
using System.Collections.Generic;
using ND_BehaviorTree;
using UnityEngine;

public class ScriptContainFullVariable : MonoBehaviour
{
    // Primitive types
    public int health = 100;
    public float speed = 5.5f;
    public bool isAlive = true;
    public string playerName = "Hero";

    // Unity types
    public GameObject target;
    public Transform spawnPoint;
    public Rigidbody rb;
    public Animator animator;
    public AudioSource audioSource;
    public Collider hitbox;
    public Light torchLight;
    public SpriteRenderer spriteRenderer;
    public Camera mainCamera;
    public Material playerMaterial;
    public Texture2D icon;

    // Math / Vector types
    public Vector3 moveDirection = Vector3.zero;
    public Vector2 screenPosition;
    public Quaternion rotation = Quaternion.identity;
    public Color playerColor = Color.white;

    // Lists and arrays
    public string[] inventoryItems = new string[5];
    public List<Transform> patrolPoints = new List<Transform>();
    public int[] damageValues = { 10, 20, 30 };

    // Dictionaries (Note: not visible in Inspector)
    public Dictionary<string, int> itemCounts = new Dictionary<string, int>();

    // Enums
    public enum CharacterClass { Warrior, Mage, Archer }
    public CharacterClass charClass = CharacterClass.Warrior;

    // Custom Struct
    [System.Serializable]
    public struct Stat
    {
        public string name;
        public float value;
    }
    public Stat[] stats;


    [SerializeReference]
    public IDynamicBranchSelector dynamicBranchSelector;

    [SerializeReference]
    public GenericParameter parameter = new FloatParameter();


    
}
