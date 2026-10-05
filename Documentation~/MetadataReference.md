# Dialogue Metadata Reference

This document is the complete reference for metadata exposed by **SAS Dialogue System**. It covers the canonical Ink tag names, their runtime behavior, authoring syntax, required Unity setup, metadata profiles, customized Inky sidecars, and project-defined tags.

> The examples below use the canonical `DialogueMetadataProfile` names. A project can rename most semantic tags in its profile; when it does, use the same names in Ink and in the customized Inky sidecar.

## Quick start

A regular dialogue line uses a contiguous block of Ink tags immediately above the text:

```ink
# id:intro.guide.welcome
# speaker:guide
# portrait:happy
# animation:Wave
Welcome to the valley.
```

A tag has a key and value separated by the first colon:

```text
# key:value
```

Whitespace around the key and value is ignored. Keys are case-insensitive at runtime. Values keep their original casing, although enum-like values such as `enable` and `fixed-character` are also compared case-insensitively.

For a choice, put metadata inside the visible choice brackets. This is necessary because Ink must expose it through `Choice.tags` before the choice is selected:

```ink
* [Ask about the ruins # id:choice.ask_ruins # analytics_event:ask_ruins] -> ruins
```

## Complete canonical tag list

| Tag | Value | Default runtime use | Where to use | Unity/game setup |
| --- | --- | --- | --- | --- |
| `id` | Stable identifier | Stored as `DialogueLineContext.LineId` | Dialogue or choice | None; subscribe to context events if the game uses it |
| `locale` | Localization entry key | Replaces raw Ink text through `DialogueLocaleTextPresenter` | Dialogue or choice | Unity Localization string table and locale presenter |
| `layout` | Animator state name | Plays a dialogue UI layout state | Dialogue | `DialogueLayoutAnimator` and matching Animator state |
| `audio` | Typewriter audio profile ID | Selects per-character typing audio | Dialogue | `TypewriterEffect`, `AudioSource`, and matching audio info asset |
| `skip` | `enable` or `disable` | Changes whether the rest of the story may be skipped | Dialogue | Optional `DialogueStorySkipButton` or custom control |
| `placement` | `follow-speaker` or `fixed-character` | Selects participant placement behavior | Dialogue | `SpeakerPresenter`; fixed/custom slots need corresponding views |
| `slot.<slot-id>` | Character ID or `clear` | Assigns or clears a persistent visual slot | Dialogue | A matching slot on `SpeakerPresenter` |
| `speaker` | Character ID | Defines the current speaker participant | Dialogue; available in choice context | `SpeakerPresenter` for visual use |
| `speaker_name` | Display text | Overrides the catalog display name | With `speaker` | Optional exceptional-case override |
| `portrait` | Character-scoped portrait key | Overrides the catalog default portrait | With `speaker` | Matching `DialogueCharacterCatalog` portrait entry |
| `animation` | Animator state/key | Overrides the catalog default animation | With `speaker` | Matching character UI animation setup |
| `listener` | Character ID | Defines the primary listener participant | Dialogue; available in choice context | `SpeakerPresenter` for visual use |
| `listener_name` | Display text | Overrides the listener's catalog display name | With `listener` | Optional exceptional-case override |
| `listener_portrait` | Character-scoped portrait key | Overrides the listener's catalog default portrait | With `listener` | Matching `DialogueCharacterCatalog` portrait entry |
| `listener_animation` | Animator state/key | Overrides the listener's catalog default animation | With `listener` | Matching character UI animation setup |
| `participant.<role>` | Character ID | Adds a project-defined participant role | Dialogue; available in choice context | A presenter slot when the role must be shown |
| `participant.<role>.name` | Display text | Overrides that participant's catalog display name | With `participant.<role>` | Optional exceptional-case override |
| `participant.<role>.portrait` | Character-scoped portrait key | Overrides that participant's catalog default portrait | With `participant.<role>` | Matching `DialogueCharacterCatalog` portrait entry |
| `participant.<role>.animation` | Animator state/key | Overrides that participant's catalog default animation | With `participant.<role>` | Matching character UI animation setup |
| Any other valid key | Project-defined text | Preserved in `DialogueLineContext.Tags` | Dialogue or choice | Game code decides what it means |

