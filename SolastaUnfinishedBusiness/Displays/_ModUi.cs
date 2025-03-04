﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Api.ModKit;
using UnityModManagerNet;
using static SolastaUnfinishedBusiness.Displays.BackgroundsAndRacesDisplay;
using static SolastaUnfinishedBusiness.Displays.BlueprintDisplay;
using static SolastaUnfinishedBusiness.Displays.CharacterDisplay;
using static SolastaUnfinishedBusiness.Displays.CreditsDisplay;
using static SolastaUnfinishedBusiness.Displays.DungeonMakerDisplay;
using static SolastaUnfinishedBusiness.Displays.EffectsDisplay;
using static SolastaUnfinishedBusiness.Displays.EncountersDisplay;
using static SolastaUnfinishedBusiness.Displays.GameServicesDisplay;
using static SolastaUnfinishedBusiness.Displays.GameUiDisplay;
using static SolastaUnfinishedBusiness.Displays.ItemsAndCraftingDisplay;
using static SolastaUnfinishedBusiness.Displays.ProficienciesDisplay;
using static SolastaUnfinishedBusiness.Displays.RulesDisplay;
using static SolastaUnfinishedBusiness.Displays.SpellsDisplay;
using static SolastaUnfinishedBusiness.Displays.SubclassesDisplay;
using static SolastaUnfinishedBusiness.Displays.ToolsDisplay;
using static SolastaUnfinishedBusiness.Displays.TranslationsDisplay;

#if DEBUG
using static SolastaUnfinishedBusiness.Displays.DiagnosticsDisplay;
#endif

