﻿using System.Collections.Generic;
using JetBrains.Annotations;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Models.CraftingContext;

namespace SolastaUnfinishedBusiness.ItemCrafting;

internal static class GreatAxeData
{
    private static ItemCollection _items;

    [NotNull]
    internal static ItemCollection Items =>
        _items ??= new ItemCollection
        {
            BaseItems =
                new List<(ItemDefinition item, ItemDefinition presentation)>
                {
                    (ItemDefinitions.Greataxe, ItemDefinitions.GreataxePlus2)
                },
            PossiblePrimedItemsToReplace = new List<ItemDefinition>
            {
                ItemDefinitions.Primed_Morningstar,
                ItemDefinitions.Primed_Mace,
                ItemDefinitions.Primed_Greatsword,
                ItemDefinitions.Primed_Battleaxe
            },
            MagicToCopy = new List<ItemCollection.MagicItemDataHolder>
            {
                // Same as +1
                new("Acuteness", ItemDefinitions.Enchanted_Mace_Of_Acuteness,
                    RecipeDefinitions.Recipe_Enchantment_MaceOfAcuteness),
                new("Bearclaw", ItemDefinitions.Enchanted_Morningstar_Bearclaw,
                    RecipeDefinitions.Recipe_Enchantment_MorningstarBearclaw),
                new("Power", ItemDefinitions.Enchanted_Morningstar_Of_Power,
                    RecipeDefinitions.Recipe_Enchantment_MorningstarOfPower),
                new("Lightbringer", ItemDefinitions.Enchanted_Greatsword_Lightbringer,
                    RecipeDefinitions.Recipe_Enchantment_GreatswordLightbringer),
                new("Punisher", ItemDefinitions.Enchanted_Battleaxe_Punisher,
                    RecipeDefinitions.Recipe_Enchantment_BattleaxePunisher)
            }
        };
}
