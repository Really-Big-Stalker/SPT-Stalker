using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SPTStalker.Client
{
    /// <summary>
    /// Everything this plugin needs from the game, looked up by name at runtime. The names come from SPT's
    /// hollowed Assembly-CSharp for SPT 4.1.x (EFT.GameWorld, EFT.Player, ActiveHealthController, DamageInfo...).
    /// Using reflection keeps the plugin buildable without the game's assemblies; if a game update renames
    /// something, Init() logs exactly what is missing.
    /// </summary>
    internal static class EftBridge
    {
        private static Type _gameWorldType, _damageInfoType, _bodyPartType, _damageTypeType, _requestHandlerType;
        private static FieldInfo _mainPlayerField, _diDamageType, _diDamage, _diHitPoint, _diDirection;
        private static PropertyInfo _locationIdProp;
        private static MethodInfo _getJson;

        public static bool Ready { get; private set; }

        public static bool Init()
        {
            if (Ready)
            {
                return true;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Type Find(string fullName) => assemblies.Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);

            _gameWorldType = Find("EFT.GameWorld");
            _damageInfoType = Find("EFT.Ballistics.DamageInfo");
            _bodyPartType = Find("EBodyPart");
            _damageTypeType = Find("EFT.EDamageType");
            _requestHandlerType = Find("SPT.Common.Http.RequestHandler");

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _mainPlayerField = _gameWorldType?.GetField("MainPlayer", flags);
            _locationIdProp = _gameWorldType?.GetProperty("LocationId", flags);
            _diDamageType = _damageInfoType?.GetField("DamageType");
            _diDamage = _damageInfoType?.GetField("Damage");
            _diHitPoint = _damageInfoType?.GetField("HitPoint");
            _diDirection = _damageInfoType?.GetField("Direction");
            _getJson = _requestHandlerType?.GetMethod("GetJson", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);

            var missing = new (string, object)[]
            {
                ("EFT.GameWorld", _gameWorldType), ("GameWorld.MainPlayer", _mainPlayerField), ("GameWorld.LocationId", _locationIdProp),
                ("EFT.Ballistics.DamageInfo", _damageInfoType), ("DamageInfo fields", _diDamage), ("EBodyPart", _bodyPartType),
                ("EFT.EDamageType", _damageTypeType), ("SPT.Common.Http.RequestHandler.GetJson", _getJson)
            }.Where(x => x.Item2 == null).Select(x => x.Item1).ToArray();

            if (missing.Length > 0)
            {
                Plugin.Log.LogError("SPT-Stalker: game API not found (game/SPT version mismatch?): " + string.Join(", ", missing));
                return false;
            }

            Ready = true;
            return true;
        }

        // ---------------------------------------------------------------- world / player

        public static MonoBehaviour FindGameWorld()
        {
            return (MonoBehaviour)UnityEngine.Object.FindObjectOfType(_gameWorldType);
        }

        public static object MainPlayer(MonoBehaviour gameWorld) => gameWorld == null ? null : _mainPlayerField.GetValue(gameWorld);

        public static string LocationId(MonoBehaviour gameWorld) => _locationIdProp.GetValue(gameWorld) as string ?? "";

        public static Vector3 PlayerPosition(object player)
        {
            return (Vector3)player.GetType().GetProperty("Position").GetValue(player);
        }

        public static object Health(object player)
        {
            return player.GetType().GetProperty("ActiveHealthController")?.GetValue(player);
        }

        public static bool IsAlive(object health)
        {
            if (health == null)
            {
                return false;
            }

            var prop = health.GetType().GetProperty("IsAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop == null || (bool)prop.GetValue(health);
        }

        // ---------------------------------------------------------------- health effects

        public static void ApplyDamage(object health, string bodyPart, float damage, string damageType, Vector3 point, Vector3 direction)
        {
            if (health == null || damage <= 0)
            {
                return;
            }

            var info = Activator.CreateInstance(_damageInfoType); // boxed struct; fields set on the box
            _diDamageType.SetValue(info, ParseEnum(_damageTypeType, damageType, "Environment"));
            _diDamage.SetValue(info, damage);
            _diHitPoint?.SetValue(info, point);
            _diDirection?.SetValue(info, direction);

            Call(health, "ApplyDamage", new[] { _bodyPartType, typeof(float), _damageInfoType },
                ParseEnum(_bodyPartType, bodyPart, "Chest"), damage, info);
        }

        public static void DoContusion(object health, float seconds, float strength) =>
            Call(health, "DoContusion", new[] { typeof(float), typeof(float) }, seconds, strength);

        public static void DoStun(object health, float seconds, float strength) =>
            Call(health, "DoStun", new[] { typeof(float), typeof(float) }, seconds, strength);

        public static void DoFracture(object health, string bodyPart) =>
            Call(health, "DoFracture", new[] { _bodyPartType }, ParseEnum(_bodyPartType, bodyPart, "LeftLeg"));

        public static void DoBleed(object health, string bodyPart) =>
            Call(health, "DoBleed", new[] { typeof(bool), _bodyPartType }, false, ParseEnum(_bodyPartType, bodyPart, "Chest"));

        public static void DoIntoxication(object health) => Call(health, "DoIntoxication", Type.EmptyTypes);

        public static void DoRadExposure(object health) => Call(health, "DoRadExposure", Type.EmptyTypes);

        // ---------------------------------------------------------------- equipment

        private static Type _equipmentSlotType;

        /// <summary>Template id of the item in the player's face-cover slot (gas mask, respirator...), or null.</summary>
        public static string FaceCoverTemplateId(object player)
        {
            var item = SlotItem(player, "FaceCover");
            return item == null ? null : TemplateId(item);
        }

        /// <summary>
        /// Template ids of everything the player carries, skipping the given equipment slots and anything inside
        /// sub-slots with the given names (e.g. the pockets' SpecialSlot1-3).
        /// </summary>
        public static System.Collections.Generic.List<string> CarriedTemplateIds(object player, string[] skipSlots, string[] skipSubSlots)
        {
            var result = new System.Collections.Generic.List<string>();
            var slotType = EquipmentSlotType();
            if (slotType == null)
            {
                return result;
            }

            foreach (var name in Enum.GetNames(slotType))
            {
                if (skipSlots != null && skipSlots.Contains(name))
                {
                    continue;
                }

                var root = SlotItem(player, name);
                if (root == null)
                {
                    continue;
                }

                var skipped = ItemsInSubSlots(root, skipSubSlots);
                foreach (var item in AllItems(root))
                {
                    if (!skipped.Contains(item))
                    {
                        result.Add(TemplateId(item));
                    }
                }
            }

            return result;
        }

        /// <summary>Template ids of the items in the given equipment slots, including everything inside them (plates, inserts...).</summary>
        public static System.Collections.Generic.List<string> SlotTemplateIds(object player, string[] slots)
        {
            var result = new System.Collections.Generic.List<string>();
            foreach (var name in slots ?? new string[0])
            {
                var root = SlotItem(player, name);
                if (root != null)
                {
                    result.AddRange(AllItems(root).Select(TemplateId));
                }
            }

            return result;
        }

        private static System.Collections.Generic.IEnumerable<object> AllItems(object root)
        {
            var all = root.GetType().GetMethod("GetAllVisibleItems", BindingFlags.Instance | BindingFlags.Public)?.Invoke(root, null)
                as System.Collections.IEnumerable;
            if (all == null)
            {
                return new[] { root };
            }

            return all.Cast<object>().ToList();
        }

        /// <summary>Items (and their contents) sitting in sub-slots of `root` whose ID/name is in `names`.</summary>
        private static System.Collections.Generic.HashSet<object> ItemsInSubSlots(object root, string[] names)
        {
            var result = new System.Collections.Generic.HashSet<object>();
            if (names == null || names.Length == 0)
            {
                return result;
            }

            var slots = root.GetType().GetProperty("AllSlots", BindingFlags.Instance | BindingFlags.Public)?.GetValue(root)
                as System.Collections.IEnumerable;
            if (slots == null)
            {
                return result;
            }

            foreach (var slot in slots)
            {
                var t = slot.GetType();
                var id = t.GetProperty("ID")?.GetValue(slot) as string ?? t.GetProperty("Name")?.GetValue(slot) as string;
                if (id == null || !names.Any(n => string.Equals(n, id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var contained = t.GetProperty("ContainedItem")?.GetValue(slot);
                if (contained != null)
                {
                    foreach (var item in AllItems(contained))
                    {
                        result.Add(item);
                    }
                }
            }

            return result;
        }

        public static string TemplateId(object item)
        {
            return item?.GetType().GetProperty("StringTemplateId", BindingFlags.Instance | BindingFlags.Public)?.GetValue(item) as string;
        }

        private static object SlotItem(object player, string slotName)
        {
            try
            {
                var equipment = player.GetType().GetProperty("Equipment", BindingFlags.Instance | BindingFlags.Public)?.GetValue(player);
                var slotType = EquipmentSlotType();
                if (equipment == null || slotType == null)
                {
                    return null;
                }

                var slot = equipment.GetType().GetMethod("GetSlot", new[] { slotType })
                    ?.Invoke(equipment, new[] { Enum.Parse(slotType, slotName) });
                return slot?.GetType().GetProperty("ContainedItem", BindingFlags.Instance | BindingFlags.Public)?.GetValue(slot);
            }
            catch
            {
                return null;
            }
        }

        private static Type EquipmentSlotType()
        {
            return _equipmentSlotType ??= AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("EFT.InventoryLogic.EquipmentSlot", false))
                .FirstOrDefault(t => t != null);
        }

        // ---------------------------------------------------------------- server

        public static string GetJson(string path) => _getJson.Invoke(null, new object[] { path }) as string;

        // ---------------------------------------------------------------- helpers

        private static object ParseEnum(Type enumType, string value, string fallback)
        {
            try
            {
                return Enum.Parse(enumType, value, true);
            }
            catch
            {
                return Enum.Parse(enumType, fallback, true);
            }
        }

        private static void Call(object target, string method, Type[] signature, params object[] args)
        {
            if (target == null)
            {
                return;
            }

            try
            {
                var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, signature, null);
                if (mi == null)
                {
                    Plugin.Log.LogWarning($"SPT-Stalker: {target.GetType().Name}.{method} not found");
                    return;
                }

                mi.Invoke(target, args);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"SPT-Stalker: {method} failed: {e.InnerException?.Message ?? e.Message}");
            }
        }
    }
}
