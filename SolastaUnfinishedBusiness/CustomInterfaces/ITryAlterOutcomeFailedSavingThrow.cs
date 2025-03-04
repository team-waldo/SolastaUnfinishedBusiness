﻿using System.Collections;
using JetBrains.Annotations;

namespace SolastaUnfinishedBusiness.CustomInterfaces;

public interface ITryAlterOutcomeFailedSavingThrow
{
    IEnumerator OnFailedSavingTryAlterOutcome(
        GameLocationBattleManager battleManager,
        CharacterAction action,
        GameLocationCharacter attacker,
        GameLocationCharacter defender,
        GameLocationCharacter featureOwner,
        ActionModifier saveModifier,
        bool hasHitVisual,
        [UsedImplicitly] bool hasBorrowedLuck);
}
