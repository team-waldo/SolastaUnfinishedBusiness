﻿using System.Linq;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomInterfaces;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.Properties;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.CharacterClassDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.ConditionDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionDamageAffinitys;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionSavingThrowAffinitys;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.SpellDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.SpellListDefinitions;
using static SolastaUnfinishedBusiness.Builders.Features.AutoPreparedSpellsGroupBuilder;

namespace SolastaUnfinishedBusiness.Subclasses;

[UsedImplicitly]
public sealed class CircleOfTheLife : AbstractSubclass
{
    private const string Name = "CircleOfTheLife";
    private const string ConditionRevitalizingBoon = $"Condition{Name}RevitalizingBoon";
    private const string ConditionSeedOfLife = $"Condition{Name}SeedOfLife";
    private const string ConditionVerdancy = $"Condition{Name}Verdancy";
    private const string ConditionVerdancy14 = $"Condition{Name}Verdancy14";

    private static readonly FeatureDefinitionMagicAffinity MagicAffinityHarmoniousBloom =
        FeatureDefinitionMagicAffinityBuilder
            .Create($"MagicAffinity{Name}HarmoniousBloom")
            .SetGuiPresentation(Category.Feature)
            .SetWarList(1) // spells are added on late load to contemplate mod spells
            .AddToDB();

