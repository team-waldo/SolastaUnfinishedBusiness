﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using SolastaUnfinishedBusiness.Properties;
using static RuleDefinitions;
using static SolastaUnfinishedBusiness.Api.DatabaseHelper;

namespace SolastaUnfinishedBusiness.Subclasses;

[UsedImplicitly]
public sealed class PathOfTheYeoman : AbstractSubclass
{
    internal const string Name = "PathOfTheYeoman";

    public PathOfTheYeoman()
    {
        // LEVEL 03

        // Fletcher

        var proficiencyFletcher = FeatureDefinitionProficiencyBuilder
            .Create($"Proficiency{Name}Fletcher")
            .SetGuiPresentation(Category.Feature)
            // don't use ToolDefinitions.ArtisanToolType as that constant has an incorrect name
            .SetProficiencies(ProficiencyType.ToolOrExpertise, "ArtisanToolSmithToolsType")
            .AddToDB();

        // Strong Bow

        var additionalDamageStrongBow = FeatureDefinitionAdditionalDamageBuilder
            .Create($"AdditionalDamage{Name}StrongBow")
            .SetGuiPresentationNoContent(true)
            .SetNotificationTag("Rage")
            .SetAdvancement(AdditionalDamageAdvancement.ClassLevel)
            .SetDamageValueDetermination(AdditionalDamageValueDetermination.RageDamage)
            .AddToDB();

        var featureStrongBow = FeatureDefinitionBuilder
            .Create($"Feature{Name}StrongBow")
            .SetGuiPresentation(Category.Feature)
            .SetCustomSubFeatures(
                new CanUseAttribute(AttributeDefinitions.Strength, IsLongBow),
                new CustomAdditionalDamageStrongBow(additionalDamageStrongBow))
            .AddToDB();

        // LEVEL 06

        // Staggering Blow

        var actionAffinityStaggeringBlow = FeatureDefinitionActionAffinityBuilder
            .Create($"ActionAffinity{Name}StaggeringBlow")
            .SetGuiPresentation(Category.Feature)
            .SetAllowedActionTypes()
            .SetAuthorizedActions(ActionDefinitions.Id.ShoveBonus)
            .SetCustomSubFeatures(
                new ValidatorsDefinitionApplication(
                    ValidatorsCharacter.HasLongbow,
                    ValidatorsCharacter.DoesNotHaveHeavyArmor,
                    ValidatorsCharacter.HasAnyOfConditions(ConditionRaging)))
            .AddToDB();

        // Keen Eye

        var additionalDamageKeenEye = FeatureDefinitionAdditionalDamageBuilder
            .Create($"AdditionalDamage{Name}KeenEye")
            .SetGuiPresentationNoContent(true)
            .SetNotificationTag("BrutalCritical")
            .SetDamageValueDetermination(AdditionalDamageValueDetermination.BrutalCriticalDice)
            .SetIgnoreCriticalDoubleDice(true)
            .AddToDB();

        var featureKeenEye = FeatureDefinitionBuilder
            .Create($"Feature{Name}KeenEye")
            .SetGuiPresentation(Category.Feature)
            .SetCustomSubFeatures(new CustomAdditionalDamageKeenEye(additionalDamageKeenEye))
            .AddToDB();

        // LEVEL 10

        // Bulwark

        var movementAffinityBulwark = FeatureDefinitionMovementAffinityBuilder
            .Create($"MovementAffinity{Name}Bulwark")
            .SetGuiPresentation(Category.Feature)
            .SetBaseSpeedMultiplicativeModifier(0)
            .AddToDB();

        var featureBulwark = FeatureDefinitionBuilder
            .Create($"Feature{Name}Bulwark")
            .SetGuiPresentation(Category.Feature)
            .SetCustomSubFeatures(
                new RangedAttackInMeleeDisadvantageRemover(IsLongBow),
                new CanMakeAoOOnReachEntered { WeaponValidator = IsLongBow, AllowRange = true })
            .AddToDB();

        var combatAffinityBulwark = FeatureDefinitionCombatAffinityBuilder
            .Create($"CombatAffinity{Name}Bulwark")
            .SetGuiPresentation(Category.Feature)
            .SetSituationalContext(SituationalContext.AttackerAwayFromTarget)
            .SetAttackOnMeAdvantage(AdvantageType.Disadvantage)
            .AddToDB();

        var conditionBulwark = ConditionDefinitionBuilder
            .Create($"Condition{Name}Bulwark")
            .SetGuiPresentation(Category.Condition, ConditionDefinitions.ConditionEncumbered)
            .SetPossessive()
            .AddFeatures(
                movementAffinityBulwark,
                featureBulwark,
                combatAffinityBulwark)
            .SetSpecialInterruptions(ConditionInterruption.BattleEnd)
            .AddToDB();

        var powerBulwark = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}Bulwark")
            .SetGuiPresentation(Category.Feature, Sprites.GetSprite("Bulwark", Resources.PowerBulwark, 256, 128))
            .SetUsesAbilityBonus(ActivationTime.BonusAction, RechargeRate.LongRest, AttributeDefinitions.Constitution)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Ally, RangeType.Self, 0, TargetType.Self)
                    .SetDurationData(DurationType.Minute, 1)
                    .SetParticleEffectParameters(FeatureDefinitionPowers.PowerTraditionCourtMageSpellShield)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .SetConditionForm(conditionBulwark, ConditionForm.ConditionOperation.Add, true)
                            .Build())
                    .Build())
            .AddToDB();

        var powerBulwarkTurnOff = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}BulwarkTurnOff")
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
                            .SetConditionForm(conditionBulwark, ConditionForm.ConditionOperation.Remove)
                            .Build())
                    .Build())
            .AddToDB();

        movementAffinityBulwark.SetCustomSubFeatures(new StopPowerConcentrationProvider(
            "Bulwark",
            "Tooltip/&BulwarkConcentration",
            Sprites.GetSprite("DeadeyeConcentrationIcon", Resources.DeadeyeConcentrationIcon, 64, 64))
        {
            StopPower = powerBulwarkTurnOff
        });

        Global.PowersThatIgnoreInterruptions.Add(powerBulwark);
        Global.PowersThatIgnoreInterruptions.Add(powerBulwarkTurnOff);

        // LEVEL 14

        // Mighty Shot

        var powerMightyShot = FeatureDefinitionPowerBuilder
            .Create($"Power{Name}MightyShot")
            .SetGuiPresentation($"Feature{Name}MightyShot", Category.Feature)
            .SetUsesFixed(ActivationTime.NoCost)
            .SetEffectDescription(
                EffectDescriptionBuilder
                    .Create()
                    .SetTargetingData(Side.Enemy, RangeType.Touch, 0, TargetType.IndividualsUnique)
                    .SetSavingThrowData(false, AttributeDefinitions.Dexterity, true,
                        EffectDifficultyClassComputation.AbilityScoreAndProficiency, AttributeDefinitions.Strength, 8)
                    .SetEffectForms(
                        EffectFormBuilder
                            .Create()
                            .HasSavingThrow(EffectSavingThrowType.Negates)
                            .SetDamageForm(DamageTypeThunder)
                            .Build())
                    .SetParticleEffectParameters(SpellDefinitions.CallLightning)
                    .Build())
            .AddToDB();

        powerMightyShot.EffectDescription.EffectParticleParameters.impactParticleReference =
            powerMightyShot.EffectDescription.EffectParticleParameters.effectParticleReference;

        powerMightyShot.SetCustomSubFeatures(
            PowerVisibilityModifier.Hidden,
            new UpgradeWeaponDice((_, damage) => (damage.diceNumber, DieType.D12, DieType.D12), IsLongBow),
            new PhysicalAttackFinishedByMeMightyShot(powerMightyShot));

        // MAIN

        Subclass = CharacterSubclassDefinitionBuilder
            .Create(Name)
            .SetGuiPresentation(Category.Subclass, Sprites.GetSprite(Name, Resources.PathOfTheYeoman, 256))
            .AddFeaturesAtLevel(3, proficiencyFletcher, featureStrongBow)
            .AddFeaturesAtLevel(6, actionAffinityStaggeringBlow, featureKeenEye)
            .AddFeaturesAtLevel(10, powerBulwark)
            .AddFeaturesAtLevel(14, powerMightyShot)
            .AddToDB();
    }

    internal override CharacterClassDefinition Klass => CharacterClassDefinitions.Barbarian;

    private static IsWeaponValidHandler IsLongBow => ValidatorsWeapon.IsOfWeaponType(WeaponTypeDefinitions.LongbowType);

    internal override CharacterSubclassDefinition Subclass { get; }

    internal override FeatureDefinitionSubclassChoice SubclassChoice =>
        FeatureDefinitionSubclassChoices.SubclassChoiceBarbarianPrimalPath;

    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal override DeityDefinition DeityDefinition { get; }

    //
    // Strong Bow
    //

    private sealed class CustomAdditionalDamageStrongBow : CustomAdditionalDamage
    {
        public CustomAdditionalDamageStrongBow(IAdditionalDamageProvider provider) : base(provider)
        {
        }

        internal override bool IsValid(
            GameLocationBattleManager battleManager,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier attackModifier,
            RulesetAttackMode attackMode,
            bool rangedAttack,
            AdvantageType advantageType,
            List<EffectForm> actualEffectForms,
            RulesetEffect rulesetEffect,
            bool criticalHit,
            bool firstTarget,
            out CharacterActionParams reactionParams)
        {
            reactionParams = null;

            var rulesetAttacker = attacker.RulesetCharacter;

            return rulesetAttacker is { IsDeadOrDyingOrUnconscious: false } &&
                   rulesetAttacker.HasConditionOfType(ConditionRaging) &&
                   !rulesetAttacker.IsWearingHeavyArmor() &&
                   IsLongBow(attackMode, null, null);
        }
    }

    //
    // Keen Eye
    //

    private sealed class CustomAdditionalDamageKeenEye : CustomAdditionalDamage
    {
        public CustomAdditionalDamageKeenEye(IAdditionalDamageProvider provider) : base(provider)
        {
        }

        internal override bool IsValid(
            GameLocationBattleManager battleManager,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier attackModifier,
            RulesetAttackMode attackMode,
            bool rangedAttack,
            AdvantageType advantageType,
            List<EffectForm> actualEffectForms,
            RulesetEffect rulesetEffect,
            bool criticalHit,
            bool firstTarget,
            out CharacterActionParams reactionParams)
        {
            reactionParams = null;

            return criticalHit && IsLongBow(attackMode, null, null);
        }
    }

    //
    // Mighty Shot
    //

    private sealed class PhysicalAttackFinishedByMeMightyShot : IPhysicalAttackFinishedByMe, IModifyEffectDescription
    {
        private readonly FeatureDefinitionPower _powerMightyShot;

        public PhysicalAttackFinishedByMeMightyShot(FeatureDefinitionPower powerMightyShot)
        {
            _powerMightyShot = powerMightyShot;
        }

        public bool IsValid(BaseDefinition definition, RulesetCharacter character, EffectDescription effectDescription)
        {
            return definition == _powerMightyShot;
        }

        public EffectDescription GetEffectDescription(
            BaseDefinition definition,
            EffectDescription effectDescription,
            RulesetCharacter character,
            RulesetEffect rulesetEffect)
        {
            var damageForm = effectDescription.FindFirstDamageForm();

            if (damageForm == null)
            {
                return effectDescription;
            }

            var levels = character.GetClassLevel(CharacterClassDefinitions.Barbarian);
            var rageBonus = levels switch
            {
                >= 16 => 4,
                >= 9 => 3,
                _ => 2
            };

            damageForm.BonusDamage = AttributeDefinitions
                                         .ComputeAbilityScoreModifier(
                                             character.TryGetAttributeValue(AttributeDefinitions.Strength))
                                     + rageBonus;

            return effectDescription;
        }

        public IEnumerator OnAttackFinishedByMe(
            GameLocationBattleManager battleManager,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackerAttackMode,
            RollOutcome attackRollOutcome,
            int damageAmount)
        {
            if (attackRollOutcome is RollOutcome.Failure or RollOutcome.CriticalFailure)
            {
                yield break;
            }

            var rulesetAttacker = attacker.RulesetCharacter;

            if (rulesetAttacker is not { IsDeadOrDyingOrUnconscious: false } ||
                !rulesetAttacker.HasConditionOfType(ConditionRaging) ||
                rulesetAttacker.IsWearingHeavyArmor() ||
                !IsLongBow(attackerAttackMode, null, null))
            {
                yield break;
            }

            var actionParams = action.ActionParams.Clone();
            var usablePower = UsablePowersProvider.Get(_powerMightyShot, rulesetAttacker);

            actionParams.ActionDefinition = DatabaseHelper.ActionDefinitions.SpendPower;
            actionParams.RulesetEffect = ServiceRepository.GetService<IRulesetImplementationService>()
                .InstantiateEffectPower(rulesetAttacker, usablePower, false)
                .AddAsActivePowerToSource();
            actionParams.TargetCharacters.SetRange(battleManager.Battle.EnemyContenders
                .Where(x =>
                    x != defender
                    && battleManager.IsWithinXCells(defender, x, 3))
                .ToList());

            action.ResultingActions.Add(new CharacterActionSpendPower(actionParams));
        }
    }
}
