﻿using System.Collections.Generic;
using JetBrains.Annotations;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;
using static SolastaUnfinishedBusiness.Models.CraftingContext;

namespace SolastaUnfinishedBusiness.ItemCrafting;

internal static class ThrowingWeaponData
{
    private static ItemCollection _items;

    [NotNull]
    internal static ItemCollection Items =>
        _items ??= new ItemCollection
        {
            BaseItems =
                new List<(ItemDefinition item, ItemDefinition presentation)>
                {
                    (ItemDefinitions.Javelin, ItemDefinitions.JavelinPlus2), (ItemDefinitions.Dart, null)
                },
            PossiblePrimedItemsToReplace = new List<ItemDefinition> { ItemDefinitions.Primed_Dagger },
            MagicToCopy = new List<ItemCollection.MagicItemDataHolder>
            {
                // Same as +1
                new("Acuteness", ItemDefinitions.Enchanted_Dagger_of_Acuteness,
                    RecipeDefinitions.Recipe_Enchantment_DaggerOfAcuteness),
                // Same as +2
                new("Sharpness", ItemDefinitions.Enchanted_Dagger_of_Sharpness,
                    RecipeDefinitions.Recipe_Enchantment_DaggerOfSharpness),
                new("Souldrinker", ItemDefinitions.Enchanted_Dagger_Souldrinker,
                    RecipeDefinitions.Recipe_Enchantment_DaggerSouldrinker),
                new("Frostburn", ItemDefinitions.Enchanted_Dagger_Frostburn,
                    RecipeDefinitions.Recipe_Enchantment_DaggerFrostburn)
            }
            // NumProduced = 3
        };
}
