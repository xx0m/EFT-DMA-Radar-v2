using System.Text.Json.Serialization;
using System.Text.Json;

namespace eft_dma_radar
{
    public class LootFilterManager
    {
        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };

        [JsonIgnore]
        private const string LootFiltersDirectory = "Configuration\\Loot Filters\\";

        [JsonIgnore]
        private static readonly object _lock = new();

        [JsonIgnore]
        public List<Filter> Filters { get; set; }

        public LootFilterManager()
        {
            Filters = new List<Filter>();
        }

        public static bool TryLoadLootFilterManager(out LootFilterManager lootFilterManager)
        {
            lock (_lock)
            {
                try
                {
                    lootFilterManager = new LootFilterManager();

                    if (!Directory.Exists(LootFiltersDirectory))
                        Directory.CreateDirectory(LootFiltersDirectory);

                    var jsonFiles = Directory.GetFiles(LootFiltersDirectory, "*.json");

                    foreach (var file in jsonFiles)
                    {
                        var json = File.ReadAllText(file);
                        var lootFilter = JsonSerializer.Deserialize<Filter>(json);
                        lootFilterManager.Filters.Add(lootFilter);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    lootFilterManager = null;
                    return false;
                }
            }
        }

        public static void SaveLootFilterManager(LootFilterManager lootFilterManager)
        {
            lock (_lock)
            {
                if (!Directory.Exists(LootFiltersDirectory))
                    Directory.CreateDirectory(LootFiltersDirectory);

                var existingFiles = Directory.GetFiles(LootFiltersDirectory, "*.json");

                foreach (var lootFilter in lootFilterManager.Filters)
                {
                    var newFileName = $"{LootFiltersDirectory}{lootFilter.Name}.json";

                    var existingFile = existingFiles.FirstOrDefault(file => file.Equals(newFileName, StringComparison.OrdinalIgnoreCase));

                    if (existingFile == null)
                    {
                        var oldFile = existingFiles.FirstOrDefault(file => file.Contains(lootFilter.Name));
                        if (oldFile != null)
                        {
                            File.Delete(oldFile);
                        }
                    }

                    var json = JsonSerializer.Serialize<Filter>(lootFilter, _jsonOptions);
                    File.WriteAllText(newFileName, json);
                }

                var filesToDelete = existingFiles.Except(lootFilterManager.Filters.Select(filter => $"{LootFiltersDirectory}{filter.Name}.json"));
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                }
            }
        }

        public void AddEmptyProfile()
        {
            this.Filters.Add(new Filter
            {
                Order = 1,
                IsActive = true,
                Name = "New Filter",
                Items = new List<string>(),
                Color = new Filter.Colors { R = 255, G = 255, B = 255, A = 255 }
            });

            LootFilterManager.SaveLootFilterManager(this);
        }

        public void UpdateFilter(Filter filter, int index)
        {
            this.Filters.RemoveAt(index);
            this.Filters.Insert(index, filter);
            LootFilterManager.SaveLootFilterManager(this);
        }

        public void RemoveFilter(Filter filter)
        {
            this.Filters.Remove(filter);
            LootFilterManager.SaveLootFilterManager(this);
        }

        public void RemoveFilter(int index)
        {
            this.Filters.RemoveAt(index);
            LootFilterManager.SaveLootFilterManager(this);
        }

        public void RemoveFilterItem(Filter filter, string itemID)
        {
            filter.Items.Remove(itemID);
            LootFilterManager.SaveLootFilterManager(this);
        }

        public class Filter
        {
            public List<string>? Items { get; set; }
            public Colors Color { get; set; }
            public bool IsActive { get; set; }
            public int Order { get; set; }
            public string Name { get; set; }

            public struct Colors
            {
                public byte A { get; set; }
                public byte R { get; set; }
                public byte G { get; set; }
                public byte B { get; set; }
            }
        }
    }
}
