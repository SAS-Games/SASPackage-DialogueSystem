# Changelog

## Unreleased

- Replaced the flat `ImageKeyMapConfig` with `DialogueCharacterCatalog`.
- This is an intentional breaking setup change: no legacy component or serialized-field compatibility is retained, so existing catalog assets and `SpeakerView` assignments must be recreated.
- Added centralized display names, optional localized names, default portraits, character-scoped portrait variations, and default animations.
- Kept participant name, portrait, and animation metadata as optional per-line overrides.
- Added cached case-insensitive character and portrait lookup with duplicate and invalid-ID diagnostics.
- Simplified the Basic Dialogue sample so ordinary lines no longer repeat participant name metadata.

## 0.3.0 - 2026-09-27

- Added metadata-driven `follow-speaker` and persistent `fixed-character` placement modes.
- Added `slot.<slot-id>` assignments, `clear` support, automatic first-appearance placement, and diagnostics.
- Decoupled active-speaker identity from visual slots through resolved presentation participants.
- Updated `SpeakerPresenter`, tests, documentation, and the Basic Dialogue sample for stable left/right positions.

## 0.2.0 - 2026-09-21

- Added `skip:enable` and `skip:disable` Ink metadata directives.
- Added latched story-skip state without changing normal line-by-line playback.
- Added the reusable `DialogueStorySkipButton` uGUI component.
- Added configurable story-skip tag mapping, tests, and authoring documentation.

## 0.1.0 - 2026-09-21

- Extracted the reusable Ink dialogue runtime into a UPM package.
- Added explicit runtime and test assemblies.
- Added reusable trigger and event-listener components.
- Decoupled portrait animation forwarding from the LittleAdventure UniRx adapter.
- Added a tagged Ink sample, metadata sidecar, UI prefabs, placeholder portraits, and play-test scene.
- Documented customized Inky integration and the package/game ownership boundary.