The default choice UI directly consumes `locale`. Other choice tags remain available in each `ChoiceOptionContext.LineContext` for custom UI, analytics, requirements, icons, and other integrations. Presentation directives such as `skip`, `placement`, and `slot.*` should be placed on dialogue lines; choice parsing alone does not apply them to the active session.

## Scalar metadata

### `id` ? stable line or choice ID

Use `id` when game code needs a stable reference that does not depend on displayed text. Typical uses include analytics, save flags, voice-over manifests, quest reactions, automated tests, and debugging.

```ink
# id:quest.blacksmith.intro
The forge has been quiet since the storm.
```

At runtime it is exposed as `DialogueLineContext.LineId`. The package does not automatically dispatch game events from this ID; subscribe to `DialogueHandler.OnLineReady` or `OnLinePresented` and apply project behavior there.

Recommended conventions:

- Keep IDs unique within the project.
- Use a hierarchical form such as `quest.blacksmith.intro`.
- Treat an ID as permanent after content ships.
- Do not use localized or player-visible text as the ID.

An ID must start with a letter or number and may contain letters, numbers, dots, hyphens, and underscores.

### `locale` ? localization key

Use `locale` when the displayed line or choice should come from a Unity Localization string table:

```ink
# locale:dialogue.guide.welcome
This fallback text is useful during writing.
```

`DialogueLocaleTextPresenter` looks up the value in its configured string table. Its default table name is `DialogueTextTable`. If `locale` is absent, the raw Ink text is shown. The default `ChoicePresenter` applies the same lookup to choices.

Game setup:

1. Install and configure Unity Localization.
2. Create the configured string table collection.
3. Add an entry whose key exactly matches the tag value.
4. Use `DialogueLocaleTextPresenter` instead of only `DialogueTextPresenter`.

The key follows the same identifier rules as `id`.

### `layout` ? dialogue UI layout state

Use `layout` to change the visual arrangement of the dialogue UI for the current line:

```ink
# layout:WideCinematic
The whole valley opens below us.
```

`DialogueLayoutAnimator` calls `Animator.Play` with this value when the line becomes ready. The value must match a state that the assigned Animator can play. At dialogue start, the component plays its `None` state. Layout values are not validated against the Animator during metadata parsing.

Good uses include cinematic vs. compact boxes, narrator panels, phone/radio conversations, or hiding character frames for a system message.

### `audio` ? typewriter audio profile

Use `audio` to select the typing sound set for a line:

```ink
# audio:robot
SYNCHRONIZATION COMPLETE.
```

`DialogueTextPresenter` passes the value to its `ITypewriterAudioEffect`. `TypewriterEffect` looks it up in its configured `DialogueAudioInfoSO` assets. If the ID is missing or unknown, it warns and uses the default audio profile.

Game setup:

1. Put an `AudioSource` on the typewriter object's parent as expected by `TypewriterEffect`.
2. Assign a default `DialogueAudioInfoSO`.
3. Add alternate audio info assets to the typewriter's audio list.
4. Give each asset the ID used in Ink.

### `skip` ? story-skip permission

Allowed values are `enable` and `disable`:

```ink
# skip:enable
You may leave now, or stay to hear the full account.
```

The directive takes effect **after that tagged line finishes presenting** and persists across following lines and choices in the same dialogue session. A new session starts with skipping disabled.

```ink
# skip:disable
This decision must be answered before you leave.
```

Use `DialogueStorySkipButton` for the ready-made uGUI control, or read `DialogueHandler.CanSkipStory` and subscribe to `OnStorySkipAvailabilityChanged` for a custom control.

Skipping exits the dialogue normally, but skipped Ink content is not evaluated: choices are not selected and external functions in the skipped path are not called. Do not put required gameplay side effects only in content that the player may skip.

## Participants

A participant contains:

- a semantic role, such as `speaker`, `listener`, or `narrator`;
- a character ID used as stable identity;
- an optional display name;
- an optional portrait key;
- an optional animation key.

The character ID is the stable link to `DialogueCharacterCatalog`. Normal lines only need the role's character ID. The catalog supplies the display name, optional localized display name, default portrait, character-scoped portrait variations, and default animation. Name, portrait, and animation tags remain available as exceptional per-line overrides.

