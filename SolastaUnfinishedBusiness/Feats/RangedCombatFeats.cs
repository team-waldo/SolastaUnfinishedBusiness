﻿using System.Collections.Generic;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Builders;
using SolastaUnfinishedBusiness.Builders.Features;
using SolastaUnfinishedBusiness.CustomBehaviors;
using SolastaUnfinishedBusiness.CustomInterfaces;
using SolastaUnfinishedBusiness.CustomUI;
using SolastaUnfinishedBusiness.CustomValidators;
using SolastaUnfinishedBusiness.Models;
using SolastaUnfinishedBusiness.Properties;
using SolastaUnfinishedBusiness.Subclasses;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.WeaponTypeDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper.FeatureDefinitionMovementAffinitys;

namespace SolastaUnfinishedBusiness.Feats;

internal static class RangedCombatFeats
{
    internal static void CreateFeats([NotNull] List<FeatDefinition> feats)
    {
        var featBowMastery = BuildBowMastery();
        var featCrossbowMastery = BuildCrossbowMastery();
        var featDeadEye = BuildDeadEye();
        var featRangedExpert = BuildRangedExpert();
        var featSteadyAim = BuildSteadyAim();

        feats.AddRange(featBowMastery, featCrossbowMastery, featDeadEye, featRangedExpert, featSteadyAim);

        GroupFeats.FeatGroupRangedCombat.AddFeats(
            featBowMastery,
            featCrossbowMastery,
            featDeadEye,
            featRangedExpert,
            featSteadyAim);
    }

    private static FeatDefinition BuildBowMastery()
    {
        const string NAME = "FeatBowMastery";

        var isLongOrShortbow = ValidatorsWeapon.IsOfWeaponType(LongbowType, ShortbowType);

        return FeatDefinitionBuilder
            .Create(NAME)
            .SetGuiPresentation(Category.Feat)
            .SetFeatures(
                FeatureDefinitionAttackModifierBuilder
                    .Create($"Custom{NAME}")
                    .SetGuiPresentation(NAME, Category.Feat)
                    .SetDamageRollModifier(1)
                    .SetCustomSubFeatures(
                        new ValidateContextInsteadOfRestrictedProperty((_, _, character, _, _, mode, _) =>
                            (OperationType.Set, isLongOrShortbow(mode, null, character))),
                        new CanUseAttribute(
                            AttributeDefinitions.Strength,
                            ValidatorsWeapon.IsOfWeaponType(LongbowType)),
                        new AddExtraRangedAttack(
                            ActionDefinitions.ActionType.Bonus,
                            ValidatorsWeapon.IsOfWeaponType(ShortbowType),
                            ValidatorsCharacter.HasUsedWeaponType(ShortbowType)))
                    .AddToDB())
            .AddToDB();
    }

    private static FeatDefinition BuildCrossbowMastery()
    {
        const string NAME = "FeatCrossbowMastery";

        var isCrossbow = ValidatorsWeapon.IsOfWeaponType(
            HeavyCrossbowType, LightCrossbowType, CustomWeaponsContext.HandXbowWeaponType);

        return FeatDefinitionBuilder
            .Create(NAME)
            .SetGuiPresentation(Category.Feat)
            .SetFeatures(
                FeatureDefinitionAttackModifierBuilder
                    .Create($"Custom{NAME}")
                    .SetGuiPresentation(NAME, Category.Feat)
                    .SetDamageRollModifier(1)
                    .SetCustomSubFeatures(
                        new ValidateContextInsteadOfRestrictedProperty((_, _, character, _, _, mode, _) =>
                            (OperationType.Set, isCrossbow(mode, null, character))),
                        new CanUseAttribute(
                            AttributeDefinitions.Strength,
                            ValidatorsWeapon.IsOfWeaponType(HeavyCrossbowType)),
                        new AddExtraRangedAttack(
                            ActionDefinitions.ActionType.Bonus,
                            ValidatorsWeapon.IsOfWeaponType(LightCrossbowType),
                            ValidatorsCharacter.HasUsedWeaponType(LightCrossbowType)),
                        new AddExtraRangedAttack(
                            ActionDefinitions.ActionType.Bonus,
                            ValidatorsWeapon.IsOfWeaponType(CustomWeaponsContext.HandXbowWeaponType),
                            ValidatorsCharacter.HasUsedWeaponType(CustomWeaponsContext.HandXbowWeaponType)))
                    .AddToDB())
            .AddToDB();
    }

