using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace eft_dma_radar
{
    internal static class TarkovDevManager
    {
        /// <summary>
        /// Contains all Tarkov Loot, Quests Items and Tasks mapped via BSGID String.
        /// </summary>
        ///
        public static ReadOnlyDictionary<string, LootItem> AllItems { get; }
        public static ReadOnlyDictionary<string, QuestItems> AllQuestItems { get; }
        public static ReadOnlyDictionary<string, Tasks> AllTasks { get; }
        public static ReadOnlyDictionary<string, LootContainerInfo> AllLootContainers { get; }

        #region Static_Constructor

        static TarkovDevManager()
        {
            TarkovDevResponse jsonResponse;

            var jsonItems = new List<TarkovDevResponse>();
            var allItems = new Dictionary<string, LootItem>(StringComparer.OrdinalIgnoreCase);
            var allQuestItems = new Dictionary<string, QuestItems>(StringComparer.OrdinalIgnoreCase);
            var allTasks = new Dictionary<string, Tasks>(StringComparer.OrdinalIgnoreCase);
            var allLootContainers = new Dictionary<string, LootContainerInfo>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists("api_tarkov_dev_items.json") || File.GetLastWriteTime("api_tarkov_dev_items.json").AddHours(480) < DateTime.Now) // only update every 480h
            {
                using (var client = new HttpClient())
                {
                    //Create body and content-type
                    var body = new
                    {
                        query = @"query {
                                    items {
                                        id
                                        name
                                        shortName
                                        normalizedName
                                        basePrice
                                        avg24hPrice
                                        low24hPrice
                                        high24hPrice
                                        sellFor {
                                        price
                                        vendor {
                                            normalizedName
                                        }
                                        }
                                        category {
                                        id
                                        name
                                        normalizedName
                                        }
                                        weight
                                        categories {
                                        id
                                        name
                                        normalizedName
                                        }
                                    }
                                    tasks {
                                        id
                                        name
                                        objectives {
                                            id
                                            type
                                            description
                                            maps {
                                                id
                                                name
                                                normalizedName
                                            }
                                            ... on TaskObjectiveItem {
                                                item {
                                                id
                                                name
                                                shortName
                                                }
                                                zones {
                                                id
                                                map {
                                                    id
                                                    normalizedName
                                                    name
                                                }
                                                position {
                                                    y
                                                    x
                                                    z
                                                }
                                                }
                                                requiredKeys {
                                                id
                                                name
                                                shortName
                                                }
                                                count
                                                foundInRaid
                                            }
                                            ... on TaskObjectiveMark {
                                                id
                                                description
                                                markerItem {
                                                id
                                                name
                                                shortName
                                                }
                                                maps {
                                                id
                                                normalizedName
                                                name
                                                }
                                                zones {
                                                id
                                                map {
                                                    id
                                                    normalizedName
                                                    name
                                                }
                                                position {
                                                    y
                                                    x
                                                    z
                                                }
                                                }
                                                requiredKeys {
                                                id
                                                name
                                                shortName
                                                }
                                            }
                                            ... on TaskObjectiveQuestItem {
                                                id
                                                description
                                                requiredKeys {
                                                id
                                                name
                                                shortName
                                                }
                                                maps {
                                                id
                                                normalizedName
                                                name
                                                }
                                                zones {
                                                id
                                                map {
                                                    id
                                                    normalizedName
                                                    name
                                                }
                                                position {
                                                    y
                                                    x
                                                    z
                                                }
                                                }
                                                requiredKeys {
                                                id
                                                name
                                                shortName
                                                }
                                                questItem {
                                                    id
                                                    name
                                                    shortName
                                                    normalizedName
                                                    description
                                                }
                                                count
                                            }
                                            ... on TaskObjectiveBasic {
                                                id
                                                description
                                                requiredKeys {
                                                id
                                                name
                                                shortName
                                                }
                                                maps {
                                                id
                                                normalizedName
                                                name
                                                }
                                                zones {
                                                id
                                                map {
                                                    id
                                                    normalizedName
                                                    name
                                                }
                                                position {
                                                    y
                                                    x
                                                    z
                                                }
                                                }
                                                requiredKeys {
                                                id
                                                name
                                                shortName
                                                }
                                            }
                                        }
                                    }
                                    questItems {
                                        id
                                        shortName
                                        name
                                        normalizedName
                                    }
                                    lootContainers {
                                        id
                                        normalizedName
                                        name
                                    }
                                }"
                    };
                    var jsonBody = JsonSerializer.Serialize(body);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    var response = client.PostAsync("https://api.tarkov.dev/graphql", content).Result;
                    var responseString = response.Content.ReadAsStringAsync().Result;
                    jsonResponse = JsonSerializer.Deserialize<TarkovDevResponse>(responseString);
                    jsonItems.Add(jsonResponse);
                    File.WriteAllText("api_tarkov_dev_items.json", responseString);
                }
            }
            else
            {
                var responseString = File.ReadAllText("api_tarkov_dev_items.json");
                jsonResponse = JsonSerializer.Deserialize<TarkovDevResponse>(responseString);
                jsonItems.Add(jsonResponse);
            }

            foreach (var item in jsonItems)
            {
                if (item.data?.items != null)
                {
                    foreach (var tarkovItem in item.data.items)
                    {
                        allItems.TryAdd(
                            tarkovItem.id,
                            new LootItem()
                            {
                                Name = tarkovItem.name,
                                Item = tarkovItem,
                            }
                        );
                    }
                }

                if (item.data?.tasks != null)
                {
                    foreach (var task in item.data.tasks)
                    {
                        var newTask = new Tasks()
                        {
                            Name = task.name,
                            ID = task.id,
                            Objectives = new List<Tasks.Objective>()
                        };

                        foreach (var objective in task.objectives)
                        {
                            var newObjective = new Tasks.Objective()
                            {
                                Description = objective.description,
                                Type = objective.type,
                                ID = objective.id,
                                Maps = objective.maps?.Select(m => new ObjectiveMaps
                                {
                                    id = m.id,
                                    name = m.name,
                                    normalizedName = m.normalizedName
                                }).ToList(),
                                Zones = objective.zones?.Select(z => new ObjectiveZones
                                {
                                    id = z.id,
                                    map = new ObjectiveZones.Map
                                    {
                                        id = z.map.id,
                                        name = z.map.name,
                                        normalizedName = z.map.normalizedName
                                    },
                                    position = new ObjectiveZones.Position
                                    {
                                        x = z.position.x,
                                        y = z.position.z,
                                        z = z.position.y
                                    }
                                }).ToList(),
                                //how to add objectiveItem
                                Count = objective.count,
                                FoundInRaid = objective.foundInRaid
                            };

                            if (objective.questItem != null) // Check if questItem exists
                            {
                                newObjective.QuestItem = new ObjectiveItem
                                {
                                    Id = objective.questItem.id,
                                    Name = objective.questItem.name,
                                    ShortName = objective.questItem.shortName,
                                    NormalizedName = objective.questItem.normalizedName,
                                    Description = objective.questItem.description,
                                };
                            }
                            //visit
                            //extract
                            //mark
                            //shoot
                            //findQuestItem
                            //giveQuestItem
                            //plantItem
                            //giveItem
                            //experience
                            // Additional processing based on the objective.type
                            switch (objective.type)
                            {
                                case "findQuestItem":
                                    // Process findQuestItem type objectives here.
                                    // You might want to add specific properties or actions.
                                    break;
                                case "visit":
                                    // Process visit type objectives here.
                                    // This could involve adding details about the maps and zones to visit.
                                    break;
                                case "mark":
                                    // Process mark type objectives here.
                                    // This might include details about the item to mark and its location.
                                    break;
                                // Add more cases for other types as necessary.
                            }

                            // Add the populated objective to the task's objectives list.
                            newTask.Objectives.Add(newObjective);
                        }

                        // Finally, add the fully constructed task to the allTasks dictionary.
                        allTasks.TryAdd(task.id, newTask);
                    }
                }

                if (item.data?.questItems != null)
                {
                    foreach (var questItem in item.data.questItems)
                    {
                        allQuestItems.TryAdd(
                            questItem.id,
                            new QuestItems()
                            {
                                Name = questItem.name,
                                ID = questItem.id,
                                ShortName = questItem.shortName
                            }
                        );
                    }
                }

                if (item.data?.lootContainers != null)
                {
                    foreach (var lootContainer in item.data.lootContainers)
                    {
                        allLootContainers.TryAdd(
                            lootContainer.id,
                            new LootContainerInfo()
                            {
                                Name = lootContainer.name,
                                ID = lootContainer.id,
                                NormalizedName = lootContainer.normalizedName
                            }
                        );
                    }
                }
            }
            AllItems = new(allItems);
            AllTasks = new(allTasks);
            AllQuestItems = new(allQuestItems);
            AllLootContainers = new(allLootContainers);
        }
        #endregion

        #region Private_Methods
        public static string FormatNumber(int num)
        {
            if (num >= 1000000)
                return (num / 1000000D).ToString("0.##") + "M";
            else if (num >= 1000)
                return (num / 1000D).ToString("0") + "K";
            else
                return num.ToString();
        }

        public static int GetItemValue(TarkovItem item)
        {
            int bestPrice = (int)item.avg24hPrice;
            foreach (var vendor in item.sellFor)
                {
                    if (vendor.price > bestPrice)
                    {
                        bestPrice = vendor.price;
                    }
                }

            return bestPrice;
        }
        #endregion
    }

    #region Classes
    /// <summary>
    /// New Class to hold Tarkov Items Data.
    /// </summary>
    public class TarkovItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string shortName { get; set; }
        public string normalizedName { get; set; }
        public int basePrice { get; set; }
        public int? avg24hPrice { get; set; }
        public int? low24hPrice { get; set; }
        public int? high24hPrice { get; set; }
        public double weight { get; set; } 
        public List<Category> categories { get; set; } = new List<Category>();
        public List<VendorPrice> sellFor { get; set; } = new List<VendorPrice>(); 

        public class VendorPrice
        {
            public string vendorName { get; set; }
            public int price { get; set; }
        }

        public class Category
        {
            public string id { get; set; }
            public string name { get; set; }
            public string normalizedName { get; set; }
        }
    }

    public class TarkovTasks
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<Objective> objectives { get; set; } = new List<Objective>();

        public class Objective
        {
            public string id { get; set; }
            public string description { get; set; }
            public string type { get; set; }
            public List<ObjectiveMaps>? maps { get; set; } 
            public List<ObjectiveZones>? zones { get; set; } 
            public int? count { get; set; }
            public bool? foundInRaid { get; set; }
            public ObjectiveItems questItem { get; set; }
        }
    }

    public class ObjectiveItems
    {
        public string id { get; set; }
        public string name { get; set; }
        public string shortName { get; set; }
        public string normalizedName { get; set; }
        public string description { get; set; }
    }

    public class ObjectiveZones
    {
        public string id { get; set; }
        public Map map { get; set; } 
        public Position position { get; set; } 

        public class Map
        {
            public string id { get; set; }
            public string normalizedName { get; set; }
            public string name { get; set; }
        }

        public class Position
        {
            public double? y { get; set; }
            public double? x { get; set; }
            public double? z { get; set; }
        }
    }

    public class ObjectiveMaps
    {
        public string id { get; set; }
        public string name { get; set; }
        public string normalizedName { get; set; }
    }

    public class TarkovQuestItems
    {
        public string id { get; set; }
        public string name { get; set; }
        public string shortName { get; set; }
        public string normalizedName { get; set; }
    }

    public class LootContainerInfo
    {
        public string ID { get; set; }
        public string NormalizedName { get; set; }
        public string Name { get; set; }
    }

    public class TarkovLootContainerInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string normalizedName { get; set; }
    }

    public class QuestItems
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string ShortName { get; set; }

    }

    public class ObjectiveItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string NormalizedName { get; set; }
        public string Description { get; set; }
    }

    public class Tasks
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public List<Objective> Objectives { get; set; } = new List<Objective>();

        public class Objective
        {
            public string Description { get; set; }
            public string Type { get; set; }
            public string ID { get; set; }
            public List<ObjectiveMaps> Maps { get; set; } = new List<ObjectiveMaps>();
            public List<ObjectiveZones> Zones { get; set; } = new List<ObjectiveZones>(); // Renamed from ObjectiveZones for clarity
            // Add more properties here as needed, for example:
            public ObjectiveItem QuestItem { get; set; }
            public int? Count { get; set; } // For objectives requiring collecting or using a certain number of items
            public bool? FoundInRaid { get; set; } // For objectives requiring items to be found in raid
            // You can add more specific fields as needed for various objective types.
        }
    }

    public class TarkovDevResponse
    {
        public TarkovDevData data { get; set; }
    }

    public class TarkovDevData
    {
        public List<TarkovItem> items { get; set; }
        public List<TarkovTasks> tasks { get; set; } 
        public List<TarkovQuestItems> questItems { get; set; }
        public List<TarkovLootContainerInfo> lootContainers { get; set; }
    }
    #endregion
}