### Character catalog

Create the catalog from **Assets > Create > Dialogue > Character Catalog** and assign the same asset to each relevant `SpeakerView`. Add one entry per stable character ID. Each entry contains:

- `Id`: the value authored in participant tags such as `speaker:guide`;
- `Display Name`: the non-localized fallback name;
- `Localized Display Name`: an optional Unity Localization reference that takes priority over the fallback name;
- `Default Portrait`: the sprite used when the line has no portrait override;
- `Default Animation State`: the animation used when the line has no animation override;
- `Portraits`: character-scoped key-to-sprite variations such as `happy`, `neutral`, or `angry`.

Character IDs and portrait keys are matched case-insensitively. IDs must be unique and use the same identifier rules as participant metadata. The catalog logs configuration warnings for missing, invalid, or duplicate IDs and duplicate portrait keys.

The resolution order is deliberately override-first:

1. Use a name, portrait, or animation supplied by the current line.
2. Otherwise use the character catalog value.
3. If no catalog name exists, show the character ID; if no catalog animation exists, use the `SpeakerView` default animation.

This keeps ordinary Ink concise while preserving exceptional labels such as `???`, disguises, player-selected names, or one-line portrait and animation changes.

### Current speaker

```ink
# speaker:guide
# portrait:happy
# animation:TalkFriendly
Welcome, traveler.
```

`DialogueLineContext.CurrentSpeakerId` returns the character ID assigned to the profile's current-speaker role. In the canonical profile that role is `speaker`.

The visual resolution order in `SpeakerView` is:

- name: metadata override, localized catalog name, catalog display name, then character ID;
- portrait: character-scoped metadata portrait, then catalog default portrait;
- animation: metadata override, catalog default animation, then the view's configured default animation.

Because of these fallbacks, the minimal form is often enough:

```ink
# speaker:guide
Welcome back.
```

### Primary listener

```ink
# listener:traveler
# listener_portrait:neutral
# listener_animation:Listen
```

`DialogueLineContext.ListenerId` returns the character ID assigned to the profile's listener role. Listener metadata is useful even when the UI only shows the current speaker: game systems can use it for reactions, camera direction, relationship tracking, or animation.

### Additional participant roles

Use the generic participant form when a line involves more than the primary speaker and listener, or when role names carry domain meaning:

```ink
# participant.narrator:narrator
# participant.narrator.name:Narrator
# participant.narrator.portrait:book_icon
# participant.narrator.animation:Read
The old road remembers every traveler.
```

Replace `narrator` with any valid role such as `interviewer`, `merchant`, or `companion`. A role must start with a letter and may contain letters, numbers, underscores, and hyphens.

A detail field is invalid without its base character ID. For example, `participant.narrator.portrait` requires `participant.narrator` on the same line.

In C#, use `TryGetParticipant(role, out participant)` or inspect `Participants`. A participant becomes visible only when its resolved presentation slot has a configured `SpeakerView`.

## Character placement

Placement answers a separate question from identity: **who is talking** and **where each character appears** do not have to be the same thing.

### `placement:follow-speaker`

This is the default and preserves the original behavior. Participants are routed by role:

- `speaker` uses the `speaker` presentation slot, which falls back to the first/left view;
- `listener` uses the `listener` presentation slot, which falls back to the second/right view;
- when speaker and listener roles swap, their visual sides can swap too.

Use it for a traditional speaker-focused dialogue box where the active role owns the primary view.

```ink
# placement:follow-speaker
# speaker:guide
# listener:traveler
Take the northern path.
```

### `placement:fixed-character`

Use this when a character should remain on the same side while speakers alternate:

```ink
# placement:fixed-character
# slot.left:guide
# slot.right:traveler
# speaker:guide
# listener:traveler
Take the northern path.

# speaker:traveler
# listener:guide
I will. You remain on the left while I speak from the right.
```

The mode and slot assignments persist for the rest of the dialogue session, so they do not need to be repeated on every line. A placement or slot directive affects the same line on which it appears.

If fixed placement is enabled without explicit slot assignments, new character IDs are pinned on first appearance using the profile's auto-placement order. The canonical order is `left`, `right`, then `center`.

### `slot.<slot-id>`

Assign a character to a named visual slot:

