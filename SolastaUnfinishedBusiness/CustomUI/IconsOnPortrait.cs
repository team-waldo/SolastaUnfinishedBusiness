﻿using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.CustomBuilders;
using SolastaUnfinishedBusiness.CustomInterfaces;

namespace SolastaUnfinishedBusiness.CustomUI;

internal static class IconsOnPortrait
{
    internal static void CharacterPanelRefresh(ActiveCharacterPanel panel)
    {
        var character = panel.GuiCharacter?.RulesetCharacter;

        if (character == null)
        {
            return;
        }

        var poolPrefab = panel.sorceryPointsBox.gameObject;
        var concentrationPrefab = panel.concentrationGroup.gameObject;
        var layout = panel.transform.Find("RightLayout");

        // Hide all custom controls
        for (var i = 0; i < layout.childCount; i++)
        {
            var child = layout.GetChild(i);
            if (child.name.StartsWith("CustomPool(") || child.name.StartsWith("CustomConcentration("))
            {
                child.gameObject.SetActive(false);
            }
        }

        // setup/update relevant custom controls
        var pools = character.GetSubFeaturesByType<ICustomPortraitPointPoolProvider>();
        if (Main.Settings.ShowChannelDivinityOnPortrait
            && character.TryGetAttributeValue(AttributeDefinitions.ChannelDivinityNumber) > 0)
        {
            pools.Add(PortraitPointChannelDivinity.Instance);
        }

        if (character.HasAnyFeature(EldritchVersatility.PowerEldritchVersatilityPointPool))
        {
            pools.Add(PortraitPointEldritchVersatility.Instance);
        }

        foreach (var provider in pools)
        {
            CustomPortraitPointPool.Setup(provider, character, poolPrefab, layout);
        }

        var concentrations = character.GetSubFeaturesByType<ICustomConcentrationProvider>();

        foreach (var provider in concentrations)
        {
            CustomConcentrationControl.Setup(provider, character, concentrationPrefab, layout);
        }
    }
}
