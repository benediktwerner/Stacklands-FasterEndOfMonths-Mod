using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FasterEndOfMonths
{
    [BepInPlugin(
        "de.benediktwerner.stacklands.fasterendofmonths",
        PluginInfo.PLUGIN_NAME,
        PluginInfo.PLUGIN_VERSION
    )]
    [BepInDependency("StoneArch", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource L;
        static ConfigEntry<int> autosaveFrequency;
        static ConfigEntry<bool> disableDebugAutosave;

        private void Awake()
        {
            L = Logger;
            autosaveFrequency = Config.Bind(
                "General",
                "AutosaveFrequency",
                1,
                "How often to save at the end of the moon (every x moons). "
                    + "By default the game saves every moon which helps prevent data loss (if the game or your PC crashes) but it also makes the cutscene lag."
                    + "Set to a large number to never save."
            );
            disableDebugAutosave = Config.Bind(
                "General",
                "DisableDebugAutosave",
                true,
                "By default, the game creates additional versioned backup saves at the end of the moon. Disabling them reduces the short lag-spike at the end of each moon."
            );
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EndOfMonthCutscenes), nameof(EndOfMonthCutscenes.FeedVillagers))]
        public static void FeedVillagersPatch(out IEnumerator __result, out bool __runOriginal)
        {
            __runOriginal = false;
            __result = FeedVillagers();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.EndOfMonthRoutine))]
        public static void EndOfMonthRoutinePrefix(ref EndOfMonthParameters param)
        {
            param.SkipEndConfirmation = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.EndOfMonth))]
        public static void RememberSpeedUp(WorldManager __instance, out float __state)
        {
            __state = __instance.SpeedUp;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.EndOfMonth))]
        public static void RestoreSpeedUp(WorldManager __instance, float __state)
        {
            __instance.SpeedUp = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.Save), new[] { typeof(bool) })]
        public static void DisableSave(out bool __runOriginal)
        {
            __runOriginal = WorldManager.instance.CurrentMonth % autosaveFrequency.Value == 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DebugScreen), nameof(DebugScreen.AutoSave))]
        public static void DisableDebugAutoSave(out bool __runOriginal)
        {
            __runOriginal = !disableDebugAutosave.Value;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(
            typeof(EndOfMonthCutscenes),
            nameof(EndOfMonthCutscenes.SpecialEvents),
            MethodType.Enumerator
        )]
        public static IEnumerable<CodeInstruction> NoWaitForSecondsInSpecialEvents(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var waitForSecondsCtor = typeof(WaitForSeconds).GetConstructor(new[] { typeof(float) });
            return new CodeMatcher(instructions)
                .MatchForward(
                    false,
                    new CodeMatch(OpCodes.Ldc_R4),
                    new CodeMatch(OpCodes.Newobj, waitForSecondsCtor)
                )
                .Repeat(matcher => matcher.SetOperandAndAdvance(0f))
                .InstructionEnumeration();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(
            typeof(WorldManager),
            nameof(WorldManager.EndOfMonthRoutine),
            MethodType.Enumerator
        )]
        public static IEnumerable<CodeInstruction> NoWaitForSecondsInEndOfMonth(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var waitForSecondsCtor = typeof(WaitForSeconds).GetConstructor(new[] { typeof(float) });
            var matcher = new CodeMatcher(instructions).MatchForward(
                false,
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Newobj, waitForSecondsCtor)
            );
            if (matcher.IsValid)
                matcher.SetOperandAndAdvance(0f);
            else
                L.LogWarning(
                    "Didn't find WaitForSeconds to patch in WorldManager.EndOfMonthRoutine"
                );
            return matcher.InstructionEnumeration();
        }

        public static IEnumerator FeedVillagers()
        {
            AudioManager.me.PlaySound2D(AudioManager.me.Eat, Random.Range(0.8f, 1.2f), 0.3f);

            int requiredFoodCount = WorldManager.instance.GetRequiredFoodCount();
            var villagersToFeed = new List<CardData>();
            foreach (var card in WorldManager.instance.AllCards)
            {
                if (card.MyBoard.IsCurrent && (card.CardData is Villager || card.CardData is Kid))
                    villagersToFeed.Add(card.CardData);
            }
            villagersToFeed = villagersToFeed
                .OrderBy(x =>
                {
                    if (x is Kid)
                        return 0;
                    if (x.Id == "dog")
                        return 1;
                    return 2;
                })
                .ToList<CardData>();

            var fedVillagers = new List<CardData>();
            for (int i = 0; i < villagersToFeed.Count; i++)
            {
                CardData vill = villagersToFeed[i];
                int foodForVillager = WorldManager.instance.GetCardRequiredFoodCount(
                    vill.MyGameCard
                );
                for (int j = 0; j < foodForVillager; j++)
                {
                    Food food =
                        EndOfMonthCutscenes.GetHotpotWithFood()
                        ?? EndOfMonthCutscenes.GetFoodToUseUp();
                    if (food == null)
                        break;
                    GameCard foodCard = food.MyGameCard;
                    food.FoodValue--;
                    requiredFoodCount--;
                    Combatable combatable = vill as Combatable;
                    if (combatable != null)
                    {
                        combatable.HealthPoints = Mathf.Min(
                            combatable.HealthPoints + 3,
                            combatable.ProcessedCombatStats.MaxHealth
                        );
                        food.ConsumedBy(combatable);
                    }
                    if (
                        food.FoodValue <= 0
                        && food.Id != "compactstorage.food_warehouse"
                        && food is not Hotpot
                    )
                    {
                        var originalStack = foodCard.GetAllCardsInStack();
                        foodCard.RemoveFromStack();
                        food.FullyConsumed(vill);
                        originalStack.Remove(foodCard);
                        WorldManager.instance.Restack(originalStack);
                        foodCard.DestroyCard(true, true);
                    }
                    if (j == foodForVillager - 1)
                    {
                        fedVillagers.Add(vill);
                    }
                }
            }

            if (requiredFoodCount > 0)
            {
                var unfedVillagers = new List<CardData>();
                foreach (CardData cardData in villagersToFeed)
                {
                    if (!fedVillagers.Contains(cardData) && cardData is not Kid)
                        unfedVillagers.Add(cardData);
                }
                var humansToDie = unfedVillagers.Count;
                EndOfMonthCutscenes.SetStarvingHumanStatus(humansToDie);
                yield return Cutscenes.WaitForContinueClicked(SokLoc.Translate("label_uh_oh"));
                for (int i = 0; i < unfedVillagers.Count; i++)
                {
                    CardData cardData2 = unfedVillagers[i];
                    if (!(cardData2 is Kid))
                    {
                        yield return WorldManager.instance.KillVillagerCoroutine(
                            cardData2 as Villager,
                            null,
                            null
                        );
                        EndOfMonthCutscenes.SetStarvingHumanStatus(humansToDie - i);
                    }
                }
                if (WorldManager.instance.CheckAllVillagersDead())
                {
                    WorldManager.instance.VillagersStarvedAtEndOfMoon = true;
                    if (WorldManager.instance.CurrentBoard.Id == "main")
                    {
                        EndOfMonthCutscenes.CutsceneText = SokLoc.Translate(
                            "label_everyone_starved"
                        );
                        yield return Cutscenes.WaitForContinueClicked(
                            SokLoc.Translate("label_game_over")
                        );
                        GameCanvas.instance.SetScreen(GameCanvas.instance.GameOverScreen);
                        WorldManager.instance.currentAnimationRoutine = null;
                    }
                    else if (WorldManager.instance.CurrentBoard.Id == "island")
                    {
                        yield return Cutscenes.EveryoneOnIslandDead();
                    }
                    else if (WorldManager.instance.CurrentBoard.Id == "forest")
                    {
                        yield return Cutscenes.EveryoneInForestDead();
                    }
                    else
                    {
                        yield return Cutscenes.EveryoneOnIslandDead();
                    }
                }
            }
            yield break;
        }
    }
}
