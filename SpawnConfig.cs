using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spawn Config", "~Beast~", "1.0.0")]
    [Description("Control the spawns on your server")]
    public class SpawnConfig : RustPlugin
    {
        #region Fields
        private List<SpawnableObject> spawnableObjects = new List<SpawnableObject>();

        private class SpawnableObject
        {
            public string Prefab;
            public float SpawnChance;
        }

        private Coroutine updateAndCleanupCoroutine = null;
        #endregion

        #region Configuration

        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Enabled")]
            public bool Enabled = false;

            [JsonProperty("Spawns")]
            public List<SpawnableObject> AssetPrefabs { get; set; } = new List<SpawnableObject>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                SaveConfig();
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Updater
        List<string> AllowedSearch = new List<string>()
        {
            "/autospawn/collectable/",
            "/autospawn/resource/",
        };

        List<string> ExcludeSearch = new List<string>()
        {
            "/crystals/"
        };

        private IEnumerator UpdateAndCleanUp()
        {
            // Update the prefab list
            foreach (GameManifest.PooledString asset in GameManifest.Current.pooledStrings)
            {
                if (AllowedSearch.Any(keyword => asset.str.Contains(keyword)) &&
                    !ExcludeSearch.Any(keyword => asset.str.Contains(keyword)) && asset.str.EndsWith(".prefab"))
                {
                    spawnableObjects.Add(new SpawnableObject
                    {
                        Prefab = asset.str,
                        SpawnChance = 1.0f
                    });
                }
            }

            // Update the configuration
            foreach (var prefab in spawnableObjects)
            {
                if (!config.AssetPrefabs.Any(key => key.Prefab == prefab.Prefab))
                {
                    // Add the prefab to the configuration if it's not already there
                    config.AssetPrefabs.Add(prefab);
                }
            }

            SaveConfig();

            if (config.Enabled)
            {
                // Cleanup entities with a spawn chance of 0
                var cleanList = config.AssetPrefabs.Where(obj => obj.SpawnChance == 0);

                PrintWarning("Cleaning up entities...");
                yield return CoroutineEx.waitForSeconds(1f);

                int count = 0;

                foreach (var p in cleanList)
                {
                    var entities = BaseNetworkable.serverEntities.Where(ent => ent.name == p.Prefab).ToList();
                    if (entities.Count > 0)
                    {
                        foreach (var entity in entities)
                        {
                            BaseEntity baseEntity = entity.gameObject.ToBaseEntity();
                            if (baseEntity.IsValid())
                            {
                                if (baseEntity.isServer)
                                {
                                    baseEntity.Kill(BaseNetworkable.DestroyMode.None);
                                }
                            }
                            else
                            {
                                GameManager.Destroy(entity.gameObject, 0f);
                            }
                        }
                        count += entities.Count;
                    }
                }

                PrintWarning($"Cleaned up {count} existing entities.");
            }

            updateAndCleanupCoroutine = null;
            yield return null;
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            updateAndCleanupCoroutine = ServerMgr.Instance.StartCoroutine(UpdateAndCleanUp());
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!config.Enabled) return;

            var prefabName = entity.name;
            var prefabConfig = config.AssetPrefabs.FirstOrDefault(p => prefabName == p.Prefab);

            if (prefabConfig == null)
                return;

            float spawnChance = prefabConfig.SpawnChance;

            if (spawnChance <= 0f)
            {
                entity.AdminKill();
                return;
            }

            if (spawnChance < 1f)
            {
                float randomValue = UnityEngine.Random.Range(0f, 1f);
                if (randomValue > spawnChance)
                {
                    entity.AdminKill();
                }
            }
        }

        private void Unload()
        {
            if (updateAndCleanupCoroutine != null)
                ServerMgr.Instance.StopCoroutine(updateAndCleanupCoroutine);
        }
        #endregion
    }
}