    public CircleOfTheLife()
    {
        var autoPreparedSpells = FeatureDefinitionAutoPreparedSpellsBuilder
            .Create($"AutoPreparedSpells{Name}")
            .SetGuiPresentation("ExpandedSpells", Category.Feature)
            .SetAutoTag("Circle")
            .SetPreparedSpellGroups(
                BuildSpellGroup(2, CureWounds, Goodberry),
                BuildSpellGroup(3, LesserRestoration, PrayerOfHealing),
                BuildSpellGroup(5, BeaconOfHope, MassHealingWord),
                BuildSpellGroup(7, FreedomOfMovement, Stoneskin),
                BuildSpellGroup(9, GreaterRestoration, MassCureWounds))
            .SetSpellcastingClass(Druid)
            .AddToDB();

        // Verdancy

        var featureVerdancyTarget = FeatureDefinitionBuilder
            .Create($"Feature{Name}VerdancyTarget")
            .SetGuiPresentationNoContent(true)
            .SetCustomSubFeatures(new CharacterTurnStartListenerVerdancy())
            .AddToDB();

        var conditionVerdancy = ConditionDefinitionBuilder
            .Create(ConditionVerdancy)
            .SetGuiPresentation(Category.Condition, ConditionChildOfDarkness_DimLight)
            // uses 2 but it will trigger 3 times as required because of the time we add it
            .SetSpecialDuration(DurationType.Round, 2, TurnOccurenceType.EndOfSourceTurn)
            .SetPossessive()
            .CopyParticleReferences(ConditionAided)
            .AllowMultipleInstances()
            .SetCustomSubFeatures(new OnConditionAddedOrRemovedVerdancy())
            .AddFeatures(featureVerdancyTarget)
            .AddToDB();

        var conditionVerdancy14 = ConditionDefinitionBuilder
            .Create(conditionVerdancy, ConditionVerdancy14)
            // uses 4 but it will trigger 5 times as required because of the time we add it
            .SetSpecialDuration(DurationType.Round, 4, TurnOccurenceType.EndOfSourceTurn)
            .SetCustomSubFeatures(new OnConditionAddedOrRemovedVerdancy())
            .AddToDB();

        var featureVerdancy = FeatureDefinitionBuilder
            .Create($"Feature{Name}Verdancy")
            .SetGuiPresentation(Category.Feature)
            .SetCustomSubFeatures(new ModifyEffectDescriptionVerdancy(conditionVerdancy, conditionVerdancy14))
            .AddToDB();

        // Seed of Life

        var featureSeedOfLife = FeatureDefinitionBuilder
            .Create($"Feature{Name}SeedOfLife")
            .SetGuiPresentationNoContent(true)
            .SetCustomSubFeatures(new CharacterTurnStartListenerSeedOfLife())
            .AddToDB();

        var conditionSeedOfLife = ConditionDefinitionBuilder
            .Create(ConditionSeedOfLife)
            .SetGuiPresentation(Category.Condition, ConditionBlessed)
            .SetPossessive()
            .CopyParticleReferences(ConditionGuided)
            .AddFeatures(featureSeedOfLife)
            .AddToDB();

        conditionSeedOfLife.SetCustomSubFeatures(new OnConditionAddedOrRemovedSeedOfLife());

        var powerSeedOfLife = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}SeedOfLife")
            .SetGuiPresentation(Category.Feature, CureWounds)
            .SetUsesFixed(ActivationTime.BonusAction, RechargeRate.LongRest, 1, 2)
            .SetShowCasting(true)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetDurationData(DurationType.Minute, 1, TurnOccurenceType.EndOfSourceTurn)
                    .SetTargetingData(Side.Ally, RangeType.Distance, 6, TargetType.IndividualsUnique)
                    .SetParticleEffectParameters(HealingWord)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionSeedOfLife, ConditionForm.ConditionOperation.Add)
                            .Build())
                    .Build())
            .AddToDB();

        // Revitalizing Boon

        var conditionRevitalizingBoom = ConditionDefinitionBuilder
            .Create(ConditionRevitalizingBoon)
            .SetGuiPresentation(Category.Condition, ConditionBrandingSmite)
            .SetSpecialDuration(DurationType.Dispelled)
            .SetPossessive()
            .CopyParticleReferences(ConditionAided)
            .SetFeatures(DamageAffinityNecroticResistance, SavingThrowAffinityDwarvenPlate)
            .AddToDB();

        var featureRevitalizingBoom = FeatureDefinitionBuilder
            .Create($"Feature{Name}RevitalizingBoon")
            .SetGuiPresentation(Category.Feature)
            .SetCustomSubFeatures(
                new ModifyEffectDescriptionRevitalizingBoon(conditionRevitalizingBoom, powerSeedOfLife))
            .AddToDB();

        // MAIN

        Subclass = CharacterSubclassDefinitionBuilder
            .Create(Name)
            .SetGuiPresentation(Category.Subclass, Sprites.GetSprite(Name, Resources.CircleOfTheLife, 256))
            .AddFeaturesAtLevel(2, autoPreparedSpells, featureVerdancy)
            .AddFeaturesAtLevel(6, powerSeedOfLife)
            .AddFeaturesAtLevel(10, featureRevitalizingBoom)
            .AddFeaturesAtLevel(14, MagicAffinityHarmoniousBloom)
            .AddToDB();
    }

    internal override CharacterClassDefinition Klass => Druid;

    internal override CharacterSubclassDefinition Subclass { get; }

    internal override FeatureDefinitionSubclassChoice SubclassChoice =>
        DatabaseHelper.FeatureDefinitionSubclassChoices.SubclassChoiceDruidCircle;

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal override DeityDefinition DeityDefinition { get; }

    internal static void LateLoad()
    {
        foreach (var spellDefinition in SpellListAllSpells
                     .SpellsByLevel
                     .SelectMany(x => x.Spells)
                     .Where(x => x.EffectDescription.EffectForms
                         .Any(y => y.FormType == EffectForm.EffectFormType.Healing)))
        {
            if (spellDefinition.SpellsBundle)
            {
                foreach (var spellInBundle in spellDefinition.SubspellsList)
                {
                    MagicAffinityHarmoniousBloom.WarListSpells.Add(spellInBundle.Name);
                }
            }
            else
            {
                MagicAffinityHarmoniousBloom.WarListSpells.Add(spellDefinition.Name);
            }
        }
    }

    private static int GetDruidLevel(ulong guid)
    {
        var caster = EffectHelpers.GetCharacterByGuid(guid);
        var hero = caster.GetOriginalHero();

        return hero?.GetClassLevel(DruidClass) ?? 0;
    }

    private static bool IsAuthorizedSpell(EffectDescription effectDescription, BaseDefinition baseDefinition)
    {
        if (baseDefinition is not SpellDefinition spellDefinition)
        {
            return false;
        }

        var hasHealingForm =
            effectDescription.EffectForms.Any(x => x.FormType == EffectForm.EffectFormType.Healing);

        return hasHealingForm || spellDefinition == LesserRestoration || spellDefinition == GreaterRestoration;
    }

    private static void RemoveRevitalizingBoonIfRequired(RulesetActor removedFrom)
    {
        var hasVerdancy =
            removedFrom.HasAnyConditionOfType(ConditionSeedOfLife, ConditionVerdancy, ConditionVerdancy14);

        if (!hasVerdancy)
        {
            removedFrom.RemoveAllConditionsOfCategoryAndType(
                AttributeDefinitions.TagEffect, ConditionRevitalizingBoon);
        }
    }

    private sealed class CharacterTurnStartListenerVerdancy : ICharacterTurnStartListener
    {
        public void OnCharacterTurnStarted(GameLocationCharacter locationCharacter)
        {
            var rulesetCharacter = locationCharacter.RulesetCharacter;

            if (rulesetCharacter is not { IsDeadOrDyingOrUnconscious: false })
            {
                return;
            }

            foreach (var rulesetCondition in rulesetCharacter.AllConditions
                         .Where(x => x.ConditionDefinition.Name is ConditionVerdancy or ConditionVerdancy14)
                         .ToList())
            {
                var caster = EffectHelpers.GetCharacterByGuid(rulesetCondition.SourceGuid);

                if (caster is not { IsDeadOrDyingOrUnconscious: false })
                {
                    continue;
                }

                var levels = caster.GetClassLevel(Druid);
                var bonus = rulesetCondition.EffectLevel;

                rulesetCharacter.ReceiveHealing(levels + bonus, true, caster.Guid);
            }
        }
    }

    private sealed class OnConditionAddedOrRemovedVerdancy : IOnConditionAddedOrRemoved
    {
        public void OnConditionAdded(RulesetCharacter target, RulesetCondition rulesetCondition)
        {
            // empty
        }

        public void OnConditionRemoved(RulesetCharacter target, RulesetCondition rulesetCondition)
        {
            RemoveRevitalizingBoonIfRequired(target);
        }
    }

    private sealed class ModifyEffectDescriptionVerdancy : IModifyEffectDescription
    {
        private readonly ConditionDefinition _conditionVerdancy;
        private readonly ConditionDefinition _conditionVerdancy14;

        public ModifyEffectDescriptionVerdancy(
            ConditionDefinition conditionVerdancy,
            ConditionDefinition conditionVerdancy14)
        {
            _conditionVerdancy = conditionVerdancy;
            _conditionVerdancy14 = conditionVerdancy14;
        }

        public bool IsValid(
            BaseDefinition definition,
            RulesetCharacter character,
            EffectDescription effectDescription)
        {
            return IsAuthorizedSpell(effectDescription, definition);
        }

        public EffectDescription GetEffectDescription(
            BaseDefinition definition,
            EffectDescription effectDescription,
            RulesetCharacter character,
            RulesetEffect rulesetEffect)
        {
            var levels = character.GetClassLevel(Druid);
            var condition = levels >= 14 ? _conditionVerdancy14 : _conditionVerdancy;

            effectDescription.EffectForms.Add(
                EffectFormBuilder
                    .Create()
                    .SetConditionForm(condition, ConditionForm.ConditionOperation.Add)
                    .Build());

            return effectDescription;
        }
    }

    private sealed class CharacterTurnStartListenerSeedOfLife : ICharacterTurnStartListener
    {
        public void OnCharacterTurnStarted(GameLocationCharacter locationCharacter)
        {
            var rulesetCharacter = locationCharacter.RulesetCharacter;

            if (rulesetCharacter is not { IsDeadOrDyingOrUnconscious: false })
            {
                return;
            }

            foreach (var rulesetCondition in rulesetCharacter.AllConditions
                         .Where(x => x.ConditionDefinition.Name == ConditionSeedOfLife)
                         .ToList())
            {
                var caster = EffectHelpers.GetCharacterByGuid(rulesetCondition.SourceGuid);

                if (caster is not { IsDeadOrDyingOrUnconscious: false })
                {
                    continue;
                }

                var pb = caster.TryGetAttributeValue(AttributeDefinitions.ProficiencyBonus);

                rulesetCharacter.ReceiveHealing(pb, true, caster.Guid);
            }
        }
    }

    private sealed class OnConditionAddedOrRemovedSeedOfLife : IOnConditionAddedOrRemoved
    {
        public void OnConditionAdded(RulesetCharacter target, RulesetCondition rulesetCondition)
        {
            // Empty
        }

        public void OnConditionRemoved(RulesetCharacter target, RulesetCondition rulesetCondition)
        {
            RemoveRevitalizingBoonIfRequired(target);

            var druidLevel = GetDruidLevel(rulesetCondition.sourceGuid);

            if (druidLevel > 0 && target.CurrentHitPoints > 0)
            {
                target.ReceiveHealing(druidLevel * 2, true, rulesetCondition.guid);
            }
        }
    }

    private sealed class ModifyEffectDescriptionRevitalizingBoon : IModifyEffectDescription
    {
        private readonly ConditionDefinition _conditionRevitalizingBoon;
        private readonly FeatureDefinitionPower _powerSeedOfLife;

        public ModifyEffectDescriptionRevitalizingBoon(
            ConditionDefinition conditionRevitalizingBoon,
            FeatureDefinitionPower powerSeedOfLife)
        {
            _conditionRevitalizingBoon = conditionRevitalizingBoon;
            _powerSeedOfLife = powerSeedOfLife;
        }

        public bool IsValid(
            BaseDefinition definition,
            RulesetCharacter character,
            EffectDescription effectDescription)
        {
            return definition == _powerSeedOfLife || IsAuthorizedSpell(effectDescription, definition);
        }

        public EffectDescription GetEffectDescription(
            BaseDefinition definition,
            EffectDescription effectDescription,
            RulesetCharacter character,
            RulesetEffect rulesetEffect)
        {
            effectDescription.EffectForms.Add(
                EffectFormBuilder
                    .Create()
                    .SetConditionForm(_conditionRevitalizingBoon, ConditionForm.ConditionOperation.Add)
                    .Build());

            return effectDescription;
        }
    }
}