namespace SolastaUnfinishedBusiness.Displays
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal static class ModUi
    {
        internal const int DontDisplayDescription = 4;
        internal const float PixelsPerColumn = 220;

        internal static void DisplaySubMenu(ref int selectedPane, string title = null, params NamedAction[] actions)
        {
            if (!Main.Enabled)
            {
                return;
            }

            if (title != null)
            {
                UI.Div();
                UI.Label(title);
                UI.Space((float)7);
            }

            UI.SubMenu(ref selectedPane, title != null, null, actions);
        }

        internal static void DisplayDefinitions<T>(
            string label,
            Action<T, bool> switchAction,
            [NotNull] HashSet<T> registeredDefinitions,
            [NotNull] List<string> selectedDefinitions,
            ref bool displayToggle,
            ref int sliderPosition,
            bool useAlternateDescription = false,
            [CanBeNull] Action headerRendering = null,
            [CanBeNull] Action additionalRendering = null) where T : BaseDefinition
        {
            if (registeredDefinitions.Count == 0)
            {
                return;
            }

            var selectAll = selectedDefinitions.Count == registeredDefinitions.Count;

            UI.Label();

            var toggle = displayToggle;

            if (UI.DisclosureToggle($"{label}:", ref toggle, 200))
            {
                displayToggle = toggle;
            }

            if (!displayToggle)
            {
                return;
            }

            UI.Label();

            headerRendering?.Invoke();

            using (UI.HorizontalScope())
            {
                if (additionalRendering != null)
                {
                    additionalRendering.Invoke();
                }
                else if (UI.Toggle(Gui.Localize("ModUi/&SelectAll"), ref selectAll, UI.Width(PixelsPerColumn)))
                {
                    foreach (var registeredDefinition in registeredDefinitions)
                    {
                        switchAction.Invoke(registeredDefinition, selectAll);
                    }
                }

                toggle = sliderPosition == 1;

                if (UI.Toggle(Gui.Localize("ModUi/&ShowDescriptions"), ref toggle, UI.Width(PixelsPerColumn)))
                {
                    sliderPosition = toggle ? 1 : 4;
                }
            }

            // UI.Slider("slide left for description / right to collapse".white().bold().italic(), ref sliderPosition, 1, maxColumns, 1, "");

            UI.Label();

            var flip = false;
            var current = 0;
            var count = registeredDefinitions.Count;

            using (UI.VerticalScope())
            {
                while (current < count)
                {
                    var columns = sliderPosition;

                    using (UI.HorizontalScope())
                    {
                        while (current < count && columns-- > 0)
                        {
                            var definition = registeredDefinitions.ElementAt(current);
                            var title = definition.FormatTitle();

                            if (flip)
                            {
                                title = title.Khaki();
                            }

                            toggle = selectedDefinitions.Contains(definition.Name);

                            if (UI.Toggle(title, ref toggle, UI.Width(PixelsPerColumn)))
                            {
                                switchAction.Invoke(definition, toggle);
                            }

                            if (sliderPosition == 1)
                            {
                                var description = useAlternateDescription
                                    ? Gui.Localize($"ModUi/&{definition.Name}Description")
                                    : definition.FormatDescription();

                                if (flip)
                                {
                                    description = description.Khaki();
                                }

                                UI.Label(description, UI.Width(PixelsPerColumn * 3));

                                flip = !flip;
                            }

                            current++;
                        }
                    }
                }
            }
        }
    }

    [UsedImplicitly]
    internal sealed class GameplayViewer : IMenuSelectablePage
    {
        private int _gamePlaySelectedPane;
        public string Name => Gui.Localize("ModUi/&Gameplay");

        public int Priority => 100;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            ModUi.DisplaySubMenu(ref _gamePlaySelectedPane, Name,
                new NamedAction(Gui.Localize("ModUi/&Tools"), DisplayTools),
                new NamedAction(Gui.Localize("ModUi/&GeneralMenu"), DisplayCharacter),
                new NamedAction(Gui.Localize("ModUi/&Rules"), DisplayRules),
                new NamedAction(Gui.Localize("ModUi/&ItemsCraftingMerchants"), DisplayItemsAndCrafting));
        }
    }

    [UsedImplicitly]
    internal sealed class CharacterViewer : IMenuSelectablePage
    {
        private int _characterSelectedPane;
        public string Name => Gui.Localize("ModUi/&Character");

        public int Priority => 200;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            ModUi.DisplaySubMenu(ref _characterSelectedPane, Name,
                new NamedAction(Gui.Localize("ModUi/&BackgroundsAndRaces"),
                    DisplayBackgroundsAndDeities),
                new NamedAction(Gui.Localize("Screen/&FeatureListingProficienciesTitle"),
                    DisplayProficiencies),
                new NamedAction(Gui.Localize("ModUi/&SpellsMenu"),
                    DisplaySpells),
                new NamedAction(Gui.Localize("ModUi/&Subclasses"),
                    DisplaySubclasses));
        }
    }

    [UsedImplicitly]
    internal sealed class InterfaceViewer : IMenuSelectablePage
    {
        private int _interfaceSelectedPane;
        public string Name => Gui.Localize("ModUi/&Interface");

        public int Priority => 300;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            ModUi.DisplaySubMenu(ref _interfaceSelectedPane, Name,
                new NamedAction(Gui.Localize("ModUi/&GameUi"), DisplayGameUi),
                new NamedAction(Gui.Localize("ModUi/&DungeonMakerMenu"), DisplayDungeonMaker),
                new NamedAction(Gui.Localize("ModUi/&Translations"), DisplayTranslations));
        }
    }

    [UsedImplicitly]
    internal sealed class PartyEditorViewer : IMenuSelectablePage
    {
        public string Name => "PartyEditor".Localized();

        public int Priority => 400;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            PartyEditor.OnGUI();
        }
    }

    [UsedImplicitly]
    internal sealed class EncountersViewer : IMenuSelectablePage
    {
        private int _encountersSelectedPane;
        public string Name => Gui.Localize("ModUi/&Encounters");

        public int Priority => 500;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            ModUi.DisplaySubMenu(ref _encountersSelectedPane, Name,
                new NamedAction(Gui.Localize("ModUi/&GeneralMenu"), DisplayEncountersGeneral),
                new NamedAction(Gui.Localize("ModUi/&Bestiary"), DisplayBestiary),
                new NamedAction(Gui.Localize("ModUi/&CharactersPool"), DisplayNpcs));
        }
    }

    [UsedImplicitly]
    internal sealed class CreditsAndDiagnosticsViewer : IMenuSelectablePage
    {
        private int _creditsSelectedPane;
        public string Name => Gui.Localize("ModUi/&CreditsAndDiagnostics");

        public int Priority => 999;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            ModUi.DisplaySubMenu(ref _creditsSelectedPane, null,
                new NamedAction(Gui.Localize("ModUi/&Credits"), DisplayCredits),
                new NamedAction("Effects", DisplayEffects),
#if DEBUG
                new NamedAction("Diagnostics", DisplayDiagnostics),
#endif
                new NamedAction(Gui.Localize("ModUi/&Blueprints"), DisplayBlueprints),
                new NamedAction(Gui.Localize("ModUi/&Services"), DisplayGameServices));
        }
    }
}
