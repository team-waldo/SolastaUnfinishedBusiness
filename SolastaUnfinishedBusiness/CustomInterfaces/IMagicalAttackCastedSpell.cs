﻿using System.Collections;
using JetBrains.Annotations;

namespace SolastaUnfinishedBusiness.CustomInterfaces;

// On spell being cast
internal interface IMagicalAttackCastedSpell
{
    IEnumerator OnSpellCast(
        RulesetCharacter featureOwner,
        GameLocationCharacter caster,
        CharacterActionCastSpell castAction,
        [UsedImplicitly] RulesetEffectSpell selectEffectSpell,
        [UsedImplicitly] RulesetSpellRepertoire selectedRepertoire,
        SpellDefinition selectedSpellDefinition);
}
