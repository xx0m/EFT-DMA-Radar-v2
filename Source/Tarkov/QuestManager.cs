using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Numerics;

namespace eft_dma_radar
{
    public class QuestManager
    {
        public Collection<QuestItem> QuestItems
        {
            get;
            private set;
        }

        public Collection<QuestZone> QuestZones
        {
            get;
            private set;
        }

        public QuestManager(ulong localGameWorld)
        {
            //@Keeegi fix this shit :D
            var mainPlayer = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.MainPlayer);
            var profile = Memory.ReadPtr(mainPlayer + Offsets.Player.Profile);
            var questData = Memory.ReadPtr(profile + Offsets.Profile.QuestsData);
            var questDataCount = Memory.ReadValue<int>(questData + Offsets.UnityList.Count);
            var questDataBaseList = Memory.ReadPtr(questData + Offsets.UnityList.Base);

            var questItems = new List<QuestItem>(questDataCount);
            var questZones = new List<QuestZone>(questDataCount * 10);

            for (int i = 0; i < questDataCount; i++)
            {
                ulong questEntry;
                string questID;
                try
                {
                    questEntry = Memory.ReadPtr(questDataBaseList + 0x20 + (uint)(i * 0x8)); //[Class] -.GClass306A : Object
                    if (questEntry == 0)
                    {
                        continue;
                    }
                    var questTemplate = Memory.ReadPtr(questEntry + Offsets.QuestData.Template); //[28] Template : -.GClass306D [Class] -.GClass306D : Object
                    if (questTemplate == 0x0)
                    {
                        continue;
                    }
                    var questIDPtr = Memory.ReadPtr(questTemplate + Offsets.QuestData.ID);
                    questID = Memory.ReadUnityString(questIDPtr);
                }
                catch
                {
                    //Noticed that some template reaad is null for some reason so we skip it for now
                    continue;
                }
                try
                {
                    var questStatus = Memory.ReadValue<int>(questEntry + 0x34);
                    //2 = started
                    if (questStatus == 2)
                    {
                        TarkovDevManager.AllTasks.TryGetValue(questID, out var task);
                        if (task is not null)
                        {
                            var objectives = task.Objectives;
                            foreach (var objective in objectives)
                            {
                                if (objective is null)
                                {
                                    continue;
                                }
                                if (objective.Type != "visit" && objective.Type != "mark" && objective.Type != "findQuestItem" && objective.Type != "plantItem" && objective.Type != "plantQuestItem")
                                {
                                    continue;
                                }
                                var zones = objective.Zones;
                                var objectiveType = Regex.Replace(objective.Type, "(\\B[A-Z])", " $1");
                                objectiveType = objectiveType[0].ToString().ToUpper() + objectiveType.Substring(1);

                                if (zones is not null)
                                {
                                    foreach (var zone in zones)
                                    {

                                        if (zone.map.name == "Streets of Tarkov")
                                        {
                                            zone.map.name = "Streets";
                                        }

                                        questZones.Add(new QuestZone
                                        {
                                            ID = zone.id,
                                            MapName = zone.map.name,
                                            Position = new Vector3((float)zone.position.x, (float)zone.position.y, (float)zone.position.z),
                                            ObjectiveType = objectiveType,
                                            Description = objective.Description,
                                            TaskName = task.Name
                                        });
                                    }
                                }
                                if (objective.Type == "findQuestItem")
                                {
                                    questItems.Add(new QuestItem
                                    {
                                        Id = objective.QuestItem.Id,
                                        Name = objective.QuestItem.Name,
                                        ShortName = objective.QuestItem.ShortName,
                                        NormalizedName = objective.QuestItem.NormalizedName,
                                        TaskName = task.Name,
                                        Description = objective.QuestItem.Description
                                    });
                                }
                            }
                        }
                        continue;
                    }
                }
                catch {}
            }
            QuestZones = new(questZones);
            QuestItems = new(questItems);
        }
    }

    public class QuestItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string NormalizedName { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 ZoomedPosition { get; set; } = new();
    }

    public class QuestZone
    {
        public string ID { get; set; }
        public string MapName { get; set; }
        public string Description { get; set; }
        public string TaskName { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 ZoomedPosition { get; set; } = new();
        public string ObjectiveType { get; internal set; }
    }
}