    private static FeatDefinition BuildDeadEye()
    {
        const string Name = "FeatDeadeye";

        var concentrationProvider = new StopPowerConcentrationProvider(
            "Deadeye",
            "Tooltip/&DeadeyeConcentration",
            Sprites.GetSprite("DeadeyeConcentrationIcon", Resources.DeadeyeConcentrationIcon, 64, 64));

        var modifyAttackModeForWeapon = FeatureDefinitionBuilder
            .Create($"ModifyAttackModeForWeapon{Name}")
            .SetGuiPresentationNoContent(true)
            .AddToDB();

        var conditionDeadeye = ConditionDefinitionBuilder
            .Create($"Condition{Name}")
            .SetGuiPresentation(Name, Category.Feat,
                DatabaseHelper.ConditionDefinitions.ConditionHeraldOfBattle)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetFeatures(modifyAttackModeForWeapon)
            .AddToDB();

        var powerDeadeye = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}")
            .SetGuiPresentation(Name, Category.Feat,
                Sprites.GetSprite("DeadeyeIcon", Resources.DeadeyeIcon, 128, 64))
            .SetUsesFixed(ActivationTime.NoCost)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .SetDurationData(DurationType.Permanent)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionDeadeye, ConditionForm.ConditionOperation.Add)
                            .Build())
                    .Build())
            .SetCustomSubFeatures(
                new ValidatorsPowerUse(ValidatorsCharacter.HasNoneOfConditions(conditionDeadeye.Name)))
            .AddToDB();

        Global.PowersThatIgnoreInterruptions.Add(powerDeadeye);

        var powerTurnOffDeadeye = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}TurnOff")
            .SetGuiPresentationNoContent(true)
            .SetUsesFixed(ActivationTime.NoCost)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .SetDurationData(DurationType.Round, 1)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionDeadeye, ConditionForm.ConditionOperation.Remove)
                            .Build())
                    .Build())
            .AddToDB();

        Global.PowersThatIgnoreInterruptions.Add(powerTurnOffDeadeye);

        var featDeadeye = FeatDefinitionBuilder
            .Create(Name)
            .SetGuiPresentation(Category.Feat)
            .SetFeatures(
                powerDeadeye,
                powerTurnOffDeadeye,
                FeatureDefinitionCombatAffinityBuilder
                    .Create($"CombatAffinity{Name}")
                    .SetGuiPresentation(Name, Category.Feat)
                    .SetIgnoreCover()
                    .SetCustomSubFeatures(new BumpWeaponWeaponAttackRangeToMax(ValidatorsWeapon.AlwaysValid))
                    .AddToDB())
            .AddToDB();

        concentrationProvider.StopPower = powerTurnOffDeadeye;
        modifyAttackModeForWeapon
            .SetCustomSubFeatures(
                concentrationProvider,
                new ModifyWeaponAttackModeFeatDeadeye(featDeadeye));

        return featDeadeye;
    }

    private static FeatDefinition BuildRangedExpert()
    {
        const string NAME = "FeatRangedExpert";

        return FeatDefinitionBuilder
            .Create(NAME)
            .SetGuiPresentation(Category.Feat)
            .SetFeatures(
                FeatureDefinitionAttackModifierBuilder
                    .Create(DatabaseHelper.FeatureDefinitionAttackModifiers.AttackModifierFightingStyleTwoWeapon,
                        $"AttackModifier{NAME}")
                    .SetGuiPresentationNoContent(true)
                    .SetCustomSubFeatures(
                        ValidatorsCharacter.HasOffhandWeaponType(
                            CustomWeaponsContext.HandXbowWeaponType, CustomWeaponsContext.LightningLauncherType))
                    .AddToDB(),
                FeatureDefinitionBuilder
                    .Create($"Feature{NAME}")
                    .SetGuiPresentationNoContent(true)
                    .SetCustomSubFeatures(
                        new RangedAttackInMeleeDisadvantageRemover(),
                        new InnovationArmor.AddLauncherAttack(ActionDefinitions.ActionType.Bonus,
                            InnovationArmor.InInfiltratorMode,
                            ValidatorsCharacter.HasAttacked),
                        new AddExtraRangedAttack(
                            ActionDefinitions.ActionType.Bonus,
                            ValidatorsWeapon.IsOfWeaponType(CustomWeaponsContext.HandXbowWeaponType),
                            ValidatorsCharacter.HasAttacked))
                    .AddToDB())
            .AddToDB();
    }

    private static FeatDefinition BuildSteadyAim()
    {
        const string NAME = "FeatSteadyAim";

        var combatAffinity = FeatureDefinitionCombatAffinityBuilder
            .Create($"CombatAffinity{NAME}")
            .SetGuiPresentation(NAME, Category.Feat)
            .SetMyAttackAdvantage(AdvantageType.Advantage)
            .AddToDB();

        var conditionAdvantage = ConditionDefinitionBuilder
            .Create($"Condition{NAME}Advantage")
            .SetGuiPresentation(Category.Condition)
            .SetPossessive()
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialInterruptions(ConditionInterruption.Attacks, ConditionInterruption.AnyBattleTurnEnd)
            .AddFeatures(combatAffinity)
            .AddToDB();

        var conditionRestrained = ConditionDefinitionBuilder
            .Create($"Condition{NAME}Restrained")
            .SetGuiPresentation(Category.Condition)
            .SetSilent(Silent.WhenAddedOrRemoved)
            .SetSpecialInterruptions(ConditionInterruption.AnyBattleTurnEnd)
            .AddFeatures(MovementAffinityConditionRestrained)
            .AddToDB();

        return FeatDefinitionBuilder
            .Create(NAME)
            .SetGuiPresentation(Category.Feat)
            .SetFeatures(
                DatabaseHelper.FeatureDefinitionAttributeModifiers.AttributeModifierCreed_Of_Misaye,
                FeatureDefinitionPowerBuilder
                    .Create($"Power{NAME}")
                    .SetGuiPresentation(NAME, Category.Feat,
                        Sprites.GetSprite("PowerSteadyAim", Resources.PowerSteadyAim, 256, 128))
                    .SetUsesFixed(ActivationTime.BonusAction)
                    .SetEffectDescription(
                        EffectDescriptionBuilder
                            .Create()
                            .SetDurationData(DurationType.Round, 0, TurnOccurenceType.StartOfTurn)
                            .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                            .SetEffectForms(
                                EffectFormBuilder
                                    .Create()
                                    .SetConditionForm(conditionAdvantage, ConditionForm.ConditionOperation.Add)
                                    .Build(),
                                EffectFormBuilder
                                    .Create()
                                    .SetConditionForm(conditionRestrained, ConditionForm.ConditionOperation.Add)
                                    .Build())
                            .Build())
                    .SetCustomSubFeatures(
                        new ValidatorsPowerUse(character =>
                        {
                            var gameLocationCharacter = GameLocationCharacter.GetFromActor(character);

                            return gameLocationCharacter == null || gameLocationCharacter.UsedTacticalMoves == 0;
                        }))
                    .AddToDB())
            .AddToDB();
    }

    //
    // HELPERS
    //

    private sealed class ModifyWeaponAttackModeFeatDeadeye : IModifyWeaponAttackMode
    {
        private readonly FeatDefinition _featDefinition;

        public ModifyWeaponAttackModeFeatDeadeye(FeatDefinition featDefinition)
        {
            _featDefinition = featDefinition;
        }

        public void ModifyAttackMode(RulesetCharacter character, [CanBeNull] RulesetAttackMode attackMode)
        {
            if (!ValidatorsWeapon.IsOfWeaponType(DartType)(attackMode, null, null) &&
                attackMode is not { ranged: true })
            {
                return;
            }

            var damage = attackMode?.EffectDescription.FindFirstDamageForm();

            if (damage == null)
            {
                return;
            }

            const int TO_HIT = -5;
            const int TO_DAMAGE = +10;

            attackMode.ToHitBonus += TO_HIT;
            attackMode.ToHitBonusTrends.Add(new TrendInfo(TO_HIT, FeatureSourceType.Feat, _featDefinition.Name,
                _featDefinition));

            damage.BonusDamage += TO_DAMAGE;
            damage.DamageBonusTrends.Add(new TrendInfo(TO_DAMAGE, FeatureSourceType.Feat, _featDefinition.Name,
                _featDefinition));
        }
    }
}