```ink
# slot.left:guide
# slot.right:traveler
# slot.center:merchant
```

Clear a slot with:

```ink
# slot.center:clear
```

The slot ID is the portion after `slot.`. It must start with a letter and may contain letters, numbers, underscores, and hyphens. Character IDs follow the `id` identifier rules.

`SpeakerPresenter` provides zero-configuration aliases for common layouts:

- first child `SpeakerView`: `speaker` and `left`;
- second child `SpeakerView`: `listener` and `right`;
- third child `SpeakerView`: `center`.

For a custom slot such as `upper-left`, add an entry to **Speaker Presenter > Participant Slots** whose role is `upper-left` and whose view is the desired `SpeakerView`. Then authors can use `# slot.upper-left:guide` without game code.

Switch back to role-driven placement at any time:

```ink
# placement:follow-speaker
```

## Choice metadata

Keep metadata inside the visible choice text:

```ink
+ [Accept # id:choice.accept # locale:choice.accept # outcome:accept] -> accepted
+ [Decline # id:choice.decline # locale:choice.decline # outcome:decline] -> declined
```

The displayed choice text does not include these tags. The default choice presenter consumes `locale`; every parsed field and custom tag is available in `ChoiceOptionContext.LineContext` through `DialogueChoiceController.OnChoicesPrepared`.

This makes choice tags appropriate for analytics IDs, requirement explanations, icons, controller hints, morality labels, or project-specific preview data. Put gameplay consequences in the selected Ink branch or the game's choice handling?not merely in an unselected choice tag.

## Custom tags

Any well-formed tag that is not mapped by the metadata profile is preserved. No profile entry is required for a custom tag.

```ink
# quest:lost_sword
# objective:find_cave
# camera:guide_closeup
# sfx:map_open
The cave is beyond the old bridge.
```

The runtime parser preserves any non-empty custom key, but projects should use the profile-compatible form: start with a letter and use only letters, numbers, underscores, hyphens, and dots. This keeps the key compatible with editor tooling and leaves open the option of promoting it to a profile field later. Custom values are strings, so use a value format that game code can validate reliably.

Read custom tags from a line:

```csharp
using SAS.DialogueSystem;
using UnityEngine;

public sealed class DialogueMetadataReceiver : MonoBehaviour
{
    [SerializeField] private DialogueHandler dialogueHandler;

    private void OnEnable()
    {
        dialogueHandler.OnLineReady += HandleLineReady;
    }

    private void OnDisable()
    {
        dialogueHandler.OnLineReady -= HandleLineReady;
    }

    private void HandleLineReady(DialogueLineContext line)
    {
        if (line.TryGetTagValue("quest", out var questId))
            Debug.Log($"Dialogue referenced quest: {questId}");

        foreach (var sfxId in line.GetTagValues("sfx"))
            Debug.Log($"Play dialogue SFX: {sfxId}");
    }
}
```

Available helpers:

- `HasTag(key)` checks whether a key exists.
- `TryGetTagValue(key, out value)` returns the final value for that key.
- `GetTagValues(key)` returns all values in authoring order.
- `RawTags` exposes the original Ink tag strings.
- `Tags` exposes the parsed case-insensitive key/value collection.

Use `OnLineReady` for behavior that must happen before or with presentation, such as camera, music, or character state. Use `OnLinePresented` for behavior that should happen after the line has finished revealing. Custom metadata is local to that line or choice; store state in your game or in Ink variables if it must persist.

For choices:

```csharp
private void OnChoicesPrepared(ChoiceContext context)
{
    foreach (var option in context.Options)
    {
        if (option.LineContext.TryGetTagValue("icon", out var iconId))
            Debug.Log($"Choice {option.ChoiceIndex} uses icon {iconId}");
    }
}
```

Subscribe this handler to `DialogueChoiceController.OnChoicesPrepared`.

### When to use a custom tag vs. a profile field

Use a custom tag when the meaning belongs to the consuming game or a one-off integration: quests, cameras, analytics, emotions, rewards, or UI badges.

Map a field in `DialogueMetadataProfile` when it represents one of the package's standard concepts but your project wants different terminology?for example, `actor` instead of `speaker`, `face` instead of `portrait`, or `loc_key` instead of `locale`.

## DialogueMetadataProfile

