using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterDrops
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        Harmony _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        public static ManualLogSource mls;

        private void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource("Monster Drops");
            // Plugin startup logic
            mls.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            _harmony.PatchAll(typeof(Plugin));

            var helperObject = new GameObject("CoroutineHelper");
            helperObject.AddComponent<CoroutineHelper>();
        }

        public void DelayedSyncScrapValues(Action action, float delay)
        {
            StartCoroutine(DelayedActionCoroutine(action, delay));
        }

        private IEnumerator DelayedActionCoroutine(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemy))]
        static void DropLoot(EnemyAI __instance)
        {
            mls.LogInfo($"Enemy {__instance.name} has been killed. Spawning scrap...");

            // Find the RoundManager instance in the scene
            RoundManager roundManager = GameObject.FindObjectOfType<RoundManager>();
            if (roundManager == null)
            {
                mls.LogWarning("RoundManager instance not found in the scene!");
            }
            else
            {
                // Create the CoroutineHelper object if it doesn't exist
                if (CoroutineHelper.Instance == null)
                {
                    var helperObject = new GameObject("CoroutineHelper");
                    helperObject.AddComponent<CoroutineHelper>();
                }

                // Get the list of spawnable scrap items
                List<SpawnableItemWithRarity> spawnableScrapItems = roundManager.currentLevel.spawnableScrap;

                if (spawnableScrapItems == null || spawnableScrapItems.Count == 0)
                { // Get the list of spawnable scrap items
                    mls.LogWarning("No scrap items are configured to spawn.");
                }
                else
                {
                    // Get a random scrap item from the list of spawnable scrap items
                    SpawnableItemWithRarity scrapSpawn = spawnableScrapItems[UnityEngine.Random.Range(0, spawnableScrapItems.Count)];

                    // Instantiate the loot item at the enemy's position
                    GameObject lootItem = UnityEngine.Object.Instantiate<GameObject>(scrapSpawn.spawnableItem.spawnPrefab, __instance.serverPosition, Quaternion.identity);

                    // Add any additional logic for initializing the loot item here
                    GrabbableObject component = lootItem.GetComponent<GrabbableObject>();
                    component.transform.rotation = Quaternion.Euler(component.itemProperties.restingRotation);
                    component.fallTime = 0f;
                    component.scrapValue = scrapSpawn.spawnableItem.maxValue;
                    NetworkObject component2 = lootItem.GetComponent<NetworkObject>();
                    component2.Spawn(false);

                    NetworkObjectReference[] networkArray = new NetworkObjectReference[1];
                    int[] intArray = new int[1];
                    networkArray[0] = component2;
                    intArray[0] = component.scrapValue;

                    CoroutineHelper.Instance.ExecuteAfterDelay(() =>
                    {
                        // The function you want to call after 11 seconds
                        roundManager.SyncScrapValuesClientRpc(networkArray, intArray);
                    }, 11f);

                }
            }
        }



    }

    public class CoroutineHelper : MonoBehaviour
    {
        public static CoroutineHelper Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
            }
        }

        public void ExecuteAfterDelay(Action action, float delay)
        {
            StartCoroutine(DelayedExecution(action, delay));
        }

        private IEnumerator DelayedExecution(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action.Invoke();
        }
    }


}
