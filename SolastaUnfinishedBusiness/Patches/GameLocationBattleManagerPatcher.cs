﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.CustomBehaviors;
using SolastaUnfinishedBusiness.CustomDefinitions;
using SolastaUnfinishedBusiness.CustomInterfaces;
using SolastaUnfinishedBusiness.CustomValidators;
using SolastaUnfinishedBusiness.Models;
using SolastaUnfinishedBusiness.Spells;
using TA;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Patches;

[UsedImplicitly]
public static class GameLocationBattleManagerPatcher
{
    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.CanCharacterUsePower))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class CanCharacterUsePower_Patch
    {
        [UsedImplicitly]
        public static void Postfix(
            ref bool __result,
            RulesetCharacter caster,
            RulesetUsablePower usablePower)
        {
            //PATCH: support for `IPowerUseValidity` when trying to react with power 
            if (!caster.CanUsePower(usablePower.PowerDefinition))
            {
                __result = false;
            }

            //PATCH: support for `IReactionAttackModeRestriction`
            if (__result)
            {
                __result = RestrictReactionAttackMode.CanCharacterReactWithPower(usablePower);
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.CanPerformReadiedActionOnCharacter))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class CanPerformReadiedActionOnCharacter_Patch
    {
        [NotNull]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler([NotNull] IEnumerable<CodeInstruction> instructions)
        {
            //PATCH: Makes only preferred cantrip valid if it is selected and forced
            var customBindMethod =
                new Func<List<SpellDefinition>, SpellDefinition, bool>(CustomReactionsContext.CheckAndModifyCantrips)
                    .Method;

            //PATCH: allows to ready non-standard ranged attacks (like Armorer's Lightning Launcher)
            var customFindMethod =
                new Func<
                        GameLocationCharacter, // character,
                        ActionDefinitions.Id, // actionId,
                        bool, // getWithMostAttackNb,
                        bool, // onlyIfRemainingUses,
                        bool, // onlyIfCanUseAction
                        ActionDefinitions.ReadyActionType, // readyActionType
                        RulesetAttackMode //result
                    >(FindActionAttackMode)
                    .Method;

            return instructions
                .ReplaceCall(
                    "Contains",
                    -1,
                    "GameLocationBattleManager.CanPerformReadiedActionOnCharacter.Contains",
                    new CodeInstruction(OpCodes.Call, customBindMethod))
                .ReplaceCall(
                    "FindActionAttackMode",
                    -1,
                    "GameLocationBattleManager.CanPerformReadiedActionOnCharacter.FindActionAttackMode",
                    new CodeInstruction(OpCodes.Call, customFindMethod)
                );
        }

        private static RulesetAttackMode FindActionAttackMode(
            GameLocationCharacter character,
            ActionDefinitions.Id actionId,
            bool getWithMostAttackNb,
            bool onlyIfRemainingUses,
            bool onlyIfCanUseAction,
            ActionDefinitions.ReadyActionType readyActionType)
        {
            var attackMode = character.FindActionAttackMode(
                actionId, getWithMostAttackNb, onlyIfRemainingUses, onlyIfCanUseAction, readyActionType);

            if (readyActionType != ActionDefinitions.ReadyActionType.Ranged)
            {
                return attackMode;
            }

            if (attackMode != null && (attackMode.Ranged || attackMode.Thrown))
            {
                return attackMode;
            }

            return character.GetFirstRangedModeThatCanBeReadied();
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.IsValidAttackForReadiedAction))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class IsValidAttackForReadiedAction_Patch
    {
        [UsedImplicitly]
        public static void Postfix(ref bool __result, BattleDefinitions.AttackEvaluationParams attackParams)
        {
            //PATCH: Checks if attack cantrip is valid to be cast as readied action on a target
            // Used to properly check if melee cantrip can hit target when used for readied action

            if (!DatabaseHelper.TryGetDefinition<SpellDefinition>(attackParams.effectName, out var cantrip))
            {
                return;
            }

            var canAttack = cantrip.GetFirstSubFeatureOfType<IAttackAfterMagicEffect>()?.CanAttack;

            if (canAttack != null)
            {
                __result = canAttack(attackParams.attacker, attackParams.defender);
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.HandleCharacterMoveStart))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterMoveStart_Patch
    {
        [UsedImplicitly]
        public static void Prefix(
            GameLocationCharacter mover,
            int3 destination)
        {
            //PATCH: support for Polearm Expert AoO
            //Stores character movements to be processed later
            AttacksOfOpportunity.ProcessOnCharacterMoveStart(mover, destination);
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.HandleCharacterMoveEnd))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterMoveEnd_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter mover)
        {
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            //PATCH: support for Polearm Expert AoO. processes saved movement to trigger AoO when appropriate
            // ReSharper disable once InvertIf
            if (__instance.Battle != null &&
                mover.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                var extraEvents = AttacksOfOpportunity.ProcessOnCharacterMoveEnd(__instance, mover);

                while (extraEvents.MoveNext())
                {
                    yield return extraEvents.Current;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.PrepareBattleEnd))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class PrepareBattleEnd_Patch
    {
        [UsedImplicitly]
        public static void Prefix()
        {
            //PATCH: support for Polearm Expert AoO
            //clears movement cache on battle end

            AttacksOfOpportunity.CleanMovingCache();
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleBardicInspirationForAttack))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleBardicInspirationForAttack_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter target,
            ActionModifier attackModifier)
        {
            //PATCH: support for IAlterAttackOutcome
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            if (action.BardicDieRoll > 0)
            {
                action.AttackSuccessDelta += action.BardicDieRoll;
            }

            // ReSharper disable once InvertIf
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                target.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var extraEvents in attacker.RulesetCharacter
                             .GetSubFeaturesByType<ITryAlterOutcomePhysicalAttack>()
                             .TakeWhile(_ =>
                                 action.AttackRollOutcome == RollOutcome.Failure &&
                                 action.AttackSuccessDelta < 0)
                             .Select(feature =>
                                 feature.OnAttackTryAlterOutcome(__instance, action, attacker, target, attackModifier)))
                {
                    while (extraEvents.MoveNext())
                    {
                        yield return extraEvents.Current;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.HandleCharacterAttackFinished))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterAttackFinished_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackerAttackMode,
            RulesetEffect rulesetEffect,
            int damageAmount)
        {
            //PATCH: support for Sentinel feat - allows reaction attack on enemy attacking ally 
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            if (rulesetEffect != null
                && rulesetEffect.EffectDescription.RangeType is not (RangeType.MeleeHit or RangeType.RangeHit))
            {
                yield break;
            }

            //PATCH: support for Sentinel Fighting Style - allows attacks of opportunity on enemies attacking allies
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                var extraEvents =
                    AttacksOfOpportunity.ProcessOnCharacterAttackFinished(__instance, attacker, defender);

                while (extraEvents.MoveNext())
                {
                    yield return extraEvents.Current;
                }
            }

            //PATCH: support for Defensive Strike Power - allows adding Charisma modifier and chain reactions
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                var extraEvents =
                    DefensiveStrikeAttack.ProcessOnCharacterAttackFinished(__instance, attacker, defender);

                while (extraEvents.MoveNext())
                {
                    yield return extraEvents.Current;
                }
            }


            //PATCH: support for Aura of the Guardian power - allows swapping hp on enemy attacking ally
            // ReSharper disable once InvertIf
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                var extraEvents =
                    GuardianAuraHpSwap.ProcessOnCharacterAttackHitFinished(
                        __instance, attacker, defender, attackerAttackMode, rulesetEffect, damageAmount);

                while (extraEvents.MoveNext())
                {
                    yield return extraEvents.Current;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleCharacterAttackHitConfirmed))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterAttackHitConfirmed_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier attackModifier,
            RulesetAttackMode attackMode,
            bool rangedAttack,
            AdvantageType advantageType,
            List<EffectForm> actualEffectForms,
            RulesetEffect rulesetEffect,
            bool criticalHit,
            bool firstTarget)
        {
            // keep a tab on last cantrip weapon attack status
            Global.LastAttackWasCantripWeaponAttackHit =
                attackMode is { AttackTags: not null } &&
                attackMode.AttackTags.Contains(SpellBuilders.CantripWeaponAttack);

            //PATCH: support for `IAttackBeforeHitConfirmedOnEnemy`
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var attackBeforeHitConfirmedOnEnemy in attacker.RulesetCharacter
                             .GetSubFeaturesByType<IAttackBeforeHitConfirmedOnEnemy>())
                {
                    yield return attackBeforeHitConfirmedOnEnemy.OnAttackBeforeHitConfirmedOnEnemy(
                        __instance,
                        attacker,
                        defender,
                        attackModifier,
                        attackMode,
                        rangedAttack,
                        advantageType,
                        actualEffectForms,
                        rulesetEffect,
                        firstTarget, criticalHit);
                }
            }

            //PATCH: support for `IAttackBeforeHitConfirmedOnMe`
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var attackBeforeHitConfirmedOnMe in defender.RulesetCharacter
                             .GetSubFeaturesByType<IAttackBeforeHitConfirmedOnMe>())
                {
                    yield return attackBeforeHitConfirmedOnMe.OnAttackBeforeHitConfirmedOnMe(
                        __instance,
                        attacker,
                        defender,
                        attackModifier,
                        attackMode,
                        rangedAttack,
                        advantageType,
                        actualEffectForms,
                        rulesetEffect,
                        firstTarget, criticalHit);
                }
            }

            //PATCH: support for `IAttackBeforeHitConfirmedOnMeOrAlly`
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var ally in __instance.Battle
                             .GetOpposingContenders(attacker.Side)
                             .Where(x => x.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                             .ToList()) // avoid changing enumerator
                {
                    foreach (var attackBeforeHitConfirmedOnMeOrAlly in ally.RulesetCharacter
                                 .GetSubFeaturesByType<IAttackBeforeHitConfirmedOnMeOrAlly>())
                    {
                        yield return attackBeforeHitConfirmedOnMeOrAlly.OnAttackBeforeHitConfirmedOnMeOrAlly(
                            __instance,
                            attacker,
                            defender,
                            ally,
                            attackModifier,
                            attackMode,
                            rangedAttack,
                            advantageType,
                            actualEffectForms,
                            rulesetEffect,
                            firstTarget, criticalHit);
                    }
                }
            }

            while (values.MoveNext())
            {
                yield return values.Current;
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleCharacterAttackHitPossible))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterAttackHitPossible_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RulesetEffect rulesetEffect,
            ActionModifier attackModifier,
            int attackRoll)
        {
            // ReSharper disable once InvertIf
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                //PATCH: Support for features before hit possible, e.g. spiritual shielding

                foreach (var extraEvents in __instance.Battle.GetOpposingContenders(attacker.Side)
                             .Where(u => u.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                             .ToList() // avoid changing enumerator
                             .SelectMany(featureOwner => featureOwner.RulesetCharacter
                                 .GetSubFeaturesByType<IAttackBeforeHitPossibleOnMeOrAlly>()
                                 .Select(x => x.OnAttackBeforeHitPossibleOnMeOrAlly(__instance, featureOwner, attacker,
                                     defender, attackMode, rulesetEffect, attackModifier, attackRoll))))
                {
                    while (extraEvents.MoveNext())
                    {
                        yield return extraEvents.Current;
                    }
                }
            }

            // Put reaction request for shield and the like after our modded features for better experience 
            while (values.MoveNext())
            {
                yield return values.Current;
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleAttackerTriggeringPowerOnCharacterAttackHitConfirmed))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleAttackerTriggeringPowerOnCharacterAttackHitConfirmed_Patch
    {
        [UsedImplicitly]
        public static void Prefix(
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RulesetEffect rulesetEffect)
        {
            //PATCH: support for `IReactionAttackModeRestriction`
            RestrictReactionAttackMode.ReactionContext = (action, attacker, defender, attackMode, rulesetEffect);
        }

        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values)
        {
            //PATCH: support for `IReactionAttackModeRestriction`
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            RestrictReactionAttackMode.ReactionContext = (null, null, null, null, null);
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleDefenderBeforeDamageReceived))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleDefenderBeforeDamageReceived_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            RulesetAttackMode attackMode,
            RulesetEffect rulesetEffect,
            ActionModifier attackModifier,
            bool rolledSavingThrow,
            bool saveOutcomeSuccess)
        {
            //PATCH: support for features that trigger when defender gets hit, like `FeatureDefinitionReduceDamage` 
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            var defenderCharacter = defender.RulesetCharacter;

            if (defenderCharacter is not { IsDeadOrDyingOrUnconscious: false })
            {
                yield break;
            }

            // Not actually used currently, but may be useful for future features.
            // var selfDamage = attacker.RulesetCharacter == defenderCharacter;

            // Not actually used currently, but may be useful for future features.
            // var canPerceiveAttacker = selfDamage
            //                           || defender.PerceivedFoes.Contains(attacker)
            //                           || defender.PerceivedAllies.Contains(attacker);

            foreach (var feature in defenderCharacter
                         .GetFeaturesByType<FeatureDefinitionReduceDamage>())
            {
                var isValid = defenderCharacter.IsValid(feature.GetAllSubFeaturesOfType<IsCharacterValidHandler>());

                if (!isValid)
                {
                    continue;
                }

                var canReact = defender.CanReact();

                //TODO: add ability to specify whether this feature can reduce magic damage
                var damageTypes = feature.DamageTypes;
                var damage = attackMode?.EffectDescription?.FindFirstDamageFormOfType(damageTypes);

                // In case of a ruleset effect, check that it shall apply damage forms, otherwise don't proceed (e.g. CounterSpell)
                if (rulesetEffect?.EffectDescription != null)
                {
                    var canForceHalfDamage = false;

                    if (rulesetEffect is RulesetEffectSpell activeSpell)
                    {
                        canForceHalfDamage = attacker.RulesetCharacter.CanForceHalfDamage(activeSpell.SpellDefinition);
                    }

                    var effectDescription = rulesetEffect.EffectDescription;

                    if (rolledSavingThrow)
                    {
                        damage = saveOutcomeSuccess
                            ? effectDescription.FindFirstNonNegatedDamageFormOfType(canForceHalfDamage, damageTypes)
                            : effectDescription.FindFirstDamageFormOfType(damageTypes);
                    }
                    else
                    {
                        damage = effectDescription.FindFirstDamageFormOfType(damageTypes);
                    }
                }

                if (damage == null)
                {
                    continue;
                }

                var totalReducedDamage = 0;

                switch (feature.TriggerCondition)
                {
                    // Can I always reduce a fixed damage amount (i.e.: Heavy Armor Feat)
                    case AdditionalDamageTriggerCondition.AlwaysActive:
                        totalReducedDamage = feature.ReducedDamage(attacker, defender);
                        break;

                    // Can I reduce the damage consuming slots? (i.e.: Blade Dancer)
                    case AdditionalDamageTriggerCondition.SpendSpellSlot:
                    {
                        if (!canReact)
                        {
                            continue;
                        }

                        var repertoire = defenderCharacter.SpellRepertoires
                            .Find(x => x.spellCastingClass == feature.SpellCastingClass);

                        if (repertoire == null)
                        {
                            continue;
                        }

                        if (!repertoire.AtLeastOneSpellSlotAvailable())
                        {
                            continue;
                        }

                        var actionService = ServiceRepository.GetService<IGameLocationActionService>();
                        var previousReactionCount = actionService.PendingReactionRequestGroups.Count;
                        var reactionParams = new CharacterActionParams(defender, ActionDefinitions.Id.SpendSpellSlot)
                        {
                            IntParameter = 1,
                            StringParameter = feature.NotificationTag,
                            SpellRepertoire = repertoire
                        };

                        actionService.ReactToSpendSpellSlot(reactionParams);

                        yield return __instance.WaitForReactions(defender, actionService, previousReactionCount);

                        if (!reactionParams.ReactionValidated)
                        {
                            continue;
                        }

                        totalReducedDamage = feature.ReducedDamage(attacker, defender) * reactionParams.IntParameter;
                        break;
                    }

                    case AdditionalDamageTriggerCondition.AdvantageOrNearbyAlly:
                        break;
                    case AdditionalDamageTriggerCondition.SpecificCharacterFamily:
                        break;
                    case AdditionalDamageTriggerCondition.TargetHasConditionCreatedByMe:
                        break;
                    case AdditionalDamageTriggerCondition.TargetHasCondition:
                        break;
                    case AdditionalDamageTriggerCondition.TargetIsWounded:
                        break;
                    case AdditionalDamageTriggerCondition.TargetHasSenseType:
                        break;
                    case AdditionalDamageTriggerCondition.TargetHasCreatureTag:
                        break;
                    case AdditionalDamageTriggerCondition.RangeAttackFromHigherGround:
                        break;
                    case AdditionalDamageTriggerCondition.EvocationSpellDamage:
                        break;
                    case AdditionalDamageTriggerCondition.TargetDoesNotHaveCondition:
                        break;
                    case AdditionalDamageTriggerCondition.SpellDamagesTarget:
                        break;
                    case AdditionalDamageTriggerCondition.SpellDamageMatchesSourceAncestry:
                        break;
                    case AdditionalDamageTriggerCondition.CriticalHit:
                        break;
                    case AdditionalDamageTriggerCondition.RagingAndTargetIsSpellcaster:
                        break;
                    case AdditionalDamageTriggerCondition.Raging:
                        break;
                    case AdditionalDamageTriggerCondition.NotWearingHeavyArmor:
                        break;
                    default:
                        throw new ArgumentException("feature.TriggerCondition");
                }

                if (totalReducedDamage <= 0)
                {
                    continue;
                }

                var tag = $"{feature.Name}:{defender.Guid}:{totalReducedDamage}";

                attackMode?.AttackTags.Add(tag);
                rulesetEffect?.SourceTags.Add(tag);

                defenderCharacter.DamageReduced(defenderCharacter, feature, totalReducedDamage);
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.CanAttack))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class CanAttack_Patch
    {
        [UsedImplicitly]
        public static void Postfix(
            BattleDefinitions.AttackEvaluationParams attackParams,
            bool __result,
            GameLocationBattleManager __instance)
        {
            //PATCH: support for features removing ranged attack disadvantage
            RangedAttackInMeleeDisadvantageRemover.CheckToRemoveRangedDisadvantage(attackParams);

            //PATCH: add modifier or advantage/disadvantage for physical and spell attack
            ApplyCustomModifiers(attackParams, __result);

            //PATCH: add a rule to grant adv to attacks that the target unable to see
            PatchIlluminationBasedAdvantage(__instance, __result, attackParams);
        }

        //TODO: move this somewhere else and maybe split?
        private static void ApplyCustomModifiers(BattleDefinitions.AttackEvaluationParams attackParams, bool __result)
        {
            if (!__result)
            {
                return;
            }

            var attacker = attackParams.attacker.RulesetCharacter;
            var defender = attackParams.defender.RulesetCharacter;

            if (attacker == null || defender == null)
            {
                return;
            }

            var attackModifiers = attacker.GetSubFeaturesByType<IModifyAttackActionModifier>();

            foreach (var feature in attackModifiers)
            {
                feature.OnAttackComputeModifier(
                    attacker,
                    defender,
                    attackParams.attackProximity,
                    attackParams.attackMode,
                    ref attackParams.attackModifier);
            }
        }

        private static void PatchIlluminationBasedAdvantage(
            GameLocationBattleManager __instance,
            bool __result,
            BattleDefinitions.AttackEvaluationParams attackParams)
        {
            if (!__result || !Main.Settings.AttackersWithDarkvisionHaveAdvantageOverDefendersWithout)
            {
                return;
            }

            var attackerLoc = attackParams.attacker;
            var defenderLoc = attackParams.defender;
            var attackerChr = attackerLoc.RulesetCharacter;
            var defenderChr = defenderLoc.RulesetCharacter;

            if (attackerChr == null || defenderChr == null)
            {
                return;
            }

            // It seems that we don't need to find the controller of the attacker or the defender
            //RulesetCharacterEffectProxy rulesetCharacterEffectProxy;
            //if ((rulesetCharacterEffectProxy = (attackerLoc.RulesetCharacter as RulesetCharacterEffectProxy)) != null)
            //{
            //    RulesetActor rulesetActor = null;
            //    if (RulesetEntity.TryGetEntity<RulesetActor>(rulesetCharacterEffectProxy.ControllerGuid, out rulesetActor))
            //    {
            //        attackerLoc = GameLocationCharacter.GetFromActor(rulesetActor);
            //    }
            //}

            var attackerGravityCenter =
                __instance.gameLocationPositioningService.ComputeGravityCenterPosition(attackerLoc);
            var defenderGravityCenter =
                __instance.gameLocationPositioningService.ComputeGravityCenterPosition(defenderLoc);

            IIlluminable attacker = attackerLoc;
            var lightingState = attackerLoc.LightingState;
            var distance = (defenderGravityCenter - attackerGravityCenter).magnitude;
            var flag = defenderLoc.RulesetCharacter.SenseModes
                .Where(senseMode => distance <= senseMode.SenseRange)
                .Any(senseMode => SenseMode.ValidForLighting(senseMode.SenseType, lightingState));

            if (flag)
            {
                return;
            }

            attackParams.attackModifier.AttackAdvantageTrends.Add(
                new TrendInfo(1, FeatureSourceType.Lighting, lightingState.ToString(),
                    attacker.TargetSource, (string)null));
            attackParams.attackModifier.AbilityCheckAdvantageTrends.Add(
                new TrendInfo(1, FeatureSourceType.Lighting, lightingState.ToString(),
                    attacker.TargetSource, (string)null));
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleAdditionalDamageOnCharacterAttackHitConfirmed))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleAdditionalDamageOnCharacterAttackHitConfirmed_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(
            GameLocationBattleManager __instance,
            out IEnumerator __result,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier attackModifier,
            RulesetAttackMode attackMode,
            bool rangedAttack,
            AdvantageType advantageType,
            List<EffectForm> actualEffectForms,
            RulesetEffect rulesetEffect,
            bool criticalHit,
            bool firstTarget)
        {
            //PATCH: Completely replace this method to support several features. Modified method based on TA provided sources.
            __result = GameLocationBattleManagerTweaks.HandleAdditionalDamageOnCharacterAttackHitConfirmed(__instance,
                attacker, defender, attackModifier, attackMode, rangedAttack, advantageType, actualEffectForms,
                rulesetEffect, criticalHit, firstTarget);

            return false;
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.ComputeAndNotifyAdditionalDamage))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class ComputeAndNotifyAdditionalDamage_Patch
    {
        [UsedImplicitly]
        public static bool Prefix(
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            IAdditionalDamageProvider provider,
            List<EffectForm> actualEffectForms,
            CharacterActionParams reactionParams,
            RulesetAttackMode attackMode,
            bool criticalHit)
        {
            //PATCH: Completely replace this method to support several features. Modified method based on TA provided sources.
            GameLocationBattleManagerTweaks.ComputeAndNotifyAdditionalDamage(__instance, attacker, defender, provider,
                actualEffectForms, reactionParams, attackMode, criticalHit);

            return false;
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.HandleTargetReducedToZeroHP))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    // ReSharper disable once InconsistentNaming
    public static class HandleTargetReducedToZeroHP_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter downedCreature,
            RulesetAttackMode rulesetAttackMode,
            RulesetEffect activeEffect)
        {
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            if (__instance.Battle == null)
            {
                yield break;
            }

            //PATCH: Support for `ITargetReducedToZeroHP` feature
            foreach (var onTargetReducedToZeroHp in
                     attacker.RulesetActor.GetSubFeaturesByType<IOnReducedToZeroHpEnemy>())
            {
                yield return onTargetReducedToZeroHp.HandleReducedToZeroHpEnemy(
                    attacker, downedCreature, rulesetAttackMode, activeEffect);
            }

            if (__instance.Battle == null)
            {
                yield break;
            }

            //PATCH: Support for `ISourceReducedToZeroHP` feature
            foreach (var onSourceReducedToZeroHp in downedCreature.RulesetActor
                         .GetSubFeaturesByType<IOnReducedToZeroHpMe>())
            {
                yield return onSourceReducedToZeroHp.HandleReducedToZeroHpMe(
                    attacker, downedCreature, rulesetAttackMode, activeEffect);
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleCharacterMagicalAttackHitConfirmed))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterMagicalAttackHitConfirmed_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier magicModifier,
            RulesetEffect rulesetEffect,
            List<EffectForm> actualEffectForms,
            bool firstTarget,
            bool criticalHit)
        {
            //PATCH: support for `IMagicalAttackBeforeHitConfirmedOnEnemy`
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var feature in attacker.RulesetActor
                             .GetSubFeaturesByType<IMagicalAttackBeforeHitConfirmedOnEnemy>())
                {
                    yield return feature.OnMagicalAttackBeforeHitConfirmedOnEnemy(
                        attacker, defender, magicModifier, rulesetEffect, actualEffectForms, firstTarget, criticalHit);
                }

                if (rulesetEffect is { SourceDefinition: SpellDefinition spellDefinition })
                {
                    var modifier = spellDefinition.GetFirstSubFeatureOfType<IMagicalAttackBeforeHitConfirmedOnEnemy>();

                    yield return modifier?.OnMagicalAttackBeforeHitConfirmedOnEnemy(
                        attacker, defender, magicModifier, rulesetEffect, actualEffectForms, firstTarget, criticalHit);
                }
            }

            //PATCH: support for `IMagicalAttackBeforeHitConfirmedOnMe`
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var feature in defender.RulesetActor
                             .GetSubFeaturesByType<IMagicalAttackBeforeHitConfirmedOnMe>())
                {
                    yield return feature.OnMagicalAttackBeforeHitConfirmedOnMe(
                        attacker, defender, magicModifier, rulesetEffect, actualEffectForms, firstTarget, criticalHit);
                }
            }

            //PATCH: support for `IMagicalAttackBeforeHitConfirmedOnMeOrAlly`
            if (__instance.Battle != null &&
                attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } &&
                defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
            {
                foreach (var ally in __instance.Battle
                             .GetOpposingContenders(attacker.Side)
                             .Where(x => x.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                             .ToList()) // avoid changing enumerator
                {
                    foreach (var magicalAttackBeforeHitConfirmedOnMeOrAlly in ally.RulesetCharacter
                                 .GetSubFeaturesByType<IMagicalAttackBeforeHitConfirmedOnMeOrAlly>())
                    {
                        yield return magicalAttackBeforeHitConfirmedOnMeOrAlly
                            .OnMagicalAttackBeforeHitConfirmedOnMeOrAlly(
                                attacker, defender, ally, magicModifier, rulesetEffect, actualEffectForms, firstTarget,
                                criticalHit);
                    }
                }
            }

            while (values.MoveNext())
            {
                yield return values.Current;
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager), nameof(GameLocationBattleManager.HandleFailedSavingThrow))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleFailedSavingThrow_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationBattleManager __instance,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier saveModifier,
            bool hasHitVisual,
            bool hasBorrowedLuck)
        {
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            // var contenders =
            //     __instance.Battle?.AllContenders ??
            //     ServiceRepository.GetService<IGameLocationCharacterService>().PartyCharacters;

            if (__instance.Battle == null)
            {
                yield break;
            }

            //PATCH: support for `ITryAlterOutcomeFailedSavingThrow`
            foreach (var unit in __instance.Battle.AllContenders
                         .Where(u => u.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                         .ToList()) // avoid changing enumerator
            {
                foreach (var feature in unit.RulesetCharacter
                             .GetSubFeaturesByType<ITryAlterOutcomeFailedSavingThrow>())
                {
                    yield return feature.OnFailedSavingTryAlterOutcome(
                        __instance, action, attacker, defender, unit, saveModifier, hasHitVisual, hasBorrowedLuck);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleCharacterPhysicalAttackInitiated))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleCharacterPhysicalAttackInitiated_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
#pragma warning disable IDE0060
            //values are not used but required for patch to work
            [NotNull] IEnumerator values,
#pragma warning restore IDE0060
            GameLocationBattleManager __instance,
            CharacterAction action,
            GameLocationCharacter attacker,
            GameLocationCharacter defender,
            ActionModifier attackModifier,
            RulesetAttackMode attackerAttackMode)
        {
            //PATCH: registers which weapon types were used so far on attacks

            ValidatorsCharacter.RegisterWeaponTypeUsed(attacker, attackerAttackMode);

            //PATCH: allow custom behavior when physical attack initiates

            if (__instance.Battle != null)
            {
                foreach (var attackInitiated in
                         attacker.RulesetCharacter.GetSubFeaturesByType<IPhysicalAttackInitiatedByMe>())
                {
                    yield return attackInitiated.OnAttackInitiatedByMe(
                        __instance, action, attacker, defender, attackModifier, attackerAttackMode);
                }
            }

            //PATCH: allow custom behavior when physical attack initiates on me

            if (__instance.Battle != null)
            {
                foreach (var attackInitiated in
                         defender.RulesetCharacter.GetSubFeaturesByType<IPhysicalAttackInitiatedOnMe>())
                {
                    yield return attackInitiated.OnAttackInitiatedOnMe(
                        __instance, action, attacker, defender, attackModifier, attackerAttackMode);
                }
            }

            //PATCH: allow custom behavior when physical attack initiates on me or ally

            if (__instance.Battle != null)
            {
                foreach (var ally in __instance.Battle.GetOpposingContenders(attacker.Side)
                             .Where(x => x.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                             .ToList()) // avoid changing enumerator
                {
                    foreach (var physicalAttackInitiatedOnMeOrAlly in ally.RulesetCharacter
                                 .GetSubFeaturesByType<IPhysicalAttackInitiatedOnMeOrAlly>())
                    {
                        yield return physicalAttackInitiatedOnMeOrAlly.OnAttackInitiatedOnMeOrAlly(
                            __instance, action, attacker, defender, ally, attackModifier, attackerAttackMode);
                    }
                }
            }

            if (__instance.Battle == null)
            {
                yield break;
            }

            // pretty much vanilla code from here

            ++defender.SustainedAttacks;

            var rulesetCharacter = attacker.RulesetCharacter;

            if (rulesetCharacter != null)
            {
                foreach (var usablePower in rulesetCharacter.UsablePowers
                             .Where(usablePower =>
                                 __instance.CanCharacterUsePower(rulesetCharacter, defender, usablePower) &&
                                 usablePower.PowerDefinition.ActivationTime ==
                                 ActivationTime.OnAttackHitMartialArts && attackerAttackMode != null &&
                                 action.ActionId != ActionDefinitions.Id.AttackReadied &&
                                 rulesetCharacter.IsWieldingMonkWeapon() &&
                                 !rulesetCharacter.IsWearingArmor() &&
                                 !rulesetCharacter.HasConditionOfTypeOrSubType(ConditionMagicallyArmored) &&
                                 // BEGIN PATCH
                                 (!rulesetCharacter.IsWearingShield() || rulesetCharacter.HasMonkShieldExpert()) &&
                                 // END PATCH
                                 !rulesetCharacter.HasConditionOfType(ConditionMonkDeflectMissile) &&
                                 !rulesetCharacter.HasConditionOfType(ConditionMonkMartialArtsUnarmedStrikeBonus) &&
                                 attacker.GetActionTypeStatus(ActionDefinitions.ActionType.Bonus) ==
                                 ActionDefinitions.ActionStatus.Available))
                {
                    __instance.PrepareAndExecuteSpendPowerAction(attacker, defender, usablePower);
                }
            }

            foreach (var opposingContender in __instance.Battle
                         .GetOpposingContenders(attacker.Side)
                         .Where(opposingContender =>
                             opposingContender != defender && opposingContender.RulesetCharacter is
                             {
                                 IsDeadOrDyingOrUnconscious: false
                             } &&
                             opposingContender.GetActionTypeStatus(ActionDefinitions.ActionType.Reaction) ==
                             ActionDefinitions.ActionStatus.Available &&
                             __instance.IsWithin1Cell(opposingContender, defender) &&
                             opposingContender.GetActionStatus(ActionDefinitions.Id.BlockAttack,
                                 ActionDefinitions.ActionScope.Battle, ActionDefinitions.ActionStatus.Available) ==
                             ActionDefinitions.ActionStatus.Available))
            {
                yield return __instance.PrepareAndReact(
                    opposingContender, attacker, attacker, ActionDefinitions.Id.BlockAttack, attackModifier,
                    additionalTargetCharacter: defender);
                break;
            }
        }

        [HarmonyPatch(typeof(GameLocationBattleManager),
            nameof(GameLocationBattleManager.HandleCharacterPhysicalAttackFinished))]
        [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
        [UsedImplicitly]
        public static class HandleCharacterPhysicalAttackFinished_Patch
        {
            [UsedImplicitly]
            public static IEnumerator Postfix(
                IEnumerator values,
                GameLocationBattleManager __instance,
                CharacterAction attackAction,
                GameLocationCharacter attacker,
                GameLocationCharacter defender,
                RulesetAttackMode attackerAttackMode,
                RollOutcome attackRollOutcome,
                int damageAmount)
            {
                while (values.MoveNext())
                {
                    yield return values.Current;
                }

                if (attacker.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } && __instance.Battle != null)
                {
                    //PATCH: allow custom behavior when physical attack finished
                    foreach (var feature in attacker.RulesetCharacter
                                 .GetSubFeaturesByType<IPhysicalAttackFinishedByMe>())
                    {
                        yield return feature.OnAttackFinishedByMe(
                            __instance, attackAction, attacker, defender, attackerAttackMode, attackRollOutcome,
                            damageAmount);
                    }
                }

                if (defender.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false } && __instance.Battle != null)
                {
                    //PATCH: allow custom behavior when physical attack finished on defender
                    foreach (var feature in defender.RulesetCharacter
                                 .GetSubFeaturesByType<IPhysicalAttackFinishedOnMe>())
                    {
                        yield return feature.OnAttackFinishedOnMe(
                            __instance, attackAction, attacker, defender, attackerAttackMode, attackRollOutcome,
                            damageAmount);
                    }
                }

                if (__instance.Battle != null)
                {
                    // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                    foreach (var gameLocationAlly in __instance.Battle.GetMyContenders(attacker.Side)
                                 .Where(x => x.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                                 .ToList()) // avoid changing enumerator
                    {
                        var allyFeatures =
                            gameLocationAlly.RulesetCharacter.GetSubFeaturesByType<IPhysicalAttackFinishedByMeOrAlly>();

                        foreach (var feature in allyFeatures)
                        {
                            yield return feature.OnPhysicalAttackFinishedByMeOrAlly(
                                __instance, attackAction, attacker, defender, gameLocationAlly, attackerAttackMode,
                                attackRollOutcome,
                                damageAmount);
                        }
                    }
                }

                // ReSharper disable once InvertIf
                if (__instance.Battle != null)
                {
                    // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                    foreach (var gameLocationAlly in __instance.Battle.GetOpposingContenders(attacker.Side)
                                 .Where(x => x.RulesetCharacter is { IsDeadOrDyingOrUnconscious: false })
                                 .ToList()) // avoid changing enumerator
                    {
                        var allyFeatures =
                            gameLocationAlly.RulesetCharacter.GetSubFeaturesByType<IPhysicalAttackFinishedOnMeOrAlly>();

                        foreach (var feature in allyFeatures)
                        {
                            yield return feature.OnAttackFinishedOnMeOrAlly(
                                __instance, attackAction, attacker, defender, gameLocationAlly, attackerAttackMode,
                                attackRollOutcome,
                                damageAmount);
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameLocationBattleManager),
        nameof(GameLocationBattleManager.HandleSpellCast))]
    [SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "Patch")]
    [UsedImplicitly]
    public static class HandleSpellCast_Patch
    {
        [UsedImplicitly]
        public static IEnumerator Postfix(
            IEnumerator values,
            GameLocationCharacter caster,
            CharacterActionCastSpell castAction,
            RulesetEffectSpell selectEffectSpell,
            RulesetSpellRepertoire selectedRepertoire,
            SpellDefinition selectedSpellDefinition)
        {
            while (values.MoveNext())
            {
                yield return values.Current;
            }

            // This also allows utilities out of battle
            var gameLocationCharacterService = ServiceRepository.GetService<IGameLocationCharacterService>();
            var allyCharacters = gameLocationCharacterService.PartyCharacters.Select(x => x.RulesetCharacter);

            foreach (var allyCharacter in allyCharacters.Where(x => x is { IsDeadOrDyingOrUnconscious: false }))
            {
                var allyFeatures = allyCharacter.GetSubFeaturesByType<IMagicalAttackCastedSpell>();

                foreach (var feature in allyFeatures)
                {
                    yield return feature.OnSpellCast(
                        allyCharacter, caster, castAction, selectEffectSpell, selectedRepertoire,
                        selectedSpellDefinition);
                }
            }
        }
    }
}
