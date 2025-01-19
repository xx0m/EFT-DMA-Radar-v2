using Offsets;
using SkiaSharp;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eft_dma_radar
{
    /// <summary>
    /// Defines map position for the 2D Map.
    /// </summary>
    public struct MapPosition
    {
        public MapPosition()
        {
        }
        /// <summary>
        /// Contains the Skia Interface (UI) Scaling Value.
        /// </summary>
        public float UIScale = 0;

        /// <summary>
        /// X coordinate on Bitmap.
        /// </summary>
        public float X = 0;

        /// <summary>
        /// Y coordinate on Bitmap.
        /// </summary>
        public float Y = 0;

        /// <summary>
        /// Unit 'height' as determined by Vector3.Z
        /// </summary>
        public float Height = 0;

        private Config _config
        {
            get => Program.Config;
        }

        /// <summary>
        /// Get exact player location (with optional X,Y offsets).
        /// </summary>
        public SKPoint GetPoint(float xOff = 0, float yOff = 0)
        {
            return new SKPoint(X + xOff, Y + yOff);
        }

        /// <summary>
        /// Gets the point where the Aimline 'Line' ends. Applies UI Scaling internally.
        /// </summary>
        private SKPoint GetAimlineEndpoint(double radians, float aimlineLength)
        {
            aimlineLength *= UIScale;
            return new SKPoint((float)(this.X + Math.Cos(radians) * aimlineLength), (float)(this.Y + Math.Sin(radians) * aimlineLength));
        }

        /// <summary>
        /// Gets up arrow where loot is. IDisposable. Applies UI Scaling internally.
        /// </summary>
        private SKPath GetUpArrow(float size = 6)
        {
            size *= UIScale;
            SKPath path = new SKPath();
            path.MoveTo(X, Y);
            path.LineTo(X - size, Y + size);
            path.LineTo(X + size, Y + size);
            path.Close();

            return path;
        }

        /// <summary>
        /// Gets down arrow where loot is. IDisposable. Applies UI Scaling internally.
        /// </summary>
        private SKPath GetDownArrow(float size = 6)
        {
            size *= UIScale;
            SKPath path = new SKPath();
            path.MoveTo(X, Y);
            path.LineTo(X - size, Y - size);
            path.LineTo(X + size, Y - size);
            path.Close();

            return path;
        }

        /// <summary>
        /// Draws an Exfil on this location.
        /// </summary>
        public void DrawExfil(SKCanvas canvas, Exfil exfil, float localPlayerHeight)
        {
            var paint = Extensions.GetEntityPaint(exfil);
            var text = Extensions.GetTextPaint(exfil);
            var heightDiff = this.Height - localPlayerHeight;

            if (heightDiff > 2) // exfil is above player
            {
                using var path = this.GetUpArrow(5);
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -2) // exfil is below player
            {
                using var path = this.GetDownArrow(5);
                canvas.DrawPath(path, paint);
            }
            else // exfil is level with player
            {
                canvas.DrawCircle(this.GetPoint(), 4 * UIScale, paint);
            }

            if (_config.ExfilNames)
            {
                var coords = this.GetPoint();
                var textWidth = text.MeasureText(exfil.Name);

                coords.X = (coords.X - textWidth / 2);
                coords.Y = (coords.Y - text.TextSize / 2) - 3;

                canvas.DrawText(exfil.Name, coords, Extensions.GetTextOutlinePaint());
                canvas.DrawText(exfil.Name, coords, text);
            }
        }

        public void DrawTransit(SKCanvas canvas, Transit transit, float localPlayerHeight)
        {
            var paint = Extensions.GetEntityPaint(transit);
            var text = Extensions.GetTextPaint(transit);
            var heightDiff = this.Height - localPlayerHeight;

            if (heightDiff > 2) // transit is above player
            {
                using var path = this.GetUpArrow(5);
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -2) // transit is below player
            {
                using var path = this.GetDownArrow(5);
                canvas.DrawPath(path, paint);
            }
            else // transit is level with player
            {
                canvas.DrawCircle(this.GetPoint(), 4 * UIScale, paint);
            }

            if (_config.ExfilNames)
            {
                var coords = this.GetPoint();
                var textWidth = text.MeasureText(transit.Name);

                coords.X = (coords.X - textWidth / 2);
                coords.Y = (coords.Y - text.TextSize / 2) - 3;

                canvas.DrawText(transit.Name, coords, Extensions.GetTextOutlinePaint());
                canvas.DrawText(transit.Name, coords, text);
            }
        }

        /// <summary>
        /// Draws a 'Hot' Grenade on this location.
        /// </summary>
        public void DrawGrenade(SKCanvas canvas, Grenade grenade)
        {
            var paint = Extensions.GetEntityPaint(grenade);
            canvas.DrawCircle(this.GetPoint(), 5 * UIScale, paint);
        }

        /// <summary>
        /// Draws a tripwire on this location.
        /// </summary>
        public void DrawTripwire(SKCanvas canvas, MapPosition toPosZoomedPos, Tripwire tripwire, float localPlayerHeight)
        {
            var paint = Extensions.GetEntityPaint(tripwire);
            var heightDiff = this.Height - localPlayerHeight;

            canvas.DrawLine(this.GetPoint().X, this.GetPoint().Y, toPosZoomedPos.X, toPosZoomedPos.Y, paint);

            paint.Style = SKPaintStyle.Fill;

            if (heightDiff > 2) // tripwire is above player
            {
                using var path = toPosZoomedPos.GetUpArrow(7);
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -2) // tripwire is below player
            {
                using var path = toPosZoomedPos.GetDownArrow(7);
                canvas.DrawPath(path, paint);
            }
            else // tripwire is level with player
            {
                canvas.DrawCircle(toPosZoomedPos.GetPoint(), 5 * UIScale, paint);
            }
        }

        /// <summary>
        /// Draws a lootable object on this location.
        /// </summary>
        public void DrawLootableObject(SKCanvas canvas, LootableObject item, float heightDiff)
        {
            if (item is LootItem lootItem)
                this.DrawLootItem(canvas, lootItem, heightDiff);
            else if (item is LootContainer container)
                this.DrawLootContainer(canvas, container, heightDiff);
            else if (item is LootCorpse corpse)
                this.DrawLootCorpse(canvas, corpse, heightDiff);
        }

        /// <summary>
        /// Draws a loot item on this location.
        /// </summary>
        public void DrawLootItem(SKCanvas canvas, LootItem item, float heightDiff)
        {
            var paint = Extensions.GetEntityPaint(item);
            var text = Extensions.GetTextPaint(item);
            var label = _config.LootValue ? item.GetFormattedValueShortName() : item.Item.shortName;

            if (heightDiff > 2)
            {
                using var path = this.GetUpArrow();
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -2)
            {
                using var path = this.GetDownArrow();
                canvas.DrawPath(path, paint);
            }
            else
            {
                canvas.DrawCircle(this.GetPoint(), 5 * UIScale, paint);
            }

            var coords = this.GetPoint(7 * UIScale, 3 * UIScale);

            canvas.DrawText(label, coords, Extensions.GetTextOutlinePaint());
            canvas.DrawText(label, coords, text);
        }

        /// <summary>
        /// Draws a loot container on this location.
        /// </summary>
        public void DrawLootContainer(SKCanvas canvas, LootContainer container, float heightDiff)
        {
            var paint = Extensions.GetEntityPaint(container);
            var text = Extensions.GetTextPaint(container);
            var label = container.Name;

            if (heightDiff > 1.45)
            {
                using var path = this.GetUpArrow();
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -1.45)
            {
                using var path = this.GetDownArrow();
                canvas.DrawPath(path, paint);
            }
            else
            {
                canvas.DrawCircle(this.GetPoint(), 5 * UIScale, paint);
            }

            var coords = this.GetPoint(7 * UIScale, 3 * UIScale);
            var paintTest = Extensions.GetTextOutlinePaint();

            canvas.DrawText(label, coords, paintTest);
            canvas.DrawText(label, coords, text);
        }

        /// <summary>
        /// Draws a loot corpse on this location.
        /// </summary>
        public void DrawLootCorpse(SKCanvas canvas, LootCorpse corpse, float heightDiff)
        {
            var length = 6 * UIScale;
            var paint = Extensions.GetDeathMarkerPaint(corpse);
            var offsetX = -15 * UIScale;

            if (heightDiff > 1.45)
            {
                using var path = this.GetUpArrow();
                path.Offset(offsetX, 0);
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -1.45)
            {
                using var path = this.GetDownArrow();
                path.Offset(offsetX, 0);
                canvas.DrawPath(path, paint);
            }
            else
            {
                canvas.DrawCircle(this.X + offsetX, this.Y, 5 * UIScale, paint);
            }

            canvas.DrawLine(new SKPoint(this.X - length, this.Y + length), new SKPoint(this.X + length, this.Y - length), paint);
            canvas.DrawLine(new SKPoint(this.X - length, this.Y - length), new SKPoint(this.X + length, this.Y + length), paint);
        }

        public void DrawDeathMarker(SKCanvas canvas)
        {
            var length = 6 * UIScale;
            var paint = Extensions.GetDeathMarkerPaint();
            canvas.DrawLine(new SKPoint(this.X - length, this.Y + length), new SKPoint(this.X + length, this.Y - length), paint);
            canvas.DrawLine(new SKPoint(this.X - length, this.Y - length), new SKPoint(this.X + length, this.Y + length), paint);
        }

        /// <summary>
        /// Draws a Quest Item on this location.
        /// </summary>
        public void DrawQuestItem(SKCanvas canvas, QuestItem item, float heightDiff)
        {
            var label = item.Name;
            var paint = Extensions.GetEntityPaint(item);
            var text = Extensions.GetTextPaint(item);

            if (heightDiff > 2) // loot is above player
            {

                using var path = this.GetUpArrow();
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -2) // loot is below player
            {
                using var path = this.GetDownArrow();
                canvas.DrawPath(path, paint);
            }
            else // loot is level with player
            {
                canvas.DrawCircle(this.GetPoint(), 5 * UIScale, paint);
            }

            var coords = this.GetPoint(7 * UIScale, 3 * UIScale);

            canvas.DrawText(label, coords, Extensions.GetTextOutlinePaint());
            canvas.DrawText(label, coords, text);
        }

        /// <summary>
        /// Draws a quest zone on this location.
        /// </summary>
        public void DrawTaskZone(SKCanvas canvas, QuestZone zone, float heightDiff)
        {
            var label = zone.ObjectiveType;
            var paint = Extensions.GetEntityPaint(zone);
            var text = Extensions.GetTextPaint(zone);

            if (heightDiff > 2) // above player
            {
                using var path = this.GetUpArrow();
                canvas.DrawPath(path, paint);
            }
            else if (heightDiff < -2) // below player
            {
                using var path = this.GetDownArrow();
                canvas.DrawPath(path, paint);
            }
            else // level with player
            {
                canvas.DrawCircle(this.GetPoint(), 5 * UIScale, paint);
            }

            var coords = this.GetPoint(7 * UIScale, 3 * UIScale);
            
            canvas.DrawText(label, coords, Extensions.GetTextOutlinePaint());
            canvas.DrawText(label, coords, text);
        }

        /// <summary>
        /// Draws a Player Marker on this location.
        /// </summary>
        public void DrawPlayerMarker(SKCanvas canvas, Player player, AimlineSettings aimlineSettings, int? mouseoverGrp)
        {
            var radians = player.Rotation.X.ToRadians();
            SKPaint markerPaint, aimlinePaint;

            if (mouseoverGrp == player.GroupID)
            {
                markerPaint = SKPaints.PaintMouseoverGroup;
                markerPaint.Color = Extensions.SKColorFromPaintColor("TeamHover");
            }
            else
            {
                markerPaint = player.GetEntityPaint();
            }

            var playerPoint = this.GetPoint();
            canvas.DrawCircle(playerPoint, 6 * UIScale, markerPaint);

            if (aimlineSettings.Enabled)
            {
                aimlinePaint = markerPaint.Clone();
                aimlinePaint.Color = markerPaint.Color.WithAlpha((byte)aimlineSettings.Opacity);

                canvas.DrawLine(playerPoint, this.GetAimlineEndpoint(radians, aimlineSettings.Length), aimlinePaint);
            }
        }

        /// <summary>
        /// Draws Player Text on this location.
        /// </summary>
        public void DrawPlayerText(SKCanvas canvas, Player player, string[] aboveLines, string[] belowLines, string[] rightLines, string[] leftLines, int? mouseoverGrp)
        {
            var type = player.Type.ToString().Replace(" ", "");
            if (player.IsPMC && player.Type is not PlayerType.Teammate && !player.IsLocalPlayer)
                type = "PMC";

            var text = Extensions.PlayerTypeTextPaints[type];
            var flagsText = Extensions.PlayerTypeFlagTextPaints[type];
            var textOutline = Extensions.GetTextOutlinePaint();

            if (mouseoverGrp is not null && mouseoverGrp == player.GroupID)
            {
                text = SKPaints.TextMouseoverGroup;
                text.Color = Extensions.SKColorFromPaintColor("TeamHover");
            }
            else
            {
                text.Color = Extensions.GetTextColor(player);
            }

            flagsText.Color = text.Color;

            textOutline.Typeface = text.Typeface;
            textOutline.TextSize = text.TextSize;

            var circleRadius = 6 * UIScale;
            var lineHeight = 12 * UIScale;
            var aboveOffset = circleRadius  - 5 * UIScale;
            var belowOffset = circleRadius + 15 * UIScale;
            var sideOffset = circleRadius + 8 * UIScale;

            var aboveTextHeight = aboveLines.Length * lineHeight;

            for (int i = 0; i < aboveLines.Length; i++)
            {
                var bounds = new SKRect();
                text.MeasureText(aboveLines[i], ref bounds);
                var xOffset = -bounds.Width / 2;
                var yOffset = -aboveOffset - aboveTextHeight + (lineHeight * i);

                var coords = this.GetPoint(xOffset, yOffset);
                canvas.DrawText(aboveLines[i], coords, textOutline);
                canvas.DrawText(aboveLines[i], coords, text);
            }

            for (int i = 0; i < belowLines.Length; i++)
            {
                var bounds = new SKRect();
                text.MeasureText(belowLines[i], ref bounds);
                var xOffset = -bounds.Width / 2;
                var yOffset = belowOffset + (lineHeight * i);

                var coords = this.GetPoint(xOffset, yOffset);
                canvas.DrawText(belowLines[i], coords, textOutline);
                canvas.DrawText(belowLines[i], coords, text);
            }

            for (int i = 0; i < leftLines.Length; i++)
            {
                var bounds = new SKRect();
                text.MeasureText(leftLines[i], ref bounds);
                var xOffset = -sideOffset - bounds.Width;
                var yOffset = (lineHeight * i);
                var coords = this.GetPoint(xOffset, yOffset);
                canvas.DrawText(leftLines[i], coords, textOutline);
                canvas.DrawText(leftLines[i], coords, text);
            }

            textOutline.Typeface = flagsText.Typeface;
            textOutline.TextSize = flagsText.TextSize;

            for (int i = 0; i < rightLines.Length; i++)
            {
                var yOffset = (lineHeight * i);
                var coords = this.GetPoint(sideOffset, yOffset);
                canvas.DrawText(rightLines[i], coords, textOutline);
                canvas.DrawText(rightLines[i], coords, flagsText);
            }
        }

        /// <summary>
        /// Draws Loot information on this location
        /// </summary>
        public void DrawLootableObjectToolTip(SKCanvas canvas, LootableObject item)
        {
            if (item is LootCorpse corpse)
                DrawToolTip(canvas, corpse);
            else if (item is LootItem lootItem)
                DrawToolTip(canvas, lootItem);
        }

        /// <summary>
        /// Draws the tool tip for quest items
        /// </summary>
        public void DrawToolTip(SKCanvas canvas, QuestItem item)
        {
            var tooltipText = new List<string>();
            tooltipText.Insert(0, item.TaskName);

            var lines = string.Join("\n", tooltipText).Split('\n');
            var maxWidth = 0f;

            foreach (var line in lines)
            {
                var width = SKPaints.TextBase.MeasureText(line);
                maxWidth = Math.Max(maxWidth, width);
            }

            var textSpacing = 12 * UIScale;
            var padding = 3 * UIScale;

            var height = lines.Length * textSpacing;

            var left = X + padding;
            var top = Y - padding;
            var right = left + maxWidth + padding * 2;
            var bottom = top + height + padding * 2;

            var backgroundRect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(backgroundRect, SKPaints.PaintTransparentBacker);

            var y = bottom - (padding * 1.5f);
            foreach (var line in lines)
            {
                canvas.DrawText(line, left + padding, y, SKPaints.TextBase);
                y -= textSpacing;
            }
        }

        /// <summary>
        /// Draws the tool tip for quest items
        /// </summary>
        public void DrawToolTip(SKCanvas canvas, QuestZone item)
        {
            var tooltipText = new List<string>();
            tooltipText.Insert(0, item.TaskName);
            tooltipText.Insert(0, item.Description);

            var lines = string.Join("\n", tooltipText).Split('\n');
            var maxWidth = 0f;

            foreach (var line in lines)
            {
                var width = SKPaints.TextBase.MeasureText(line);
                maxWidth = Math.Max(maxWidth, width);
            }

            var textSpacing = 12 * UIScale;
            var padding = 3 * UIScale;

            var height = lines.Length * textSpacing;

            var left = X + padding;
            var top = Y - padding;
            var right = left + maxWidth + padding * 2;
            var bottom = top + height + padding * 2;

            var backgroundRect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(backgroundRect, SKPaints.PaintTransparentBacker);

            var y = bottom - (padding * 1.5f);
            foreach (var line in lines)
            {
                canvas.DrawText(line, left + padding, y, SKPaints.TextBase);
                y -= textSpacing;
            }
        }

        /// <summary>
        /// Draws player tool tip based on if theyre alive or not
        /// </summary>
        public void DrawToolTip(SKCanvas canvas, Player player)
        {
            if (!player.IsHostileActive || !player.IsAlive)
                return;

            DrawHostileTooltip(canvas, player);
        }

        /// <summary>
        /// Draws the tool tip for loot items
        /// </summary>
        private void DrawToolTip(SKCanvas canvas, LootItem lootItem)
        {
            var width = SKPaints.TextBase.MeasureText(lootItem.GetFormattedValueName());

            var textSpacing = 15 * UIScale;
            var padding = 3 * UIScale;

            var height = 1 * textSpacing;

            var left = X + padding;
            var top = Y - padding;
            var right = left + width + padding * 2;
            var bottom = top + height + padding * 2;

            var backgroundRect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(backgroundRect, SKPaints.PaintTransparentBacker);

            var y = bottom - (padding * 2.2f);

            canvas.DrawText(lootItem.GetFormattedValueName(), left + padding, y, Extensions.GetTextPaint(lootItem));
        }

        /// <summary>
        /// Draws the tool tip for loot corpses
        /// </summary>
        private void DrawToolTip(SKCanvas canvas, LootCorpse corpse)
        {
            var maxWidth = SKPaints.TextBase.MeasureText(corpse.Name);
            var items = corpse.Items;
            var height = items.Count;
            var isEmptyCorpseName = corpse.Name.Contains("Clone");

            if (!isEmptyCorpseName)
                height += 1;

            foreach (var gearItem in items)
            {
                var width = SKPaints.TextBase.MeasureText(gearItem.Item.GetFormattedTotalValueName());
                maxWidth = Math.Max(maxWidth, width);

                var tmpItems = gearItem.Item.Loot.Count > 1 ? LootManager.MergeDupelicateLootItems(gearItem.Item.Loot) : gearItem.Item.Loot;

                if (_config.SubItems && tmpItems.Count > 0)
                {
                    foreach (var lootItem in tmpItems)
                    {
                        if (lootItem.AlwaysShow || lootItem.Important || (!_config.ImportantLootOnly && _config.SubItems && lootItem.Value > _config.MinSubItemValue))
                        {
                            width = SKPaints.TextBase.MeasureText($"     {lootItem.GetFormattedValueName()}");
                            maxWidth = Math.Max(maxWidth, width);

                            height++;
                        }
                    }
                }
            }

            var textSpacing = 15 * UIScale;
            var padding = 3 * UIScale;

            height = (int)(height * textSpacing);

            var left = X + padding;
            var top = Y - padding;
            var right = left + maxWidth + padding * 2;
            var bottom = top + height + padding * 2;

            var backgroundRect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(backgroundRect, SKPaints.PaintTransparentBacker);

            var y = bottom - (padding * 2.2f);
            foreach (var gearItem in items)
            {
                var tmpItems = gearItem.Item.Loot.Count > 1 ? LootManager.MergeDupelicateLootItems(gearItem.Item.Loot) : gearItem.Item.Loot;

                if (_config.SubItems && tmpItems.Count > 0)
                {
                    foreach (var lootItem in tmpItems)
                    {
                        if (lootItem.AlwaysShow || lootItem.Important || (!_config.ImportantLootOnly && _config.SubItems && lootItem.Value > _config.MinSubItemValue))
                        {
                            canvas.DrawText("   " + lootItem.GetFormattedValueName(), left + padding, y, Extensions.GetTextPaint(lootItem));
                            y -= textSpacing;
                        }
                    }
                }

                canvas.DrawText(gearItem.Item.GetFormattedTotalValueName(), left + padding, y, Extensions.GetTextPaint(gearItem.Item));
                y -= textSpacing;
            }

            if (!isEmptyCorpseName)
            {
                canvas.DrawText(corpse.Name, left + padding, y, SKPaints.TextBase);
                y -= textSpacing;
            }
        }

        /// <summary>
        /// Draws tool tip of hostile players 
        /// </summary>
        private void DrawHostileTooltip(SKCanvas canvas, Player player)
        {
            var lines = new List<string>();

            lines.Insert(0, player.Name);

            if (player.Type == PlayerType.Special)
                lines.Insert(0, $"Tag: {player.Tag}");

            if (player.Gear is not null)
            {
                foreach (var gear in player.Gear)
                {
                    var key = gear.Slot.Key;

                    if (GearManager.GEAR_SLOT_NAMES.ContainsKey(key))
                    {
                        var gearItem = gear.Item;
                        var itemName = gearItem.Short;

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.AmmoType))
                            itemName += $" ({gearItem.GearInfo.AmmoType}/{gearItem.GearInfo.AmmoCount})";

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.Thermal))
                            itemName += $" ({gearItem.GearInfo.Thermal})";

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.NightVision))
                            itemName += $" ({gearItem.GearInfo.NightVision})";

                        lines.Insert(0, $"{GearManager.GEAR_SLOT_NAMES[key]}: {itemName.Trim()}");
                    }
                }
            }

            lines.Insert(0, $"Value: {TarkovDevManager.FormatNumber(player.Value)}");

            DrawToolTip(canvas, string.Join("\n", lines));
        }

        /// <summary>
        /// Draws the tool tip for players/hostiles
        /// </summary>
        private void DrawToolTip(SKCanvas canvas, string tooltipText)
        {
            var lines = tooltipText.Split('\n');
            var maxWidth = 0f;

            foreach (var line in lines)
            {
                var width = SKPaints.TextBase.MeasureText(line);
                maxWidth = Math.Max(maxWidth, width);
            }

            var textSpacing = 12 * UIScale;
            var padding = 3 * UIScale;

            var height = lines.Length * textSpacing;

            var left = X + padding;
            var top = Y - padding;
            var right = left + maxWidth + padding * 2;
            var bottom = top + height + padding * 2;

            var backgroundRect = new SKRect(left, top, right, bottom);
            canvas.DrawRect(backgroundRect, SKPaints.PaintTransparentBacker);

            var y = bottom - (padding * 1.5f);
            foreach (var line in lines)
            {
                canvas.DrawText(line, left + padding, y, SKPaints.TextBase);
                y -= textSpacing;
            }
        }
    }

    /// <summary>
    /// Defines a Map for use in the GUI.
    /// </summary>
    public class Map
    {
        /// <summary>
        /// Name of map (Ex: Customs)
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// 'MapConfig' class instance
        /// </summary>
        public readonly MapConfig ConfigFile;
        /// <summary>
        /// File path to Map .JSON Config
        /// </summary>
        public readonly string ConfigFilePath;

        public Map(string name, MapConfig config, string configPath, string mapID)
        {
            Name = name;
            ConfigFile = config;
            ConfigFilePath = configPath;
            MapID = mapID;
        }

        public readonly string MapID;
    }

    /// <summary>
    /// Contains multiple map parameters used by the GUI.
    /// </summary>
    public struct MapParameters
    {
        public float UIScale;
        public int MapLayerIndex;
        public SKRect Bounds;
        public float XScale;
        public float YScale;
    }

    /// <summary>
    /// Defines a .JSON Map Config File
    /// </summary>
    public class MapConfig
    {
        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };

        [JsonPropertyName("mapID")]
        public List<string> MapID { get; set; } // New property for map IDs

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("scale")]
        public float Scale { get; set; }

        // Updated to match new JSON format
        [JsonPropertyName("mapLayers")]
        public List<MapLayer> MapLayers { get; set; }

        public static MapConfig LoadFromFile(string file)
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<MapConfig>(json, _jsonOptions);
        }

        public void Save(Map map)
        {
            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(map.ConfigFilePath, json);
        }
    }

    public class MapLayer
    {
        [JsonPropertyName("minHeight")]
        public float MinHeight { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; }
    }
}
