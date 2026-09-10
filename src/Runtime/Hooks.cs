using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SteamManagerRuntime
{
    public static class Hooks
    {
        [ThreadStatic] static int consuming;
        public static void Install()
        {
            Bridge.Patch(2, typeof(Character), "ApplyDamage", "Incoming");
            Bridge.Patch(9, typeof(Character), "Damage", "Damage");
            Bridge.Patch(23, typeof(Character), "Damage", "Outgoing");
            Bridge.Patch(25, typeof(Character), "Damage", "OneHit");
            Bridge.Patch(3, typeof(Player), "UseStamina", "Stamina");
            Bridge.Patch(3, typeof(Player), "HaveStamina", postfix:"HasStamina");
            Bridge.Patch(4, typeof(Player), "UseEitr", "Eitr");
            Bridge.Patch(4, typeof(Player), "HaveEitr", postfix:"HasEitr");
            Bridge.Patch(6, typeof(Player), "GetMaxCarryWeight", postfix:"Carry");
            Bridge.Patch(15, typeof(Player), "UpdateFood", "Food");
            Bridge.Patch(16, typeof(StatusEffect), "UpdateStatusEffect", "Rested");
            Bridge.Patch(17, typeof(SE_Rested), "CalculateComfortLevel", postfix:"Comfort", args:new[] { typeof(Player) });
            Bridge.Patch(17, typeof(Player), "GetComfortLevel", postfix:"PlayerComfort");
            Bridge.Patch(29, typeof(Player), "ConsumeResources", "Resources");
            Bridge.Patch(29, typeof(Inventory), "RemoveOneItem", "RemoveOne");
            Bridge.Patch(29, typeof(Inventory), "RemoveItem", "RemoveAmount", args:new[] { typeof(ItemDrop.ItemData), typeof(int) });
            Scope(typeof(Player), "ConsumeItem"); Scope(typeof(Attack), "UseAmmo");
            Bridge.Patch(30, typeof(Humanoid), "DrainEquipedItemDurability", "Durability");
            Bridge.Patch(34, typeof(Skills), "RaiseSkill", "SkillXP");
            Bridge.Patch(37, typeof(Player), "GetAvailableRecipes", postfix:"Recipes");
            Bridge.Patch(37, typeof(PieceTable), "UpdateAvailable", "Pieces");
            Bridge.Patch(43, typeof(Inventory), "IsTeleportable", postfix:"Portal");
            Bridge.Patch(49, typeof(Achievements), "CanGetAchievements", postfix:"AchievementsAllowed");
        }
        static void Scope(Type type, string method)
        {
            try { Bridge.Harmony.Patch(AccessTools.Method(type, method), new HarmonyMethod(typeof(Hooks), "ConsumeStart"), finalizer:new HarmonyMethod(typeof(Hooks), "ConsumeEnd")); }
            catch (Exception e) { Bridge.Features.Find(x => x.Id == 29).Error = "Consumption hook unavailable: " + e.GetBaseException().Message; }
        }
        public static void Pump() { Bridge.Tick(); }
        public static bool Incoming(Character __instance) { return !(Bridge.Local(__instance) && Bridge.On(2)); }
        public static bool Damage(Character __instance, HitData hit)
        { return !(Bridge.Local(__instance) && ((Bridge.On(9) && hit.m_hitType == HitData.HitType.Fall) || Bridge.On(2))); }
        static bool EnemyHit(Character target, HitData hit)
        { return Player.m_localPlayer != null && target != null && !target.IsPlayer() && !target.IsTamed() && hit.GetAttacker() == Player.m_localPlayer; }
        public static void Outgoing(Character __instance, HitData hit)
        { if (Bridge.On(23) && !Bridge.On(25) && EnemyHit(__instance, hit)) hit.ApplyModifier(Bridge.Value(23)); }
        public static void OneHit(Character __instance, HitData hit)
        { if (Bridge.On(25) && EnemyHit(__instance, hit)) hit.m_damage.m_damage = Math.Max(100000f, __instance.GetMaxHealth() * 100); }
        public static bool Stamina(Player __instance) { return !(Bridge.Local(__instance) && Bridge.On(3)); }
        public static void HasStamina(Player __instance, ref bool __result) { if (Bridge.Local(__instance) && Bridge.On(3)) __result = true; }
        public static bool Eitr(Player __instance) { return !(Bridge.Local(__instance) && Bridge.On(4)); }
        public static void HasEitr(Player __instance, ref bool __result) { if (Bridge.Local(__instance) && Bridge.On(4)) __result = true; }
        public static void Carry(Player __instance, ref float __result) { if (Bridge.Local(__instance) && Bridge.On(6)) __result = Bridge.Value(6); }
        public static void Food(Player __instance, float dt, bool forceUpdate, float ___m_foodUpdateTimer)
        {
            if (!Bridge.Local(__instance) || !Bridge.On(15)) return;
            if (___m_foodUpdateTimer + dt * Game.m_foodRate < 1 && !forceUpdate) return;
            float multiplier = Bridge.Value(15);
            float adjustment = multiplier == 0 ? 1 : 1 - 1 / multiplier;
            foreach (var food in __instance.GetFoods()) food.m_time += adjustment;
        }
        public static void Rested(StatusEffect __instance, ref float dt)
        { if (__instance is SE_Rested && Bridge.Local(__instance.m_character) && Bridge.On(16)) dt = Bridge.Value(16) == 0 ? 0 : dt / Bridge.Value(16); }
        public static void Comfort(Player player, ref int __result) { if (Bridge.Local(player) && Bridge.On(17)) __result = Math.Max(__result, Bridge.Comfort); }
        public static void PlayerComfort(Player __instance, ref int __result) { if (Bridge.Local(__instance) && Bridge.On(17)) __result = Math.Max(__result, Bridge.Comfort); }
        public static bool Resources(Player __instance) { return !(Bridge.Local(__instance) && (Bridge.On(29) || Bridge.On(36))); }
        public static void ConsumeStart(object __instance, out bool __state)
        {
            var character = __instance as Character;
            if (__instance is Attack) character = (Character)AccessTools.Field(typeof(Attack), "m_character").GetValue(__instance);
            __state = Bridge.Local(character) && Bridge.On(29);
            if (__state) consuming++;
        }
        public static Exception ConsumeEnd(bool __state, Exception __exception) { if (__state) consuming--; return __exception; }
        public static bool RemoveOne(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (consuming <= 0 || Player.m_localPlayer == null || __instance != Player.m_localPlayer.GetInventory()) return true;
            __result = __instance.ContainsItem(item); return false;
        }
        public static bool RemoveAmount(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { return RemoveOne(__instance, item, ref __result); }
        public static bool Durability(Humanoid __instance) { return !(Bridge.Local(__instance) && Bridge.On(30)); }
        public static void SkillXP(Skills __instance, ref float factor)
        { if (Player.m_localPlayer != null && __instance == Player.m_localPlayer.GetSkills() && Bridge.On(34)) factor *= Bridge.Value(34); }
        public static void Recipes(Player __instance, ref List<Recipe> available)
        {
            if (!Bridge.Local(__instance) || !Bridge.On(37)) return;
            foreach (var recipe in ObjectDB.instance.m_recipes)
                if (recipe != null && recipe.m_enabled && recipe.m_item != null && !available.Contains(recipe) && (string.IsNullOrEmpty(recipe.m_item.m_itemData.m_shared.m_dlc) || DLCMan.instance.IsDLCInstalled(recipe.m_item.m_itemData.m_shared.m_dlc))) available.Add(recipe);
        }
        public static void Pieces(PieceTable __instance, Player player, ref HashSet<string> knownRecipies)
        {
            if (!Bridge.Local(player) || !Bridge.On(37)) return;
            knownRecipies = new HashSet<string>(knownRecipies);
            foreach (var prefab in __instance.m_pieces) { if (prefab == null) continue; var p = prefab.GetComponent<Piece>(); if (p != null && p.m_enabled) knownRecipies.Add(p.m_name); }
        }
        public static void Portal(Inventory __instance, ref bool __result)
        { if (Player.m_localPlayer != null && __instance == Player.m_localPlayer.GetInventory() && Bridge.On(43)) __result = true; }
        public static void AchievementsAllowed(ref bool __result) { if (Player.m_localPlayer != null && Bridge.On(49)) __result = true; }
    }
}