Create the asset from **Assets > Create > Dialogue > Metadata Profile** and assign it to `DialogueHandler`. A `DialogueTrigger` may optionally override it per story.

The profile controls:

- tag names for line ID, localization, layout, audio, skip, and placement;
- the slot prefix and automatic fixed-placement slot order;
- which role is exposed as `CurrentSpeakerId` and `ListenerId`;
- explicit participant tag bindings;
- the prefix and suffixes used for dynamic participant roles.

Example project vocabulary:

| Package concept | Canonical tag | Possible project tag |
| --- | --- | --- |
| Current speaker ID | `speaker` | `actor` |
| Speaker portrait | `portrait` | `face` |
| Localization key | `locale` | `loc_key` |
| Story skip | `skip` | `allow_skip` |

Renaming a profile field changes runtime interpretation; it does not rewrite existing Ink. Update the story tags and the customized Inky sidecar together. Avoid assigning the same tag key to two profile concepts; profile validation rejects ambiguous mappings.

## Customized Inky sidecar

A `<story>.metadata.json` file next to an Ink story controls the customized editor experience: visible fields, labels, contexts, and suggestions. It does **not** change Unity runtime semantics.

Example:

```json
{
  "schemaVersion": 2,
  "tags": {
    "speaker": {
      "label": "Speaker",
      "values": ["guide", "traveler", "merchant"],
      "contexts": ["dialogue"]
    },
    "portrait": {
      "label": "Portrait",
      "values": ["happy", "serious", "neutral"],
      "contexts": ["dialogue"]
    },
    "slot.left": {
      "label": "Left Character",
      "values": ["guide", "traveler", "merchant", "clear"],
      "contexts": ["dialogue"]
    },
    "mood": {
      "label": "Mood",
      "values": ["friendly", "worried", "angry"],
      "contexts": ["dialogue", "choice"]
    },
    "analytics_event": {
      "label": "Analytics Event",
      "contexts": ["choice"]
    }
  }
}
```

Use the sidecar to reduce typing mistakes and give designers useful dropdowns. The built-in fields such as `skip` and `placement` already expose constrained choices in the customized editor. Add project-specific value suggestions when the valid set is known; leave values open when authors genuinely need free text.

Keep the sidecar keys aligned with the `DialogueMetadataProfile` assigned to that story. The sidecar is an authoring aid, while the profile is the runtime contract.

## Unity setup checklist

### Minimum setup

1. Install the package dependencies listed in the package README.
2. Add a `DialogueHandler` under the dialogue Canvas.
3. Add the text and choice presenter components, or start from the imported Basic Dialogue sample.
4. Create and assign a `DialogueMetadataProfile`.
5. Add a `DialogueTrigger`, assign compiled Ink JSON, and call `ShowDialogue()`.

### Speaker and participant visuals

1. Add `SpeakerPresenter` below the same handler.
2. Add one or more child `SpeakerView` objects.
3. For the standard layout, child order automatically provides left/speaker, right/listener, and center slots.
4. For named custom slots, add explicit **Participant Slots** mappings.
5. Configure name labels, portrait images, and animators on each view.

### Portraits

1. Create a **Dialogue > Character Catalog** asset.
2. Add one entry per stable character ID.
3. Configure its display name, optional localized display name, default portrait, default animation, and character-scoped portrait variations.
4. Assign the catalog to every relevant `SpeakerView`.

Character and portrait lookups are case-insensitive. A missing character produces a warning and falls back to the character ID for display text. An unknown portrait override falls back to that character's default portrait.

### Typewriter audio

1. Configure the presenter's `TypewriterEffect`.
2. Provide its required `AudioSource`.
3. Assign a default audio info asset.
4. Add every alternate `DialogueAudioInfoSO` referenced by an `audio` tag.

### Layout animation

1. Add `DialogueLayoutAnimator` under the handler.
2. Assign its Animator.
3. Add Animator states matching the `layout` values used in Ink.

### Localization

1. Configure Unity Localization and the string table collection.
2. Use `DialogueLocaleTextPresenter`.
3. Ensure every `locale` value exists in the configured table.

### Story skipping

Add `DialogueStorySkipButton` under the handler for the ready-made control, or bind custom UI to `CanSkipStory`, `SkipStory()`, and `OnStorySkipAvailabilityChanged`.

