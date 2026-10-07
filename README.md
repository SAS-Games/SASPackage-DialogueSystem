# SAS Dialogue System

An Ink-powered Unity dialogue runtime. Ink remains the narrative source of truth; ordinary Ink tags are parsed into stable runtime metadata for speakers, listeners, portraits, animation, audio, layout, localization, and project-defined fields.

For authoring syntax, every built-in tag, Unity setup, custom tags, and runtime APIs, see the [Dialogue Metadata Reference](Documentation~/MetadataReference.md).

## Requirements

- Unity 2022.3 or newer
- [Ink Unity Integration](https://github.com/inkle/ink-unity-integration) 1.2.1 or newer
- SAS Core 1.0.0 or newer
- Unity Input System, Localization, and uGUI

Ink Unity Integration and SAS Core are Git packages in the SAS projects. Install those direct dependencies in the consuming project's `Packages/manifest.json` before installing this package from Git.

## Quick start

1. Add the package to the project.
2. In Package Manager, import **Basic Dialogue** from the Samples tab.
3. Open the imported `Basic Dialogue.unity` scene under `Assets/Samples/SAS Dialogue System/<package version>/Basic Dialogue`.
4. Enter Play Mode.
5. Press Space to reveal or advance text. Use the mouse or UI navigation to choose a response.

The sample uses direct component discovery and does not require a project-specific context binder.

## Runtime setup

1. Add a `DialogueHandler` and its presenter components to a Canvas, or start from the sample prefab.
2. Create a **Dialogue > Metadata Profile** asset and assign it to the handler.
3. Create a **Dialogue > Character Catalog** asset, add every character ID used by the stories, and configure each character's display name, optional localized display name, portraits, and default animation.
4. Assign that catalog to every `SpeakerView`. The old `ImageKeyMapConfig` is not supported; existing prefabs and assets must be configured again.
5. Add `DialogueTrigger` to a scene object, assign compiled Ink JSON, and optionally assign a per-story metadata profile.
6. Call `DialogueTrigger.ShowDialogue()` from proximity, interaction, quest, or other game-owned code.
7. Subscribe to `IDialogueHandler` events or use `DialogueEventListener` for game reactions.

`DialogueTrigger` supports SAS Core injection, an explicit handler reference, and scene lookup as a final fallback.

## Story-specific Ink bindings

Add an `InkStoryBinding` subclass beside `DialogueTrigger` when a story needs game methods or initial variables. The trigger discovers it automatically; bindings on another object can be assigned through **Binding Sources**.

```csharp
public sealed class ShopInkBinding : InkStoryBinding
{
    [InkVariable("shop_item_price")]
    [SerializeField] private int m_ItemPrice = 120;

    [InkExternal("shop_coin_count")]
    private int GetCoinCount() => 200;
}
```

`InkExternal` exposes methods with zero to four parameters. `InkVariable` supports fields and readable, non-indexed properties. Omitting the attribute name uses the C# member name unchanged; explicit names are recommended when Ink uses snake_case.

## Metadata contract

| Runtime value | Ink tag |
| --- | --- |
| Line ID | `id` |
| Localization key | `locale` |
| Smart String runtime argument | `loc-arg` |
| Layout animation | `layout` |
| Audio profile | `audio` |
| Story skip permission | `skip` |
| Character placement mode | `placement` |
| Character assigned to a visual slot | `slot.<slot-id>` |
| Active speaker | `speaker` |
| Speaker name override | `speaker_name` |
| Speaker portrait override | `portrait` |
| Speaker animation override | `animation` |
| Primary listener | `listener` |
| Listener name override | `listener_name` |
| Listener portrait override | `listener_portrait` |
| Listener animation override | `listener_animation` |

Additional roles use `participant.<role>`, with optional `.name`, `.portrait`, and `.animation` overrides. Character display names, localized names, default portraits, portrait variations, and default animations belong in a shared `DialogueCharacterCatalog`. Unmapped `key:value` tags remain available through `DialogueLineContext.TryGetTagValue`. A `DialogueMetadataProfile` can map project-owned names such as `actor`, `face`, or `loc_key` onto the same runtime semantics.

The complete behavior, allowed values, setup requirements, choice syntax, and extension examples are documented in the [Dialogue Metadata Reference](Documentation~/MetadataReference.md).

## Customized Inky workflow

## Metadata-driven character placement

Existing stories use `follow-speaker`: the `speaker` role uses the primary/left view and the `listener` role uses the secondary/right view. No migration is required.

Use `fixed-character` when character positions should remain stable while the active speaker changes:

```ink
# placement:fixed-character
# slot.left:guide
# slot.right:traveler
# speaker:guide
# listener:traveler
Welcome.

# speaker:traveler
# listener:guide
I stay on the right while speaking.
```

Placement mode and slot assignments persist for the dialogue session. `# slot.right:clear` empties a slot. If fixed placement omits explicit slot tags, participants are pinned on first appearance to the profile's auto-placement order (`left`, `right`, then `center` by default).

`SpeakerPresenter` routes resolved participants by slot ID. Its child fallback maps the first view to both `speaker` and `left`, the second to both `listener` and `right`, and the third to `center`. Existing prefabs therefore keep working, while custom prefabs can explicitly map arbitrary slot IDs in **Participant Slots**.

The customized Inky editor adds a metadata inspector but still writes standard Ink tags and uses the official compiler. It is an authoring companion, not a runtime dependency.

For regular dialogue, place the contiguous metadata block immediately above the line:

```ink
# id:guide.welcome
# speaker:guide
# portrait:happy
Welcome to the sample.
```

For choices, keep metadata inside the visible choice text so Ink exposes it through `Choice.tags` before selection:

```ink
* [Ask a question # id:choice.ask # analytics_event:sample.ask] -> answer
```

The optional `story.metadata.json` sidecar configures fields, labels, contexts, and suggestions in customized Inky. Unity runtime behavior is controlled by `DialogueMetadataProfile`, so the tag names in both tools should match.

## Metadata-driven story skipping

Add `# skip:enable` above the line after which the player may skip the rest of the dialogue:

```ink
# skip:enable
You can leave now, or stay and hear the rest.
```

The permission is applied after that line finishes presenting and remains enabled across later lines and choices. Normal reveal and line-by-line advance behavior is unchanged. Use `# skip:disable` on a later line to revoke the permission after that line finishes.

Add `DialogueStorySkipButton` to a uGUI Button under the same `DialogueHandler`. The component drives `Button.interactable` from `DialogueHandler.CanSkipStory`; it can optionally hide a visual root while unavailable. Pressing the button exits the dialogue immediately and raises the normal dialogue-end notifications. Skipped Ink content is not evaluated, choices are not selected, and external functions in skipped content are not invoked.

The default metadata profile uses the `skip` tag. Projects can rename it with the profile's **Story Skip Tag** field, as long as the Inky sidecar uses the same key.

## Package boundary

The package owns reusable session logic, metadata parsing, UI/presenter components, trigger/listener components, and tests. Proximity detection, player state changes, device-specific story variables, quest behavior, and game character animation adapters belong to the consuming game.
