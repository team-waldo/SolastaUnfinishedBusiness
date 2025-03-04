﻿using System.Collections.Generic;
using System.Linq;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;

namespace SolastaUnfinishedBusiness.CustomBehaviors;

public class AddPBToSummonCheck
{
    private readonly string[] _abilities;
    private readonly int _multiplier;

    public AddPBToSummonCheck(int multiplier, params string[] abilities)
    {
        _multiplier = multiplier;
        _abilities = abilities;
    }

    private int Modifier(string ability)
    {
        return _abilities.Contains(ability) ? _multiplier : 0;
    }

    public static void ModifyCheckBonus<T>(
        RulesetCharacterMonster monster,
        ref int result,
        string proficiency,
        List<RuleDefinitions.TrendInfo> trends) where T : class
    {
        var features = monster.FeaturesToBrowse;

        monster.EnumerateFeaturesToBrowse<T>(features);

        var mods = features.SelectMany(f => f.GetAllSubFeaturesOfType<AddPBToSummonCheck>()).ToList();

        if (mods.Empty())
        {
            return;
        }

        var mult = mods.Max(m => m.Modifier(proficiency));

        if (mult == 0)
        {
            return;
        }

        var summoner = EffectHelpers.GetSummoner(monster);

        if (summoner == null)
        {
            return;
        }

        var pb = summoner.TryGetAttributeValue(AttributeDefinitions.ProficiencyBonus);

        if (pb == 0)
        {
            return;
        }

        pb *= mult;

        if (trends != null)
        {
            trends.Clear();

            var info = new RuleDefinitions.TrendInfo(
                result, RuleDefinitions.FeatureSourceType.Base, string.Empty, null);

            trends.Add(info);

            info = new RuleDefinitions.TrendInfo(
                pb, RuleDefinitions.FeatureSourceType.Proficiency, string.Empty, null);

            trends.Add(info);
        }

        result += pb;
    }
}