### Custom game behavior

Subscribe to `OnLineReady`, `OnLinePresented`, and/or `OnChoicesPrepared`. No custom presenter or metadata profile change is required merely to read an arbitrary custom tag.

## Validation and diagnostics

The parser reports diagnostics in `DialogueLineContext.Diagnostics`. Important rules are:

- identifier values for `id`, `locale`, character IDs, and slot assignments must use the supported identifier characters;
- participant name/portrait/animation fields require their participant ID on the same line;
- generic roles cannot reuse a role that has an explicit profile binding;
- unsupported `participant.<role>.<field>` names are errors;
- invalid slot IDs or slot character IDs are errors;
- duplicate semantic fields produce a warning and the final value wins;
- `skip` accepts only `enable` or `disable`;
- `placement` accepts only `follow-speaker` or `fixed-character`.

`DialogueHandler` rejects metadata containing errors when **Reject Invalid Metadata** is enabled, which is the default. Warnings do not stop playback. Unknown custom keys are valid and preserved.

Common problems:

| Symptom | Check |
| --- | --- |
| Raw Ink text appears instead of translation | `locale` key, table name, locale presenter, and table entry |
| Name falls back to character ID | character entry and display/localized name in `DialogueCharacterCatalog` |
| Portrait is empty | character entry, portrait key, default portrait, and `DialogueCharacterCatalog` assignment |
| Audio falls back to default | `audio` value and `DialogueAudioInfoSO.id` |
| Layout does not change | Animator assignment and exact state name |
| Character disappears in fixed mode | `slot.*` ID has a matching presenter slot/view |
| Character swaps sides | use `placement:fixed-character` and stable slot assignments |
| Choice tag is unavailable before selection | put the tag inside the choice's visible brackets |
| Custom tag does nothing | subscribe game code and define the tag's behavior |
| Metadata works in Inky but not Unity | match the sidecar keys to the assigned runtime profile |

## Runtime API map

The most useful `DialogueLineContext` members are:

| Member | Meaning |
| --- | --- |
| `LineId` | Parsed `id` |
| `Locale` | Parsed localization key |
| `LayoutAnim` | Parsed layout state |
| `AudioInfoId` | Parsed typewriter audio ID |
| `StorySkipDirective` | Directive on this line |
| `PlacementDirective` | Placement change requested by this line |
| `PlacementMode` | Effective mode after session state is applied |
| `CurrentSpeakerId` | Character ID in the current-speaker role |
| `ListenerId` | Character ID in the listener role |
| `Participants` | Semantic role/character data parsed from the line |
| `PresentationParticipants` | Resolved visible characters, slots, and current-speaker flags |
| `SlotAssignments` | Slot directives parsed from the line |
| `Tags` | All parsed tags, including custom tags |
| `Diagnostics` / `HasErrors` | Validation results |

Use `Participants` when game logic cares about semantic roles. Use `PresentationParticipants` when custom UI cares about the resolved visual arrangement.

## Full example

```ink
=== village_intro ===

# id:village_intro.guide.hello
# placement:fixed-character
# slot.left:guide
# slot.right:traveler
# speaker:guide
# portrait:happy
# animation:Wave
# listener:traveler
# locale:dialogue.village.guide_hello
# audio:guide
# layout:TwoCharacter
Welcome to our village.

# id:village_intro.traveler.reply
# speaker:traveler
# portrait:neutral
# listener:guide
# skip:enable
Thank you. I only need directions.

+ [Ask about the ruins # id:choice.ruins # locale:choice.ask_ruins # analytics_event:ruins] -> ruins
+ [Say goodbye # id:choice.goodbye # locale:choice.goodbye # analytics_event:goodbye] -> goodbye

=== ruins ===
# speaker:guide
# listener:traveler
# camera:ruins_map
The ruins lie beyond the northern bridge.
-> END

=== goodbye ===
# speaker:traveler
# listener:guide
# slot.right:clear
Farewell.
-> END
```

This story needs no project-specific placement code. Unity assets supply the visual slots, portraits, animations, audio profiles, layout states, and localization entries. Only `analytics_event` and `camera` need game-owned handlers because they are intentionally custom tags.
